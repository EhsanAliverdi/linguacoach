using System.Text.Json;
using LinguaCoach.Application.LearningPath;
using LinguaCoach.Application.Memory;
using LinguaCoach.Application.Placement;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Infrastructure.Ai;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LinguaCoach.Infrastructure.Placement;

/// <summary>
/// Orchestrates the placement assessment lifecycle: start/resume, save section answers
/// (with deterministic scoring of objective sections), evaluate and complete, and expose
/// status/current-section/result. Implements all placement handler interfaces.
///
/// After completion it sets the source-of-truth CEFR level, seeds the student skill profile
/// and learning memory from the placement skill levels, and advances the lifecycle to CourseReady.
/// See: docs/architecture/placement-assessment-model.md
/// </summary>
public sealed class PlacementService :
    IStartPlacementHandler,
    ISavePlacementAnswersHandler,
    ICompletePlacementHandler,
    IGetPlacementStatusHandler,
    IGetPlacementCurrentSectionHandler,
    IGetPlacementResultHandler
{
    private const int ResponseTextMax = 2000;

    private static readonly Dictionary<string, string> SkillLabels = new(StringComparer.OrdinalIgnoreCase)
    {
        ["grammar"] = "Grammar accuracy",
        ["vocabulary"] = "Vocabulary",
        ["listening"] = "Listening comprehension",
        ["reading"] = "Reading comprehension",
        ["writing"] = "Writing",
        ["speaking"] = "Speaking",
        ["workplaceTone"] = "Formal tone",
    };

    private readonly LinguaCoachDbContext _db;
    private readonly IPlacementEvaluator _evaluator;
    private readonly IStudentMemoryService _memory;
    private readonly PlacementAudioService _audio;
    private readonly ILearningPathGenerator _pathGenerator;
    private readonly ILogger<PlacementService> _logger;
    private readonly Quartz.ISchedulerFactory? _schedulerFactory;

    public PlacementService(
        LinguaCoachDbContext db,
        IPlacementEvaluator evaluator,
        IStudentMemoryService memory,
        PlacementAudioService audio,
        ILearningPathGenerator pathGenerator,
        ILogger<PlacementService> logger,
        Quartz.ISchedulerFactory? schedulerFactory = null)
    {
        _db = db;
        _evaluator = evaluator;
        _memory = memory;
        _audio = audio;
        _pathGenerator = pathGenerator;
        _logger = logger;
        _schedulerFactory = schedulerFactory;
    }

    // â”€â”€ Start / resume â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    public async Task<PlacementStatusDto> HandleAsync(StartPlacementCommand command, CancellationToken ct = default)
    {
        var profile = await GetProfileAsync(command.UserId, ct);
        var assessment = await GetOrCreateAsync(profile, ct);

        if (assessment.Status != PlacementStatus.Completed)
        {
            assessment.Start();
            profile.SetLifecycleStage(StudentLifecycleStage.PlacementInProgress);
            await _db.SaveChangesAsync(ct);
        }

        return ToStatusDto(assessment, profile);
    }

    // â”€â”€ Save answers for a section â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    public async Task<PlacementStatusDto> HandleAsync(SavePlacementAnswersCommand command, CancellationToken ct = default)
    {
        if (!PlacementContent.IsValidSection(command.SectionKey))
            throw new InvalidOperationException($"Unknown placement section '{command.SectionKey}'.");

        var profile = await GetProfileAsync(command.UserId, ct);
        var assessment = await GetOrCreateAsync(profile, ct);

        if (assessment.Status == PlacementStatus.Completed)
            throw new InvalidOperationException("Placement is already completed.");

        var section = PlacementContent.GetSection(command.SectionKey)!;
        var answers = BuildAnswers(assessment.Id, section, command.Answers);

        // Replace any previously saved answers for this section (idempotent re-submit).
        var existingForSection = await _db.PlacementAnswers
            .Where(a => a.PlacementAssessmentId == assessment.Id && a.SectionKey == section.Key)
            .ToListAsync(ct);
        if (existingForSection.Count > 0)
            _db.PlacementAnswers.RemoveRange(existingForSection);

        _db.PlacementAnswers.AddRange(answers);

        var nextKey = PlacementContent.NextSectionKey(command.SectionKey);
        assessment.AdvanceSection(command.SectionKey, nextKey);
        profile.SetLifecycleStage(StudentLifecycleStage.PlacementInProgress);

        await _db.SaveChangesAsync(ct);
        return ToStatusDto(assessment, profile);
    }

    // â”€â”€ Complete (evaluate) â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    public async Task<PlacementResultDto> HandleAsync(CompletePlacementCommand command, CancellationToken ct = default)
    {
        var profile = await _db.StudentProfiles
            .Include(p => p.CareerProfile)
            .Include(p => p.LanguagePair).ThenInclude(lp => lp!.SourceLanguage)
            .Include(p => p.LanguagePair).ThenInclude(lp => lp!.TargetLanguage)
            .FirstOrDefaultAsync(p => p.UserId == command.UserId, ct)
            ?? throw new InvalidOperationException("Student profile not found.");

        var assessment = await GetOrCreateAsync(profile, ct);

        if (assessment.Status == PlacementStatus.Completed)
            return await BuildResultDtoAsync(assessment, ct);

        var allAnswers = await _db.PlacementAnswers
            .Where(a => a.PlacementAssessmentId == assessment.Id)
            .ToListAsync(ct);

        var summaries = BuildSectionSummaries(allAnswers);

        var selfReported = ExtractSelfReportedLevel(allAnswers);
        var domainComplexity = (profile.WorkplaceSeniority ?? DomainComplexity.JuniorRole).ToString();

        var input = new PlacementEvaluationInput(
            StudentProfileId: profile.Id,
            CareerContext: profile.CareerProfile?.Name ?? "General workplace",
            SourceLanguageName: LanguageSupportResolver.ResolveSourceLanguageName(profile),
            TargetLanguageName: profile.LanguagePair?.TargetLanguage?.Name ?? "English",
            SelfReportedLevel: selfReported,
            ProfessionalExperienceLevel: profile.ProfessionalExperienceLevel?.ToString() ?? "Unknown",
            RoleFamiliarity: profile.RoleFamiliarity?.ToString() ?? "Unknown",
            DomainComplexity: domainComplexity,
            Sections: summaries);

        var result = await _evaluator.EvaluateAsync(input, ct);

        var resultJson = JsonSerializer.Serialize(result);
        var skillLevelsJson = JsonSerializer.Serialize(result.SkillLevels);
        assessment.Complete(resultJson, result.EstimatedOverallLevel, skillLevelsJson);

        // PlacementResult is the source of truth for CEFR level.
        ApplyCefrLevel(profile, result.EstimatedOverallLevel);

        profile.SetLifecycleStage(StudentLifecycleStage.PlacementCompleted);
        await _db.SaveChangesAsync(ct);

        // Seed learning memory + skill profile from placement, then mark course ready.
        await SeedLearningMemoryAsync(profile, result, ct);
        profile.SetLifecycleStage(StudentLifecycleStage.CourseReady);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Placement completed StudentProfileId={StudentProfileId} OverallLevel={Level}",
            profile.Id, result.EstimatedOverallLevel);

        // Generate the student's LearningPath now, so SessionGeneratorService never
        // has to fall back to lazy generation on the student's first "Today's Lesson" request.
        await GenerateLearningPathAsync(profile.UserId, profile.Id, ct);

        // Kick off background lesson buffer generation now that the course is ready.
        await TriggerInitialBufferAsync(profile.Id, ct);

        return MapResult(result, isCompleted: true);
    }

    /// <summary>
    /// Best-effort: generates the student's LearningPath right after placement, so
    /// SessionGeneratorService's lazy-generation fallback becomes an edge case rather
    /// than the normal path. AiLearningPathGeneratorHandler never throws (falls back
    /// to DefaultPathFactory), but this is wrapped defensively to never block placement
    /// completion.
    /// </summary>
    private async Task GenerateLearningPathAsync(Guid userId, Guid studentProfileId, CancellationToken ct)
    {
        try
        {
            await _pathGenerator.GenerateAsync(new GenerateLearningPathCommand(userId), ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "LearningPath generation failed after placement for StudentProfileId={StudentProfileId}; " +
                "SessionGeneratorService will generate it lazily on first request.",
                studentProfileId);
        }
    }

    /// <summary>Best-effort: queues the first lesson batch after placement. Never blocks placement completion.</summary>
    private async Task TriggerInitialBufferAsync(Guid studentProfileId, CancellationToken ct)
    {
        if (_schedulerFactory is null) return;
        try
        {
            var settings = await _db.LessonGenerationSettings.AsNoTracking().FirstOrDefaultAsync(ct);
            if (settings is null || !settings.EnableBackgroundGeneration) return;

            var scheduler = await _schedulerFactory.GetScheduler(ct);
            await Infrastructure.Jobs.LessonBatchGenerationJob.TriggerAsync(
                scheduler, studentProfileId, Domain.Enums.GenerationTriggerReason.PlacementCompleted,
                settings.ReadyLessonBufferSize, ct);

            _logger.LogInformation(
                "Queued initial lesson buffer ({Count}) for student {ProfileId} after placement.",
                settings.ReadyLessonBufferSize, studentProfileId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Initial buffer trigger failed for student {ProfileId} (non-fatal).", studentProfileId);
        }
    }

    // â”€â”€ Status â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    public async Task<PlacementStatusDto> HandleAsync(GetPlacementStatusQuery query, CancellationToken ct = default)
    {
        var profile = await _db.StudentProfiles
            .FirstOrDefaultAsync(p => p.UserId == query.UserId, ct);

        // No profile: student exists in identity but StudentProfile row is missing
        // (edge case — provisioning failure or legacy user). Return NotStarted so the
        // placement page shows its intro/start state rather than a 400 error.
        if (profile is null)
        {
            return new PlacementStatusDto(
                Status: PlacementStatus.NotStarted.ToString(),
                CurrentSectionKey: PlacementContent.FirstSectionKey,
                CurrentSectionOrder: 1,
                TotalSections: PlacementContent.SectionOrder.Count,
                LifecycleStage: StudentLifecycleStage.PlacementRequired.ToString(),
                IsCompleted: false);
        }

        var assessment = await _db.PlacementAssessments
            .FirstOrDefaultAsync(a => a.StudentProfileId == profile.Id, ct);

        if (assessment is null)
        {
            return new PlacementStatusDto(
                Status: PlacementStatus.NotStarted.ToString(),
                CurrentSectionKey: PlacementContent.FirstSectionKey,
                CurrentSectionOrder: 1,
                TotalSections: PlacementContent.SectionOrder.Count,
                LifecycleStage: profile.LifecycleStage.ToString(),
                IsCompleted: false);
        }

        return ToStatusDto(assessment, profile);
    }

    // â”€â”€ Current section â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    public async Task<PlacementCurrentSectionDto> HandleAsync(GetPlacementCurrentSectionQuery query, CancellationToken ct = default)
    {
        var profile = await GetProfileAsync(query.UserId, ct);
        var assessment = await _db.PlacementAssessments
            .FirstOrDefaultAsync(a => a.StudentProfileId == profile.Id, ct);

        var status = assessment?.Status ?? PlacementStatus.NotStarted;
        var currentKey = assessment?.CurrentSectionKey ?? PlacementContent.FirstSectionKey;
        var order = PlacementContent.IndexOf(currentKey) + 1;
        var isCompleted = status == PlacementStatus.Completed;

        var section = isCompleted ? null : SanitiseSection(PlacementContent.GetSection(currentKey));

        string? audioUrl = null;
        var audioAvailable = false;

        if (!isCompleted && assessment is not null
            && string.Equals(currentKey, PlacementContent.ListeningKey, StringComparison.OrdinalIgnoreCase))
        {
            var listeningSection = PlacementContent.GetSection(PlacementContent.ListeningKey);
            if (!string.IsNullOrWhiteSpace(listeningSection?.AudioScript))
            {
                (audioAvailable, audioUrl) = await _audio.EnsureListeningAudioAsync(
                    assessment.Id, listeningSection.AudioScript, ct);
            }
        }

        return new PlacementCurrentSectionDto(
            Status: status.ToString(),
            Section: section,
            CurrentSectionOrder: order < 1 ? 1 : order,
            TotalSections: PlacementContent.SectionOrder.Count,
            IsCompleted: isCompleted,
            AudioUrl: audioUrl,
            AudioAvailable: audioAvailable);
    }

    // â”€â”€ Result â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    public async Task<PlacementResultDto> HandleAsync(GetPlacementResultQuery query, CancellationToken ct = default)
    {
        var profile = await GetProfileAsync(query.UserId, ct);
        // Order by most recent: a student can have more than one PlacementAssessment
        // row (e.g. an earlier abandoned/interrupted attempt plus a later completed
        // one) and an unordered FirstOrDefaultAsync can non-deterministically return
        // a stale row, incorrectly reporting a completed placement as "not completed".
        var assessment = await _db.PlacementAssessments
            .Where(a => a.StudentProfileId == profile.Id)
            .OrderByDescending(a => a.CreatedAt)
            .FirstOrDefaultAsync(ct)
            ?? throw new InvalidOperationException("Placement has not been started.");

        if (assessment.Status != PlacementStatus.Completed)
            throw new InvalidOperationException("Placement is not completed yet.");

        // Adaptive assessments (Phase 13A/13B) complete via CompleteAdaptive() and
        // never populate the legacy ResultJson field (that's only written by this
        // service's own non-adaptive Complete() path) -- build the DTO from the
        // adaptive PlacementSkillResults rows instead of throwing.
        if (assessment.ResultJson is null && assessment.IsAdaptive)
            return await BuildAdaptiveResultDtoAsync(assessment, ct);

        if (assessment.ResultJson is null)
            throw new InvalidOperationException("Placement is not completed yet.");

        return await BuildResultDtoAsync(assessment, ct);
    }

    private async Task<PlacementResultDto> BuildAdaptiveResultDtoAsync(PlacementAssessment assessment, CancellationToken ct)
    {
        var skillResults = await _db.PlacementSkillResults
            .Where(r => r.PlacementAssessmentId == assessment.Id)
            .ToListAsync(ct);

        return new PlacementResultDto(
            EstimatedOverallLevel: assessment.OverallEstimatedLevel ?? "A2",
            SkillLevels: skillResults
                .Select(r => new PlacementSkillLevelDto(SkillLabels.GetValueOrDefault(r.Skill, r.Skill), r.EstimatedCefrLevel))
                .ToList(),
            Strengths: skillResults.Select(r => r.Strengths).Where(s => !string.IsNullOrWhiteSpace(s)).ToList()!,
            Weaknesses: skillResults.Select(r => r.Weaknesses).Where(s => !string.IsNullOrWhiteSpace(s)).ToList()!,
            RecommendedStartingCourse: null,
            RecommendedSessionDuration: null,
            PlacementNotes: assessment.ResultSummary,
            IsCompleted: true);
    }

    // â”€â”€ Helpers â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    private async Task<StudentProfile> GetProfileAsync(Guid userId, CancellationToken ct)
        => await _db.StudentProfiles.FirstOrDefaultAsync(p => p.UserId == userId, ct)
           ?? throw new InvalidOperationException("Student profile not found.");

    private async Task<PlacementAssessment> GetOrCreateAsync(StudentProfile profile, CancellationToken ct)
    {
        var existing = await _db.PlacementAssessments
            .FirstOrDefaultAsync(a => a.StudentProfileId == profile.Id, ct);
        if (existing is not null) return existing;

        var created = new PlacementAssessment(profile.Id, PlacementContent.FirstSectionKey);
        _db.PlacementAssessments.Add(created);
        return created;
    }

    private static List<PlacementAnswer> BuildAnswers(
        Guid assessmentId,
        PlacementSectionDto section,
        IReadOnlyList<PlacementAnswerDto> submitted)
    {
        var byKey = section.Questions.ToDictionary(q => q.Key, StringComparer.OrdinalIgnoreCase);
        var answers = new List<PlacementAnswer>();

        foreach (var dto in submitted)
        {
            if (!byKey.TryGetValue(dto.QuestionKey, out var question))
                continue; // ignore unknown question keys defensively

            double? score = null;
            if (section.Scored && question.CorrectOption is not null && dto.SelectedOption is not null)
                score = string.Equals(dto.SelectedOption.Trim(), question.CorrectOption.Trim(), StringComparison.OrdinalIgnoreCase)
                    ? 100d : 0d;

            var responseText = Truncate(dto.ResponseText, ResponseTextMax);

            answers.Add(new PlacementAnswer(
                assessmentId,
                section.Key,
                question.Key,
                responseText,
                dto.SelectedOption,
                score));
        }

        return answers;
    }

    private static List<PlacementSectionSummary> BuildSectionSummaries(IReadOnlyList<PlacementAnswer> allAnswers)
    {
        var summaries = new List<PlacementSectionSummary>();

        foreach (var key in PlacementContent.SectionOrder)
        {
            var sectionDef = PlacementContent.GetSection(key)!;
            var sectionAnswers = allAnswers
                .Where(a => string.Equals(a.SectionKey, key, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (sectionAnswers.Count == 0)
                continue; // section not attempted (partial placement)

            if (key == PlacementContent.SelfCheckKey)
            {
                var notes = sectionAnswers
                    .Select(a => $"{a.QuestionKey}: {a.SelectedOption ?? a.ResponseText}")
                    .Where(n => !string.IsNullOrWhiteSpace(n))
                    .ToList();

                summaries.Add(new PlacementSectionSummary(
                    key, Scored: false, Score: null, AnsweredCount: sectionAnswers.Count,
                    CorrectCount: 0, ResponseText: null, Notes: notes));
                continue;
            }

            if (sectionDef.SectionType is "mcq" or "reading" or "listening")
            {
                var scored = sectionAnswers.Where(a => a.Score.HasValue).ToList();
                var correct = scored.Count(a => a.Score >= 100);
                double? avg = scored.Count > 0 ? scored.Average(a => a.Score!.Value) : null;

                summaries.Add(new PlacementSectionSummary(
                    key, Scored: true, Score: avg, AnsweredCount: sectionAnswers.Count,
                    CorrectCount: correct, ResponseText: null, Notes: null));
                continue;
            }

            // writing / speaking â€” productive sections (text response).
            var text = sectionAnswers
                .Select(a => a.ResponseText)
                .FirstOrDefault(t => !string.IsNullOrWhiteSpace(t));

            summaries.Add(new PlacementSectionSummary(
                key, Scored: true, Score: null, AnsweredCount: sectionAnswers.Count,
                CorrectCount: 0, ResponseText: Truncate(text, ResponseTextMax), Notes: null));
        }

        return summaries;
    }

    private static string? ExtractSelfReportedLevel(IReadOnlyList<PlacementAnswer> allAnswers)
        => allAnswers
            .FirstOrDefault(a => string.Equals(a.QuestionKey, "self_level", StringComparison.OrdinalIgnoreCase))
            ?.SelectedOption;

    private static void ApplyCefrLevel(StudentProfile profile, string estimatedLevel)
    {
        // Normalise "B1+" style labels to the base CEFR band the profile accepts.
        var baseLevel = new string(estimatedLevel.Where(char.IsLetterOrDigit).ToArray());
        if (baseLevel.Length >= 2)
            baseLevel = baseLevel[..2];
        try
        {
            profile.SetCefrLevel(baseLevel);
        }
        catch (ArgumentException)
        {
            // Fall back to B1 if the AI returned an unexpected label.
            profile.SetCefrLevel("B1");
        }
    }

    private async Task SeedLearningMemoryAsync(
        StudentProfile profile, PlacementEvaluationResult result, CancellationToken ct)
    {
        try
        {
            var weakKeys = result.SkillLevels
                .Where(kvp => IsBelowB1(kvp.Value))
                .Select(kvp => MapSkillKey(kvp.Key))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var strongKeys = result.SkillLevels
                .Where(kvp => !IsBelowB1(kvp.Value))
                .Select(kvp => MapSkillKey(kvp.Key))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            await _memory.SeedFromPlacementAsync(new PlacementMemorySeed(
                StudentProfileId: profile.Id,
                EstimatedLevel: result.EstimatedOverallLevel,
                Strengths: result.Strengths,
                Weaknesses: result.Weaknesses,
                WeakSkillKeys: weakKeys,
                StrongSkillKeys: strongKeys), ct);
        }
        catch (Exception ex)
        {
            // Best-effort â€” do not fail placement completion if memory seeding fails.
            _logger.LogWarning(ex,
                "Seeding learning memory from placement failed StudentProfileId={StudentProfileId}", profile.Id);
        }
    }

    private async Task<PlacementResultDto> BuildResultDtoAsync(PlacementAssessment assessment, CancellationToken ct)
    {
        await Task.CompletedTask;
        var result = JsonSerializer.Deserialize<PlacementEvaluationResult>(assessment.ResultJson!)
            ?? throw new InvalidOperationException("Stored placement result is invalid.");
        return MapResult(result, isCompleted: true);
    }

    private static PlacementResultDto MapResult(PlacementEvaluationResult result, bool isCompleted)
        => new(
            EstimatedOverallLevel: result.EstimatedOverallLevel,
            SkillLevels: result.SkillLevels
                .Select(kvp => new PlacementSkillLevelDto(SkillLabels.GetValueOrDefault(kvp.Key, kvp.Key), kvp.Value))
                .ToList(),
            Strengths: result.Strengths,
            Weaknesses: result.Weaknesses,
            RecommendedStartingCourse: result.RecommendedStartingCourse,
            RecommendedSessionDuration: result.RecommendedSessionDuration,
            PlacementNotes: result.PlacementNotes,
            IsCompleted: isCompleted);

    private static PlacementStatusDto ToStatusDto(PlacementAssessment assessment, StudentProfile profile)
    {
        var order = PlacementContent.IndexOf(assessment.CurrentSectionKey) + 1;
        return new PlacementStatusDto(
            Status: assessment.Status.ToString(),
            CurrentSectionKey: assessment.CurrentSectionKey,
            CurrentSectionOrder: order < 1 ? 1 : order,
            TotalSections: PlacementContent.SectionOrder.Count,
            LifecycleStage: profile.LifecycleStage.ToString(),
            IsCompleted: assessment.Status == PlacementStatus.Completed);
    }

    /// <summary>Strips correct-option metadata so it is never sent to the student.</summary>
    private static PlacementSectionDto? SanitiseSection(PlacementSectionDto? section)
    {
        if (section is null) return null;
        var safeQuestions = section.Questions
            .Select(q => q with { CorrectOption = null })
            .ToList();
        return section with { Questions = safeQuestions };
    }

    private static bool IsBelowB1(string level)
    {
        var l = level.Trim().ToUpperInvariant();
        return l.StartsWith("A1") || l.StartsWith("A2");
    }

    private static string MapSkillKey(string placementSkill) => placementSkill.ToLowerInvariant() switch
    {
        "grammar" => "grammar_accuracy",
        "vocabulary" => "workplace_vocabulary",
        "writing" => "concise_writing",
        "workplacetone" => "formal_tone",
        "reading" => "summarising_information",
        "listening" => "clarifying_questions",
        "speaking" => "sentence_clarity",
        _ => "message_structure"
    };

    private static string? Truncate(string? value, int max)
    {
        if (string.IsNullOrWhiteSpace(value)) return value;
        var trimmed = value.Trim();
        return trimmed.Length <= max ? trimmed : trimmed[..max];
    }
}


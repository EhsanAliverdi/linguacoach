using LinguaCoach.Application.Activity;
using LinguaCoach.Application.Curriculum;
using LinguaCoach.Application.Learning;
using LinguaCoach.Application.ReadinessPool;
using LinguaCoach.Application.Sessions;
using LinguaCoach.Domain.Constants;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Infrastructure.Ai;
using LinguaCoach.Infrastructure.Curriculum;
using LinguaCoach.Infrastructure.Progress;
using LinguaCoach.Infrastructure.Sessions;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Quartz;

namespace LinguaCoach.Infrastructure.Jobs;

/// <summary>
/// Generates LearningActivity content for every SessionExercise in a generation batch's
/// sessions and marks each session Ready once all its exercises have activities.
///
/// Does NOT generate listening audio — that is TtsAudioGenerationJob, which this job triggers.
/// This must never run on the lesson page-load path.
///
/// Idempotent: exercises that already have a LearningActivityId are skipped.
/// </summary>
[DisallowConcurrentExecution]
public sealed class ActivityMaterializationJob : IJob
{
    public const string JobName = "activity-materialization";
    public const string BatchIdKey = "batchId";

    private readonly LinguaCoachDbContext _db;
    private readonly IAiActivityGenerator _aiGenerator;
    private readonly IExercisePatternRepository _patternRepo;
    private readonly StudentProgressService _progress;
    private readonly ISchedulerFactory _schedulerFactory;
    private readonly ILearningGoalContextResolver _goalContextResolver;
    private readonly ICurriculumRoutingService _routing;
    private readonly IStudentActivityReadinessPoolService _readinessPool;
    private readonly IActivityNoveltyPolicy _noveltyPolicy;
    private readonly IActivityContentFingerprintService _fingerprintService;
    private readonly ITodayBankResourceSelector _bankResourceSelector;
    private readonly ILogger<ActivityMaterializationJob> _logger;

    private const int MaxGenerationAttempts = 2; // bounded retry on duplicate content — never unbounded

    public ActivityMaterializationJob(
        LinguaCoachDbContext db,
        IAiActivityGenerator aiGenerator,
        IExercisePatternRepository patternRepo,
        StudentProgressService progress,
        ISchedulerFactory schedulerFactory,
        ILearningGoalContextResolver goalContextResolver,
        ICurriculumRoutingService routing,
        IStudentActivityReadinessPoolService readinessPool,
        IActivityNoveltyPolicy noveltyPolicy,
        IActivityContentFingerprintService fingerprintService,
        ITodayBankResourceSelector bankResourceSelector,
        ILogger<ActivityMaterializationJob> logger)
    {
        _db = db;
        _aiGenerator = aiGenerator;
        _patternRepo = patternRepo;
        _progress = progress;
        _schedulerFactory = schedulerFactory;
        _goalContextResolver = goalContextResolver;
        _routing = routing;
        _readinessPool = readinessPool;
        _noveltyPolicy = noveltyPolicy;
        _fingerprintService = fingerprintService;
        _bankResourceSelector = bankResourceSelector;
        _logger = logger;
    }

    public static async Task TriggerAsync(IScheduler scheduler, Guid batchId, CancellationToken ct)
    {
        var job = JobBuilder.Create<ActivityMaterializationJob>()
            .WithIdentity($"{JobName}-{batchId:N}-{Guid.NewGuid():N}")
            .UsingJobData(BatchIdKey, batchId.ToString())
            .Build();
        await scheduler.ScheduleJob(job, TriggerBuilder.Create().StartNow().Build(), ct);
    }

    public async Task Execute(IJobExecutionContext context)
    {
        var ct = context.CancellationToken;
        var batchId = Guid.Parse(context.MergedJobDataMap.GetString(BatchIdKey)!);

        var sessions = await _db.LearningSessions
            .Where(s => s.GenerationBatchId == batchId && s.GenerationStatus != GenerationStatus.Ready)
            .ToListAsync(ct);

        if (sessions.Count == 0)
        {
            _logger.LogInformation("ActivityMaterializationJob: no pending sessions for BatchId={BatchId}.", batchId);
            return;
        }

        foreach (var session in sessions)
        {
            if (session.StudentProfileId is null) continue;
            var profile = await _db.StudentProfiles
                .Include(p => p.CareerProfile)
                .Include(p => p.LanguagePair!).ThenInclude(lp => lp.SourceLanguage)
                .Include(p => p.LanguagePair!).ThenInclude(lp => lp.TargetLanguage)
                .FirstOrDefaultAsync(p => p.Id == session.StudentProfileId.Value, ct);
            if (profile is null) continue;

            var exercises = await _db.SessionExercises
                .Where(e => e.LearningSessionId == session.Id)
                .OrderBy(e => e.Order)
                .ToListAsync(ct);

            var allReady = true;
            foreach (var exercise in exercises)
            {
                if (exercise.LearningActivityId.HasValue) continue;
                try
                {
                    await MaterializeExerciseAsync(profile, session, exercise, ct);
                }
                catch (Exception ex)
                {
                    allReady = false;
                    _logger.LogWarning(ex,
                        "ActivityMaterializationJob: exercise {ExerciseId} generation failed.", exercise.Id);
                }
            }

            if (allReady)
            {
                session.MarkGenerationReady();
                await _db.SaveChangesAsync(ct);
                _logger.LogInformation("ActivityMaterializationJob: session {SessionId} marked Ready.", session.Id);
            }
            else
            {
                session.MarkGenerationFailed();
                await _db.SaveChangesAsync(ct);
            }
        }

        // Queue audio generation for any listening activities now materialized.
        var scheduler = await _schedulerFactory.GetScheduler(ct);
        await TtsAudioGenerationJob.TriggerAsync(scheduler, batchId, ct);
    }

    private async Task MaterializeExerciseAsync(
        StudentProfile profile, LearningSession session, SessionExercise exercise, CancellationToken ct)
    {
        // Review reflection steps — lightweight placeholder, no AI.
        if (exercise.ExercisePatternKey is "lesson_reflection")
        {
            var placeholder = new LearningActivity(
                ActivityType.WritingScenario, ActivitySource.SystemGenerated,
                $"Lesson reflection: {session.Title}", profile.CefrLevel ?? "B1",
                "{\"activityType\":\"Review\"}", session.LearningModuleId, exercisePatternKey: "lesson_reflection");
            _db.LearningActivities.Add(placeholder);
            await _db.SaveChangesAsync(ct);
            exercise.AssignActivity(placeholder.Id);
            await _db.SaveChangesAsync(ct);
            return;
        }

        var pattern = await _patternRepo.GetByKeyAsync(exercise.ExercisePatternKey, ct);
        ActivityType activityType;
        string? overridePromptKey = null;
        string? patternKey = null;
        if (pattern is not null && pattern.IsActive)
        {
            activityType = pattern.ActivityType;
            overridePromptKey = pattern.AiGeneratePromptKey;
            patternKey = pattern.Key;
        }
        else
        {
            activityType = ExercisePrepareHandler.MapKindToActivityType(
                ResolveKind(exercise.ExercisePatternKey));
        }

        var focusArea = await _progress.GetCurrentFocusAreaAsync(profile.Id, ct);
        var recentMistakes = StudentProgressService.BuildRecentMistakesSummary(focusArea);

        var resolvedGoalContext = _goalContextResolver.Resolve(
            profile, new LearningGoalResolutionContext { Source = "ActivityMaterializationJob" });

        var routingRequest = CurriculumRoutingRequestFactory.Build(
            profile, resolvedGoalContext,
            source: "ActivityMaterializationJob",
            requestedPatternKey: patternKey,
            allowReviewOrScaffold: false);
        var routing = await _routing.RecommendAsync(routingRequest, ct);

        var avoidRepeatingHint = await BuildAvoidRepeatingHintAsync(profile.Id, patternKey, ct);
        var baseTopicHint = $"{session.Title}: {exercise.Instructions}";
        var topicHint = string.IsNullOrWhiteSpace(avoidRepeatingHint)
            ? baseTopicHint
            : $"{baseTopicHint} (Avoid repeating: {avoidRepeatingHint})";

        var isIntentionalReview = routing.RoutingReason is
            RoutingReason.Review or RoutingReason.Scaffold or RoutingReason.Remediation;

        // Phase D1/D2 — bank-first Today slice: for vocabulary/reading patterns only, try to
        // pull in a small, balanced bundle of published resource-bank entries as supporting
        // prompt material. Never blocks or alters generation on failure — a selector error just
        // means no bank supplement.
        var bankSelection = TodayBankSelectionResult.NoResources;
        if (pattern is not null)
        {
            try
            {
                var secondarySkills = ParseSecondarySkills(pattern.SecondarySkillsJson);
                // Phase D6 — feed the reliable routing signals into the selector. Focus tags prefer the
                // matched objective's tags (routing.FocusTags) and fall back to the learner's resolved
                // focus areas when routing matched no objective. Subskill comes from the matched
                // objective. Difficulty is derived conservatively from the learner's difficulty
                // preference relative to the routed CEFR's normal band (see DeriveDifficultyBand).
                var preferredFocusTags = routing.FocusTags.Count > 0
                    ? routing.FocusTags
                    : ParseFocusTags(resolvedGoalContext.FocusAreaKeys);
                var preferredDifficultyBand = DeriveDifficultyBand(
                    profile.DifficultyPreference, routing.TargetCefrLevel);
                bankSelection = await _bankResourceSelector.SelectAsync(
                    new TodayBankSelectionRequest(
                        StudentProfileId: profile.Id,
                        CefrLevel: routing.TargetCefrLevel,
                        PatternPrimarySkill: pattern.PrimarySkill,
                        PatternSecondarySkills: secondarySkills,
                        AllowLowerLevelReview: isIntentionalReview,
                        PatternKey: patternKey,
                        // Phase D4 — keep the bank general-English by default; only route
                        // workplace-tagged content when the learner's goal context is workplace-specific.
                        PrefersWorkplaceContext: resolvedGoalContext.WorkplaceSpecific,
                        // Phase D5/D6 — soft context-aware selection: prefer bank resources whose focus
                        // tags match the objective/learner focus areas, relaxing when none match.
                        PreferredFocusTags: preferredFocusTags,
                        // Phase D6 — reliable runtime subskill + difficulty signals (TODO-E10-1 closed).
                        PreferredSubskill: routing.Subskill,
                        PreferredDifficultyBand: preferredDifficultyBand), ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "ActivityMaterializationJob: bank resource selection failed for exercise {ExerciseId} — continuing without bank resources.",
                    exercise.Id);
            }
        }

        if (bankSelection.Resources.Count > 0 && !string.IsNullOrWhiteSpace(bankSelection.PromptSupplementText))
        {
            topicHint = $"{topicHint} (Bank resources: {bankSelection.PromptSupplementText})";
        }

        _logger.LogInformation(
            "ActivityMaterializationJob: bank resource selection for exercise {ExerciseId} (pattern {PatternKey}, CEFR {Cefr}) => {Outcome}, {Count} resource(s){Ids}.",
            exercise.Id, patternKey, routing.TargetCefrLevel, bankSelection.Outcome, bankSelection.Resources.Count,
            bankSelection.Resources.Count > 0
                ? $" [{string.Join(", ", bankSelection.Resources.Select(r => $"{r.ResourceType}:{r.Id}"))}]"
                : string.Empty);

        var pair = profile.LanguagePair;
        var context = new ActivityGenerationContext(
            ActivityType: activityType,
            CefrLevel: routing.TargetCefrLevel,
            CareerContext: profile.CareerProfile?.Name ?? "General",
            LanguagePairCode: $"{pair?.SourceLanguage?.Code ?? "en"}-{pair?.TargetLanguage?.Code ?? "en"}",
            SourceLanguageName: LanguageSupportResolver.ResolveSourceLanguageName(profile),
            TargetLanguageName: pair?.TargetLanguage?.Name ?? "English",
            TopicHint: topicHint,
            RecentMistakesSummary: recentMistakes,
            OverridePromptKey: overridePromptKey,
            ExercisePatternKey: patternKey,
            LearnerPreferenceContext: LearnerPreferenceContextFormatter.Build(
                profile, pair?.TargetLanguage?.Name),
            LearningGoalContext: resolvedGoalContext.ContextSummary,
            RoutingContext: routing.RoutingContextSummary,
            RoutingReason: routing.RoutingReason.ToString().ToLowerInvariant(),
            IsReviewOrScaffold: routing.IsLowerLevelContent);

        string contentJson = "{}";
        for (var attempt = 1; attempt <= MaxGenerationAttempts; attempt++)
        {
            contentJson = await _aiGenerator.GenerateActivityContentAsync(context, ct);

            // Never break Today lesson generation on a novelty-check failure — a thrown
            // exception here would fail the whole exercise/session; swallow and serve the
            // content as-is if the check itself misbehaves.
            bool allowed;
            NoveltyBlockReason reason = NoveltyBlockReason.None;
            try
            {
                var fingerprint = _fingerprintService.ComputeFingerprint(new ActivityContentFingerprintRequest(
                    ContentJson: contentJson,
                    ContentShape: ActivityContentShape.ModuleStageSchema,
                    PatternKey: patternKey,
                    CefrLevel: routing.TargetCefrLevel));

                var check = await _noveltyPolicy.CheckAsync(new ActivityNoveltyCheckRequest(
                    StudentProfileId: profile.Id,
                    ContentFingerprint: fingerprint,
                    IsIntentionalReview: isIntentionalReview), ct);

                allowed = check.Allowed;
                reason = check.Reason;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "ActivityMaterializationJob: novelty check failed for exercise {ExerciseId} — serving generated content without a repetition check.",
                    exercise.Id);
                allowed = true;
            }

            if (allowed)
                break;

            if (attempt < MaxGenerationAttempts)
            {
                _logger.LogWarning(
                    "ActivityMaterializationJob: generated content for exercise {ExerciseId} duplicated recent content ({Reason}), retrying (attempt {Attempt}/{Max}).",
                    exercise.Id, reason, attempt, MaxGenerationAttempts);
                continue;
            }

            _logger.LogWarning(
                "ActivityMaterializationJob: exhausted {Max} generation attempts for exercise {ExerciseId} without novel content ({Reason}) — serving the last generated content anyway; Today lesson generation must not be blocked.",
                MaxGenerationAttempts, exercise.Id, reason);
        }

        var activity = new LearningActivity(
            activityType, ActivitySource.AiGenerated,
            ExtractTitle(contentJson) ?? $"{activityType} activity",
            profile.CefrLevel ?? "B1", contentJson,
            session.LearningModuleId, exercisePatternKey: patternKey);

        // Phase D2 — durable provenance for every bank resource offered to the AI prompt (not
        // just one representative id). Set before the first save so it's part of the same insert.
        // Deliberately NOT StudentActivityReadinessItem.SetBankItemProvenance — that column is
        // FK-constrained to PlacementItemDefinition and cannot hold a Phase E Cefr* bank row id
        // (attempting to would throw a foreign-key violation against a real database).
        if (bankSelection.Resources.Count > 0)
        {
            try
            {
                activity.SetBankResourceProvenance(BuildBankResourceProvenanceJson(bankSelection.Resources));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "ActivityMaterializationJob: failed to build bank provenance JSON for exercise {ExerciseId} — continuing without it.",
                    exercise.Id);
            }
        }

        _db.LearningActivities.Add(activity);
        await _db.SaveChangesAsync(ct);

        exercise.AssignActivity(activity.Id);
        await _db.SaveChangesAsync(ct);

        // Link activity id to any readiness pool item already tracking this session.
        var poolItem = await _db.StudentActivityReadinessItems
            .Where(i => i.LearningSessionId == session.Id
                     && i.LearningActivityId == null
                     && i.Status == ReadinessPoolStatus.Ready)
            .FirstOrDefaultAsync(ct);
        if (poolItem is not null)
        {
            await _readinessPool.LinkMaterializedIdsAsync(
                poolItem.Id,
                learningSessionId: session.Id,
                learningActivityId: activity.Id,
                sessionExerciseId: exercise.Id,
                ct);
        }
    }

    /// <summary>Serializes the selected bank resources into the JSON array shape documented on
    /// LearningActivity.BankResourceProvenanceJson.</summary>
    private static string BuildBankResourceProvenanceJson(IReadOnlyList<TodayBankSelectedResource> resources) =>
        System.Text.Json.JsonSerializer.Serialize(resources.Select(r => new
        {
            type = r.ResourceType,
            id = r.Id,
            sourceId = r.SourceId,
            contentFingerprint = r.ContentFingerprint,
            selectionReason = r.SelectionReason,
            // Phase D4 — the resource's role in the bundle ("primary"/"supporting") so a
            // multi-resource bundle's shape stays legible in durable provenance.
            role = r.Role,
            // Phase D3 — full-passage provenance carries CEFR + title too (null for short resources).
            cefrLevel = r.CefrLevel,
            title = r.Title,
            // Phase D5 — which E9 metadata filters were applied and the resource's matched context
            // tags, so the context-aware selection stays auditable in durable provenance.
            appliedFilters = r.AppliedFilters,
            matchedContextTags = r.MatchedContextTags
        }));

    /// <summary>Parses ExercisePatternDefinition.SecondarySkillsJson (a JSON string array); returns
    /// an empty list on any malformed/missing input rather than throwing.</summary>
    private static IReadOnlyList<string> ParseSecondarySkills(string? secondarySkillsJson)
    {
        if (string.IsNullOrWhiteSpace(secondarySkillsJson)) return Array.Empty<string>();
        try
        {
            return System.Text.Json.JsonSerializer.Deserialize<string[]>(secondarySkillsJson) ?? Array.Empty<string>();
        }
        catch
        {
            return Array.Empty<string>();
        }
    }

    /// <summary>Phase D5 — splits ResolvedLearningGoalContext.FocusAreaKeys (a comma-joined key list)
    /// into normalized lowercase focus tags for the bank focus-tag filter. Empty/null ⇒ no
    /// preference; never throws.</summary>
    private static IReadOnlyList<string> ParseFocusTags(string? focusAreaKeys)
    {
        if (string.IsNullOrWhiteSpace(focusAreaKeys)) return Array.Empty<string>();
        return focusAreaKeys
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(k => k.ToLowerInvariant())
            .Distinct()
            .ToList();
    }

    /// <summary>
    /// Phase D6 — derives a conservative preferred difficulty band (1-5) from the learner's difficulty
    /// preference relative to the routed CEFR's normal band. Balanced → the CEFR-normal band;
    /// Gentle → one band lower (easier, same CEFR); Challenging → one band higher. Returns null when
    /// the preference is unknown or the CEFR level is not mappable, so nothing indefensible is filtered.
    /// The band scale is shared with the E10 metadata enrichment via <see cref="CefrDifficultyBand"/>.
    /// </summary>
    private static int? DeriveDifficultyBand(DifficultyPreference? preference, string? cefrLevel)
    {
        if (preference is null) return null;
        if (CefrDifficultyBand.FromCefr(cefrLevel) is not { } band) return null;

        var shifted = preference switch
        {
            DifficultyPreference.Gentle => band - 1,
            DifficultyPreference.Challenging => band + 1,
            _ => band, // Balanced (and any future default) → CEFR-normal band
        };
        return CefrDifficultyBand.Clamp(shifted);
    }

    /// <summary>
    /// Best-effort recent-use hint for this student + pattern, built from real
    /// StudentActivityUsageLog history (Phase B) rather than session titles — a stronger signal
    /// than LessonBatchGenerationJob's session-topic-only avoidRepeating. Returns null/empty on
    /// any failure or when no recent history exists; never throws (generation must not break).
    /// </summary>
    private async Task<string?> BuildAvoidRepeatingHintAsync(Guid studentProfileId, string? patternKey, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(patternKey)) return null;

        try
        {
            var cutoff = DateTime.UtcNow.AddDays(-14);
            var recentTopics = await _db.StudentActivityUsageLogs
                .AsNoTracking()
                .Where(l => l.StudentProfileId == studentProfileId
                         && l.PatternKey == patternKey
                         && l.ConsumedAtUtc >= cutoff
                         && l.TopicKey != null)
                .OrderByDescending(l => l.ConsumedAtUtc)
                .Select(l => l.TopicKey!)
                .Distinct()
                .Take(5)
                .ToListAsync(ct);

            return recentTopics.Count > 0 ? string.Join(", ", recentTopics) : null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "ActivityMaterializationJob: avoid-repeating hint lookup failed for pattern {PatternKey} — generation continues without it.",
                patternKey);
            return null;
        }
    }

    private static ExerciseKind ResolveKind(string patternKey) => patternKey switch
    {
        "phrase_match" or "gap_fill_workplace_phrase" => ExerciseKind.VocabularyWarmup,
        "listen_and_answer" or "listen_and_gap_fill" => ExerciseKind.ListeningInput,
        "email_reply" or "teams_chat_simulation" or "writing_response" => ExerciseKind.WritingTask,
        "spoken_response_from_prompt" or "speaking_role_play" => ExerciseKind.SpeakingTask,
        _ when patternKey.StartsWith("listen", StringComparison.OrdinalIgnoreCase) => ExerciseKind.ListeningInput,
        _ when patternKey.StartsWith("speaking", StringComparison.OrdinalIgnoreCase) => ExerciseKind.SpeakingTask,
        _ when patternKey.StartsWith("writing", StringComparison.OrdinalIgnoreCase) => ExerciseKind.WritingTask,
        _ => ExerciseKind.ContextInput
    };

    private static string? ExtractTitle(string contentJson)
    {
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(contentJson);
            if (doc.RootElement.TryGetProperty("title", out var t) && t.ValueKind == System.Text.Json.JsonValueKind.String)
                return t.GetString();
        }
        catch { }
        return null;
    }
}

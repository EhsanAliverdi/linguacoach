using LinguaCoach.Application.Activity;
using LinguaCoach.Application.Ai;
using LinguaCoach.Application.Sessions;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Infrastructure.Ai;
using LinguaCoach.Infrastructure.Activity;
using LinguaCoach.Infrastructure.Progress;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LinguaCoach.Infrastructure.Sessions;

/// <summary>
/// Generates and attaches a LearningActivity to a SessionExercise on demand.
///
/// Idempotent: if the exercise already has a LearningActivityId, returns the existing activity.
///
/// Pattern-aware (Phase 2): when the exercise has an ExercisePatternKey, the handler
/// loads the ExercisePatternDefinition and uses its ActivityType and AiGeneratePromptKey
/// instead of the legacy ExerciseKind → ActivityType fallback.
///
/// Review steps (lesson_reflection) do not generate a full AI activity — a lightweight
/// reflection placeholder is created instead.
/// </summary>
public sealed class ExercisePrepareHandler : IPrepareExerciseHandler
{
    private readonly LinguaCoachDbContext _db;
    private readonly IAiActivityGenerator _aiGenerator;
    private readonly IExercisePatternRepository _patternRepo;
    private readonly StudentProgressService _progress;
    private readonly ListeningAudioService _listeningAudio;
    private readonly ILogger<ExercisePrepareHandler> _logger;

    public ExercisePrepareHandler(
        LinguaCoachDbContext db,
        IAiActivityGenerator aiGenerator,
        IExercisePatternRepository patternRepo,
        StudentProgressService progress,
        ListeningAudioService listeningAudio,
        ILogger<ExercisePrepareHandler> logger)
    {
        _db = db;
        _aiGenerator = aiGenerator;
        _patternRepo = patternRepo;
        _progress = progress;
        _listeningAudio = listeningAudio;
        _logger = logger;
    }

    public async Task<PrepareExerciseResult> HandleAsync(
        PrepareExerciseCommand command, CancellationToken ct = default)
    {
        var (profile, session) = await LoadAndVerifyAsync(command.UserId, command.SessionId, ct);

        var exercise = await _db.SessionExercises
            .FirstOrDefaultAsync(e => e.Id == command.ExerciseId && e.LearningSessionId == command.SessionId, ct)
            ?? throw new InvalidOperationException(
                $"Exercise {command.ExerciseId} not found in session {command.SessionId}.");

        // Idempotent: activity already assigned — return existing.
        if (exercise.LearningActivityId.HasValue)
        {
            var existingActivity = await _db.LearningActivities
                .FirstOrDefaultAsync(a => a.Id == exercise.LearningActivityId.Value, ct)
                ?? throw new InvalidOperationException(
                    $"Assigned LearningActivity {exercise.LearningActivityId.Value} not found.");

            _logger.LogInformation(
                "Prepare exercise: idempotent return ExerciseId={ExerciseId} ActivityId={ActivityId}",
                exercise.Id, existingActivity.Id);

            return new PrepareExerciseResult(
                ActivityId: existingActivity.Id,
                ActivityType: existingActivity.ActivityType,
                IsReview: false);
        }

        // Review step — lightweight reflection placeholder, no AI generation.
        if (IsReviewPattern(exercise.ExercisePatternKey))
        {
            var placeholder = CreateReviewPlaceholder(session, exercise, profile.CefrLevel ?? "B1");
            _db.LearningActivities.Add(placeholder);
            await _db.SaveChangesAsync(ct);

            exercise.AssignActivity(placeholder.Id);
            await _db.SaveChangesAsync(ct);

            _logger.LogInformation(
                "Prepare exercise: Review placeholder created ExerciseId={ExerciseId} ActivityId={ActivityId}",
                exercise.Id, placeholder.Id);

            return new PrepareExerciseResult(
                ActivityId: placeholder.Id,
                ActivityType: null,
                IsReview: true);
        }

        // ── Pattern-aware path ──────────────────────────────────────────────────
        // Try to load the ExercisePatternDefinition. If found and active, use it.
        // If not found or inactive, fall back to legacy ExerciseKind mapping.
        ExercisePatternDefinition? pattern = null;
        if (!string.IsNullOrWhiteSpace(exercise.ExercisePatternKey))
        {
            pattern = await _patternRepo.GetByKeyAsync(exercise.ExercisePatternKey, ct);
            if (pattern is not null && !pattern.IsActive)
            {
                _logger.LogWarning(
                    "ExercisePattern {Key} is inactive for ExerciseId={ExerciseId}. Returning error.",
                    exercise.ExercisePatternKey, exercise.Id);
                throw new InvalidOperationException(
                    $"Exercise pattern '{exercise.ExercisePatternKey}' is inactive and cannot be used.");
            }
        }

        ActivityType activityType;
        string? overridePromptKey;
        string? patternKey;

        if (pattern is not null)
        {
            activityType = pattern.ActivityType;
            overridePromptKey = pattern.AiGeneratePromptKey;
            patternKey = pattern.Key;

            _logger.LogInformation(
                "Prepare exercise: pattern-aware path PatternKey={PatternKey} ActivityType={ActivityType}",
                patternKey, activityType);
        }
        else
        {
            // Legacy fallback: derive ActivityType from ExerciseKind
            var kind = ResolveKind(exercise.ExercisePatternKey);
            activityType = MapKindToActivityType(kind);
            overridePromptKey = null;
            patternKey = null;

            _logger.LogInformation(
                "Prepare exercise: legacy fallback PatternKey={PatternKey} Kind={Kind} ActivityType={ActivityType}",
                exercise.ExercisePatternKey, kind, activityType);
        }

        // Eager-load profile relations needed for generation context.
        await _db.Entry(profile)
            .Reference(p => p.LanguagePair!)
            .Query()
            .Include(lp => lp.SourceLanguage)
            .Include(lp => lp.TargetLanguage)
            .LoadAsync(ct);

        await _db.Entry(profile)
            .Reference(p => p.CareerProfile!)
            .LoadAsync(ct);

        var focusArea = await _progress.GetCurrentFocusAreaAsync(profile.Id, ct);
        var recentMistakes = StudentProgressService.BuildRecentMistakesSummary(focusArea);

        var context = new ActivityGenerationContext(
            ActivityType: activityType,
            CefrLevel: profile.CefrLevel ?? "B1",
            CareerContext: profile.CareerProfile?.Name ?? "General",
            LanguagePairCode: BuildPairCode(profile.LanguagePair),
            SourceLanguageName: profile.LanguagePair?.SourceLanguage?.Name ?? "Persian",
            TargetLanguageName: profile.LanguagePair?.TargetLanguage?.Name ?? "English",
            TopicHint: $"{session.Title}: {exercise.Instructions}",
            RecentMistakesSummary: recentMistakes,
            OverridePromptKey: overridePromptKey,
            ExercisePatternKey: patternKey,
            LearnerPreferenceContext: LearnerPreferenceContextFormatter.Build(
                profile, profile.LanguagePair?.TargetLanguage?.Name),
            LearningGoalContext: LearnerPreferenceContextFormatter.BuildLearningGoalContext(profile));

        _logger.LogInformation(
            "Prepare exercise: generating ActivityType={ActivityType} ExerciseId={ExerciseId} PromptKey={PromptKey}",
            activityType, exercise.Id, overridePromptKey ?? "(legacy)");

        string contentJson;
        try
        {
            contentJson = await _aiGenerator.GenerateActivityContentAsync(context, ct);
        }
        catch (NotSupportedException ex)
        {
            _logger.LogWarning(ex,
                "AI generation not supported for ActivityType={ActivityType} PatternKey={PatternKey}",
                activityType, patternKey ?? "(none)");
            throw new AiServiceUnavailableException(patternKey ?? activityType.ToString(), ex);
        }
        catch (AiServiceUnavailableException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "AI generation failed for ExerciseId={ExerciseId} PatternKey={PatternKey}",
                exercise.Id, patternKey ?? "(none)");
            throw new AiServiceUnavailableException(patternKey ?? activityType.ToString(), ex);
        }

        var title = ExtractTitle(contentJson, activityType);

        var activity = new LearningActivity(
            activityType: activityType,
            source: ActivitySource.AiGenerated,
            title: title,
            difficulty: profile.CefrLevel ?? "B1",
            aiGeneratedContentJson: contentJson,
            learningModuleId: session.LearningModuleId,
            exercisePatternKey: patternKey);

        _db.LearningActivities.Add(activity);
        await _db.SaveChangesAsync(ct);

        if (activityType == ActivityType.ListeningComprehension)
        {
            var langCode = profile.LanguagePair?.TargetLanguage?.Code ?? "en";
            await _listeningAudio.EnsureAudioAsync(activity, langCode, ct);
            await _db.SaveChangesAsync(ct);
        }

        exercise.AssignActivity(activity.Id);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Prepare exercise: activity created ActivityId={ActivityId} ExerciseId={ExerciseId} PatternKey={PatternKey}",
            activity.Id, exercise.Id, patternKey ?? "(legacy)");

        return new PrepareExerciseResult(
            ActivityId: activity.Id,
            ActivityType: activityType,
            IsReview: false);
    }

    // ── Private helpers ────────────────────────────────────────────────────────

    private async Task<(Domain.Entities.StudentProfile profile, Domain.Entities.LearningSession session)>
        LoadAndVerifyAsync(Guid userId, Guid sessionId, CancellationToken ct)
    {
        var profile = await _db.StudentProfiles
            .FirstOrDefaultAsync(p => p.UserId == userId, ct)
            ?? throw new InvalidOperationException("Student profile not found.");

        var session = await _db.LearningSessions
            .FirstOrDefaultAsync(s => s.Id == sessionId, ct)
            ?? throw new InvalidOperationException($"Session {sessionId} not found.");

        var moduleOnStudentPath = await _db.LearningPaths
            .Where(p => p.StudentProfileId == profile.Id && p.IsActive)
            .SelectMany(p => p.Modules)
            .AnyAsync(m => m.Id == session.LearningModuleId, ct);

        if (!moduleOnStudentPath)
            throw new UnauthorizedAccessException("Session does not belong to this student.");

        return (profile, session);
    }

    public static ActivityType MapKindToActivityType(ExerciseKind kind) => kind switch
    {
        ExerciseKind.VocabularyWarmup => ActivityType.VocabularyPractice,
        ExerciseKind.ContextInput     => ActivityType.WritingScenario,
        ExerciseKind.ListeningInput   => ActivityType.ListeningComprehension,
        ExerciseKind.ReadingInput     => ActivityType.ReadingTask,
        ExerciseKind.WritingTask      => ActivityType.WritingScenario,
        ExerciseKind.SpeakingTask     => ActivityType.SpeakingRolePlay,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "No ActivityType mapping for this ExerciseKind.")
    };

    private static bool IsReviewPattern(string patternKey) =>
        patternKey == Domain.ExercisePatternKey.LessonReflection
        || patternKey == "lesson_reflection";

    private static ExerciseKind ResolveKind(string patternKey) => patternKey switch
    {
        "phrase_match"
            or "gap_fill_workplace_phrase"                                 => ExerciseKind.VocabularyWarmup,
        "listen_and_answer"
            or "listen_and_gap_fill"                                       => ExerciseKind.ListeningInput,
        "email_reply"
            or "teams_chat_simulation"
            or "writing_response"                                          => ExerciseKind.WritingTask,
        "spoken_response_from_prompt"
            or "speaking_role_play"                                        => ExerciseKind.SpeakingTask,
        "lesson_reflection"                                                => ExerciseKind.Review,
        _ when patternKey.StartsWith("listen",   StringComparison.OrdinalIgnoreCase) => ExerciseKind.ListeningInput,
        _ when patternKey.StartsWith("speaking", StringComparison.OrdinalIgnoreCase) => ExerciseKind.SpeakingTask,
        _ when patternKey.StartsWith("writing",  StringComparison.OrdinalIgnoreCase) => ExerciseKind.WritingTask,
        _ => ExerciseKind.ContextInput
    };

    private static LearningActivity CreateReviewPlaceholder(
        Domain.Entities.LearningSession session,
        SessionExercise exercise,
        string cefrLevel)
    {
        var contentJson = System.Text.Json.JsonSerializer.Serialize(new
        {
            activityType = "Review",
            title = $"Lesson reflection: {session.Title}",
            instructions = exercise.Instructions,
            reflectionPrompts = new[]
            {
                "What was the most useful phrase or idea from this lesson?",
                "Is there anything you want to practise more?",
                "How confident did you feel with today's topic?"
            }
        });

        return new LearningActivity(
            activityType: ActivityType.WritingScenario,
            source: ActivitySource.SystemGenerated,
            title: $"Lesson reflection: {session.Title}",
            difficulty: cefrLevel,
            aiGeneratedContentJson: contentJson,
            learningModuleId: session.LearningModuleId,
            exercisePatternKey: Domain.ExercisePatternKey.LessonReflection);
    }

    private static string BuildPairCode(Domain.Entities.LanguagePair? pair)
    {
        if (pair is null) return "fa-en";
        var src = pair.SourceLanguage?.Code ?? "fa";
        var tgt = pair.TargetLanguage?.Code ?? "en";
        return $"{src}-{tgt}";
    }

    private static string ExtractTitle(string contentJson, ActivityType type)
    {
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(contentJson);
            if (doc.RootElement.TryGetProperty("title", out var t) && t.ValueKind == System.Text.Json.JsonValueKind.String)
                return t.GetString() ?? $"AI {type} activity";
            if (doc.RootElement.TryGetProperty("situation", out var s) && s.ValueKind == System.Text.Json.JsonValueKind.String)
            {
                var sit = s.GetString() ?? string.Empty;
                return sit.Length > 100 ? sit[..100] + "…" : sit;
            }
        }
        catch { /* ignore */ }
        return $"AI {type} activity";
    }
}

using LinguaCoach.Application.Activity;
using LinguaCoach.Application.Sessions;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Infrastructure.Progress;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LinguaCoach.Infrastructure.Sessions;

/// <summary>
/// Generates and attaches a LearningActivity to a SessionExercise on demand.
///
/// Idempotent: if the exercise already has a LearningActivityId, returns the existing activity.
/// Review steps do not generate a full AI activity — a lightweight reflection placeholder is returned.
///
/// ExerciseKind → ActivityType mapping:
///   VocabularyWarmup  → VocabularyPractice
///   ContextInput      → WritingScenario
///   ListeningInput    → ListeningComprehension
///   ReadingInput      → ReadingTask
///   WritingTask       → WritingScenario
///   SpeakingTask      → SpeakingRolePlay
///   Review            → (no full activity; lightweight reflection placeholder)
/// </summary>
public sealed class ExercisePrepareHandler : IPrepareExerciseHandler
{
    private readonly LinguaCoachDbContext _db;
    private readonly IAiActivityGenerator _aiGenerator;
    private readonly StudentProgressService _progress;
    private readonly ILogger<ExercisePrepareHandler> _logger;

    public ExercisePrepareHandler(
        LinguaCoachDbContext db,
        IAiActivityGenerator aiGenerator,
        StudentProgressService progress,
        ILogger<ExercisePrepareHandler> logger)
    {
        _db = db;
        _aiGenerator = aiGenerator;
        _progress = progress;
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

        var kind = ResolveKind(exercise.ExercisePatternKey);

        // Review step — lightweight reflection placeholder, no AI generation.
        if (kind == ExerciseKind.Review)
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

        var activityType = MapKindToActivityType(kind);

        // VocabularyPractice generation is handled by the dedicated VocabularyPracticeGenerator,
        // not by IAiActivityGenerator. For session exercise preparation we create a placeholder
        // that the front-end resolves to the vocab practice flow.
        if (activityType == ActivityType.VocabularyPractice)
        {
            var vocabPlaceholder = CreatePlaceholder(activityType, session, exercise, "B1");
            _db.LearningActivities.Add(vocabPlaceholder);
            await _db.SaveChangesAsync(ct);
            exercise.AssignActivity(vocabPlaceholder.Id);
            await _db.SaveChangesAsync(ct);
            return new PrepareExerciseResult(
                ActivityId: vocabPlaceholder.Id,
                ActivityType: activityType,
                IsReview: false);
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
            RecentMistakesSummary: recentMistakes);

        _logger.LogInformation(
            "Prepare exercise: generating ActivityType={ActivityType} ExerciseId={ExerciseId}",
            activityType, exercise.Id);

        string contentJson;
        try
        {
            contentJson = await _aiGenerator.GenerateActivityContentAsync(context, ct);
        }
        catch (NotSupportedException)
        {
            // AI generation not yet implemented for this ActivityType — create a placeholder.
            var notSupported = CreatePlaceholder(activityType, session, exercise, profile.CefrLevel ?? "B1");
            _db.LearningActivities.Add(notSupported);
            await _db.SaveChangesAsync(ct);
            exercise.AssignActivity(notSupported.Id);
            await _db.SaveChangesAsync(ct);
            return new PrepareExerciseResult(
                ActivityId: notSupported.Id,
                ActivityType: activityType,
                IsReview: false);
        }
        var title = ExtractTitle(contentJson, activityType);

        var activity = new LearningActivity(
            activityType: activityType,
            source: ActivitySource.AiGenerated,
            title: title,
            difficulty: profile.CefrLevel ?? "B1",
            aiGeneratedContentJson: contentJson,
            learningModuleId: session.LearningModuleId);

        _db.LearningActivities.Add(activity);
        await _db.SaveChangesAsync(ct);

        exercise.AssignActivity(activity.Id);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Prepare exercise: activity created ActivityId={ActivityId} ExerciseId={ExerciseId}",
            activity.Id, exercise.Id);

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

    private static ExerciseKind ResolveKind(string patternKey) => patternKey switch
    {
        "phrase_match"                                         => ExerciseKind.VocabularyWarmup,
        "listen_and_answer" or "listen_and_gap_fill"          => ExerciseKind.ListeningInput,
        "writing_response"                                     => ExerciseKind.WritingTask,
        "speaking_role_play"                                   => ExerciseKind.SpeakingTask,
        "lesson_reflection"                                    => ExerciseKind.Review,
        _ when patternKey.StartsWith("listen", StringComparison.OrdinalIgnoreCase)   => ExerciseKind.ListeningInput,
        _ when patternKey.StartsWith("speaking", StringComparison.OrdinalIgnoreCase) => ExerciseKind.SpeakingTask,
        _ when patternKey.StartsWith("writing", StringComparison.OrdinalIgnoreCase)  => ExerciseKind.WritingTask,
        _ => ExerciseKind.ContextInput
    };

    private static LearningActivity CreatePlaceholder(
        ActivityType activityType,
        Domain.Entities.LearningSession session,
        SessionExercise exercise,
        string cefrLevel)
    {
        var contentJson = System.Text.Json.JsonSerializer.Serialize(new
        {
            activityType = activityType.ToString(),
            title = $"{activityType}: {session.Title}",
            instructions = exercise.Instructions,
        });

        return new LearningActivity(
            activityType: activityType,
            source: ActivitySource.SystemFallback,
            title: $"{activityType}: {session.Title}",
            difficulty: cefrLevel,
            aiGeneratedContentJson: contentJson,
            learningModuleId: session.LearningModuleId);
    }

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
            source: ActivitySource.SystemFallback,
            title: $"Lesson reflection: {session.Title}",
            difficulty: cefrLevel,
            aiGeneratedContentJson: contentJson,
            learningModuleId: session.LearningModuleId);
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

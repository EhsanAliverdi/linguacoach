using System.Text.Json;
using LinguaCoach.Application.Activity;
using LinguaCoach.Application.LearningPath;
using LinguaCoach.Application.Sessions;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Infrastructure.Progress;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LinguaCoach.Infrastructure.Activity;

/// <summary>
/// Returns the next activity for a student.
/// Primary path: AI generates a fresh activity.
/// Fallback path: returns a SystemFallback activity from DB if AI fails or is unavailable.
/// Never throws a 500 — fallback is always available if seed data is present.
/// </summary>
public sealed class ActivityGetHandler : IGetNextActivityHandler, IGetActivityByIdHandler
{
    private const int CompletionThreshold = 3;
    private const int VocabPracticeIntervalAttempts = 4; // every 4th activity
    private const int ListeningIntervalAttempts = 5; // every 5th activity

    private readonly LinguaCoachDbContext _db;
    private readonly IAiActivityGenerator _aiGenerator;
    private readonly ILearningPathGenerator _pathGenerator;
    private readonly StudentProgressService _progress;
    private readonly VocabularyPracticeGenerator _vocabGenerator;
    private readonly ListeningAudioService _listeningAudio;
    private readonly IExercisePatternRepository _patternRepo;
    private readonly ILogger<ActivityGetHandler> _logger;

    public ActivityGetHandler(
        LinguaCoachDbContext db,
        IAiActivityGenerator aiGenerator,
        ILearningPathGenerator pathGenerator,
        StudentProgressService progress,
        VocabularyPracticeGenerator vocabGenerator,
        ListeningAudioService listeningAudio,
        IExercisePatternRepository patternRepo,
        ILogger<ActivityGetHandler> logger)
    {
        _db = db;
        _aiGenerator = aiGenerator;
        _pathGenerator = pathGenerator;
        _progress = progress;
        _vocabGenerator = vocabGenerator;
        _listeningAudio = listeningAudio;
        _patternRepo = patternRepo;
        _logger = logger;
    }

    public async Task<ActivityDto> HandleAsync(GetNextActivityQuery query, CancellationToken ct = default)
    {
        var profile = await _db.StudentProfiles
            .Include(p => p.LanguagePair)
                .ThenInclude(lp => lp!.SourceLanguage)
            .Include(p => p.LanguagePair)
                .ThenInclude(lp => lp!.TargetLanguage)
            .Include(p => p.CareerProfile)
            .FirstOrDefaultAsync(p => p.UserId == query.UserId, ct)
            ?? throw new InvalidOperationException("Student profile not found.");

        if (profile.OnboardingStatus != Domain.Enums.OnboardingStatus.Complete)
            throw new InvalidOperationException("Activity requires completed onboarding.");

        var activityType = await ResolveActivityTypeAsync(query, profile.Id, ct);
        _logger.LogInformation("Next activity requested UserId={UserId} ActivityType={ActivityType}",
            query.UserId, activityType);

        // Resolve active learning path + current module (lazy-generate if missing).
        var (currentModuleId, topicHint) = await ResolveCurrentModuleAsync(profile.UserId, profile.Id, ct);
        if (currentModuleId.HasValue)
            _logger.LogInformation("Module resolved ModuleId={ModuleId} TopicHint={TopicHint}",
                currentModuleId.Value, topicHint ?? "none");

        // Detect focus area from recent feedback to guide AI generation.
        var focusArea = await _progress.GetCurrentFocusAreaAsync(profile.Id, ct);
        var recentMistakes = StudentProgressService.BuildRecentMistakesSummary(focusArea);
        if (focusArea is not null)
            _logger.LogInformation("Focus area detected FocusCategory={Category} Frequency={Frequency}",
                focusArea.Category, focusArea.Frequency);

        // VocabularyPractice: deterministic path — no AI call needed.
        if (activityType == ActivityType.VocabularyPractice)
        {
            // Before generating, check prerequisites explicitly so we can give a clear message.
            var hasEnoughVocab = await _vocabGenerator.HasEnoughVocabularyAsync(profile.Id, ct);
            if (!hasEnoughVocab)
            {
                throw new InvalidOperationException(
                    "Vocabulary practice unlocks after you save at least 3 vocabulary items from writing activities. " +
                    "Complete more writing activities to build your vocabulary bank.");
            }

            try
            {
                var (currentModuleIdVp, _) = await ResolveCurrentModuleAsync(profile.UserId, profile.Id, ct);
                var (contentJson, title) = await _vocabGenerator.GenerateContentAsync(profile.Id, ct);

                var vocabActivity = new Domain.Entities.LearningActivity(
                    activityType: ActivityType.VocabularyPractice,
                    source: ActivitySource.AiGenerated,
                    title: title,
                    difficulty: profile.CefrLevel ?? "B1",
                    aiGeneratedContentJson: contentJson,
                    learningModuleId: currentModuleIdVp);

                _db.LearningActivities.Add(vocabActivity);
                await _db.SaveChangesAsync(ct);

                _logger.LogInformation(
                    "VocabularyPractice activity created ActivityId={ActivityId} StudentProfileId={ProfileId}",
                    vocabActivity.Id, profile.Id);

                return MapToDto(vocabActivity, null);
            }
            catch (InvalidOperationException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "VocabularyPractice generation failed UserId={UserId} — returning error (not falling back to WritingScenario)",
                    query.UserId);
                throw new InvalidOperationException(
                    "Could not generate vocabulary practice. Please try again shortly.");
            }
        }

        // Primary path — AI generation.
        try
        {
            var context = new ActivityGenerationContext(
                ActivityType: activityType,
                CefrLevel: profile.CefrLevel ?? "B1",
                CareerContext: profile.CareerProfile?.Name ?? "General",
                LanguagePairCode: BuildPairCode(profile.LanguagePair),
                SourceLanguageName: profile.LanguagePair?.SourceLanguage?.Name ?? "Persian",
                TargetLanguageName: profile.LanguagePair?.TargetLanguage?.Name ?? "English",
                TopicHint: topicHint,
                RecentMistakesSummary: recentMistakes);

            _logger.LogInformation("AI activity generation started ActivityType={ActivityType} CefrLevel={CefrLevel}",
                activityType, context.CefrLevel);
            var contentJson = await _aiGenerator.GenerateActivityContentAsync(context, ct);
            _logger.LogInformation("AI activity generation succeeded ActivityType={ActivityType}", activityType);

            var cefrLevel = profile.CefrLevel ?? "B1";
            var title = ExtractTitle(contentJson, activityType);

            var activity = new Domain.Entities.LearningActivity(
                activityType: activityType,
                source: ActivitySource.AiGenerated,
                title: title,
                difficulty: cefrLevel,
                aiGeneratedContentJson: contentJson,
                learningModuleId: currentModuleId);

            _db.LearningActivities.Add(activity);
            await _db.SaveChangesAsync(ct);

            if (activityType == ActivityType.ListeningComprehension)
            {
                await _listeningAudio.EnsureAudioAsync(activity, profile.LanguagePair?.TargetLanguage?.Code ?? "en", ct);
                await _db.SaveChangesAsync(ct);
            }

            return MapToDto(activity, null);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "AI activity generation failed UserId={UserId} ActivityType={ActivityType} ExceptionType={ExType} — using SystemFallback",
                query.UserId, activityType, ex.GetType().Name);
        }

        if (activityType == ActivityType.ListeningComprehension)
        {
            var fallbackJson = BuildListeningFallbackJson(profile.CefrLevel ?? "B1", profile.CareerProfile?.Name ?? "General");
            var fallbackActivity = new Domain.Entities.LearningActivity(
                activityType: ActivityType.ListeningComprehension,
                source: ActivitySource.SystemFallback,
                title: ExtractTitle(fallbackJson, activityType),
                difficulty: profile.CefrLevel ?? "B1",
                aiGeneratedContentJson: fallbackJson,
                learningModuleId: currentModuleId);

            _db.LearningActivities.Add(fallbackActivity);
            await _db.SaveChangesAsync(ct);
            await _listeningAudio.EnsureAudioAsync(fallbackActivity, profile.LanguagePair?.TargetLanguage?.Code ?? "en", ct);
            await _db.SaveChangesAsync(ct);
            return MapToDto(fallbackActivity, null);
        }

        if (activityType == ActivityType.SpeakingRolePlay)
        {
            var fallbackJson = BuildSpeakingFallbackJson(profile.CefrLevel ?? "B1", profile.CareerProfile?.Name ?? "General");
            var fallbackActivity = new Domain.Entities.LearningActivity(
                activityType: ActivityType.SpeakingRolePlay,
                source: ActivitySource.SystemFallback,
                title: ExtractTitle(fallbackJson, activityType),
                difficulty: profile.CefrLevel ?? "B1",
                aiGeneratedContentJson: fallbackJson,
                learningModuleId: currentModuleId);

            _db.LearningActivities.Add(fallbackActivity);
            await _db.SaveChangesAsync(ct);
            return MapToDto(fallbackActivity, null);
        }

        // Fallback path — return a seeded SystemFallback activity.
        var fallbacks = await _db.LearningActivities
            .Where(a => a.ActivityType == activityType
                     && a.Source == ActivitySource.SystemFallback
                     && a.IsActive)
            .ToListAsync(ct);

        var fallback = fallbacks.Count > 0
            ? fallbacks[Random.Shared.Next(fallbacks.Count)]
            : null;

        if (fallback is null)
            throw new InvalidOperationException(
                $"No SystemFallback activity found for type {activityType}. Ensure seed data has run.");

        return MapToDto(fallback, null);
    }

    // ── IGetActivityByIdHandler ────────────────────────────────────────────────

    public async Task<ActivityDto> HandleAsync(GetActivityByIdQuery query, CancellationToken ct = default)
    {
        var activity = await _db.LearningActivities
            .FirstOrDefaultAsync(a => a.Id == query.ActivityId && a.IsActive, ct)
            ?? throw new InvalidOperationException($"Activity {query.ActivityId} not found.");

        if (activity.ActivityType == ActivityType.ListeningComprehension)
        {
            await _listeningAudio.EnsureAudioAsync(
                activity, "en", ct); // language code is already embedded in audio record
            await _db.SaveChangesAsync(ct);
        }

        // Resolve pattern InteractionMode for the DTO if the activity has a pattern key.
        InteractionMode? interactionMode = null;
        if (!string.IsNullOrWhiteSpace(activity.ExercisePatternKey))
        {
            var pattern = await _patternRepo.GetByKeyAsync(activity.ExercisePatternKey, ct);
            interactionMode = pattern?.InteractionMode;
        }

        return MapToDto(activity, interactionMode);
    }

    private async Task<ActivityType> ResolveActivityTypeAsync(
        GetNextActivityQuery query, Guid studentProfileId, CancellationToken ct)
    {
        // Explicit override always wins
        if (query.PreferredType.HasValue)
            return query.PreferredType.Value;

        // Check if conditions are right for vocabulary practice
        var totalAttempts = await _db.ActivityAttempts
            .CountAsync(a => a.StudentProfileId == studentProfileId, ct);

        if (totalAttempts > 0
            && totalAttempts % VocabPracticeIntervalAttempts == 0
            && await _vocabGenerator.HasEnoughVocabularyAsync(studentProfileId, ct))
        {
            _logger.LogInformation(
                "VocabularyPractice selected StudentProfileId={ProfileId} TotalAttempts={Count}",
                studentProfileId, totalAttempts);
            return ActivityType.VocabularyPractice;
        }

        if (totalAttempts > 0 && totalAttempts % ListeningIntervalAttempts == 0)
        {
            _logger.LogInformation(
                "ListeningComprehension selected StudentProfileId={ProfileId} TotalAttempts={Count}",
                studentProfileId, totalAttempts);
            return ActivityType.ListeningComprehension;
        }

        return ActivityType.WritingScenario;
    }

    private async Task<(Guid? ModuleId, string? TopicHint)> ResolveCurrentModuleAsync(
        Guid userId, Guid studentProfileId, CancellationToken ct)
    {
        try
        {
            var path = await _db.LearningPaths
                .Include(p => p.Modules)
                .FirstOrDefaultAsync(p => p.StudentProfileId == studentProfileId && p.IsActive, ct);

            if (path is null)
            {
                // Lazy generation: student has no path yet (e.g. existing test account).
                _logger.LogInformation(
                    "No active LearningPath for profile {ProfileId}. Generating default path lazily.",
                    studentProfileId);
                await _pathGenerator.GenerateAsync(new Application.LearningPath.GenerateLearningPathCommand(userId), ct);

                path = await _db.LearningPaths
                    .Include(p => p.Modules)
                    .FirstOrDefaultAsync(p => p.StudentProfileId == studentProfileId && p.IsActive, ct);
            }

            if (path is null || path.Modules.Count == 0)
                return (null, null);

            var modules = path.Modules.OrderBy(m => m.Order).ToList();
            var moduleIds = modules.Select(m => m.Id).ToList();

            var completedCounts = await _db.ActivityAttempts
                .Where(a => a.StudentProfileId == studentProfileId)
                .Join(_db.LearningActivities.Where(la => la.LearningModuleId.HasValue && moduleIds.Contains(la.LearningModuleId!.Value)),
                      attempt => attempt.LearningActivityId,
                      activity => activity.Id,
                      (attempt, activity) => activity.LearningModuleId!.Value)
                .GroupBy(moduleId => moduleId)
                .Select(g => new { ModuleId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.ModuleId, x => x.Count, ct);

            var current = modules.FirstOrDefault(m =>
                completedCounts.GetValueOrDefault(m.Id, 0) < CompletionThreshold)
                ?? modules.Last();

            var hint = $"{current.Title}: {current.Description}";
            return (current.Id, hint);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to resolve current module for profile {ProfileId}. Proceeding without module context.", studentProfileId);
            return (null, null);
        }
    }

    private static string BuildPairCode(Domain.Entities.LanguagePair? pair)
    {
        if (pair is null) return "fa-en";
        var src = pair.SourceLanguage?.Code ?? "fa";
        var tgt = pair.TargetLanguage?.Code ?? "en";
        return $"{src}-{tgt}";
    }

    private static ActivityDto MapToDto(Domain.Entities.LearningActivity activity, InteractionMode? interactionMode)
    {
        var patternKey = string.IsNullOrWhiteSpace(activity.ExercisePatternKey) ? null : activity.ExercisePatternKey;
        var contentJson = string.IsNullOrWhiteSpace(activity.AiGeneratedContentJson) ? null : activity.AiGeneratedContentJson;
        var rendererContentJson = patternKey is null ? null : contentJson;

        if (activity.ActivityType == ActivityType.VocabularyPractice)
        {
            VocabPracticeContent? vpc = null;
            try
            {
                vpc = JsonSerializer.Deserialize<VocabPracticeContent>(
                    activity.AiGeneratedContentJson,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            catch { /* safe defaults */ }

            var vocabItems = vpc?.Items?.Select(i => new VocabPracticeItemDto(
                VocabularyItemId: i.VocabularyItemId,
                Term: i.Term ?? string.Empty,
                Prompt: i.Prompt ?? string.Empty,
                Hint: i.Hint ?? string.Empty,
                Explanation: i.Explanation ?? string.Empty)).ToList()
                as IReadOnlyList<VocabPracticeItemDto> ?? [];

            return new ActivityDto(
                ActivityId: activity.Id,
                ActivityType: activity.ActivityType,
                Source: activity.Source,
                Title: activity.Title,
                Difficulty: activity.Difficulty,
                Situation: null,
                LearningGoal: null,
                TargetPhrases: [],
                TargetVocabulary: [],
                ExampleText: null,
                CommonMistakeToAvoid: null,
                InstructionInSourceLanguage: null,
                Instructions: vpc?.Instructions,
                PracticeMode: vpc?.PracticeMode,
                VocabItems: vocabItems,
                InteractionMode: interactionMode,
                ExercisePatternKey: patternKey,
                ContentJson: rendererContentJson);
        }

        if (activity.ActivityType == ActivityType.ListeningComprehension)
        {
            ListeningContent? lc = null;
            try
            {
                lc = JsonSerializer.Deserialize<ListeningContent>(
                    activity.AiGeneratedContentJson,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            catch { /* safe defaults */ }

            var questions = lc?.Questions?.Select(q => new ListeningQuestionDto(
                Id: q.Id ?? string.Empty,
                Question: q.Question ?? string.Empty,
                Type: q.Type ?? "short_answer")).ToList()
                as IReadOnlyList<ListeningQuestionDto> ?? [];

            var responseTask = lc?.ResponseTask is null
                ? null
                : new ListeningResponseTaskDto(lc.ResponseTask.Prompt ?? string.Empty, lc.ResponseTask.ExpectedFocus);
            var audio = lc?.Audio;

            return new ActivityDto(
                ActivityId: activity.Id,
                ActivityType: activity.ActivityType,
                Source: activity.Source,
                Title: activity.Title,
                Difficulty: activity.Difficulty,
                Situation: null,
                LearningGoal: null,
                TargetPhrases: [],
                TargetVocabulary: [],
                ExampleText: null,
                CommonMistakeToAvoid: null,
                InstructionInSourceLanguage: null,
                Scenario: lc?.Scenario,
                Instructions: lc?.Instructions,
                SpeakerRole: lc?.SpeakerRole,
                ListenerRole: lc?.ListenerRole,
                TranscriptAvailableAfterSubmit: lc?.TranscriptAvailableAfterSubmit ?? true,
                ListeningQuestions: questions,
                ResponseTask: responseTask,
                AudioAvailable: audio?.AudioAvailable ?? false,
                AudioUrl: audio?.AudioAvailable == true ? $"/api/activity/{activity.Id}/audio" : null,
                AudioContentType: audio?.ContentType,
                AudioDurationSeconds: audio?.DurationMs is > 0 ? Math.Round(audio.DurationMs.Value / 1000.0, 1) : null,
                AudioUnavailableMessage: audio?.AudioAvailable == false ? audio.UnavailableMessage : null,
                InteractionMode: interactionMode,
                ExercisePatternKey: patternKey,
                ContentJson: rendererContentJson);
        }

        if (activity.ActivityType == ActivityType.SpeakingRolePlay)
        {
            SpeakingContent? sc = null;
            try
            {
                sc = JsonSerializer.Deserialize<SpeakingContent>(
                    activity.AiGeneratedContentJson,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            catch { /* safe defaults */ }

            return new ActivityDto(
                ActivityId: activity.Id,
                ActivityType: activity.ActivityType,
                Source: activity.Source,
                Title: activity.Title,
                Difficulty: activity.Difficulty,
                Situation: null,
                LearningGoal: null,
                TargetPhrases: [],
                TargetVocabulary: [],
                ExampleText: null,
                CommonMistakeToAvoid: null,
                InstructionInSourceLanguage: null,
                SpeakingScenario: sc?.Scenario,
                StudentRole: sc?.StudentRole,
                SpeakingListenerRole: sc?.ListenerRole,
                SpeakingGoal: sc?.SpeakingGoal,
                SpeakingPrompt: sc?.Prompt,
                ExpectedPoints: sc?.ExpectedPoints?.AsReadOnly(),
                SuggestedPhrases: sc?.SuggestedPhrases?.AsReadOnly(),
                MaxDurationSeconds: sc?.MaxDurationSeconds,
                InteractionMode: interactionMode,
                ExercisePatternKey: patternKey,
                ContentJson: rendererContentJson);
        }

        WritingContent? wc = null;
        if (activity.ActivityType == ActivityType.WritingScenario)
        {
            try
            {
                wc = JsonSerializer.Deserialize<WritingContent>(
                    activity.AiGeneratedContentJson,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            catch { /* leave null, safe defaults below */ }
        }

        return new ActivityDto(
            ActivityId: activity.Id,
            ActivityType: activity.ActivityType,
            Source: activity.Source,
            Title: activity.Title,
            Difficulty: activity.Difficulty,
            Situation: wc?.Situation,
            LearningGoal: wc?.LearningGoal,
            TargetPhrases: wc?.TargetPhrases ?? [],
            TargetVocabulary: wc?.TargetVocabulary ?? [],
            ExampleText: wc?.ExampleText,
            CommonMistakeToAvoid: wc?.CommonMistakeToAvoid,
            InstructionInSourceLanguage: wc?.InstructionInSourceLanguage,
            InteractionMode: interactionMode,
            ExercisePatternKey: patternKey,
            ContentJson: rendererContentJson);
    }

    private static string ExtractTitle(string contentJson, ActivityType type)
    {
        try
        {
            using var doc = JsonDocument.Parse(contentJson);
            if (doc.RootElement.TryGetProperty("title", out var t) && t.ValueKind == JsonValueKind.String)
                return t.GetString() ?? $"AI {type} activity";
            if (doc.RootElement.TryGetProperty("situation", out var s) && s.ValueKind == JsonValueKind.String)
            {
                var sit = s.GetString() ?? string.Empty;
                return sit.Length > 100 ? sit[..100] + "…" : sit;
            }
        }
        catch { /* ignore */ }
        return $"AI {type} activity";
    }

    private static string BuildListeningFallbackJson(string cefrLevel, string careerContext)
    {
        return JsonSerializer.Serialize(new
        {
            activityType = "ListeningComprehension",
            title = "Understand a project update",
            scenario = $"Imagine you listened to a short workplace message for a {careerContext} task.",
            instructions = "Read the situation first. Answer the questions as if you listened to the message. Transcript unlocks after you answer.",
            speakerRole = "Manager",
            listenerRole = careerContext,
            difficulty = cefrLevel,
            audioScript = "Hi, could you please check the latest delivery schedule? The supplier has confirmed a two-day delay, and I need an updated timeline before our 3 pm meeting.",
            transcriptAvailableAfterSubmit = true,
            questions = new[]
            {
                new { id = "q1", question = "What does the manager ask the listener to check?", expectedAnswer = "the latest delivery schedule", type = "short_answer" },
                new { id = "q2", question = "How long is the supplier delay?", expectedAnswer = "two days", type = "short_answer" }
            },
            responseTask = new
            {
                prompt = "Write a short reply confirming what you will do.",
                expectedFocus = "confirm task, updated timeline, before 3 pm, professional tone"
            }
        });
    }

    private sealed class WritingContent
    {
        public string? Situation { get; set; }
        public string? LearningGoal { get; set; }
        public string[]? TargetPhrases { get; set; }
        public string[]? TargetVocabulary { get; set; }
        public string? ExampleText { get; set; }
        public string? CommonMistakeToAvoid { get; set; }
        public string? InstructionInSourceLanguage { get; set; }
    }

    private sealed class VocabPracticeContent
    {
        public string? Instructions { get; set; }
        public string? PracticeMode { get; set; }
        public List<VocabPracticeItemContent>? Items { get; set; }
    }

    private sealed class VocabPracticeItemContent
    {
        public Guid VocabularyItemId { get; set; }
        public string? Term { get; set; }
        public string? Prompt { get; set; }
        public string? ExpectedAnswer { get; set; }
        public string? Hint { get; set; }
        public string? Explanation { get; set; }
    }

    private sealed class ListeningContent
    {
        public string? Scenario { get; set; }
        public string? Instructions { get; set; }
        public string? SpeakerRole { get; set; }
        public string? ListenerRole { get; set; }
        public bool? TranscriptAvailableAfterSubmit { get; set; }
        public List<ListeningQuestionContent>? Questions { get; set; }
        public ListeningResponseTaskContent? ResponseTask { get; set; }
        public ListeningAudioMetadata? Audio { get; set; }
    }

    private sealed class ListeningQuestionContent
    {
        public string? Id { get; set; }
        public string? Question { get; set; }
        public string? Type { get; set; }
        public string? ExpectedAnswer { get; set; }
    }

    private sealed class ListeningResponseTaskContent
    {
        public string? Prompt { get; set; }
        public string? ExpectedFocus { get; set; }
    }

    private sealed class SpeakingContent
    {
        public string? Scenario { get; set; }
        public string? StudentRole { get; set; }
        public string? ListenerRole { get; set; }
        public string? SpeakingGoal { get; set; }
        public string? Prompt { get; set; }
        public List<string>? ExpectedPoints { get; set; }
        public List<string>? SuggestedPhrases { get; set; }
        public int? MaxDurationSeconds { get; set; }
    }

    private static string BuildSpeakingFallbackJson(string cefrLevel, string careerContext)
    {
        return JsonSerializer.Serialize(new
        {
            activityType = "SpeakingRolePlay",
            title = "Explain a delay to your manager",
            scenario = $"Your manager asks why a project task has been delayed. Record a short professional response as a {careerContext}.",
            studentRole = careerContext,
            listenerRole = "Manager",
            difficulty = cefrLevel,
            speakingGoal = "Explain the delay clearly and politely.",
            prompt = "Record a 30–60 second response explaining the delay, the reason, and your next action.",
            expectedPoints = new[]
            {
                "mention the delay",
                "give a brief reason",
                "explain the next action",
                "use polite professional tone"
            },
            suggestedPhrases = new[]
            {
                "I wanted to update you on...",
                "The delay is due to...",
                "I will follow up with..."
            },
            maxDurationSeconds = 60
        });
    }
}

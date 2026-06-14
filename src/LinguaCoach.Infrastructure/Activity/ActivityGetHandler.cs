using System.Text.Json;
using LinguaCoach.Application.Activity;
using LinguaCoach.Application.Ai;
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
/// If AI generation fails or is not configured, throws AiServiceUnavailableException (→ 503).
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

        // Pattern-keyed path: bypass legacy type routing and VocabPracticeGenerator.
        if (!string.IsNullOrWhiteSpace(query.PreferredPatternKey))
            return await HandlePatternKeyedAsync(query.PreferredPatternKey, profile, ct);

        var activityType = await ResolveActivityTypeAsync(query, profile.Id, ct);
        _logger.LogInformation("Next activity requested UserId={UserId} ActivityType={ActivityType}",
            query.UserId, activityType);

        // VocabularyPractice / ListeningComprehension cadence picks are now routed
        // through the pattern engine so every new activity carries interactionMode.
        if (!query.PreferredType.HasValue)
        {
            if (activityType == ActivityType.VocabularyPractice)
                return await HandlePatternKeyedAsync(Domain.ExercisePatternKey.GapFillWorkplacePhrase, profile, ct);

            if (activityType == ActivityType.ListeningComprehension)
                return await HandlePatternKeyedAsync(Domain.ExercisePatternKey.ListenAndAnswer, profile, ct);

            if (activityType == ActivityType.WritingScenario)
                return await HandlePatternKeyedAsync(Domain.ExercisePatternKey.OpenWritingTask, profile, ct);

            if (activityType == ActivityType.SpeakingRolePlay)
                return await HandlePatternKeyedAsync(Domain.ExercisePatternKey.SpeakingRoleplayTurn, profile, ct);
        }

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
                var (vocabContentJson, vocabTitle) = await _vocabGenerator.GenerateContentAsync(profile.Id, ct);

                var vocabActivity = new Domain.Entities.LearningActivity(
                    activityType: ActivityType.VocabularyPractice,
                    source: ActivitySource.AiGenerated,
                    title: vocabTitle,
                    difficulty: profile.CefrLevel ?? "B1",
                    aiGeneratedContentJson: vocabContentJson,
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

        string contentJson;
        try
        {
            contentJson = await _aiGenerator.GenerateActivityContentAsync(context, ct);
        }
        catch (AiServiceUnavailableException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "AI activity generation failed UserId={UserId} ActivityType={ActivityType}",
                query.UserId, activityType);
            throw new AiServiceUnavailableException(activityType.ToString(), ex);
        }

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

    /// <summary>
    /// Generates an activity for a specific exercise pattern key, bypassing legacy type routing.
    /// Throws AiServiceUnavailableException if AI generation fails or is not configured.
    /// </summary>
    private async Task<ActivityDto> HandlePatternKeyedAsync(
        string patternKey,
        Domain.Entities.StudentProfile profile,
        CancellationToken ct)
    {
        var pattern = await _patternRepo.GetByKeyAsync(patternKey, ct)
            ?? throw new InvalidOperationException(
                $"Exercise pattern '{patternKey}' is not recognised. Check the pattern key and try again.");

        _logger.LogInformation(
            "Pattern-keyed activity requested UserId={UserId} PatternKey={PatternKey} ActivityType={ActivityType}",
            profile.UserId, patternKey, pattern.ActivityType);

        var cached = await TryAssignReadyPracticeCacheAsync(profile.Id, patternKey, ct);
        if (cached is not null)
        {
            _logger.LogInformation(
                "Pattern-keyed activity served from practice cache ActivityId={ActivityId} PatternKey={PatternKey}",
                cached.Id, patternKey);
            return MapToDto(cached, pattern.InteractionMode);
        }

        var (currentModuleId, topicHint) = await ResolveCurrentModuleAsync(profile.UserId, profile.Id, ct);
        var focusArea = await _progress.GetCurrentFocusAreaAsync(profile.Id, ct);
        var recentMistakes = StudentProgressService.BuildRecentMistakesSummary(focusArea);

        var generationContext = new ActivityGenerationContext(
            ActivityType: pattern.ActivityType,
            CefrLevel: profile.CefrLevel ?? "B1",
            CareerContext: profile.CareerProfile?.Name ?? "General",
            LanguagePairCode: BuildPairCode(profile.LanguagePair),
            SourceLanguageName: profile.LanguagePair?.SourceLanguage?.Name ?? "Persian",
            TargetLanguageName: profile.LanguagePair?.TargetLanguage?.Name ?? "English",
            TopicHint: topicHint,
            RecentMistakesSummary: recentMistakes,
            OverridePromptKey: pattern.AiGeneratePromptKey,
            ExercisePatternKey: patternKey);

        string contentJson;
        try
        {
            contentJson = await _aiGenerator.GenerateActivityContentAsync(generationContext, ct);
        }
        catch (AiServiceUnavailableException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Pattern-keyed AI generation failed PatternKey={PatternKey}",
                patternKey);
            throw new AiServiceUnavailableException(patternKey, ex);
        }

        var title = ExtractTitle(contentJson, pattern.ActivityType);

        var activity = new Domain.Entities.LearningActivity(
            activityType: pattern.ActivityType,
            source: Domain.Enums.ActivitySource.AiGenerated,
            title: title,
            difficulty: profile.CefrLevel ?? "B1",
            aiGeneratedContentJson: contentJson,
            learningModuleId: currentModuleId,
            exercisePatternKey: patternKey);

        _db.LearningActivities.Add(activity);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Pattern-keyed activity created ActivityId={ActivityId} PatternKey={PatternKey}",
            activity.Id, patternKey);

        var patternDef = await _patternRepo.GetByKeyAsync(patternKey, ct);
        return MapToDto(activity, patternDef?.InteractionMode);
    }

    private async Task<Domain.Entities.LearningActivity?> TryAssignReadyPracticeCacheAsync(
        Guid studentProfileId,
        string patternKey,
        CancellationToken ct)
    {
        var excludedIds = new HashSet<Guid>();

        while (true)
        {
            var cache = await _db.PracticeActivityCache
                .Where(c => c.StudentProfileId == studentProfileId
                         && c.PatternKey == patternKey
                         && c.Status == PracticeCacheStatus.Ready
                         && c.LearningActivityId.HasValue
                         && !excludedIds.Contains(c.Id)
                         && (c.ExpiresAtUtc == null || c.ExpiresAtUtc > DateTime.UtcNow))
                .OrderBy(c => c.CreatedAt)
                .FirstOrDefaultAsync(ct);

            if (cache is null)
                return null;

            var activity = await _db.LearningActivities
                .FirstOrDefaultAsync(a => a.Id == cache.LearningActivityId!.Value && a.IsActive, ct);
            if (activity is null)
            {
                cache.MarkExpired();
                await _db.SaveChangesAsync(ct);
                return null;
            }

            cache.MarkAssigned();
            try
            {
                await _db.SaveChangesAsync(ct);
            }
            catch (DbUpdateConcurrencyException)
            {
                // Another concurrent request already claimed this cache row.
                // Detach and try the next ready row instead of returning a
                // duplicate activity to two students/requests.
                _db.Entry(cache).State = EntityState.Detached;
                excludedIds.Add(cache.Id);
                continue;
            }

            return activity;
        }
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
            var stageContent = BuildStageContent(activity.AiGeneratedContentJson, activity.Title);

            ListeningContent? lc = null;
            try
            {
                var exerciseJson = stageContent is not null && stageContent.Practice.ExerciseData.ValueKind == JsonValueKind.Object
                    ? stageContent.Practice.ExerciseData.GetRawText()
                    : activity.AiGeneratedContentJson;
                lc = JsonSerializer.Deserialize<ListeningContent>(
                    exerciseJson,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                // Audio metadata is written back at the root of AiGeneratedContentJson by ListeningAudioService,
                // not inside practiceContent.exerciseData — read it separately.
                using var rootDoc = JsonDocument.Parse(activity.AiGeneratedContentJson);
                if (lc is not null && rootDoc.RootElement.TryGetProperty("audio", out var audioEl))
                {
                    lc.Audio = audioEl.Deserialize<ListeningAudioMetadata>(
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                }
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
                ContentJson: rendererContentJson,
                StageContent: stageContent);
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
        StageContentDto? writingStageContent = null;
        if (activity.ActivityType == ActivityType.WritingScenario)
        {
            writingStageContent = BuildStageContent(activity.AiGeneratedContentJson, activity.Title);
            try
            {
                var writingJson = writingStageContent is not null && writingStageContent.Practice.ExerciseData.ValueKind == JsonValueKind.Object
                    ? writingStageContent.Practice.ExerciseData.GetRawText()
                    : activity.AiGeneratedContentJson;
                wc = JsonSerializer.Deserialize<WritingContent>(
                    writingJson,
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
            ContentJson: rendererContentJson,
            StageContent: writingStageContent);
    }

    internal static StageContentDto? BuildStageContent(string contentJson, string activityTitle)
    {
        try
        {
            using var doc = JsonDocument.Parse(contentJson);
            var root = doc.RootElement;
            if (root.TryGetProperty("schemaVersion", out var sv) && sv.GetString() == ModuleStageSchema.Version)
            {
                var wire = JsonSerializer.Deserialize<ModuleStageWireDto>(root.GetRawText(), new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                return wire is null ? null : new StageContentDto(wire.SchemaVersion, wire.PrimarySkill, wire.SecondarySkills ?? [], wire.ExerciseType, wire.LearnContent, wire.PracticeContent, wire.FeedbackPlan);
            }

            return AdaptLegacy(root, activityTitle);
        }
        catch
        {
            return null;
        }
    }

    private static StageContentDto AdaptLegacy(JsonElement root, string activityTitle)
    {
        if (LooksLikeLegacyWriting(root))
            return AdaptLegacyWriting(root, activityTitle);

        return AdaptLegacyListening(root, activityTitle);
    }

    private static StageContentDto AdaptLegacyListening(JsonElement root, string activityTitle)
    {
        var learn = new LearnContentDto(
            TeachingTitle: activityTitle,
            Explanation: "Workplace listening practice. You will hear a short message and answer questions about it.",
            KeyPoints: [],
            Examples: [],
            Strategy: "Listen for the main idea, the requested action, and any deadline or timing.",
            CommonMistakes: [],
            SourceLanguageSupport: null);

        var instructions = root.TryGetProperty("instructions", out var instr) ? instr.GetString() ?? "" : "";
        var scenario = root.TryGetProperty("scenario", out var scn) ? scn.GetString() : null;
        string? task = root.TryGetProperty("responseTask", out var rt)
            && rt.ValueKind == JsonValueKind.Object
            && rt.TryGetProperty("prompt", out var p)
            ? p.GetString() : null;

        var practice = new PracticeContentDto(instructions, scenario, task, root.Clone());

        var feedbackPlan = new FeedbackPlanDto(
            EvaluationCriteria: ["Main idea understood", "Key details identified"],
            Rubric: [],
            FeedbackFocus: "Main idea and key details from the message",
            SuccessCriteria: []);

        return new StageContentDto(ModuleStageSchema.LegacyAdaptedVersion, "listening", [], "listening_comprehension", learn, practice, feedbackPlan);
    }


    private static bool LooksLikeLegacyWriting(JsonElement root) =>
        root.ValueKind == JsonValueKind.Object
        && (root.TryGetProperty("learningGoal", out _)
            || root.TryGetProperty("targetPhrases", out _)
            || root.TryGetProperty("exampleText", out _)
            || root.TryGetProperty("situation", out _) && !root.TryGetProperty("audioScript", out _));

    private static StageContentDto AdaptLegacyWriting(JsonElement root, string activityTitle)
    {
        var situation = root.TryGetProperty("situation", out var sit) ? sit.GetString() : null;
        var audience = root.TryGetProperty("audience", out var aud) ? aud.GetString() : null;
        var tone = root.TryGetProperty("tone", out var tn) ? tn.GetString() : null;
        var expectedLength = root.TryGetProperty("expectedLength", out var len) ? len.GetString() : null;
        var learningGoal = root.TryGetProperty("learningGoal", out var goal) ? goal.GetString() : null;
        var skillFocus = root.TryGetProperty("skillFocus", out var sf) ? sf.GetString() : "professional workplace writing";
        var commonMistake = root.TryGetProperty("commonMistakeToAvoid", out var cm) ? cm.GetString() : null;
        var sourceSupport = root.TryGetProperty("instructionInSourceLanguage", out var sl) ? sl.GetString() : null;

        var examples = root.TryGetProperty("targetPhrases", out var phrases) && phrases.ValueKind == JsonValueKind.Array
            ? phrases.EnumerateArray()
                .Where(p => p.ValueKind == JsonValueKind.String)
                .Take(4)
                .Select(p => new LearnExampleDto(p.GetString() ?? string.Empty, "Useful phrase for this workplace message.", "Adapt the phrase to your situation."))
                .ToList()
            : [];

        var learn = new LearnContentDto(
            TeachingTitle: activityTitle,
            Explanation: $"This module practises {skillFocus}. Focus on a clear purpose, a professional tone, and a simple structure.",
            KeyPoints: ["State the purpose clearly.", "Use a tone that fits the audience.", "Keep sentences direct and easy to follow."],
            Examples: examples,
            Strategy: "Before writing, identify the reader, your purpose, and the key information they need.",
            CommonMistakes: string.IsNullOrWhiteSpace(commonMistake) ? [] : [commonMistake],
            SourceLanguageSupport: sourceSupport);

        var practiceEnvelope = new
        {
            situation,
            audience,
            tone,
            expectedLength,
            prompt = situation ?? learningGoal ?? "Write a clear professional response for this workplace situation.",
            requiredPhrases = ReadStringArray(root, "targetPhrases"),
            targetVocabulary = ReadStringArray(root, "targetVocabulary"),
            successChecklist = new[] { "Address the situation.", "Use an appropriate tone.", "Write clearly and completely." }
        };
        using var practiceDoc = JsonDocument.Parse(JsonSerializer.Serialize(practiceEnvelope));
        var practice = new PracticeContentDto(
            Instructions: "Write a professional workplace response for the situation.",
            Scenario: situation,
            Task: learningGoal,
            ExerciseData: practiceDoc.RootElement.Clone());

        var feedbackPlan = new FeedbackPlanDto(
            EvaluationCriteria: ["Task completion", "Clarity", "Tone", "Grammar accuracy", "Vocabulary use"],
            Rubric: [],
            FeedbackFocus: "Help the student improve clarity, tone, grammar, and task completion.",
            SuccessCriteria: ["The message is clear and complete.", "The tone fits the reader."]);

        return new StageContentDto(ModuleStageSchema.LegacyAdaptedVersion, "writing", ["grammar", "vocabulary"], "writing_scenario", learn, practice, feedbackPlan);
    }

    private static string[] ReadStringArray(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var arr) || arr.ValueKind != JsonValueKind.Array)
            return [];
        return arr.EnumerateArray()
            .Where(e => e.ValueKind == JsonValueKind.String)
            .Select(e => e.GetString() ?? string.Empty)
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .ToArray();
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

}

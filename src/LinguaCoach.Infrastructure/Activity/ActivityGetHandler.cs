using System.Text.Json;
using LinguaCoach.Application.Activity;
using LinguaCoach.Application.Ai;
using LinguaCoach.Application.Learning;
using LinguaCoach.Application.LearningPath;
using LinguaCoach.Application.Sessions;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Infrastructure.Ai;
using LinguaCoach.Infrastructure.Progress;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LinguaCoach.Infrastructure.Activity;

/// <summary>
/// Resolves an activity by id (<see cref="IGetActivityByIdHandler"/>) — used for existing
/// LearningActivity rows, including those launched via the H10 Exercise runtime
/// launch bridge.
///
/// Phase I2A (legacy fallback deletion): this class previously also implemented
/// <c>IGetNextActivityHandler</c> (on-demand AI generation / practice cache assignment
/// for "next activity" requests). That interface and its implementation were removed —
/// Practice Gym no longer falls back to AI generation; see
/// docs/reviews/2026-07-10-phase-i2a-practice-gym-legacy-deletion-review.md.
/// </summary>
public sealed class ActivityGetHandler : IGetActivityByIdHandler
{
    private static readonly HashSet<string> ListeningPatternKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        Domain.ExercisePatternKey.ListenAndAnswer,
        Domain.ExercisePatternKey.ListenAndGapFill,
        Domain.ExercisePatternKey.ListeningMultipleChoiceSingle,
        Domain.ExercisePatternKey.ListeningMultipleChoiceMulti,
        Domain.ExercisePatternKey.ListeningFillInBlanks,
        Domain.ExercisePatternKey.SelectMissingWord,
        Domain.ExercisePatternKey.HighlightCorrectSummary,
        Domain.ExercisePatternKey.HighlightIncorrectWords,
        Domain.ExercisePatternKey.WriteFromDictation,
    };

    private readonly LinguaCoachDbContext _db;
    private readonly ListeningAudioService _listeningAudio;
    private readonly IExercisePatternRepository _patternRepo;
    private readonly ILogger<ActivityGetHandler> _logger;

    public ActivityGetHandler(
        LinguaCoachDbContext db,
        ListeningAudioService listeningAudio,
        IExercisePatternRepository patternRepo,
        ILogger<ActivityGetHandler> logger)
    {
        _db = db;
        _listeningAudio = listeningAudio;
        _patternRepo = patternRepo;
        _logger = logger;
    }

    // ── IGetActivityByIdHandler ────────────────────────────────────────────────

    public async Task<ActivityDto> HandleAsync(GetActivityByIdQuery query, CancellationToken ct = default)
    {
        var activity = await _db.LearningActivities
            .FirstOrDefaultAsync(a => a.Id == query.ActivityId && a.IsActive, ct)
            ?? throw new InvalidOperationException($"Activity {query.ActivityId} not found.");

        var isListeningById = activity.ActivityType == ActivityType.ListeningComprehension
            || (!string.IsNullOrWhiteSpace(activity.ExercisePatternKey)
                && ListeningPatternKeys.Contains(activity.ExercisePatternKey));

        if (isListeningById)
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

    private static string BuildPairCode(Domain.Entities.LanguagePair? pair)
    {
        if (pair is null) return "fa-en";
        var src = pair.SourceLanguage?.Code ?? "fa";
        var tgt = pair.TargetLanguage?.Code ?? "en";
        return $"{src}-{tgt}";
    }

    /// <summary>
    /// Returns the content JSON with the top-level "audio" block removed.
    /// The audio fields are surfaced via dedicated ActivityDto properties (audioUrl, audioStatus etc.)
    /// and must not be exposed raw in ContentJson to avoid leaking storage keys.
    /// </summary>
    private static string? StripAudioFromContentJson(string? contentJson)
    {
        if (contentJson is null) return null;
        try
        {
            using var doc = JsonDocument.Parse(contentJson);
            if (!doc.RootElement.TryGetProperty("audio", out _))
                return contentJson; // nothing to strip

            var dict = new Dictionary<string, JsonElement>();
            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                if (!prop.Name.Equals("audio", StringComparison.OrdinalIgnoreCase))
                    dict[prop.Name] = prop.Value.Clone();
            }
            return JsonSerializer.Serialize(dict, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        }
        catch
        {
            return contentJson;
        }
    }

    private static ActivityDto MapToDto(Domain.Entities.LearningActivity activity, InteractionMode? interactionMode)
    {
        var patternKey = string.IsNullOrWhiteSpace(activity.ExercisePatternKey) ? null : activity.ExercisePatternKey;

        // Form.io Practice Gym pilot — short-circuit before any AiGeneratedContentJson parsing
        // (that field is a harmless "{}" placeholder for Form.io-rendered activities, not
        // ModuleStageSchema content). FormIoSchemaJson is student-safe; nothing else is needed
        // for the client to render this activity.
        if (!string.IsNullOrWhiteSpace(activity.FormIoSchemaJson))
        {
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
                InteractionMode: interactionMode,
                ExercisePatternKey: patternKey,
                FormIoSchemaJson: activity.FormIoSchemaJson);
        }

        var contentJson = string.IsNullOrWhiteSpace(activity.AiGeneratedContentJson) ? null : activity.AiGeneratedContentJson;
        var rendererContentJson = patternKey is null ? null : StripAudioFromContentJson(contentJson);

        if (activity.ActivityType == ActivityType.VocabularyPractice)
        {
            var stageContent = BuildStageContent(activity.AiGeneratedContentJson, activity.Title);

            VocabPracticeContent? vpc = null;
            try
            {
                var vocabJson = stageContent is not null && stageContent.Practice.ExerciseData.ValueKind == JsonValueKind.Object
                    ? stageContent.Practice.ExerciseData.GetRawText()
                    : activity.AiGeneratedContentJson;
                vpc = JsonSerializer.Deserialize<VocabPracticeContent>(
                    vocabJson,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            catch { /* safe defaults */ }

            var vocabItems = vpc?.Items?.Select(i => new VocabPracticeItemDto(
                VocabularyItemId: i.VocabularyItemId,
                Term: i.Term ?? string.Empty,
                Prompt: i.Prompt ?? i.Example ?? string.Empty,
                Hint: i.Hint ?? string.Empty,
                Explanation: i.Explanation ?? i.Meaning ?? string.Empty)).ToList()
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
                Instructions: stageContent?.Practice.Instructions ?? vpc?.Instructions,
                PracticeMode: vpc?.PracticeMode,
                VocabItems: vocabItems,
                InteractionMode: interactionMode,
                ExercisePatternKey: patternKey,
                ContentJson: rendererContentJson,
                StageContent: stageContent);
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
                AudioStatus: audio == null ? "pending" : (audio.AudioAvailable ? "ready" : "unavailable"),
                InteractionMode: interactionMode,
                ExercisePatternKey: patternKey,
                ContentJson: rendererContentJson,
                StageContent: stageContent);
        }

        if (activity.ActivityType == ActivityType.SpeakingRolePlay)
        {
            var speakingStageContent = BuildStageContent(activity.AiGeneratedContentJson, activity.Title);

            SpeakingContent? sc = null;
            try
            {
                // For staged content, pull legacy-compatible fields from exerciseData; for flat JSON use root directly.
                var speakingJson = speakingStageContent is not null
                    && speakingStageContent.Practice.ExerciseData.ValueKind == JsonValueKind.Object
                    ? speakingStageContent.Practice.ExerciseData.GetRawText()
                    : activity.AiGeneratedContentJson;
                sc = JsonSerializer.Deserialize<SpeakingContent>(
                    speakingJson,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                // For staged content, pull scenario from practiceContent.scenario if available.
                if (speakingStageContent is not null && sc is not null)
                {
                    sc.Scenario ??= speakingStageContent.Practice.Scenario;
                }
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
                StudentRole: sc?.StudentRole ?? sc?.Role,
                SpeakingListenerRole: sc?.ListenerRole ?? sc?.PartnerRole,
                SpeakingGoal: sc?.SpeakingGoal,
                SpeakingPrompt: sc?.Prompt,
                ExpectedPoints: sc?.ExpectedPoints?.AsReadOnly() ?? sc?.SuccessChecklist?.AsReadOnly(),
                SuggestedPhrases: sc?.SuggestedPhrases?.AsReadOnly() ?? sc?.RequiredPhrases?.AsReadOnly(),
                MaxDurationSeconds: sc?.MaxDurationSeconds ?? 60,
                InteractionMode: interactionMode,
                ExercisePatternKey: patternKey,
                ContentJson: rendererContentJson,
                StageContent: speakingStageContent);
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
        if (LooksLikeLegacySpeaking(root))
            return AdaptLegacySpeaking(root, activityTitle);

        if (LooksLikeLegacyWriting(root))
            return AdaptLegacyWriting(root, activityTitle);

        if (LooksLikeLegacyVocabulary(root))
            return AdaptLegacyVocabulary(root, activityTitle);

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


    private static bool LooksLikeLegacyVocabulary(JsonElement root) =>
        root.ValueKind == JsonValueKind.Object
        && (root.TryGetProperty("practiceMode", out _)
            || root.TryGetProperty("items", out var items) && items.ValueKind == JsonValueKind.Array && !root.TryGetProperty("audioScript", out _));

    private static StageContentDto AdaptLegacyVocabulary(JsonElement root, string activityTitle)
    {
        var instructions = root.TryGetProperty("instructions", out var instr) ? instr.GetString() ?? "Practise the vocabulary items." : "Practise the vocabulary items.";
        var practiceMode = root.TryGetProperty("practiceMode", out var pm) ? pm.GetString() ?? "fill_blank" : "fill_blank";
        var examples = new List<LearnExampleDto>();
        if (root.TryGetProperty("items", out var items) && items.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in items.EnumerateArray().Take(5))
            {
                var term = item.TryGetProperty("term", out var termEl) ? termEl.GetString() ?? string.Empty : string.Empty;
                var meaning = item.TryGetProperty("explanation", out var expEl) ? expEl.GetString() ?? string.Empty : string.Empty;
                if (string.IsNullOrWhiteSpace(meaning) && item.TryGetProperty("meaning", out var meaningEl))
                    meaning = meaningEl.GetString() ?? string.Empty;
                var note = item.TryGetProperty("hint", out var hintEl) ? hintEl.GetString() : null;
                if (!string.IsNullOrWhiteSpace(term))
                    examples.Add(new LearnExampleDto(term, meaning, note));
            }
        }

        var learn = new LearnContentDto(
            TeachingTitle: activityTitle,
            Explanation: "This module teaches the target vocabulary before practice. Focus on meaning, usage, spelling, and natural workplace context.",
            KeyPoints: ["Understand the meaning before answering.", "Notice spelling and word form.", "Use each phrase in a professional context."],
            Examples: examples,
            Strategy: "Read the example sentence, recall the meaning, then practise using the word from context.",
            CommonMistakes: ["Choosing a similar word with the wrong meaning.", "Using the correct word with incorrect spelling."],
            SourceLanguageSupport: null);

        var practice = new PracticeContentDto(instructions, null, "Complete the vocabulary task.", root.Clone());

        var feedbackPlan = new FeedbackPlanDto(
            EvaluationCriteria: ["Meaning accuracy", "Context use", "Word form", "Spelling", "Collocation"],
            Rubric: [],
            FeedbackFocus: "Help the student remember meaning, usage, spelling, and natural collocations.",
            SuccessCriteria: ["The student identifies the correct meaning.", "The student uses the word in a suitable context."]);

        return new StageContentDto(ModuleStageSchema.LegacyAdaptedVersion, "vocabulary", ["reading", "writing"], "vocabulary_practice", learn, practice, feedbackPlan);
    }

    private static bool LooksLikeLegacySpeaking(JsonElement root) =>
        root.ValueKind == JsonValueKind.Object
        && (root.TryGetProperty("speakingGoal", out _)
            || root.TryGetProperty("studentRole", out _)
            || (root.TryGetProperty("prompt", out _) && root.TryGetProperty("listenerRole", out _)));

    private static StageContentDto AdaptLegacySpeaking(JsonElement root, string activityTitle)
    {
        var scenario = root.TryGetProperty("scenario", out var scn) ? scn.GetString() : null;
        var studentRole = root.TryGetProperty("studentRole", out var sr) ? sr.GetString() : null;
        var listenerRole = root.TryGetProperty("listenerRole", out var lr) ? lr.GetString() : null;
        var speakingGoal = root.TryGetProperty("speakingGoal", out var sg) ? sg.GetString() : null;
        var prompt = root.TryGetProperty("prompt", out var pr) ? pr.GetString() : null;
        var maxDuration = root.TryGetProperty("maxDurationSeconds", out var md) && md.TryGetInt32(out var mdv) ? mdv : 60;

        var suggestedPhrases = ReadStringArray(root, "suggestedPhrases");
        var expectedPoints = ReadStringArray(root, "expectedPoints");

        var learn = new LearnContentDto(
            TeachingTitle: activityTitle,
            Explanation: $"This module practises spoken workplace English. {(speakingGoal is not null ? speakingGoal : "Focus on clarity, professional tone, and a direct structure.")}",
            KeyPoints: ["State your purpose clearly.", "Match the tone to the listener.", "Keep the message short and direct."],
            Examples: suggestedPhrases.Take(3).Select(p => new LearnExampleDto(p, "Useful spoken phrase for this situation.", null)).ToList(),
            Strategy: "Before recording, decide your opening sentence, the key point, and a short closing.",
            CommonMistakes: ["Giving too much background.", "Using an informal tone in a professional setting."],
            SourceLanguageSupport: null);

        var practiceEnvelope = new
        {
            role = studentRole,
            partnerRole = listenerRole,
            situation = scenario,
            prompt = prompt ?? speakingGoal ?? "Record a short spoken response for this workplace situation.",
            expectedResponseLength = $"{maxDuration} seconds",
            tone = "professional",
            requiredPhrases = suggestedPhrases,
            successChecklist = expectedPoints.Length > 0 ? expectedPoints : ["Address the situation.", "Use a professional tone.", "Speak clearly."]
        };
        using var practiceDoc = JsonDocument.Parse(JsonSerializer.Serialize(practiceEnvelope));
        var practice = new PracticeContentDto(
            Instructions: "Record a short spoken response for this workplace situation.",
            Scenario: scenario,
            Task: speakingGoal,
            ExerciseData: practiceDoc.RootElement.Clone());

        var feedbackPlan = new FeedbackPlanDto(
            EvaluationCriteria: ["Task completion", "Fluency", "Pronunciation clarity", "Tone", "Grammar and vocabulary"],
            Rubric: [],
            FeedbackFocus: "Help the student improve fluency, pronunciation clarity, tone, and task completion.",
            SuccessCriteria: ["The response is clear and relevant.", "The tone fits the situation.", "The response can be understood by the listener."]);

        return new StageContentDto(ModuleStageSchema.LegacyAdaptedVersion, "speaking", ["listening", "vocabulary"], "speaking_roleplay", learn, practice, feedbackPlan);
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
        public string? CorrectAnswer { get; set; }
        public string? Hint { get; set; }
        public string? Explanation { get; set; }
        public string? Meaning { get; set; }
        public string? Example { get; set; }
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
        // Legacy flat fields
        public string? Scenario { get; set; }
        public string? StudentRole { get; set; }
        public string? ListenerRole { get; set; }
        public string? SpeakingGoal { get; set; }
        public string? Prompt { get; set; }
        public List<string>? ExpectedPoints { get; set; }
        public List<string>? SuggestedPhrases { get; set; }
        public int? MaxDurationSeconds { get; set; }
        // Staged exerciseData fields
        public string? Role { get; set; }
        public string? PartnerRole { get; set; }
        public string? Situation { get; set; }
        public string? Tone { get; set; }
        public List<string>? RequiredPhrases { get; set; }
        public List<string>? SuccessChecklist { get; set; }
    }

}

using System.Security.Claims;
using LinguaCoach.Application.Activity;
using LinguaCoach.Application.Ai;
using LinguaCoach.Application.Speaking;
using LinguaCoach.Domain;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Infrastructure.Activity;
using LinguaCoach.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace LinguaCoach.Api.Controllers;

[ApiController]
[Route("api/activity")]
[Authorize]
public sealed class ActivityController : ControllerBase
{
    private readonly IGetNextActivityHandler _getNextActivity;
    private readonly IGetActivityByIdHandler _getActivityById;
    private readonly ISubmitActivityAttemptHandler _submitAttempt;
    private readonly LinguaCoachDbContext _db;
    private readonly ListeningAudioService _listeningAudio;
    private readonly SpeakingAudioService _speakingAudio;
    private readonly ISpeechToTextService _stt;
    private readonly SpeakingRolePlayEvaluator _speakingEvaluator;
    private readonly IPatternEvaluationRouter _patternRouter;
    private readonly LinguaCoach.Application.Storage.IFileStorageService _storage;

    private static readonly TimeSpan SignedUrlExpiry = TimeSpan.FromMinutes(5);

    public ActivityController(
        IGetNextActivityHandler getNextActivity,
        IGetActivityByIdHandler getActivityById,
        ISubmitActivityAttemptHandler submitAttempt,
        LinguaCoachDbContext db,
        ListeningAudioService listeningAudio,
        SpeakingAudioService speakingAudio,
        ISpeechToTextService stt,
        SpeakingRolePlayEvaluator speakingEvaluator,
        IPatternEvaluationRouter patternRouter,
        LinguaCoach.Application.Storage.IFileStorageService storage)
    {
        _getNextActivity = getNextActivity;
        _getActivityById = getActivityById;
        _submitAttempt = submitAttempt;
        _db = db;
        _listeningAudio = listeningAudio;
        _speakingAudio = speakingAudio;
        _stt = stt;
        _speakingEvaluator = speakingEvaluator;
        _patternRouter = patternRouter;
        _storage = storage;
    }

    /// <summary>
    /// Returns the next recommended activity for the student.
    /// Primary: AI-generated or deterministic. Fallback: SystemFallback from seed data.
    /// </summary>
    [HttpGet("next")]
    [EnableRateLimiting("WritingAi")]
    public async Task<IActionResult> GetNext(
        [FromQuery] ActivityType? type = null,
        [FromQuery] string? pattern = null,
        CancellationToken ct = default)
    {
        var userId = GetCurrentUserId();
        if (userId == Guid.Empty) return Unauthorized();

        // pattern takes precedence over type when both are supplied.
        var query = !string.IsNullOrWhiteSpace(pattern)
            ? new GetNextActivityQuery(userId, PreferredPatternKey: pattern.Trim())
            : new GetNextActivityQuery(userId, PreferredType: type);

        try
        {
            var result = await _getNextActivity.HandleAsync(query, ct);
            return Ok(ToActivityResponse(result));
        }
        catch (AiServiceUnavailableException ex)
        {
            return StatusCode(503, new { error = "The AI service is not available. Please try again shortly.", retryable = true, featureKey = ex.FeatureKey });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>Returns a specific activity by ID. Used by the lesson page to load a prepared activity.</summary>
    [HttpGet("{activityId:guid}")]
    public async Task<IActionResult> GetById(Guid activityId, CancellationToken ct = default)
    {
        var userId = GetCurrentUserId();
        if (userId == Guid.Empty) return Unauthorized();

        try
        {
            var result = await _getActivityById.HandleAsync(new GetActivityByIdQuery(userId, activityId), ct);
            return Ok(ToActivityResponse(result));
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    [HttpGet("{activityId:guid}/audio")]
    public async Task<IActionResult> GetAudio(Guid activityId, CancellationToken ct = default)
    {
        var userId = GetCurrentUserId();
        if (userId == Guid.Empty) return Unauthorized();

        var profile = await _db.StudentProfiles.FirstOrDefaultAsync(p => p.UserId == userId, ct);
        if (profile is null) return Unauthorized();

        var activity = await _db.LearningActivities
            .FirstOrDefaultAsync(a => a.Id == activityId && a.IsActive, ct);
        if (activity is null || activity.ActivityType != ActivityType.ListeningComprehension)
            return NotFound();

        if (activity.LearningModuleId.HasValue)
        {
            var ownsActivity = await _db.LearningModules
                .Where(m => m.Id == activity.LearningModuleId.Value)
                .Join(_db.LearningPaths.Where(p => p.StudentProfileId == profile.Id),
                    m => m.LearningPathId,
                    p => p.Id,
                    (m, p) => m.Id)
                .AnyAsync(ct);
            if (!ownsActivity) return NotFound();
        }

        var audio = await _listeningAudio.GetAudioAsync(activity, ct);
        if (audio is null) return NotFound();
        return File(audio.Bytes, audio.ContentType);
    }

    /// <summary>
    /// Returns a short-lived signed URL (or, for local storage, the authenticated streaming
    /// endpoint) for an activity's listening audio. Response: { url, expiresAt }.
    /// Checks the AudioAsset table first, then falls back to the legacy JSON StorageKey.
    /// </summary>
    [HttpGet("{activityId:guid}/audio-url")]
    public async Task<IActionResult> GetAudioUrl(Guid activityId, CancellationToken ct = default)
    {
        var userId = GetCurrentUserId();
        if (userId == Guid.Empty) return Unauthorized();

        var profile = await _db.StudentProfiles.FirstOrDefaultAsync(p => p.UserId == userId, ct);
        if (profile is null) return Unauthorized();

        var activity = await _db.LearningActivities
            .FirstOrDefaultAsync(a => a.Id == activityId && a.IsActive, ct);
        if (activity is null || activity.ActivityType != ActivityType.ListeningComprehension)
            return NotFound();

        // Ownership check — same chain as the streaming endpoint.
        if (activity.LearningModuleId.HasValue)
        {
            var ownsActivity = await _db.LearningModules
                .Where(m => m.Id == activity.LearningModuleId.Value)
                .Join(_db.LearningPaths.Where(p => p.StudentProfileId == profile.Id),
                    m => m.LearningPathId, p => p.Id, (m, p) => m.Id)
                .AnyAsync(ct);
            if (!ownsActivity) return Forbid();
        }

        // 1. Prefer an AudioAsset row (new path).
        var asset = await _db.AudioAssets
            .Where(a => a.LearningActivityId == activityId
                     && a.AssetType == AssetType.ListeningTts
                     && a.GenerationStatus == GenerationStatus.Ready)
            .OrderByDescending(a => a.CreatedAtUtc)
            .FirstOrDefaultAsync(ct);

        var storageKey = asset?.ObjectKey;

        // 2. Fall back to the legacy JSON StorageKey for activities generated before AudioAsset.
        storageKey ??= _listeningAudio.GetStorageKey(activity);

        if (string.IsNullOrWhiteSpace(storageKey))
            return NotFound();

        var signed = await _storage.GenerateSignedUrlAsync(storageKey, SignedUrlExpiry, ct);

        // Local storage returns "local://{key}" — clients must use the streaming endpoint instead.
        var url = signed.Url.StartsWith("local://", StringComparison.OrdinalIgnoreCase)
                  || signed.Url.StartsWith("fake://", StringComparison.OrdinalIgnoreCase)
            ? $"/api/activity/{activityId}/audio"
            : signed.Url;

        return Ok(new
        {
            url,
            expiresAt = signed.ExpiresAt.ToUniversalTime().ToString("o")
        });
    }

    /// <summary>
    /// Submits a student attempt. Supports both WritingScenario (text) and VocabularyPractice (answers array).
    /// </summary>
    [HttpPost("{activityId:guid}/attempt")]
    [EnableRateLimiting("WritingAi")]
    public async Task<IActionResult> SubmitAttempt(
        Guid activityId,
        [FromBody] SubmitAttemptRequest request,
        CancellationToken ct = default)
    {
        var userId = GetCurrentUserId();
        if (userId == Guid.Empty) return Unauthorized();

        // For VocabularyPractice, answers array is used; submittedContent may be empty.
        // For WritingScenario, submittedContent is required.
        var hasContent = !string.IsNullOrWhiteSpace(request.SubmittedContent);
        var hasAnswers = request.Answers is { Count: > 0 };
        var hasResponseText = !string.IsNullOrWhiteSpace(request.ResponseText);

        if (!hasContent && !hasAnswers && !hasResponseText)
            return BadRequest(new { error = "Either SubmittedContent, Answers, or ResponseText is required." });

        var vocabAnswers = request.Answers?
            .Where(a => a.VocabularyItemId.HasValue)
            .Select(a => new VocabAnswerDto(a.VocabularyItemId!.Value, a.Answer ?? string.Empty))
            .ToList()
            as IReadOnlyList<VocabAnswerDto>;

        var listeningAnswers = request.Answers?
            .Where(a => !string.IsNullOrWhiteSpace(a.QuestionId))
            .Select(a => new ListeningAnswerDto(a.QuestionId!, a.Answer ?? string.Empty))
            .ToList()
            as IReadOnlyList<ListeningAnswerDto>;

        try
        {
            var result = await _submitAttempt.HandleAsync(
                new SubmitActivityAttemptCommand(
                    userId, activityId,
                    request.SubmittedContent ?? string.Empty,
                    request.AudioUrl,
                    vocabAnswers,
                    listeningAnswers,
                    request.ResponseText),
                ct);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Submits a spoken response for a SpeakingRolePlay activity.
    /// Accepts multipart/form-data with the audio file.
    /// Uses fake STT in MVP to produce a transcript, then AI evaluation.
    /// </summary>
    [HttpPost("{activityId:guid}/speaking-attempt")]
    [EnableRateLimiting("WritingAi")]
    [RequestSizeLimit(20 * 1024 * 1024)] // 20 MB hard ceiling
    public async Task<IActionResult> SubmitSpeakingAttempt(
        Guid activityId,
        IFormFile audioFile,
        [FromForm] double? durationSeconds = null,
        CancellationToken ct = default)
    {
        var userId = GetCurrentUserId();
        if (userId == Guid.Empty) return Unauthorized();

        if (audioFile is null || audioFile.Length == 0)
            return BadRequest(new { error = "Audio file is required." });

        var mimeType = audioFile.ContentType?.Split(';')[0].Trim() ?? string.Empty;
        if (!_speakingAudio.IsAllowedMimeType(mimeType))
            return BadRequest(new { error = $"Audio format '{mimeType}' is not supported. Use webm, wav, mp3, or mp4." });

        if (audioFile.Length > _speakingAudio.GetMaxAudioBytes())
            return BadRequest(new { error = "Recording is too large. Maximum size is 10 MB." });

        var profile = await _db.StudentProfiles
            .Include(p => p.LanguagePair).ThenInclude(lp => lp!.SourceLanguage)
            .Include(p => p.LanguagePair).ThenInclude(lp => lp!.TargetLanguage)
            .Include(p => p.CareerProfile)
            .FirstOrDefaultAsync(p => p.UserId == userId, ct);
        if (profile is null) return Unauthorized();

        var activity = await _db.LearningActivities
            .FirstOrDefaultAsync(a => a.Id == activityId && a.IsActive, ct);
        if (activity is null) return NotFound();

        if (activity.ActivityType != ActivityType.SpeakingRolePlay)
            return BadRequest(new { error = "This endpoint is for SpeakingRolePlay activities only." });

        // Ownership check — same chain as existing /attempt endpoint
        if (activity.LearningModuleId.HasValue)
        {
            var ownsActivity = await _db.LearningModules
                .Where(m => m.Id == activity.LearningModuleId.Value)
                .Join(_db.LearningPaths.Where(p => p.StudentProfileId == profile.Id),
                    m => m.LearningPathId,
                    p => p.Id,
                    (m, p) => m.Id)
                .AnyAsync(ct);
            if (!ownsActivity)
                return NotFound();
        }

        // Per-student audio file limit (enforced via DB count, not filesystem scan)
        if (await _speakingAudio.ExceedsStorageLimitAsync(profile.Id, ct))
            return BadRequest(new { error = "Speaking history is full (50 recordings). Contact your teacher to clear old recordings." });

        string? tempKey = null;
        try
        {
            // 1. Store audio to temp key
            await using var stream = audioFile.OpenReadStream();
            tempKey = await _speakingAudio.StoreTemporaryAsync(stream, mimeType, ct);

            // 2. Transcribe
            await using var sttStream = audioFile.OpenReadStream();
            var sttOptions = new SpeechToTextOptions(
                AudioMimeType: mimeType,
                TargetLanguageCode: profile.LanguagePair?.TargetLanguage?.Code ?? "en");
            var sttResult = await _stt.TranscribeAsync(sttStream, sttOptions, ct);

            if (!sttResult.Success || string.IsNullOrWhiteSpace(sttResult.Transcript))
            {
                await _speakingAudio.DeleteTemporaryAsync(tempKey, ct);
                return BadRequest(new { error = "Could not transcribe your recording. Please try again." });
            }

            // 3. AI evaluation — pattern-keyed activities route through the pattern
            // evaluation router; legacy SpeakingRolePlay activities (spoken_response_from_prompt
            // or no pattern key) keep using the original SpeakingRolePlayEvaluator.
            string feedbackJson;
            double score;
            string promptKey;
            ActivityFeedbackDto feedback;

            if (activity.ExercisePatternKey == ExercisePatternKey.SpeakingRoleplayTurn)
            {
                var evalResult = await _patternRouter.EvaluateAsync(
                    new PatternEvaluationRequest(
                        ActivityId: activity.Id,
                        StudentProfileId: profile.Id,
                        ExercisePatternKey: activity.ExercisePatternKey,
                        MarkingMode: MarkingMode.AiOpenEnded,
                        InteractionMode: InteractionMode.AudioResponse,
                        ActivityType: activity.ActivityType,
                        ContentJson: activity.AiGeneratedContentJson,
                        SubmittedAnswerJson: sttResult.Transcript!,
                        CefrLevel: profile.CefrLevel ?? "B1",
                        DomainComplexity: profile.CareerProfile?.Name ?? "General"),
                    ct);

                score = evalResult.Score;
                promptKey = "activity_evaluate_speaking_roleplay_turn";
                feedbackJson = System.Text.Json.JsonSerializer.Serialize(new
                {
                    score = evalResult.Score,
                    coachSummary = evalResult.CoachSummary,
                    improvements = evalResult.Corrections.Where(c => c.Category == "speaking").Select(c => c.Original).ToList(),
                    missingExpectedPoints = evalResult.Corrections.Where(c => c.Category == "missing_point").Select(c => c.Original).ToList(),
                    suggestedImprovedResponse = evalResult.SuggestedImprovedAnswer,
                });

                feedback = new ActivityFeedbackDto(
                    AttemptId: Guid.Empty, // replaced below once attempt is persisted
                    Score: score,
                    CoachSummary: evalResult.CoachSummary,
                    FocusFirst: false,
                    Changes: [],
                    CorrectedText: null,
                    WhatYouDidWell: [],
                    MainMistakes: [],
                    GrammarIssues: [],
                    VocabularyIssues: [],
                    ToneIssues: [],
                    ClarityIssues: [],
                    GrammarExplanation: null,
                    ToneExplanation: null,
                    VocabularyToRemember: [],
                    MiniLesson: null,
                    NextImprovementStep: null,
                    RewriteChallenge: null,
                    NextPracticeSuggestion: null,
                    FeedbackInSourceLanguage: null,
                    Transcript: sttResult.Transcript,
                    SpeakingStrengths: evalResult.Corrections.Where(c => c.Category == "speaking").Select(c => c.Suggestion).ToList(),
                    SpeakingImprovements: evalResult.Corrections.Where(c => c.Category == "speaking").Select(c => c.Original).OfType<string>().ToList(),
                    MissingExpectedPoints: evalResult.Corrections.Where(c => c.Category == "missing_point").Select(c => c.Original).OfType<string>().ToList(),
                    SuggestedImprovedResponse: evalResult.SuggestedImprovedAnswer);
            }
            else
            {
                (feedbackJson, score) = await _speakingEvaluator.EvaluateAsync(
                    transcript: sttResult.Transcript,
                    activityContentJson: activity.AiGeneratedContentJson,
                    cefrLevel: profile.CefrLevel ?? "B1",
                    careerContext: profile.CareerProfile?.Name ?? "General",
                    sourceLanguageName: profile.LanguagePair?.SourceLanguage?.Name ?? "Persian",
                    targetLanguageName: profile.LanguagePair?.TargetLanguage?.Name ?? "English",
                    ct: ct);
                promptKey = SpeakingRolePlayEvaluator.EvaluatePromptKey;
                feedback = SpeakingRolePlayEvaluator.ParseFeedback(Guid.Empty, feedbackJson, score);
            }

            // 4. Ensure transcript is embedded in feedbackJson for history retrieval
            var storedFeedbackJson = EnsureTranscriptInFeedbackJson(feedbackJson, sttResult.Transcript!);

            var attempt = new ActivityAttempt(
                studentProfileId: profile.Id,
                learningActivityId: activityId,
                submittedContent: sttResult.Transcript,
                feedbackJson: storedFeedbackJson,
                promptKey: promptKey,
                score: score,
                audioStorageKey: tempKey); // will be renamed below

            _db.ActivityAttempts.Add(attempt);
            await _db.SaveChangesAsync(ct);

            // 5. Commit audio to final key keyed by attemptId
            var finalKey = await _speakingAudio.CommitAudioAsync(tempKey, attempt.Id, mimeType, ct);
            tempKey = null; // already committed — nothing to delete on error

            // Update the stored key to the final name
            attempt.SetAudioStorageKey(finalKey);
            await _db.SaveChangesAsync(ct);

            // Ensure transcript and attemptId are always present in the returned DTO
            var finalFeedback = feedback with { AttemptId = attempt.Id, Transcript = feedback.Transcript ?? sttResult.Transcript };
            return Ok(finalFeedback);
        }
        catch (Exception) when (tempKey is not null)
        {
            await _speakingAudio.DeleteTemporaryAsync(tempKey, ct);
            return StatusCode(500, new { error = "Could not process your recording. Please try again." });
        }
    }

    [HttpGet("{activityId:guid}/attempts/{attemptId:guid}/audio")]
    public async Task<IActionResult> GetSpeakingAudio(
        Guid activityId, Guid attemptId, CancellationToken ct = default)
    {
        var userId = GetCurrentUserId();
        if (userId == Guid.Empty) return Unauthorized();

        var profile = await _db.StudentProfiles.FirstOrDefaultAsync(p => p.UserId == userId, ct);
        if (profile is null) return Unauthorized();

        var attempt = await _db.ActivityAttempts
            .FirstOrDefaultAsync(a => a.Id == attemptId
                                   && a.LearningActivityId == activityId
                                   && a.StudentProfileId == profile.Id, ct);
        if (attempt is null || string.IsNullOrWhiteSpace(attempt.AudioStorageKey))
            return NotFound();

        // Verify activity is SpeakingRolePlay
        var activity = await _db.LearningActivities
            .FirstOrDefaultAsync(a => a.Id == activityId && a.IsActive, ct);
        if (activity is null || activity.ActivityType != ActivityType.SpeakingRolePlay)
            return NotFound();

        var audio = await _speakingAudio.GetAudioAsync(attempt.AudioStorageKey, ct);
        if (audio is null) return NotFound();

        return File(audio.Bytes, audio.ContentType);
    }

    private static string EnsureTranscriptInFeedbackJson(string feedbackJson, string transcript)
    {
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(feedbackJson);
            if (doc.RootElement.TryGetProperty("transcript", out var t)
                && t.ValueKind == System.Text.Json.JsonValueKind.String
                && !string.IsNullOrWhiteSpace(t.GetString()))
                return feedbackJson; // already has transcript

            // Inject transcript field
            var dict = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, System.Text.Json.JsonElement>>(feedbackJson)
                ?? [];
            var merged = new Dictionary<string, object?>();
            foreach (var kvp in dict) merged[kvp.Key] = kvp.Value;
            merged["transcript"] = transcript;
            return System.Text.Json.JsonSerializer.Serialize(merged);
        }
        catch
        {
            return feedbackJson;
        }
    }

    private static object ToActivityResponse(ActivityDto dto) => new
    {
        activityId = dto.ActivityId,
        activityType = ToCamelCase(dto.ActivityType.ToString()),
        source = ToCamelCase(dto.Source.ToString()),
        title = dto.Title,
        difficulty = dto.Difficulty,
        // WritingScenario fields
        situation = dto.Situation,
        learningGoal = dto.LearningGoal,
        targetPhrases = dto.TargetPhrases,
        targetVocabulary = dto.TargetVocabulary,
        exampleText = dto.ExampleText,
        commonMistakeToAvoid = dto.CommonMistakeToAvoid,
        instructionInSourceLanguage = dto.InstructionInSourceLanguage,
        // VocabularyPractice fields
        instructions = dto.Instructions,
        practiceMode = dto.PracticeMode,
        vocabItems = dto.VocabItems?.Select(i => new
        {
            vocabularyItemId = i.VocabularyItemId,
            term = i.Term,
            prompt = i.Prompt,
            hint = i.Hint,
            explanation = i.Explanation,
        }),
        // ListeningComprehension fields. Transcript and expected answers are intentionally omitted.
        scenario = dto.Scenario,
        speakerRole = dto.SpeakerRole,
        listenerRole = dto.ListenerRole,
        transcriptAvailableAfterSubmit = dto.TranscriptAvailableAfterSubmit,
        listeningQuestions = dto.ListeningQuestions?.Select(q => new
        {
            id = q.Id,
            question = q.Question,
            type = q.Type,
        }),
        responseTask = dto.ResponseTask is null ? null : new
        {
            prompt = dto.ResponseTask.Prompt,
            expectedFocus = dto.ResponseTask.ExpectedFocus,
        },
        audioAvailable = dto.AudioAvailable,
        audioUrl = dto.AudioUrl,
        audioContentType = dto.AudioContentType,
        audioDurationSeconds = dto.AudioDurationSeconds,
        audioUnavailableMessage = dto.AudioUnavailableMessage,
        // SpeakingRolePlay fields
        speakingScenario = dto.SpeakingScenario,
        studentRole = dto.StudentRole,
        speakingListenerRole = dto.SpeakingListenerRole,
        speakingGoal = dto.SpeakingGoal,
        speakingPrompt = dto.SpeakingPrompt,
        expectedPoints = dto.ExpectedPoints,
        suggestedPhrases = dto.SuggestedPhrases,
        maxDurationSeconds = dto.MaxDurationSeconds,
        interactionMode = dto.InteractionMode is null ? null : ToCamelCase(dto.InteractionMode.Value.ToString()),
        exercisePatternKey = dto.ExercisePatternKey,
        contentJson = dto.ContentJson,
        stageContent = dto.StageContent is null ? null : new
        {
            schemaVersion = dto.StageContent.SchemaVersion,
            primarySkill = dto.StageContent.PrimarySkill,
            secondarySkills = dto.StageContent.SecondarySkills,
            exerciseType = dto.StageContent.ExerciseType,
            learn = new
            {
                teachingTitle = dto.StageContent.Learn.TeachingTitle,
                explanation = dto.StageContent.Learn.Explanation,
                keyPoints = dto.StageContent.Learn.KeyPoints,
                examples = dto.StageContent.Learn.Examples.Select(e => new
                {
                    phrase = e.Phrase,
                    meaning = e.Meaning,
                    note = e.Note,
                }),
                strategy = dto.StageContent.Learn.Strategy,
                commonMistakes = dto.StageContent.Learn.CommonMistakes,
                sourceLanguageSupport = dto.StageContent.Learn.SourceLanguageSupport,
            },
            practice = new
            {
                instructions = dto.StageContent.Practice.Instructions,
                scenario = dto.StageContent.Practice.Scenario,
                task = dto.StageContent.Practice.Task,
                exerciseData = dto.StageContent.Practice.ExerciseData,
            },
            feedbackPlan = new
            {
                evaluationCriteria = dto.StageContent.FeedbackPlan.EvaluationCriteria,
                rubric = dto.StageContent.FeedbackPlan.Rubric.Select(r => new
                {
                    criterion = r.Criterion,
                    description = r.Description,
                    weight = r.Weight,
                }),
                feedbackFocus = dto.StageContent.FeedbackPlan.FeedbackFocus,
                successCriteria = dto.StageContent.FeedbackPlan.SuccessCriteria,
            },
        },
    };

    private static string ToCamelCase(string s) =>
        string.IsNullOrEmpty(s) ? s : char.ToLowerInvariant(s[0]) + s[1..];

    private Guid GetCurrentUserId()
        => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub"), out var id) ? id : Guid.Empty;
}

public sealed record SubmitAttemptRequest(
    string? SubmittedContent,
    string? AudioUrl = null,
    IReadOnlyList<AnswerRequest>? Answers = null,
    string? ResponseText = null);

public sealed record AnswerRequest(Guid? VocabularyItemId, string? QuestionId, string? Answer);

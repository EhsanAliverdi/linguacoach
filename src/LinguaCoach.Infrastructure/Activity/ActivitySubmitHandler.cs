using System.Text.Json;
using System.Text.Json.Serialization;
using LinguaCoach.Application.Activity;
using LinguaCoach.Application.Learning;
using LinguaCoach.Application.LearningPlan;
using LinguaCoach.Application.Memory;
using LinguaCoach.Application.PracticeGym;
using LinguaCoach.Application.Sessions;
using LinguaCoach.Application.Vocabulary;
using LinguaCoach.Application.Writing;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Infrastructure.Ai;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LinguaCoach.Infrastructure.Activity;

public sealed class ActivitySubmitHandler : ISubmitActivityAttemptHandler
{
    private readonly LinguaCoachDbContext _db;
    private readonly IAiActivityGenerator _aiGenerator;
    private readonly IStudentMemoryService _memoryService;
    private readonly IVocabularyExtractionService _vocabExtraction;
    private readonly VocabularyPracticeEvaluator _vocabEvaluator;
    private readonly ListeningComprehensionEvaluator _listeningEvaluator;
    private readonly IPatternEvaluationRouter _patternRouter;
    private readonly IExercisePatternRepository _patternRepo;
    private readonly PatternSkillUpdateService _patternSkillUpdate;
    private readonly IMultiSkillProgressService _multiSkillProgress;
    private readonly IStudentLearningLedger _learningLedger;
    private readonly ILearningPlanService _learningPlan;
    private readonly ILearningGoalContextResolver _goalContextResolver;
    private readonly IPracticeGymSuggestionService _practiceGymSuggestions;
    private readonly IWritingEvaluationService _writingEvaluation;
    private readonly ILogger<ActivitySubmitHandler> _logger;

    public ActivitySubmitHandler(
        LinguaCoachDbContext db,
        IAiActivityGenerator aiGenerator,
        IStudentMemoryService memoryService,
        IVocabularyExtractionService vocabExtraction,
        VocabularyPracticeEvaluator vocabEvaluator,
        ListeningComprehensionEvaluator listeningEvaluator,
        IPatternEvaluationRouter patternRouter,
        IExercisePatternRepository patternRepo,
        PatternSkillUpdateService patternSkillUpdate,
        IMultiSkillProgressService multiSkillProgress,
        IStudentLearningLedger learningLedger,
        ILearningPlanService learningPlan,
        ILearningGoalContextResolver goalContextResolver,
        IPracticeGymSuggestionService practiceGymSuggestions,
        IWritingEvaluationService writingEvaluation,
        ILogger<ActivitySubmitHandler> logger)
    {
        _db = db;
        _aiGenerator = aiGenerator;
        _memoryService = memoryService;
        _vocabExtraction = vocabExtraction;
        _vocabEvaluator = vocabEvaluator;
        _listeningEvaluator = listeningEvaluator;
        _patternRouter = patternRouter;
        _patternRepo = patternRepo;
        _patternSkillUpdate = patternSkillUpdate;
        _multiSkillProgress = multiSkillProgress;
        _learningLedger = learningLedger;
        _learningPlan = learningPlan;
        _goalContextResolver = goalContextResolver;
        _practiceGymSuggestions = practiceGymSuggestions;
        _writingEvaluation = writingEvaluation;
        _logger = logger;
    }

    public async Task<ActivityFeedbackDto> HandleAsync(
        SubmitActivityAttemptCommand command,
        CancellationToken ct = default)
    {
        // VocabularyPractice may have empty SubmittedContent (answers go in VocabAnswers)
        var hasVocabAnswers = command.VocabAnswers is { Count: > 0 };
        var hasListeningAnswers = command.ListeningAnswers is { Count: > 0 };
        if (!hasVocabAnswers && !hasListeningAnswers)
        {
            if (string.IsNullOrWhiteSpace(command.SubmittedContent))
                throw new ArgumentException("SubmittedContent is required.", nameof(command));
            if (command.SubmittedContent.Length > 3000)
                throw new ArgumentException("SubmittedContent must be at most 3000 characters.", nameof(command));
        }

        var profile = await _db.StudentProfiles
            .Include(p => p.LanguagePair)
                .ThenInclude(lp => lp!.SourceLanguage)
            .Include(p => p.LanguagePair)
                .ThenInclude(lp => lp!.TargetLanguage)
            .Include(p => p.CareerProfile)
            .FirstOrDefaultAsync(p => p.UserId == command.UserId, ct)
            ?? throw new InvalidOperationException("Student profile not found.");

        if (profile.OnboardingStatus != Domain.Enums.OnboardingStatus.Complete)
            throw new InvalidOperationException("Activity attempt requires completed onboarding.");

        var activity = await _db.LearningActivities
            .FirstOrDefaultAsync(a => a.Id == command.ActivityId && a.IsActive, ct)
            ?? throw new InvalidOperationException("Activity not found.");
        var module = activity.LearningModuleId.HasValue
            ? await _db.LearningModules.FirstOrDefaultAsync(m => m.Id == activity.LearningModuleId.Value, ct)
            : null;
        if (module is not null)
        {
            var ownsModule = await _db.LearningPaths
                .AnyAsync(p => p.Id == module.LearningPathId && p.StudentProfileId == profile.Id, ct);
            if (!ownsModule)
                throw new InvalidOperationException("Activity not found.");
        }

        _logger.LogInformation(
            "Activity attempt submission received ActivityId={ActivityId} UserId={UserId} ActivityType={ActivityType} ExercisePatternKey={PatternKey}",
            command.ActivityId, command.UserId, activity.ActivityType, activity.ExercisePatternKey);

        // ── Pattern Evaluation Engine path ─────────────────────────────────────
        // When the activity has an ExercisePatternKey, route through the evaluator and
        // persist canonical evaluation fields. Legacy activities fall through to below.
        if (!string.IsNullOrWhiteSpace(activity.ExercisePatternKey))
        {
            return await HandlePatternEvaluationAsync(command, profile, activity, module, ct);
        }

        // VocabularyPractice: deterministic evaluation — no AI call.
        if (activity.ActivityType == Domain.Enums.ActivityType.VocabularyPractice)
        {
            var answers = command.VocabAnswers ?? [];
            var (vpFeedbackJson, vpScore) = await _vocabEvaluator.EvaluateAsync(
                profile.Id, activity.AiGeneratedContentJson, answers, ct);

            // Encode answers as submitted content JSON for audit trail
            var submittedContentJson = System.Text.Json.JsonSerializer.Serialize(new
            {
                answers = answers.Select(a => new { vocabularyItemId = a.VocabularyItemId, answer = a.Answer })
            });

            var vpAttempt = new ActivityAttempt(
                studentProfileId: profile.Id,
                learningActivityId: activity.Id,
                submittedContent: submittedContentJson,
                feedbackJson: vpFeedbackJson,
                promptKey: "vocabulary_practice_deterministic",
                score: vpScore);

            _db.ActivityAttempts.Add(vpAttempt);
            await _db.SaveChangesAsync(ct);

            _logger.LogInformation("VocabularyPractice attempt saved AttemptId={AttemptId} Score={Score}",
                vpAttempt.Id, vpScore);

            // Memory update not needed for vocabulary practice (it's not a writing attempt)
            // Vocabulary extraction not needed (no AI feedback to extract from)
            await TryConsumeReadinessItemAsync(command.UserId, profile.Id, activity.Id, ct);
            return ParseVocabularyFeedback(vpAttempt.Id, vpFeedbackJson, vpScore);
        }

        if (activity.ActivityType == ActivityType.ListeningComprehension)
        {
            var answers = command.ListeningAnswers ?? [];
            var (listeningFeedbackJson, listeningScore) = _listeningEvaluator.Evaluate(
                activity.AiGeneratedContentJson, answers, command.ResponseText);

            var submittedContentJson = JsonSerializer.Serialize(new
            {
                answers = answers.Select(a => new { questionId = a.QuestionId, answer = a.Answer }),
                responseText = command.ResponseText ?? command.SubmittedContent
            });

            var listeningAttempt = new ActivityAttempt(
                studentProfileId: profile.Id,
                learningActivityId: activity.Id,
                submittedContent: submittedContentJson,
                feedbackJson: listeningFeedbackJson,
                promptKey: "listening_comprehension_deterministic",
                score: listeningScore);

            _db.ActivityAttempts.Add(listeningAttempt);
            await _db.SaveChangesAsync(ct);

            _logger.LogInformation("ListeningComprehension attempt saved AttemptId={AttemptId} Score={Score}",
                listeningAttempt.Id, listeningScore);

            await TryConsumeReadinessItemAsync(command.UserId, profile.Id, activity.Id, ct);
            return ParseListeningFeedback(listeningAttempt.Id, listeningFeedbackJson, listeningScore);
        }

        // Evaluate with AI (WritingScenario and other future types).
        string feedbackJson;
        double? score = null;
        var promptKey = GetPromptKey(activity.ActivityType);

        try
        {
            var evalContext = new ActivityEvaluationContext(
                ActivityType: activity.ActivityType,
                ActivityContentJson: activity.AiGeneratedContentJson,
                StudentSubmission: command.SubmittedContent,
                CefrLevel: profile.CefrLevel ?? "B1",
                CareerContext: profile.CareerProfile?.Name ?? "General",
                SourceLanguageName: LanguageSupportResolver.ResolveSourceLanguageName(profile),
                TargetLanguageName: profile.LanguagePair?.TargetLanguage?.Name ?? "English",
                LearnerPreferenceContext: LearnerPreferenceContextFormatter.Build(
                    profile, profile.LanguagePair?.TargetLanguage?.Name),
                LearningGoalContext: _goalContextResolver.Resolve(
                    profile, new LearningGoalResolutionContext { Source = "ActivitySubmitHandler" }).ContextSummary);

            _logger.LogInformation("AI evaluation started ActivityId={ActivityId} PromptKey={PromptKey}",
                command.ActivityId, promptKey);
            feedbackJson = await _aiGenerator.EvaluateAttemptAsync(evalContext, ct);
            score = ExtractScore(feedbackJson);
            _logger.LogInformation("AI evaluation succeeded ActivityId={ActivityId} Score={Score}",
                command.ActivityId, score);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "AI evaluation failed ActivityId={ActivityId} UserId={UserId} ExceptionType={ExType} — saving attempt with empty feedback",
                command.ActivityId, command.UserId, ex.GetType().Name);
            feedbackJson = "{}";
        }

        var attempt = new ActivityAttempt(
            studentProfileId: profile.Id,
            learningActivityId: activity.Id,
            submittedContent: command.SubmittedContent,
            feedbackJson: feedbackJson,
            promptKey: promptKey,
            score: score,
            audioUrl: command.AudioUrl);

        _db.ActivityAttempts.Add(attempt);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Attempt saved AttemptId={AttemptId} ActivityId={ActivityId} Score={Score}",
            attempt.Id, activity.Id, score);

        // Phase 17A — Non-fatal writing evaluation trigger. Never blocks submission.
        if (activity.ActivityType == ActivityType.WritingScenario &&
            !string.IsNullOrWhiteSpace(command.SubmittedContent))
        {
            await _writingEvaluation.RequestEvaluationAsync(attempt.Id, profile.Id, activity.Id, ct);
        }

        var legacyLinkedExercise = await _db.SessionExercises
            .Where(e => e.LearningActivityId == activity.Id)
            .Select(e => new { ExerciseId = e.Id, SessionId = e.LearningSessionId })
            .FirstOrDefaultAsync(ct);

        var legacySource = legacyLinkedExercise is not null
            ? LearningEventSource.TodayLesson
            : LearningEventSource.PracticeGym;

        var legacyOutcome = score >= 70
            ? LearningEventOutcome.Practised
            : score.HasValue
                ? LearningEventOutcome.NeedsReview
                : LearningEventOutcome.Practised;

        var legacyGoalContext = _goalContextResolver.Resolve(profile, new LearningGoalResolutionContext { Source = "ActivitySubmitHandler.Legacy" });
        var legacyObjectiveKey = await TryGetReadinessObjectiveKeyAsync(profile.Id, activity.Id, ct);
        var legacyEvent = new StudentLearningEvent(
            studentProfileId: profile.Id,
            source: legacySource,
            outcome: legacyOutcome,
            activityId: activity.Id,
            sessionId: legacyLinkedExercise is not null ? (Guid?)legacyLinkedExercise.SessionId : null,
            sessionExerciseId: legacyLinkedExercise is not null ? (Guid?)legacyLinkedExercise.ExerciseId : null,
            activityAttemptId: attempt.Id,
            exerciseType: activity.ActivityType.ToString(),
            patternKey: activity.ExercisePatternKey,
            learningGoalContext: legacyGoalContext.ContextSummary,
            cefrLevelAtEvent: profile.CefrLevel,
            score: score.HasValue ? Math.Round(score.Value, 1) : null,
            normalizedScore: score.HasValue ? Math.Round(score.Value / 100.0, 4) : null,
            curriculumObjectiveKey: legacyObjectiveKey);

        await _learningLedger.RecordAsync(legacyEvent, ct);
        await TryUpdateLearningPlanProgressAsync(profile.Id, activity.ExercisePatternKey, ct);

        // Multi-skill progress: derive affected skills from ActivityType fallback (no pattern metadata here).
        var legacyMultiSkillReq = _multiSkillProgress.BuildRequest(
            studentProfileId: profile.Id,
            exercisePatternKey: activity.ExercisePatternKey,
            patternPrimarySkill: null,
            patternSecondarySkills: null,
            activityType: activity.ActivityType,
            normalizedScore: score ?? 0,
            completed: true,
            source: "legacy_activity");
        await _multiSkillProgress.ApplyAsync(legacyMultiSkillReq, ct);

        await _memoryService.UpdateMemoryAsync(new ActivityMemoryUpdateRequest(
            profile,
            activity,
            module,
            attempt,
            feedbackJson,
            score,
            CorrelationId: null), ct);

        // Best-effort vocabulary extraction — must not fail the submission
        var improvedVersion = ExtractImprovedVersion(feedbackJson);
        await _vocabExtraction.ExtractAsync(new ExtractVocabularyCommand(
            UserId: command.UserId,
            ActivityAttemptId: attempt.Id,
            ActivityId: activity.Id,
            ModuleId: activity.LearningModuleId,
            SubmittedContent: command.SubmittedContent,
            FeedbackJson: feedbackJson,
            ImprovedVersion: improvedVersion,
            CorrelationId: null), ct);

        // Best-effort readiness consumption — must not block or fail the response.
        await TryConsumeReadinessItemAsync(command.UserId, profile.Id, activity.Id, ct);

        return ParseFeedback(attempt.Id, feedbackJson, score);
    }

    private async Task<ActivityFeedbackDto> HandlePatternEvaluationAsync(
        SubmitActivityAttemptCommand command,
        Domain.Entities.StudentProfile profile,
        Domain.Entities.LearningActivity activity,
        Domain.Entities.LearningModule? module,
        CancellationToken ct)
    {
        var pattern = await _patternRepo.GetByKeyAsync(activity.ExercisePatternKey!, ct)
            ?? throw new InvalidOperationException(
                $"ExercisePatternDefinition not found for key '{activity.ExercisePatternKey}'.");

        var submittedAnswerJson = command.SubmittedContent;

        var studentSkillContext = await BuildStudentSkillContextAsync(
            profile.Id, activity.ExercisePatternKey, pattern.MarkingMode, ct);

        // Form.io Practice Gym pilot: ContentJson carries the student-safe schema (never the
        // legacy AiGeneratedContentJson placeholder), and ScoringRulesJson is sourced
        // server-side only — never derived from anything sent to the client.
        var isFormIoScored = pattern.MarkingMode == MarkingMode.FormIoScored;

        var evalRequest = new PatternEvaluationRequest(
            ActivityId: activity.Id,
            StudentProfileId: profile.Id,
            ExercisePatternKey: activity.ExercisePatternKey,
            MarkingMode: pattern.MarkingMode,
            InteractionMode: pattern.InteractionMode,
            ActivityType: activity.ActivityType,
            ContentJson: isFormIoScored ? (activity.FormIoSchemaJson ?? "{}") : activity.AiGeneratedContentJson,
            SubmittedAnswerJson: submittedAnswerJson,
            CefrLevel: profile.CefrLevel,
            DomainComplexity: profile.CareerProfile?.Name,
            StudentSkillContext: studentSkillContext,
            SourceLanguageName: LanguageSupportResolver.ResolveSourceLanguageName(profile),
            TargetLanguageName: LanguageSupportResolver.ResolveTargetLanguageName(profile),
            ScoringRulesJson: isFormIoScored ? activity.ScoringRulesJson : null);

        var evalResult = await _patternRouter.EvaluateAsync(evalRequest, ct);

        var evalResultJson = JsonSerializer.Serialize(evalResult, new JsonSerializerOptions(JsonSerializerDefaults.Web));

        // score field on ActivityAttempt is 0–100; map percentage for legacy compat
        double? legacyScore = evalResult.MaxScore > 0 ? evalResult.Percentage : null;

        var attempt = new ActivityAttempt(
            studentProfileId: profile.Id,
            learningActivityId: activity.Id,
            submittedContent: submittedAnswerJson,
            feedbackJson: evalResultJson,
            promptKey: $"pattern_evaluate_{pattern.Key}",
            score: legacyScore,
            submittedAnswerJson: submittedAnswerJson,
            evaluationResultJson: evalResultJson,
            maxScore: evalResult.MaxScore,
            percentage: evalResult.Percentage,
            passed: evalResult.Passed,
            completed: evalResult.Completed,
            markingMode: pattern.MarkingMode);

        _db.ActivityAttempts.Add(attempt);

        // Mark linked SessionExercise complete when evaluation says completed
        if (evalResult.Completed)
        {
            var exercise = await _db.SessionExercises
                .FirstOrDefaultAsync(e => e.LearningActivityId == activity.Id, ct);

            if (exercise is not null && exercise.Status != Domain.Enums.ExerciseStatus.Completed)
            {
                exercise.Complete();
            }
        }

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Pattern attempt saved AttemptId={AttemptId} PatternKey={PatternKey} MarkingMode={MarkingMode} Score={Score}/{MaxScore} Passed={Passed}",
            attempt.Id, pattern.Key, pattern.MarkingMode, evalResult.Score, evalResult.MaxScore, evalResult.Passed);

        // Best-effort post-submission updates — must not fail activity submission.
        await _patternSkillUpdate.ApplyAsync(profile.Id, evalResult, activity.ExercisePatternKey, ct);

        // Multi-skill progress: derive affected skills from pattern metadata, then update all.
        var patternMultiSkillReq = _multiSkillProgress.BuildRequest(
            studentProfileId: profile.Id,
            exercisePatternKey: activity.ExercisePatternKey,
            patternPrimarySkill: pattern.PrimarySkill,
            patternSecondarySkills: DeserialiseSecondarySkills(pattern.SecondarySkillsJson),
            activityType: activity.ActivityType,
            normalizedScore: evalResult.Percentage,
            completed: evalResult.Completed,
            source: "pattern_evaluation");
        await _multiSkillProgress.ApplyAsync(patternMultiSkillReq, ct);

        // Determine whether this came from a Today lesson or Practice Gym.
        // A linked SessionExercise means Today lesson; no link means Practice Gym.
        var linkedExercise = await _db.SessionExercises
            .Where(e => e.LearningActivityId == activity.Id)
            .Select(e => new { ExerciseId = e.Id, SessionId = e.LearningSessionId })
            .FirstOrDefaultAsync(ct);

        var ledgerSource = linkedExercise is not null
            ? LearningEventSource.TodayLesson
            : LearningEventSource.PracticeGym;

        var ledgerOutcome = evalResult.Passed == true
            ? LearningEventOutcome.Mastered
            : evalResult.Percentage >= 50
                ? LearningEventOutcome.Practised
                : LearningEventOutcome.NeedsReview;

        var primarySkillKey = PatternSkillUpdateService.GetPrimarySkillKey(activity.ExercisePatternKey);
        var mistakeTags = evalResult.Corrections.Count > 0
            ? System.Text.Json.JsonSerializer.Serialize(
                evalResult.Corrections.Take(5).Select(c => c.Category).Distinct())
            : null;

        var patternGoalContext = _goalContextResolver.Resolve(profile, new LearningGoalResolutionContext { Source = "ActivitySubmitHandler.Pattern" });
        var patternObjectiveKey = await TryGetReadinessObjectiveKeyAsync(profile.Id, activity.Id, ct);
        var learningEvent = new StudentLearningEvent(
            studentProfileId: profile.Id,
            source: ledgerSource,
            outcome: ledgerOutcome,
            activityId: activity.Id,
            sessionId: linkedExercise is not null ? (Guid?)linkedExercise.SessionId : null,
            sessionExerciseId: linkedExercise is not null ? (Guid?)linkedExercise.ExerciseId : null,
            activityAttemptId: attempt.Id,
            exerciseType: activity.ActivityType.ToString(),
            patternKey: activity.ExercisePatternKey,
            primarySkill: primarySkillKey,
            learningGoalContext: patternGoalContext.ContextSummary,
            cefrLevelAtEvent: profile.CefrLevel,
            curriculumObjectiveKey: patternObjectiveKey,
            score: Math.Round(evalResult.Percentage, 1),
            normalizedScore: Math.Round(evalResult.Percentage / 100.0, 4),
            mistakeTagsJson: mistakeTags);

        await _learningLedger.RecordAsync(learningEvent, ct);
        await TryUpdateLearningPlanProgressAsync(profile.Id, activity.ExercisePatternKey, ct);

        var memoryRequest = BuildPatternMemoryUpdateRequest(profile, activity, module, attempt, evalResult);
        await _memoryService.UpdateMemoryAsync(memoryRequest, ct);

        // Best-effort vocabulary extraction — only when the evaluator produced AI corrections
        // (AiStructured/AiOpenEnded patterns). Deterministic evaluators (ExactMatch,
        // KeyedSelection, NoMarking) never populate Corrections, so this never triggers
        // an AI call for gap fill / phrase match, preserving the no-AI guarantee for them.
        if (evalResult.Corrections.Count > 0)
        {
            await _vocabExtraction.ExtractAsync(new ExtractVocabularyCommand(
                UserId: command.UserId,
                ActivityAttemptId: attempt.Id,
                ActivityId: activity.Id,
                ModuleId: activity.LearningModuleId,
                SubmittedContent: submittedAnswerJson,
                FeedbackJson: evalResultJson,
                ImprovedVersion: evalResult.SuggestedImprovedAnswer,
                CorrelationId: null), ct);
        }
        else
        {
            _logger.LogInformation(
                "VocabularyExtraction skipped — no corrections ActivityAttemptId={ActivityAttemptId} ExercisePatternKey={ExercisePatternKey}",
                attempt.Id, activity.ExercisePatternKey);
        }

        // Best-effort readiness consumption — must not block or fail the response.
        if (evalResult.Completed)
            await TryConsumeReadinessItemAsync(command.UserId, profile.Id, activity.Id, ct);

        var patternDto = BuildPatternEvaluationDto(activity.ExercisePatternKey, pattern.MarkingMode, evalResult);

        return new ActivityFeedbackDto(
            AttemptId: attempt.Id,
            Score: legacyScore,
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
            PatternEvaluation: patternDto);
    }

    /// <summary>
    /// Best-effort lookup of the CurriculumObjectiveKey snapshot on the readiness item linked to
    /// this activity, if any — read before consumption (item must still be Reserved). Returns
    /// null on any failure or when no linked item exists; never throws. See
    /// docs/reviews/2026-07-07-ai-bank-assessment-architecture-plan.md, Phase 8.
    /// </summary>
    private async Task<string?> TryGetReadinessObjectiveKeyAsync(Guid profileId, Guid activityId, CancellationToken ct)
    {
        try
        {
            return await _db.StudentActivityReadinessItems
                .AsNoTracking()
                .Where(i => i.StudentId == profileId
                         && i.LearningActivityId == activityId
                         && i.Status == Domain.Enums.ReadinessPoolStatus.Reserved)
                .Select(i => i.CurriculumObjectiveKey)
                .FirstOrDefaultAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Best-effort readiness objective key lookup failed ActivityId={ActivityId} — event recorded without it",
                activityId);
            return null;
        }
    }

    /// <summary>
    /// Builds a short human-readable summary of the student's current standing on the
    /// skill primarily targeted by this exercise pattern, for AI evaluation prompts to
    /// Looks up a Reserved readiness item linked to the completed activity and marks it
    /// consumed. Best-effort: logs and swallows any exception so completion is never blocked.
    /// <paramref name="userId"/> is the auth user ID passed to TryMarkConsumedAsync for profile resolution.
    /// <paramref name="profileId"/> is the StudentProfile.Id used to scope the DB lookup.
    private async Task TryConsumeReadinessItemAsync(Guid userId, Guid profileId, Guid activityId, CancellationToken ct)
    {
        try
        {
            var item = await _db.StudentActivityReadinessItems
                .FirstOrDefaultAsync(
                    i => i.StudentId == profileId
                      && i.LearningActivityId == activityId
                      && i.Status == Domain.Enums.ReadinessPoolStatus.Reserved,
                    ct);

            if (item is null) return;

            await _practiceGymSuggestions.TryMarkConsumedAsync(userId, item.Id, ct);

            _logger.LogInformation(
                "ReadinessItem consumed ActivityId={ActivityId} ReadinessItemId={ReadinessItemId}",
                activityId, item.Id);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Best-effort readiness consumption failed ActivityId={ActivityId} — completion not affected",
                activityId);
        }
    }

    /// reference (so coachSummary can be grounded in student progress, not generic).
    /// Only meaningful for AI-marked patterns — returns null for deterministic ones.
    /// </summary>
    private async Task<string?> BuildStudentSkillContextAsync(
        Guid studentProfileId, string? exercisePatternKey, MarkingMode markingMode, CancellationToken ct)
    {
        if (markingMode is not (MarkingMode.AiStructured or MarkingMode.AiOpenEnded))
            return null;

        var skillKey = PatternSkillUpdateService.GetPrimarySkillKey(exercisePatternKey);
        if (skillKey is null) return null;

        var profile = await _db.StudentSkillProfiles
            .Where(x => x.StudentProfileId == studentProfileId && x.SkillKey == skillKey)
            .Select(x => new { x.SkillLabel, x.ScorePercent })
            .FirstOrDefaultAsync(ct);

        if (profile is null) return null;

        var standing = profile.ScorePercent switch
        {
            < 50 => "an area the student is still developing — give specific, encouraging guidance on this skill",
            < 75 => "an area the student is making steady progress in",
            _ => "an area the student is already strong in — acknowledge this and focus feedback elsewhere",
        };

        return $"{profile.SkillLabel} ({profile.ScorePercent}/100): {standing}.";
    }

    private static PatternEvaluationDto BuildPatternEvaluationDto(
        string? patternKey,
        Domain.Enums.MarkingMode markingMode,
        PatternEvaluationResult result)
    {
        var itemResults = result.ItemResults.Select(i => new PatternEvaluationItemResultDto(
            ItemKey: i.ItemKey,
            StudentAnswer: i.StudentAnswer,
            CorrectAnswer: i.CorrectAnswer,
            AcceptedAnswers: i.AcceptedAnswers,
            IsCorrect: i.IsCorrect,
            Score: i.Score,
            MaxScore: i.MaxScore,
            Feedback: i.Feedback)).ToList();

        var corrections = result.Corrections.Select(c => new PatternEvaluationCorrectionDto(
            Category: c.Category,
            Original: c.Original,
            Suggestion: c.Suggestion,
            Explanation: c.Explanation)).ToList();

        var skillImpacts = result.SkillImpacts.Select(s => new PatternEvaluationSkillImpactDto(
            SkillKey: s.SkillKey,
            Label: s.Label,
            Delta: s.Delta,
            Evidence: s.Evidence)).ToList();

        var memorySignals = result.MemorySignals.Select(m => new PatternEvaluationMemorySignalDto(
            Type: m.Type,
            Key: m.Key,
            Summary: m.Summary,
            Confidence: m.Confidence)).ToList();

        return new PatternEvaluationDto(
            ExercisePatternKey: patternKey,
            MarkingMode: markingMode,
            Score: result.Score,
            MaxScore: result.MaxScore,
            Percentage: result.Percentage,
            Passed: result.Passed,
            Completed: result.Completed,
            ItemResults: itemResults,
            CoachSummary: result.CoachSummary,
            Corrections: corrections,
            SuggestedImprovedAnswer: result.SuggestedImprovedAnswer,
            SkillImpacts: skillImpacts,
            MemorySignals: memorySignals);
    }

    private static ActivityMemoryUpdateRequest BuildPatternMemoryUpdateRequest(
        Domain.Entities.StudentProfile profile,
        Domain.Entities.LearningActivity activity,
        Domain.Entities.LearningModule? module,
        ActivityAttempt attempt,
        PatternEvaluationResult evalResult)
    {
        // Build a compact feedback JSON from evaluation fields — never include raw submitted text.
        var compactFeedback = JsonSerializer.Serialize(new
        {
            exercisePatternKey = activity.ExercisePatternKey,
            activityType = activity.ActivityType.ToString(),
            score = evalResult.Percentage,
            passed = evalResult.Passed,
            coachSummary = evalResult.CoachSummary,
            topCorrections = evalResult.Corrections.Take(3).Select(c => new { c.Category, c.Suggestion }),
            skillImpacts = evalResult.SkillImpacts.Take(5).Select(s => new { s.SkillKey, s.Delta }),
            memorySignals = evalResult.MemorySignals.Take(3).Select(m => new { m.Type, m.Key, m.Summary }),
        }, new JsonSerializerOptions(JsonSerializerDefaults.Web));

        return new ActivityMemoryUpdateRequest(
            StudentProfile: profile,
            Activity: activity,
            Module: module,
            Attempt: attempt,
            FeedbackJson: compactFeedback,
            Score: evalResult.MaxScore > 0 ? evalResult.Percentage : null,
            CorrelationId: attempt.Id.ToString());
    }

    private static ActivityFeedbackDto ParseVocabularyFeedback(Guid attemptId, string feedbackJson, double score)
    {
        // Parse vocab feedback into the standard ActivityFeedbackDto shape.
        VocabFeedbackPayload? payload = null;
        try
        {
            payload = JsonSerializer.Deserialize<VocabFeedbackPayload>(feedbackJson,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch { /* safe defaults */ }

        return new ActivityFeedbackDto(
            AttemptId: attemptId,
            Score: score,
            CoachSummary: payload?.CoachSummary,
            FocusFirst: false,
            Changes: [],
            CorrectedText: null,
            WhatYouDidWell: payload?.WhatYouDidWell ?? [],
            MainMistakes: payload?.MainMistakes ?? [],
            GrammarIssues: [],
            VocabularyIssues: [],
            ToneIssues: [],
            ClarityIssues: [],
            GrammarExplanation: null,
            ToneExplanation: null,
            VocabularyToRemember: [],
            MiniLesson: payload?.MiniLesson,
            NextImprovementStep: payload?.NextImprovementStep,
            RewriteChallenge: null,
            NextPracticeSuggestion: null,
            FeedbackInSourceLanguage: null);
    }

    private static ActivityFeedbackDto ParseListeningFeedback(Guid attemptId, string feedbackJson, double score)
    {
        ListeningFeedbackPayload? payload = null;
        try
        {
            payload = JsonSerializer.Deserialize<ListeningFeedbackPayload>(feedbackJson,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch { /* safe defaults */ }

        var questions = payload?.QuestionFeedback?.Select(q => new ListeningQuestionFeedbackDto(
            q.QuestionId ?? string.Empty,
            q.Question ?? string.Empty,
            q.StudentAnswer ?? string.Empty,
            q.ExpectedAnswerSummary ?? string.Empty,
            q.IsCorrect,
            q.Score,
            q.Feedback ?? string.Empty)).ToList()
            as IReadOnlyList<ListeningQuestionFeedbackDto> ?? [];

        return new ActivityFeedbackDto(
            AttemptId: attemptId,
            Score: score,
            CoachSummary: payload?.CoachSummary,
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
            MiniLesson: payload?.MiniLesson,
            NextImprovementStep: payload?.NextImprovementStep,
            RewriteChallenge: null,
            NextPracticeSuggestion: null,
            FeedbackInSourceLanguage: null,
            QuestionFeedback: questions,
            Transcript: payload?.Transcript,
            ResponseFeedback: payload?.ResponseFeedback);
    }

    private static string? ExtractImprovedVersion(string feedbackJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(feedbackJson);
            if (doc.RootElement.TryGetProperty("improvedVersion", out var iv)
                && iv.ValueKind == JsonValueKind.String)
                return iv.GetString();
        }
        catch { /* ignore */ }
        return null;
    }

    private static string GetPromptKey(ActivityType type) => type switch
    {
        ActivityType.WritingScenario => "activity_evaluate_writing",
        _ => $"activity_evaluate_{type.ToString().ToLowerInvariant()}"
    };

    private static double? ExtractScore(string feedbackJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(feedbackJson);
            if (doc.RootElement.TryGetProperty("overallScore", out var s)
                && s.ValueKind == JsonValueKind.Number
                && s.TryGetDouble(out var val)
                && val is >= 0 and <= 100)
                return val;
        }
        catch { /* ignore */ }
        return null;
    }

    private async Task TryUpdateLearningPlanProgressAsync(
        Guid studentProfileId, string? objectiveKey, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(objectiveKey))
            return;

        var result = await _learningPlan.TryUpdateObjectiveProgressAsync(studentProfileId, objectiveKey, ct);
        if (result.StatusChanged)
            _logger.LogInformation(
                "ActivitySubmitHandler: real-time plan update — objective '{Key}' {Prev} → {New}",
                objectiveKey, result.PreviousStatus, result.NewStatus);
    }

    private static ActivityFeedbackDto ParseFeedback(Guid attemptId, string feedbackJson, double? score)
    {
        ActivityFeedbackPayload? payload = null;
        try
        {
            payload = JsonSerializer.Deserialize<ActivityFeedbackPayload>(
                feedbackJson,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch { /* return safe defaults */ }

        var changes = payload?.Changes?
            .Select(c => new FeedbackChangeDto(
                Type: c.Type ?? "replace",
                Original: c.Original,
                Suggested: c.Suggested,
                Reason: c.Reason,
                Category: c.Category,
                Severity: c.Severity))
            .ToList()
            ?? (IReadOnlyList<FeedbackChangeDto>)[];

        return new ActivityFeedbackDto(
            AttemptId: attemptId,
            Score: score ?? payload?.OverallScore,
            CoachSummary: payload?.CoachSummary,
            FocusFirst: payload?.FocusFirst ?? false,
            Changes: changes,
            // improvedVersion is the primary improved text; fall back to correctedEmail for legacy prompts
            CorrectedText: payload?.ImprovedVersion ?? payload?.CorrectedEmail,
            WhatYouDidWell: payload?.WhatYouDidWell ?? [],
            MainMistakes: payload?.MainMistakes ?? [],
            GrammarIssues: payload?.GrammarIssues ?? [],
            VocabularyIssues: payload?.VocabularyIssues ?? [],
            ToneIssues: payload?.ToneIssues ?? [],
            ClarityIssues: payload?.ClarityIssues ?? [],
            GrammarExplanation: payload?.GrammarExplanation,
            ToneExplanation: payload?.ToneExplanation,
            VocabularyToRemember: payload?.VocabularyToRemember ?? [],
            MiniLesson: payload?.MiniLesson,
            NextImprovementStep: payload?.NextImprovementStep,
            RewriteChallenge: payload?.RewriteChallenge,
            NextPracticeSuggestion: payload?.NextPracticeSuggestion,
            FeedbackInSourceLanguage: payload?.FeedbackInSourceLanguage);
    }

    private static IReadOnlyList<string> DeserialiseSecondarySkills(string? secondarySkillsJson)
    {
        if (string.IsNullOrWhiteSpace(secondarySkillsJson) || secondarySkillsJson == "[]")
            return [];
        try
        {
            return JsonSerializer.Deserialize<List<string>>(secondarySkillsJson) ?? [];
        }
        catch
        {
            return [];
        }
    }
}

internal sealed class VocabFeedbackPayload
{
    [JsonPropertyName("overallScore")] public double? OverallScore { get; set; }
    [JsonPropertyName("coachSummary")] public string? CoachSummary { get; set; }
    [JsonPropertyName("miniLesson")] public string? MiniLesson { get; set; }
    [JsonPropertyName("nextImprovementStep")] public string? NextImprovementStep { get; set; }
    [JsonPropertyName("whatYouDidWell")] public List<string>? WhatYouDidWell { get; set; }
    [JsonPropertyName("mainMistakes")] public List<string>? MainMistakes { get; set; }
}

internal sealed class ActivityFeedbackChangePayload
{
    [JsonPropertyName("type")] public string? Type { get; set; }
    [JsonPropertyName("original")] public string? Original { get; set; }
    [JsonPropertyName("suggested")] public string? Suggested { get; set; }
    [JsonPropertyName("reason")] public string? Reason { get; set; }
    [JsonPropertyName("category")] public string? Category { get; set; }
    [JsonPropertyName("severity")] public string? Severity { get; set; }
}

internal sealed class ActivityFeedbackPayload
{
    [JsonPropertyName("overallScore")] public double? OverallScore { get; set; }
    [JsonPropertyName("coachSummary")] public string? CoachSummary { get; set; }
    [JsonPropertyName("focusFirst")] public bool? FocusFirst { get; set; }
    [JsonPropertyName("changes")] public List<ActivityFeedbackChangePayload>? Changes { get; set; }
    // Legacy field — kept for old prompt responses
    [JsonPropertyName("correctedEmail")] public string? CorrectedEmail { get; set; }
    // New field — preferred improved version label
    [JsonPropertyName("improvedVersion")] public string? ImprovedVersion { get; set; }
    [JsonPropertyName("feedbackInSourceLanguage")] public string? FeedbackInSourceLanguage { get; set; }
    [JsonPropertyName("whatYouDidWell")] public List<string>? WhatYouDidWell { get; set; }
    [JsonPropertyName("mainMistakes")] public List<string>? MainMistakes { get; set; }
    [JsonPropertyName("grammarIssues")] public List<string>? GrammarIssues { get; set; }
    [JsonPropertyName("vocabularyIssues")] public List<string>? VocabularyIssues { get; set; }
    [JsonPropertyName("toneIssues")] public List<string>? ToneIssues { get; set; }
    [JsonPropertyName("clarityIssues")] public List<string>? ClarityIssues { get; set; }
    [JsonPropertyName("grammarExplanation")] public string? GrammarExplanation { get; set; }
    [JsonPropertyName("toneExplanation")] public string? ToneExplanation { get; set; }
    [JsonPropertyName("vocabularyToRemember")] public List<string>? VocabularyToRemember { get; set; }
    [JsonPropertyName("miniLesson")] public string? MiniLesson { get; set; }
    [JsonPropertyName("nextImprovementStep")] public string? NextImprovementStep { get; set; }
    [JsonPropertyName("rewriteChallenge")] public string? RewriteChallenge { get; set; }
    [JsonPropertyName("nextPracticeSuggestion")] public string? NextPracticeSuggestion { get; set; }
}

internal sealed class ListeningFeedbackPayload
{
    [JsonPropertyName("overallScore")] public double? OverallScore { get; set; }
    [JsonPropertyName("coachSummary")] public string? CoachSummary { get; set; }
    [JsonPropertyName("questionFeedback")] public List<ListeningQuestionFeedbackPayload>? QuestionFeedback { get; set; }
    [JsonPropertyName("transcript")] public string? Transcript { get; set; }
    [JsonPropertyName("responseFeedback")] public string? ResponseFeedback { get; set; }
    [JsonPropertyName("miniLesson")] public string? MiniLesson { get; set; }
    [JsonPropertyName("nextImprovementStep")] public string? NextImprovementStep { get; set; }
}

internal sealed class ListeningQuestionFeedbackPayload
{
    [JsonPropertyName("questionId")] public string? QuestionId { get; set; }
    [JsonPropertyName("question")] public string? Question { get; set; }
    [JsonPropertyName("studentAnswer")] public string? StudentAnswer { get; set; }
    [JsonPropertyName("expectedAnswerSummary")] public string? ExpectedAnswerSummary { get; set; }
    [JsonPropertyName("isCorrect")] public bool IsCorrect { get; set; }
    [JsonPropertyName("score")] public double Score { get; set; }
    [JsonPropertyName("feedback")] public string? Feedback { get; set; }
}

using LinguaCoach.Application.Activity;
using LinguaCoach.Application.ActivityTemplates;
using LinguaCoach.Application.Curriculum;
using LinguaCoach.Application.LearningPlan;
using LinguaCoach.Application.Learning;
using LinguaCoach.Application.Mastery;
using LinguaCoach.Application.PracticeGym;
using LinguaCoach.Application.ReadinessPool;
using LinguaCoach.Application.Sessions;
using LinguaCoach.Domain;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Infrastructure.Ai;
using LinguaCoach.Infrastructure.Activity;
using LinguaCoach.Infrastructure.Curriculum;
using LinguaCoach.Infrastructure.Progress;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Quartz;

namespace LinguaCoach.Infrastructure.Jobs;

/// <summary>
/// Materializes queued Practice Gym cache entries into ready LearningActivity rows.
/// The student path can still generate on demand if this cache is empty.
/// </summary>
[DisallowConcurrentExecution]
public sealed class PracticeGymGenerationJob : IJob
{
    public const string JobName = "practice-gym-generation";
    private const int MaxItemsPerRun = 20;

    /// <summary>
    /// Pattern keys allowed to attempt the bank-first Form.io template path (Phase C1, 2026-07-08
    /// — generalizes the original single-pattern pilot to a small first batch). Any pattern NOT
    /// in this set always uses the legacy freeform <see cref="IAiActivityGenerator"/> path,
    /// unchanged. Adding/removing a key here is the safe, code-level way to expand or roll back
    /// which patterns attempt the template path — no admin UI, per Phase C1 scope. The master
    /// <see cref="IPracticeGymFormIoTemplatePilotSettingsProvider"/> toggle remains the single
    /// admin-editable kill switch covering every key in this set.
    /// See docs/architecture/practice-gym.md.
    /// </summary>
    private static readonly HashSet<string> TemplateMigratedPatternKeys = new(StringComparer.Ordinal)
    {
        ExercisePatternKey.FormIoPracticeGymPilot,
        ExercisePatternKey.PhraseMatch,
        ExercisePatternKey.GapFillWorkplacePhrase,
        ExercisePatternKey.ReadingMultipleChoiceSingle,
    };

    private readonly LinguaCoachDbContext _db;
    private readonly IAiActivityGenerator _aiGenerator;
    private readonly IExercisePatternRepository _patternRepo;
    private readonly StudentProgressService _progress;
    private readonly ListeningAudioService _listeningAudio;
    private readonly ILearningGoalContextResolver _goalContextResolver;
    private readonly ICurriculumRoutingService _routing;
    private readonly IStudentMasteryEvaluationService _mastery;
    private readonly IStudentActivityReadinessPoolService _readinessPool;
    private readonly ILearningPlanService _learningPlan;
    private readonly IPracticeGymFormIoTemplatePilotSettingsProvider _formIoPilotSettings;
    private readonly IActivityTemplateInstanceGenerator _templateGenerator;
    private readonly IActivityNoveltyPolicy _noveltyPolicy;
    private readonly IActivityContentFingerprintService _fingerprintService;
    private readonly ILogger<PracticeGymGenerationJob> _logger;

    private const int MaxTemplateGenerationAttempts = 2; // bounded retry on duplicate content — never unbounded

    public PracticeGymGenerationJob(
        LinguaCoachDbContext db,
        IAiActivityGenerator aiGenerator,
        IExercisePatternRepository patternRepo,
        StudentProgressService progress,
        ListeningAudioService listeningAudio,
        ILearningGoalContextResolver goalContextResolver,
        ICurriculumRoutingService routing,
        IStudentMasteryEvaluationService mastery,
        IStudentActivityReadinessPoolService readinessPool,
        ILearningPlanService learningPlan,
        IPracticeGymFormIoTemplatePilotSettingsProvider formIoPilotSettings,
        IActivityTemplateInstanceGenerator templateGenerator,
        IActivityNoveltyPolicy noveltyPolicy,
        IActivityContentFingerprintService fingerprintService,
        ILogger<PracticeGymGenerationJob> logger)
    {
        _db = db;
        _aiGenerator = aiGenerator;
        _patternRepo = patternRepo;
        _progress = progress;
        _listeningAudio = listeningAudio;
        _goalContextResolver = goalContextResolver;
        _routing = routing;
        _mastery = mastery;
        _readinessPool = readinessPool;
        _learningPlan = learningPlan;
        _formIoPilotSettings = formIoPilotSettings;
        _templateGenerator = templateGenerator;
        _noveltyPolicy = noveltyPolicy;
        _fingerprintService = fingerprintService;
        _logger = logger;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        var ct = context.CancellationToken;

        var pending = await _db.PracticeActivityCache
            .Where(c => c.Status == PracticeCacheStatus.Pending)
            .OrderBy(c => c.CreatedAt)
            .Take(MaxItemsPerRun)
            .ToListAsync(ct);

        if (pending.Count == 0)
        {
            _logger.LogInformation("PracticeGymGenerationJob: no pending cache rows.");
            return;
        }

        foreach (var cache in pending)
        {
            try
            {
                await MaterializeAsync(cache, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "PracticeGymGenerationJob: failed to generate cache row {CacheId}.",
                    cache.Id);
                cache.MarkExpired();
                await _db.SaveChangesAsync(ct);
            }
        }
    }

    private async Task MaterializeAsync(PracticeActivityCache cache, CancellationToken ct)
    {
        var exerciseType = await _db.ExerciseTypeDefinitions
            .FirstOrDefaultAsync(e => e.Key == cache.PatternKey
                                   && e.IsEnabled
                                   && e.ImplementationStatus == "ready"
                                   && e.SupportsPracticeGym, ct);
        if (exerciseType is null)
        {
            cache.MarkExpired();
            await _db.SaveChangesAsync(ct);
            return;
        }

        var pattern = await _patternRepo.GetByKeyAsync(cache.PatternKey, ct);
        if (pattern is null || !pattern.IsActive)
        {
            cache.MarkExpired();
            await _db.SaveChangesAsync(ct);
            return;
        }

        var profile = await _db.StudentProfiles
            .Include(p => p.CareerProfile)
            .Include(p => p.LanguagePair!).ThenInclude(lp => lp.SourceLanguage)
            .Include(p => p.LanguagePair!).ThenInclude(lp => lp.TargetLanguage)
            .FirstOrDefaultAsync(p => p.Id == cache.StudentProfileId, ct);
        if (profile is null || profile.LifecycleStage == StudentLifecycleStage.Archived)
        {
            cache.MarkExpired();
            await _db.SaveChangesAsync(ct);
            return;
        }

        var focusArea = await _progress.GetCurrentFocusAreaAsync(profile.Id, ct);
        var recentMistakes = StudentProgressService.BuildRecentMistakesSummary(focusArea);
        var pair = profile.LanguagePair;

        var resolvedGoalContext = _goalContextResolver.Resolve(
            profile, new LearningGoalResolutionContext { Source = "PracticeGymGenerationJob" });

        var masteryReport = await _mastery.EvaluateStudentAsync(
            profile.Id, MasteryEvaluationReason.BeforeReplenishment, ct);

        // Phase 12D — consult learning plan for practice gym objective alignment.
        string? plannedObjectiveKey = null;
        try
        {
            var gymObjectives = await _learningPlan.GetPracticeGymObjectivesAsync(profile.Id, maxCount: 1, ct: ct);
            plannedObjectiveKey = gymObjectives.Count > 0 ? gymObjectives[0].ObjectiveKey : null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "PracticeGymGenerationJob: could not read learning plan for {StudentProfileId} — falling back to free routing.", profile.Id);
        }

        var routingRequest = CurriculumRoutingRequestFactory.Build(
            profile, resolvedGoalContext,
            source: "PracticeGymGenerationJob",
            requestedPatternKey: pattern.Key,
            allowReviewOrScaffold: false,
            masteredObjectiveKeys: masteryReport.MasteredObjectiveKeys,
            mode: RoutingMode.NewLearning,
            preferredObjectiveKey: plannedObjectiveKey);
        var routing = await _routing.RecommendAsync(routingRequest, ct);

        // Phase 12E — if routing consumed a planned objective, advance its status.
        if (routing.RoutingReason == RoutingReason.LearningPlan
            && routing.CurriculumObjectiveKey is not null)
        {
            try
            {
                await _learningPlan.MarkObjectiveInProgressAsync(
                    profile.Id, routing.CurriculumObjectiveKey, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "PracticeGymGenerationJob: could not mark objective '{Key}' InProgress for {StudentProfileId} — continuing.",
                    routing.CurriculumObjectiveKey, profile.Id);
            }
        }

        // Record pool item with routing snapshot before generation.
        var poolRequest = ReadinessItemRequestBuilder.FromRoutingRecommendation(
            studentId: profile.Id,
            source: ReadinessPoolSource.PracticeGym,
            recommendation: routing,
            originalCefrLevelSnapshot: profile.CefrLevel,
            difficultyPreference: profile.DifficultyPreference?.ToString(),
            supportLanguageCode: profile.SupportLanguageCode,
            supportLanguageName: profile.SupportLanguageName,
            translationHelpPreference: profile.TranslationHelpPreference?.ToString(),
            patternKey: pattern.Key,
            activityType: pattern.ActivityType.ToString(),
            generatedBy: "PracticeGymGenerationJob");
        var poolItemId = await _readinessPool.CreateQueuedAsync(poolRequest, ct);
        await _readinessPool.MarkGeneratingAsync(poolItemId, ct);

        var generationContext = new ActivityGenerationContext(
            ActivityType: pattern.ActivityType,
            CefrLevel: routing.TargetCefrLevel,
            CareerContext: profile.CareerProfile?.Name ?? profile.CareerContext ?? "General",
            LanguagePairCode: $"{pair?.SourceLanguage?.Code ?? "en"}-{pair?.TargetLanguage?.Code ?? "en"}",
            SourceLanguageName: LanguageSupportResolver.ResolveSourceLanguageName(profile),
            TargetLanguageName: pair?.TargetLanguage?.Name ?? "English",
            TopicHint: cache.SkillFocus ?? "English class practice",
            RecentMistakesSummary: recentMistakes,
            OverridePromptKey: pattern.AiGeneratePromptKey,
            ExercisePatternKey: pattern.Key,
            LearnerPreferenceContext: LearnerPreferenceContextFormatter.Build(
                profile, pair?.TargetLanguage?.Name),
            LearningGoalContext: resolvedGoalContext.ContextSummary,
            RoutingContext: routing.RoutingContextSummary,
            RoutingReason: routing.RoutingReason.ToString().ToLowerInvariant(),
            IsReviewOrScaffold: routing.IsLowerLevelContent);

        // AI Bank-First Teaching Architecture pilot (feature-flagged, inert unless the exercise
        // type is also promoted from "planned" to "ready") — personalize from a published,
        // approved ActivityTemplate instead of free-form AI generation. Falls back to the
        // standard path below on any failure or when no matching template exists.
        if (TemplateMigratedPatternKeys.Contains(pattern.Key)
            && await _formIoPilotSettings.IsEnabledAsync(ct))
        {
            var templateActivity = await TryMaterializeFromTemplateAsync(cache, pattern, routing, poolItemId, ct);
            if (templateActivity is not null)
            {
                _logger.LogInformation(
                    "PracticeGymGenerationJob: cache row {CacheId} ready (Form.io template path) PatternKey={PatternKey} ActivityId={ActivityId}.",
                    cache.Id, pattern.Key, templateActivity.Id);
                return;
            }

            _logger.LogInformation(
                "PracticeGymGenerationJob: no usable template for PatternKey={PatternKey} cache row {CacheId} — falling back to legacy generation.",
                pattern.Key, cache.Id);
        }

        var contentJson = await _aiGenerator.GenerateActivityContentAsync(generationContext, ct);
        var title = ExtractTitle(contentJson) ?? pattern.Name;

        var activity = new LearningActivity(
            pattern.ActivityType,
            ActivitySource.AiGenerated,
            title,
            cache.CefrLevel,
            contentJson,
            learningModuleId: null,
            exercisePatternKey: pattern.Key);

        _db.LearningActivities.Add(activity);
        await _db.SaveChangesAsync(ct);

        if (pattern.RequiresAudio || pattern.ActivityType == ActivityType.ListeningComprehension)
        {
            await _listeningAudio.EnsureAudioAsync(
                activity,
                pair?.TargetLanguage?.Code ?? "en",
                ct);
            await _db.SaveChangesAsync(ct);
        }

        cache.MarkReady(activity.Id);
        await _db.SaveChangesAsync(ct);

        await _readinessPool.MarkReadyAsync(poolItemId, learningActivityId: activity.Id, ct: ct);

        _logger.LogInformation(
            "PracticeGymGenerationJob: cache row {CacheId} ready with ActivityId={ActivityId}.",
            cache.Id,
            activity.Id);
    }

    /// <summary>
    /// Attempts to materialize the activity from a published, approved ActivityTemplate matching
    /// this pattern via the Phase 5 generation pipeline. Returns null (never throws) if no
    /// matching template exists or generation/validation fails — the caller falls back to
    /// standard free-form AI generation in that case.
    /// </summary>
    private async Task<LearningActivity?> TryMaterializeFromTemplateAsync(
        PracticeActivityCache cache,
        ExercisePatternDefinition pattern,
        CurriculumRoutingRecommendation routing,
        Guid poolItemId,
        CancellationToken ct)
    {
        var template = await _db.ActivityTemplates
            .Where(t => t.PatternKey == pattern.Key && t.IsPublished && t.ReviewStatus == AdminReviewStatus.Approved)
            .OrderByDescending(t => t.VersionNumber)
            .FirstOrDefaultAsync(ct);

        if (template is null)
            return null;

        // Intentional review/remediation is allowed to bypass the template cooldown; everything
        // else (Normal/Fallback/LearningPlan) must respect it.
        var isIntentionalReview = routing.RoutingReason is
            RoutingReason.Review or RoutingReason.Scaffold or RoutingReason.Remediation;

        // Template-cooldown pre-check, before spending an AI call. ContentFingerprint is a
        // synthetic per-template marker here (never a real SHA-256 hex fingerprint), so only the
        // SourceTemplateId branch of the novelty policy can ever match it — this call is not
        // asking "has this exact content been seen", only "has this template been seen recently".
        var templatePreCheck = await _noveltyPolicy.CheckAsync(new ActivityNoveltyCheckRequest(
            StudentProfileId: cache.StudentProfileId,
            ContentFingerprint: $"template-precheck:{template.Id}",
            SourceTemplateId: template.Id,
            IsIntentionalReview: isIntentionalReview), ct);

        if (!templatePreCheck.Allowed)
        {
            _logger.LogWarning(
                "PracticeGymGenerationJob: template {TemplateId} blocked by novelty policy ({Reason}) for cache row {CacheId} — falling back to standard generation.",
                template.Id, templatePreCheck.Reason, cache.Id);
            return null;
        }

        for (var attempt = 1; attempt <= MaxTemplateGenerationAttempts; attempt++)
        {
            try
            {
                var genResult = await _templateGenerator.GenerateInstanceAsync(
                    template.Id,
                    new ActivityTemplateInstanceGenerationContext(
                        CefrLevelOverride: routing.TargetCefrLevel,
                        TopicHint: cache.SkillFocus,
                        GenerationSource: "PracticeGymFormIoPilot"),
                    ct);

                var contentFingerprint = _fingerprintService.ComputeFingerprint(new ActivityContentFingerprintRequest(
                    ContentJson: genResult.GeneratedSchemaJson,
                    ContentShape: ActivityContentShape.FormIoSchema,
                    PatternKey: pattern.Key,
                    Skill: pattern.PrimarySkill,
                    CefrLevel: routing.TargetCefrLevel));

                var contentCheck = await _noveltyPolicy.CheckAsync(new ActivityNoveltyCheckRequest(
                    StudentProfileId: cache.StudentProfileId,
                    ContentFingerprint: contentFingerprint,
                    IsIntentionalReview: isIntentionalReview), ct);

                if (!contentCheck.Allowed)
                {
                    _logger.LogWarning(
                        "PracticeGymGenerationJob: generated content for template {TemplateId} duplicated recent content ({Reason}), attempt {Attempt}/{Max} for cache row {CacheId}.",
                        template.Id, contentCheck.Reason, attempt, MaxTemplateGenerationAttempts, cache.Id);
                    if (attempt < MaxTemplateGenerationAttempts)
                        continue;

                    _logger.LogWarning(
                        "PracticeGymGenerationJob: exhausted {Max} generation attempts for template {TemplateId} without novel content — falling back to standard generation for cache row {CacheId}.",
                        MaxTemplateGenerationAttempts, template.Id, cache.Id);
                    return null;
                }

                var activity = new LearningActivity(
                    pattern.ActivityType,
                    ActivitySource.AiGenerated,
                    template.Key,
                    cache.CefrLevel,
                    aiGeneratedContentJson: "{}",
                    learningModuleId: null,
                    exercisePatternKey: pattern.Key);
                activity.SetFormIoContent(genResult.GeneratedSchemaJson, template.ScoringModelJson);

                _db.LearningActivities.Add(activity);
                await _db.SaveChangesAsync(ct);

                cache.MarkReady(activity.Id);
                await _db.SaveChangesAsync(ct);

                await _readinessPool.MarkReadyAsync(poolItemId, learningActivityId: activity.Id, ct: ct);
                await _readinessPool.SetTemplateProvenanceAsync(
                    poolItemId,
                    sourceTemplateId: template.Id,
                    formIoSchemaSnapshotJson: genResult.GeneratedSchemaJson,
                    scoringRulesSnapshotJson: template.ScoringModelJson,
                    personalizationReason: $"Personalized from template '{template.Key}' v{template.VersionNumber} for topic hint '{cache.SkillFocus ?? "none"}' at level {routing.TargetCefrLevel}.",
                    generatedByModel: genResult.ModelName,
                    generatedByProvider: genResult.ProviderName,
                    validationStatus: ActivityValidationStatus.Passed,
                    ct: ct);

                return activity;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "PracticeGymGenerationJob: Form.io template pilot generation failed for cache row {CacheId} TemplateId={TemplateId} — falling back to standard generation.",
                    cache.Id, template.Id);
                return null;
            }
        }

        return null;
    }

    private static string? ExtractTitle(string contentJson)
    {
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(contentJson);
            return doc.RootElement.TryGetProperty("title", out var title)
                && title.ValueKind == System.Text.Json.JsonValueKind.String
                    ? title.GetString()
                    : null;
        }
        catch
        {
            return null;
        }
    }
}

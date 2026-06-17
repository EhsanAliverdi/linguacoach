using LinguaCoach.Application.Activity;
using LinguaCoach.Application.Activity.Evaluators;
using LinguaCoach.Infrastructure.Activity.Evaluators;
using LinguaCoach.Application.Admin;
using LinguaCoach.Application.Assessment;
using LinguaCoach.Application.Ai;
using LinguaCoach.Application.Auth;
using LinguaCoach.Application.Dashboard;
using LinguaCoach.Application.LearningPath;
using LinguaCoach.Application.LearningPlan;
using LinguaCoach.Application.Onboarding;
using LinguaCoach.Application.Reference;
using LinguaCoach.Application.Speaking;
using LinguaCoach.Infrastructure.Activity;
using LinguaCoach.Infrastructure.Admin;
using LinguaCoach.Infrastructure.Assessment;
using LinguaCoach.Infrastructure.Ai;
using LinguaCoach.Infrastructure.Auth;
using LinguaCoach.Infrastructure.Dashboard;
using LinguaCoach.Infrastructure.LearningPath;
using LinguaCoach.Infrastructure.LearningPlan;
using LinguaCoach.Infrastructure.Onboarding;
using LinguaCoach.Infrastructure.Reference;
using LinguaCoach.Application.History;
using LinguaCoach.Application.Memory;
using LinguaCoach.Application.Learning;
using LinguaCoach.Infrastructure.History;
using LinguaCoach.Infrastructure.Learning;
using LinguaCoach.Infrastructure.Memory;
using LinguaCoach.Application.Progress;
using LinguaCoach.Application.Vocabulary;
using LinguaCoach.Infrastructure.Progress;
using LinguaCoach.Infrastructure.Vocabulary;
using LinguaCoach.Application.Placement;
using LinguaCoach.Application.Sessions;
using LinguaCoach.Infrastructure.Placement;
using LinguaCoach.Infrastructure.Sessions;
using LinguaCoach.Infrastructure.Speaking;
using LinguaCoach.Application.Curriculum;
using LinguaCoach.Infrastructure.Curriculum;
using LinguaCoach.Application.PracticeGym;
using LinguaCoach.Application.ReadinessPool;
using LinguaCoach.Infrastructure.ReadinessPool;
using Microsoft.Extensions.Options;
using LinguaCoach.Application.Profile;
using LinguaCoach.Application.Storage;
using LinguaCoach.Infrastructure.Profile;
using LinguaCoach.Infrastructure.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LinguaCoach.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration? configuration = null)
    {
        // File storage (IFileStorageService) — provider chosen at startup from config.
        // Registered as a singleton; holds no per-request state.
        services.AddSingleton<IFileStorageService>(sp =>
        {
            var config = sp.GetRequiredService<IConfiguration>();
            var provider = config["FileStorage:Provider"]
                ?? Environment.GetEnvironmentVariable("FILE_STORAGE_PROVIDER")
                ?? "Local";
            return provider.Equals("Minio", StringComparison.OrdinalIgnoreCase)
                ? ActivatorUtilities.CreateInstance<MinioFileStorageService>(sp)
                : ActivatorUtilities.CreateInstance<LocalFileStorageService>(sp);
        });
        // Always registered so the test WebApplicationFactory can swap it in.
        services.AddSingleton<FakeFileStorageService>();

        // Background generation jobs (Quartz registers these by type; also scoped for DI).
        services.AddScoped<Jobs.LessonBufferRefillJob>();
        services.AddScoped<Jobs.LessonBatchGenerationJob>();
        services.AddScoped<Jobs.ActivityMaterializationJob>();
        services.AddScoped<Jobs.TtsAudioGenerationJob>();
        services.AddScoped<Jobs.AudioCleanupJob>();
        services.AddScoped<Jobs.PracticeGymBufferRefillJob>();
        services.AddScoped<Jobs.PracticeGymGenerationJob>();

        // Auth
        services.AddScoped<ITokenService, JwtTokenService>();
        services.AddScoped<ILoginHandler, LoginHandler>();
        services.AddScoped<IChangePasswordHandler, ChangePasswordHandler>();

        // Admin
        services.AddScoped<ICreateStudentHandler, CreateStudentHandler>();
        services.AddScoped<IAdminStudentQuery, AdminHandler>();
        services.AddScoped<IAdminPromptHandler, AdminHandler>();
        services.AddScoped<IAdminCurriculumHandler, AdminHandler>();
        services.AddScoped<IAdminAiConfigHandler, AdminHandler>();
        services.AddScoped<IExerciseTypeCatalogService, ExerciseTypeCatalogService>();
        services.AddScoped<IExerciseTypeRegistry, ExerciseTypeRegistry>();
        services.AddScoped<IPracticeGymPoolService, PracticeGymPoolService>();

        // Onboarding
        services.AddScoped<IOnboardingHandler, OnboardingHandler>();
        services.AddScoped<IOnboardingStatusQuery, OnboardingHandler>();
        services.AddScoped<IOnboardingExperienceHandler, OnboardingHandler>();

        // Onboarding v2
        services.AddScoped<IOnboardingV2Query, OnboardingV2QueryHandler>();
        services.AddScoped<IOnboardingV2StepHandler, OnboardingV2StepHandler>();
        services.AddScoped<IOnboardingV2CompleteHandler, OnboardingV2CompleteHandler>();
        services.AddScoped<IAdminOnboardingFlowQuery, AdminOnboardingFlowQueryHandler>();

        // Dashboard
        services.AddScoped<StudentProgressService>();
        services.AddScoped<IDashboardQueryHandler, DashboardQueryHandler>();

        // Student Profile (Phase 10G)
        services.AddScoped<IGetStudentProfileQueryHandler, ProfileQueryHandler>();
        services.AddScoped<IUpdateLearningPreferencesCommandHandler, ProfileCommandHandler>();

        // Progress
        services.AddScoped<IGetProgressHandler, GetProgressHandler>();

        // Vocabulary
        services.AddScoped<IVocabularyExtractionService, VocabularyExtractionService>();
        services.AddScoped<IGetVocabularyHandler, GetVocabularyHandler>();
        services.AddScoped<IUpdateVocabularyStatusHandler, UpdateVocabularyStatusHandler>();

        // Learning history
        services.AddScoped<IGetModuleActivitiesHandler, ModuleActivitiesHandler>();
        services.AddScoped<IGetActivityAttemptsHandler, ActivityAttemptsHandler>();

        // Reference data
        services.AddScoped<IReferenceQueryService, ReferenceQueryService>();

        // AI — context builder and provider selection
        services.AddScoped<IAiContextBuilder, DbPromptAiContextBuilder>();
        services.AddHttpClient<GeminiProvider>();
        services.AddScoped<OpenAiProvider>();
        services.AddScoped<GeminiProvider>();
        services.AddScoped<AnthropicProvider>();
        services.AddScoped<QwenProvider>();
        services.AddScoped<IAiProviderResolver, AiProviderResolver>();
        services.AddScoped<IAiProvider, OpenAiProvider>();
        services.AddScoped<IAiProviderTester, AiProviderTester>();
        services.AddScoped<AiExecutionService>();

        // CEFR assessment
        services.AddScoped<ICefrAssessmentHandler, CefrAssessmentHandler>();

        // Learning planner (SM-2 spaced repetition + vocabulary selection)
        services.AddScoped<ILearningPlanner, LearningPlannerService>();

        // Learning path generation
        services.AddScoped<ILearningPathGenerator, AiLearningPathGeneratorHandler>();
        services.AddScoped<IGetLearningPathHandler, LearningPathQueryHandler>();
        services.AddScoped<ICompleteModuleHandler, CompleteModuleHandler>();
        services.AddScoped<LearningPathDtoBuilder>();
        services.AddScoped<IAdaptivePathGenerator, AdaptivePathGeneratorHandler>();
        services.AddScoped<IStudentMemoryService, StudentMemoryService>();
        services.AddScoped<IStudentMemoryQuery, StudentMemoryService>();
        services.AddScoped<IStudentLearningLedger, StudentLearningLedgerService>();
        services.AddSingleton<ILearningGoalContextResolver, LearningGoalContextResolver>();
        services.AddScoped<IAdminAiUsageHandler, AiUsageHandler>();

        // Activity (AI-first learning flow)
        services.AddScoped<IAiActivityGenerator, AiActivityGeneratorHandler>();
        services.AddScoped<VocabularyPracticeGenerator>();
        services.AddScoped<VocabularyPracticeEvaluator>();
        services.AddScoped<ListeningComprehensionEvaluator>();
        services.AddScoped<ListeningAudioService>();
        services.AddScoped<SpeakingAudioService>();
        services.AddScoped<SpeakingRolePlayEvaluator>();
        services.AddScoped<ActivityGetHandler>();
        services.AddScoped<IGetNextActivityHandler>(sp => sp.GetRequiredService<ActivityGetHandler>());
        services.AddScoped<IGetActivityByIdHandler>(sp => sp.GetRequiredService<ActivityGetHandler>());
        services.AddScoped<ISubmitActivityAttemptHandler, ActivitySubmitHandler>();

        // Pattern Evaluation Engine — skill update (Phase 5), evaluators (Phases 2 & 4) + router (Phase 3)
        services.AddScoped<PatternSkillUpdateService>();
        services.AddScoped<IMultiSkillProgressService, MultiSkillProgressService>();
        services.AddScoped<IPatternEvaluator, ExactMatchEvaluator>();
        services.AddScoped<IPatternEvaluator, KeyedSelectionEvaluator>();
        services.AddScoped<IPatternEvaluator, NoMarkingEvaluator>();
        services.AddScoped<IPatternEvaluator, AiStructuredEvaluator>();
        services.AddScoped<IPatternEvaluator, AiOpenEndedEvaluator>();
        services.AddScoped<IPatternEvaluationRouter, PatternEvaluationRouter>();

        // STT: use FakeSpeechToTextService for MVP; swap in a real provider later
        services.AddScoped<ISpeechToTextService, FakeSpeechToTextService>();
        // TTS: FakeTextToSpeechService is the default (no API key needed).
        // OpenAiTextToSpeechService activates when AiProviderConfig selects "openai" for tts.* feature keys.
        // TtsProviderResolver reads the DB config and returns the correct implementation.
        services.AddScoped<ITextToSpeechService, FakeTextToSpeechService>();
        services.AddScoped<FakeTextToSpeechService>();
        services.AddScoped<OpenAiTextToSpeechService>();
        services.AddScoped<GeminiTextToSpeechService>();
        services.AddScoped<QwenTextToSpeechService>();
        services.AddScoped<TtsProviderResolver>();

        // Speaking sessions
        services.AddScoped<ICreateSpeakingSessionHandler, SpeakingSessionHandler>();
        services.AddScoped<ISubmitSpeakingTurnHandler, SpeakingSessionHandler>();

        // Session generator + session handlers
        services.AddScoped<ISessionGeneratorService, SessionGeneratorService>();
        services.AddScoped<SessionQueryHandler>();
        services.AddScoped<IGetTodaysSessionHandler>(sp => sp.GetRequiredService<SessionQueryHandler>());
        services.AddScoped<IGetSessionHandler>(sp => sp.GetRequiredService<SessionQueryHandler>());
        services.AddScoped<IGetSessionHistoryHandler>(sp => sp.GetRequiredService<SessionQueryHandler>());
        services.AddScoped<SessionLifecycleHandler>();
        services.AddScoped<IStartSessionHandler>(sp => sp.GetRequiredService<SessionLifecycleHandler>());
        services.AddScoped<ICompleteSessionHandler>(sp => sp.GetRequiredService<SessionLifecycleHandler>());
        services.AddScoped<ICompleteExerciseHandler>(sp => sp.GetRequiredService<SessionLifecycleHandler>());
        services.AddScoped<ExercisePrepareHandler>();
        services.AddScoped<IPrepareExerciseHandler>(sp => sp.GetRequiredService<ExercisePrepareHandler>());
        services.AddScoped<IExercisePatternRepository, ExercisePatternRepository>();

        // Curriculum syllabus (Phase 10K)
        services.AddScoped<CurriculumSyllabusQueryService>();
        services.AddScoped<ICurriculumSyllabusQuery>(sp => sp.GetRequiredService<CurriculumSyllabusQueryService>());
        services.AddScoped<IAdminCurriculumSyllabusQuery>(sp => sp.GetRequiredService<CurriculumSyllabusQueryService>());

        // Curriculum write service (Phase 10Q)
        services.AddScoped<ICurriculumObjectiveWriteService, CurriculumObjectiveWriteService>();

        // Curriculum routing (Phase 10L)
        services.AddScoped<ICurriculumRoutingService, CurriculumRoutingService>();

        // Student activity readiness pool (Phase 10M)
        services.AddScoped<IStudentActivityReadinessPoolService, StudentActivityReadinessPoolService>();

        // Readiness pool replenishment (Phase 10N)
        if (configuration is not null)
            services.Configure<ReadinessPoolReplenishmentOptions>(
                configuration.GetSection(ReadinessPoolReplenishmentOptions.SectionName));
        else
            services.Configure<ReadinessPoolReplenishmentOptions>(_ => { });
        services.AddScoped<IReadinessPoolReplenishmentService, ReadinessPoolReplenishmentService>();
        services.AddScoped<Jobs.ReadinessPoolReplenishmentJob>();

        // Practice Gym suggestion service (Phase 10O)
        services.AddScoped<IPracticeGymSuggestionService, LinguaCoach.Infrastructure.PracticeGym.PracticeGymSuggestionService>();

        // Placement assessment
        services.AddScoped<PlacementAudioService>();
        services.AddScoped<FakePlacementEvaluator>();
        services.AddScoped<IPlacementEvaluator, AiPlacementEvaluator>();
        services.AddScoped<PlacementService>();
        services.AddScoped<IStartPlacementHandler>(sp => sp.GetRequiredService<PlacementService>());
        services.AddScoped<ISavePlacementAnswersHandler>(sp => sp.GetRequiredService<PlacementService>());
        services.AddScoped<ICompletePlacementHandler>(sp => sp.GetRequiredService<PlacementService>());
        services.AddScoped<IGetPlacementStatusHandler>(sp => sp.GetRequiredService<PlacementService>());
        services.AddScoped<IGetPlacementCurrentSectionHandler>(sp => sp.GetRequiredService<PlacementService>());
        services.AddScoped<IGetPlacementResultHandler>(sp => sp.GetRequiredService<PlacementService>());

        // Usage governance, token tracking & quota enforcement (Phase 10R)
        services.AddScoped<LinguaCoach.Application.UsageGovernance.IUsageQuotaService,
            LinguaCoach.Infrastructure.UsageGovernance.UsageQuotaService>();
        services.AddScoped<LinguaCoach.Application.UsageGovernance.IUsageGovernanceAdminService,
            LinguaCoach.Infrastructure.UsageGovernance.UsageGovernanceAdminService>();

        return services;
    }
}

using LinguaCoach.Application.Activity;
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
using LinguaCoach.Infrastructure.History;
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
using Microsoft.Extensions.DependencyInjection;

namespace LinguaCoach.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
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

        // Onboarding
        services.AddScoped<IOnboardingHandler, OnboardingHandler>();
        services.AddScoped<IOnboardingStatusQuery, OnboardingHandler>();

        // Dashboard
        services.AddScoped<StudentProgressService>();
        services.AddScoped<IDashboardQueryHandler, DashboardQueryHandler>();

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

        // STT: use FakeSpeechToTextService for MVP; swap in a real provider later
        services.AddScoped<ISpeechToTextService, FakeSpeechToTextService>();
        services.AddScoped<ITextToSpeechService, FakeTextToSpeechService>();

        // Speaking sessions
        services.AddScoped<ICreateSpeakingSessionHandler, SpeakingSessionHandler>();
        services.AddScoped<ISubmitSpeakingTurnHandler, SpeakingSessionHandler>();

        // Session generator + session handlers
        services.AddScoped<ISessionGeneratorService, SessionGeneratorService>();
        services.AddScoped<SessionQueryHandler>();
        services.AddScoped<IGetTodaysSessionHandler>(sp => sp.GetRequiredService<SessionQueryHandler>());
        services.AddScoped<IGetSessionHandler>(sp => sp.GetRequiredService<SessionQueryHandler>());
        services.AddScoped<SessionLifecycleHandler>();
        services.AddScoped<IStartSessionHandler>(sp => sp.GetRequiredService<SessionLifecycleHandler>());
        services.AddScoped<ICompleteSessionHandler>(sp => sp.GetRequiredService<SessionLifecycleHandler>());
        services.AddScoped<ICompleteExerciseHandler>(sp => sp.GetRequiredService<SessionLifecycleHandler>());
        services.AddScoped<ExercisePrepareHandler>();
        services.AddScoped<IPrepareExerciseHandler>(sp => sp.GetRequiredService<ExercisePrepareHandler>());
        services.AddScoped<IExercisePatternRepository, ExercisePatternRepository>();

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

        return services;
    }
}

using LinguaCoach.Application.Activity;
using LinguaCoach.Application.Activity.Evaluators;
using LinguaCoach.Application.Notifications;
using LinguaCoach.Infrastructure.Notifications;
using LinguaCoach.Infrastructure.Activity.Evaluators;
using LinguaCoach.Application.Admin;
using LinguaCoach.Application.Admin.RuntimeSettings;
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
using Microsoft.AspNetCore.DataProtection;
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

        // Secret protection (ASP.NET Core Data Protection)
        // Keys are persisted to a configurable directory (DataProtection:KeysPath).
        // Key-at-rest encryption is optional (DataProtection:KeyProtectionMode).
        // Production: mount the keys directory as a persistent volume (see docker-compose.yml).
        // See: docs/reviews/2026-06-23-phase-10w-5c-5-data-protection-key-encryption-hardening-review.md
        if (configuration is not null)
            services.Configure<NotificationKeyProtectionOptions>(configuration.GetSection(NotificationKeyProtectionOptions.SectionName));
        else
            services.Configure<NotificationKeyProtectionOptions>(_ => { });

        // External login config — safe defaults: all providers disabled
        if (configuration is not null)
            services.Configure<GoogleExternalLoginOptions>(configuration.GetSection(GoogleExternalLoginOptions.SectionName));
        else
            services.Configure<GoogleExternalLoginOptions>(_ => { });

        // Read all DP config at registration time — PersistKeysToFileSystem/ProtectKeysWith* must be
        // called on the IDataProtectionBuilder before the container is built.
        var dpKeysPath = configuration?["DataProtection:KeysPath"] ?? "./app-data/data-protection-keys";
        var dpAppName = configuration?["DataProtection:ApplicationName"];
        if (string.IsNullOrWhiteSpace(dpAppName)) dpAppName = "SpeakPath";

        var dpMode = Enum.TryParse<DataProtectionKeyMode>(
            configuration?["DataProtection:KeyProtectionMode"], ignoreCase: true, out var parsedMode)
            ? parsedMode
            : DataProtectionKeyMode.None;

        var dpDir = Path.IsPathRooted(dpKeysPath)
            ? new DirectoryInfo(dpKeysPath)
            : new DirectoryInfo(Path.Combine(AppContext.BaseDirectory, dpKeysPath));

        var dpRegistration = services.AddDataProtection().SetApplicationName(dpAppName);
        try
        {
            if (!dpDir.Exists) dpDir.Create();
            dpRegistration.PersistKeysToFileSystem(dpDir);
        }
        catch
        {
            // Directory creation failed — keys remain in-memory (ephemeral).
            // App continues rather than failing hard; issue is visible via startup logs.
        }

        if (dpMode == DataProtectionKeyMode.Certificate)
        {
            var certPath = configuration?["DataProtection:CertificatePath"];
            var certPassword = configuration?["DataProtection:CertificatePassword"];
            var certThumbprint = configuration?["DataProtection:CertificateThumbprint"];

            System.Security.Cryptography.X509Certificates.X509Certificate2? cert = null;

            if (!string.IsNullOrWhiteSpace(certPath))
            {
                if (!File.Exists(certPath))
                    throw new InvalidOperationException(
                        $"DataProtection:KeyProtectionMode is Certificate but the certificate file was not found: {certPath}");
                cert = System.Security.Cryptography.X509Certificates.X509CertificateLoader
                    .LoadPkcs12FromFile(certPath, certPassword,
                        System.Security.Cryptography.X509Certificates.X509KeyStorageFlags.MachineKeySet);
            }
            else if (!string.IsNullOrWhiteSpace(certThumbprint) && OperatingSystem.IsWindows())
            {
                using var store = new System.Security.Cryptography.X509Certificates.X509Store(
                    System.Security.Cryptography.X509Certificates.StoreLocation.LocalMachine);
                store.Open(System.Security.Cryptography.X509Certificates.OpenFlags.ReadOnly);
                var found = store.Certificates.Find(
                    System.Security.Cryptography.X509Certificates.X509FindType.FindByThumbprint,
                    certThumbprint, validOnly: false);
                if (found.Count == 0)
                    throw new InvalidOperationException(
                        $"DataProtection:KeyProtectionMode is Certificate but no certificate with thumbprint '{certThumbprint}' was found in LocalMachine store.");
                cert = found[0];
            }
            else
            {
                throw new InvalidOperationException(
                    "DataProtection:KeyProtectionMode is Certificate but neither CertificatePath nor CertificateThumbprint (Windows only) is configured.");
            }

            dpRegistration.ProtectKeysWithCertificate(cert);
        }

        services.AddSingleton<ISecretProtector, DataProtectionSecretProtector>();

        // Runtime notification channel config resolver (DB override → appsettings fallback)
        services.AddScoped<INotificationChannelConfigResolver, NotificationChannelConfigResolver>();

        // Notifications
        services.AddScoped<INotificationPreferenceService, NotificationPreferenceService>();
        services.AddScoped<INotificationService, NotificationService>();
        services.AddScoped<INotificationQueryService, NotificationQueryService>();
        services.AddScoped<INotificationDispatchService, NotificationDispatchService>();

        // Email sender — DisabledEmailSender when Email:Enabled is false/missing; SmtpEmailSender otherwise.
        // App never crashes at startup due to missing email config.
        if (configuration is not null)
            services.Configure<EmailOptions>(configuration.GetSection(EmailOptions.SectionName));
        else
            services.Configure<EmailOptions>(_ => { });

        services.AddScoped<SmtpEmailSender>();
        services.AddScoped<ResendEmailSender>();
        services.AddScoped<SendGridEmailSender>();
        services.AddScoped<DisabledEmailSender>();
        // RoutingEmailSender reads provider from resolved config at send time and delegates to the
        // correct concrete sender (Smtp / Resend / SendGrid). All three are registered above.
        services.AddScoped<IEmailSender, RoutingEmailSender>();
        // Named HttpClient used by ResendEmailSender — base address not set; full URL used per call.
        services.AddHttpClient("Resend");

        // SMS sender — DisabledSmsSender when Sms:Enabled is false/missing.
        // App never crashes at startup due to missing SMS config.
        if (configuration is not null)
            services.Configure<SmsOptions>(configuration.GetSection(SmsOptions.SectionName));
        else
            services.Configure<SmsOptions>(_ => { });

        services.AddScoped<DisabledSmsSender>();
        services.AddScoped<ISmsSender>(sp => sp.GetRequiredService<DisabledSmsSender>());

        // Auth
        services.AddScoped<ITokenService, JwtTokenService>();
        services.AddScoped<IRefreshTokenService, RefreshTokenService>();
        services.AddScoped<IGoogleTokenValidator, GoogleTokenValidator>();
        services.AddScoped<IExternalLoginService, ExternalLoginService>();
        services.AddScoped<ILoginHandler, LoginHandler>();
        services.AddScoped<IChangePasswordHandler, ChangePasswordHandler>();
        services.AddScoped<IPasswordResetService, PasswordResetHandler>();
        services.AddScoped<IAuthSecurityAuditService, AuthSecurityAuditService>();

        // Admin
        services.AddScoped<ICreateStudentHandler, CreateStudentHandler>();
        services.AddScoped<IAdminStudentQuery, AdminHandler>();
        services.AddScoped<IAdminPromptHandler, AdminHandler>();
        services.AddScoped<IAdminCurriculumHandler, AdminHandler>();
        services.AddScoped<IAdminAiConfigHandler, AdminHandler>();
        services.AddScoped<IAdminGenerationQualityHandler, AdminGenerationQualityHandler>();
        services.AddScoped<IAdminNotificationHandler, AdminNotificationHandler>();
        services.AddScoped<IAdminAuthEventHandler, AdminAuthEventHandler>();
        services.AddScoped<IAdminSecurityHandler, AdminSecurityHandler>();
        services.AddScoped<IAdminTemplateHandler, AdminTemplateHandler>();
        services.AddScoped<IAdminDashboardAggregateHandler, AdminDashboardAggregateHandler>();
        services.AddSingleton<INotificationTemplateRenderer, SimpleNotificationTemplateRenderer>();
        services.AddScoped<IAiPricingResolver, AiPricingResolver>();
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
        services.AddScoped<IAdminOnboardingFlowListQuery, AdminOnboardingFlowListQueryHandler>();
        services.AddScoped<IAdminCreateOnboardingFlowHandler, AdminCreateOnboardingFlowHandler>();
        services.AddScoped<IAdminActivateOnboardingFlowHandler, AdminActivateOnboardingFlowHandler>();
        services.AddScoped<IAdminAddOnboardingStepHandler, AdminAddOnboardingStepHandler>();
        services.AddScoped<IAdminUpdateOnboardingStepHandler, AdminUpdateOnboardingStepHandler>();
        services.AddScoped<IAdminRemoveOnboardingStepHandler, AdminRemoveOnboardingStepHandler>();
        services.AddScoped<IAdminReorderOnboardingStepsHandler, AdminReorderOnboardingStepsHandler>();

        services.AddScoped<IAdminStudentPracticeQuery, AdminStudentPracticeQueryHandler>();
        services.AddScoped<IAdminStudentSpeakingAttemptsQuery, AdminStudentSpeakingAttemptsHandler>();

        // Dashboard
        services.AddScoped<StudentProgressService>();
        services.AddScoped<IDashboardQueryHandler, DashboardQueryHandler>();
        services.AddScoped<IStudentDashboardSummaryHandler, StudentDashboardSummaryHandler>();

        // Student Profile (Phase 10G)
        services.AddScoped<IGetStudentProfileQueryHandler, ProfileQueryHandler>();
        services.AddScoped<IUpdateLearningPreferencesCommandHandler, ProfileCommandHandler>();

        // Progress
        services.AddScoped<IGetProgressHandler, GetProgressHandler>();
        services.AddScoped<IStudentProgressSummaryHandler, StudentProgressSummaryHandler>();

        // Admin — student progress summary
        services.AddScoped<IAdminStudentProgressQuery, AdminStudentProgressHandler>();

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

        // Curriculum validation (Phase 11B)
        services.AddScoped<ICurriculumValidationService, CurriculumValidationService>();

        // Student activity readiness pool (Phase 10M)
        services.AddScoped<IStudentActivityReadinessPoolService, StudentActivityReadinessPoolService>();

        // Readiness pool replenishment (Phase 10N)
        if (configuration is not null)
            services.Configure<ReadinessPoolReplenishmentOptions>(
                configuration.GetSection(ReadinessPoolReplenishmentOptions.SectionName));
        else
            services.Configure<ReadinessPoolReplenishmentOptions>(_ => { });
        services.AddScoped<LinguaCoach.Application.ReadinessPool.IEffectiveReadinessPoolSettingsProvider,
            LinguaCoach.Infrastructure.ReadinessPool.EffectiveReadinessPoolSettingsProvider>();
        services.AddScoped<IReadinessPoolReplenishmentService, ReadinessPoolReplenishmentService>();
        services.AddScoped<Jobs.ReadinessPoolReplenishmentJob>();
        services.AddScoped<Jobs.NotificationDispatchJob>();

        // Practice Gym suggestion service (Phase 10O)
        services.AddScoped<IPracticeGymSuggestionService, LinguaCoach.Infrastructure.PracticeGym.PracticeGymSuggestionService>();

        // Admin runtime settings / feature gate registry (Phase 20B)
        services.AddSingleton<IFeatureGateRegistry, LinguaCoach.Infrastructure.Admin.FeatureGateRegistryService>();
        services.AddScoped<IRuntimeSettingsService, LinguaCoach.Infrastructure.Admin.RuntimeSettingsService>();

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

        // Mastery re-evaluation engine (Phase 10Z)
        if (configuration is not null)
            services.Configure<LinguaCoach.Application.Mastery.MasteryOptions>(
                configuration.GetSection(LinguaCoach.Application.Mastery.MasteryOptions.SectionName));
        else
            services.Configure<LinguaCoach.Application.Mastery.MasteryOptions>(_ => { });
        services.AddScoped<LinguaCoach.Application.Mastery.IStudentMasteryEvaluationService,
            LinguaCoach.Infrastructure.Mastery.StudentMasteryEvaluationService>();
        services.AddScoped<Jobs.StudentMasteryEvaluationJob>();

        // Phase 12D — Learning Plan Orchestrator
        if (configuration is not null)
            services.Configure<LinguaCoach.Application.LearningPlan.LearningPlanOptions>(
                configuration.GetSection(LinguaCoach.Application.LearningPlan.LearningPlanOptions.SectionName));
        else
            services.Configure<LinguaCoach.Application.LearningPlan.LearningPlanOptions>(_ => { });
        services.AddScoped<LinguaCoach.Application.LearningPlan.ILearningPlanService,
            LinguaCoach.Infrastructure.LearningPlan.LearningPlanService>();

        // Phase 13A+13B — Adaptive Placement Engine
        if (configuration is not null)
            services.Configure<LinguaCoach.Application.Placement.PlacementAssessmentOptions>(
                configuration.GetSection(LinguaCoach.Application.Placement.PlacementAssessmentOptions.Section));
        else
            services.Configure<LinguaCoach.Application.Placement.PlacementAssessmentOptions>(_ => { });
        services.AddScoped<LinguaCoach.Application.Placement.IPlacementScoringService,
            LinguaCoach.Infrastructure.Placement.PlacementScoringService>();
        services.AddScoped<LinguaCoach.Application.Placement.IPlacementAssessmentService,
            LinguaCoach.Infrastructure.Placement.PlacementAssessmentService>();

        // Phase 16F/16G — Speaking Evaluation Foundation + Provider-Backed Evaluation
        if (configuration is not null)
            services.Configure<LinguaCoach.Application.Speaking.SpeakingEvaluationOptions>(
                configuration.GetSection(LinguaCoach.Application.Speaking.SpeakingEvaluationOptions.SectionName));
        else
            services.Configure<LinguaCoach.Application.Speaking.SpeakingEvaluationOptions>(_ => { });

        // Register both concrete providers; ISpeakingEvaluationProvider resolved by config at runtime.
        services.AddScoped<LinguaCoach.Infrastructure.Speaking.NoOpSpeakingEvaluationProvider>();
        services.AddScoped<LinguaCoach.Infrastructure.Speaking.OpenAiSpeakingEvaluationProvider>();
        services.AddScoped<LinguaCoach.Application.Speaking.ISpeakingEvaluationProvider>(sp =>
        {
            var opts = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<
                LinguaCoach.Application.Speaking.SpeakingEvaluationOptions>>().Value;
            return opts.Provider.Equals("OpenAI", StringComparison.OrdinalIgnoreCase)
                ? (LinguaCoach.Application.Speaking.ISpeakingEvaluationProvider)
                  sp.GetRequiredService<LinguaCoach.Infrastructure.Speaking.OpenAiSpeakingEvaluationProvider>()
                : sp.GetRequiredService<LinguaCoach.Infrastructure.Speaking.NoOpSpeakingEvaluationProvider>();
        });

        services.AddScoped<LinguaCoach.Application.Speaking.ISpeakingEvaluationService,
            LinguaCoach.Infrastructure.Speaking.SpeakingEvaluationService>();
        services.AddScoped<LinguaCoach.Application.Speaking.ISpeakingEvaluationQualityQuery,
            LinguaCoach.Infrastructure.Speaking.SpeakingEvaluationQualityHandler>();
        services.AddScoped<LinguaCoach.Application.Speaking.ISpeakingEvaluationSignalApplicationService,
            LinguaCoach.Infrastructure.Speaking.SpeakingEvaluationSignalApplicationService>();
        services.AddScoped<Jobs.SpeakingEvaluationJob>();
        services.AddScoped<Jobs.SpeakingEvaluationSignalApplicationJob>();

        // Phase 17A — Writing Evaluation Foundation
        if (configuration is not null)
            services.Configure<LinguaCoach.Application.Writing.WritingEvaluationOptions>(
                configuration.GetSection(LinguaCoach.Application.Writing.WritingEvaluationOptions.SectionName));
        else
            services.Configure<LinguaCoach.Application.Writing.WritingEvaluationOptions>(_ => { });

        services.AddScoped<LinguaCoach.Infrastructure.Writing.NoOpWritingEvaluationProvider>();
        services.AddScoped<LinguaCoach.Infrastructure.Writing.OpenAiWritingEvaluationProvider>();
        services.AddScoped<LinguaCoach.Application.Writing.IWritingEvaluationProvider>(sp =>
        {
            var opts = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<
                LinguaCoach.Application.Writing.WritingEvaluationOptions>>().Value;
            return opts.Provider.Equals("OpenAI", StringComparison.OrdinalIgnoreCase)
                ? (LinguaCoach.Application.Writing.IWritingEvaluationProvider)
                  sp.GetRequiredService<LinguaCoach.Infrastructure.Writing.OpenAiWritingEvaluationProvider>()
                : sp.GetRequiredService<LinguaCoach.Infrastructure.Writing.NoOpWritingEvaluationProvider>();
        });

        services.AddScoped<LinguaCoach.Application.Writing.IWritingEvaluationService,
            LinguaCoach.Infrastructure.Writing.WritingEvaluationService>();
        services.AddScoped<LinguaCoach.Application.Writing.IAdminWritingEvaluationQuery,
            LinguaCoach.Infrastructure.Writing.AdminWritingEvaluationHandler>();
        services.AddScoped<LinguaCoach.Application.Writing.IWritingEvaluationSignalApplicationService,
            LinguaCoach.Infrastructure.Writing.WritingEvaluationSignalApplicationService>();
        services.AddScoped<Jobs.WritingEvaluationJob>();
        services.AddScoped<Jobs.WritingEvaluationSignalApplicationJob>();

        return services;
    }
}

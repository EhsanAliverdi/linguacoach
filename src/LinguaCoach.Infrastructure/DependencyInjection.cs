using System.Linq;
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
        // Phase I2B — LessonBufferRefillJob/LessonBatchGenerationJob/ActivityMaterializationJob/
        // TtsAudioGenerationJob deleted: Today is module-only now, no legacy generation pipeline.
        services.AddScoped<Jobs.AudioCleanupJob>();

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

        // Onboarding
        services.AddScoped<IOnboardingHandler, OnboardingHandler>();
        services.AddScoped<IOnboardingStatusQuery, OnboardingHandler>();
        services.AddScoped<IOnboardingExperienceHandler, OnboardingHandler>();

        // Onboarding — Form.io template model (replaces old OnboardingV2/AdminOnboardingFlow*)
        services.AddScoped<IFormIoSchemaValidationService, FormIoSchemaValidationService>();
        services.AddScoped<LinguaCoach.Application.FormIo.IFormIoQuizSchemaSplitter, LinguaCoach.Infrastructure.FormIo.FormIoQuizSchemaSplitter>();
        services.AddScoped<AdminOnboardingTemplateService>();
        services.AddScoped<IAdminListOnboardingTemplatesQuery>(sp => sp.GetRequiredService<AdminOnboardingTemplateService>());
        services.AddScoped<IAdminGetOnboardingTemplateQuery>(sp => sp.GetRequiredService<AdminOnboardingTemplateService>());
        services.AddScoped<IAdminGetActiveOnboardingTemplateQuery>(sp => sp.GetRequiredService<AdminOnboardingTemplateService>());
        services.AddScoped<IAdminCreateOnboardingTemplateHandler>(sp => sp.GetRequiredService<AdminOnboardingTemplateService>());
        services.AddScoped<IAdminSaveOnboardingTemplateDraftHandler>(sp => sp.GetRequiredService<AdminOnboardingTemplateService>());
        services.AddScoped<IAdminPublishOnboardingTemplateHandler>(sp => sp.GetRequiredService<AdminOnboardingTemplateService>());
        services.AddScoped<IAdminArchiveOnboardingTemplateHandler>(sp => sp.GetRequiredService<AdminOnboardingTemplateService>());

        services.AddScoped<StudentOnboardingFlowService>();
        services.AddScoped<IStudentOnboardingActiveQuery>(sp => sp.GetRequiredService<StudentOnboardingFlowService>());
        services.AddScoped<IStudentOnboardingSaveDraftHandler>(sp => sp.GetRequiredService<StudentOnboardingFlowService>());
        services.AddScoped<IStudentOnboardingSubmitHandler>(sp => sp.GetRequiredService<StudentOnboardingFlowService>());

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

        // Phase B — Repetition/novelty foundation
        if (configuration is not null)
            services.Configure<NoveltyPolicySettings>(configuration.GetSection("Novelty"));
        else
            services.Configure<NoveltyPolicySettings>(_ => { });
        services.AddSingleton<IActivityContentFingerprintService, ActivityContentFingerprintService>();
        services.AddScoped<IActivityNoveltyPolicy, ActivityNoveltyPolicy>();

        // Phase D1 — bank-first Today slice (vocabulary/reading patterns only; see docs/architecture)
        services.AddScoped<ITodayBankResourceSelector, TodayBankResourceSelector>();
        services.AddScoped<VocabularyPracticeGenerator>();
        services.AddScoped<VocabularyPracticeEvaluator>();
        services.AddScoped<ListeningComprehensionEvaluator>();
        services.AddScoped<ListeningAudioService>();
        services.AddScoped<SpeakingAudioService>();
        services.AddScoped<SpeakingRolePlayEvaluator>();
        services.AddScoped<ActivityGetHandler>();
        services.AddScoped<IGetActivityByIdHandler>(sp => sp.GetRequiredService<ActivityGetHandler>());
        services.AddScoped<ISubmitActivityAttemptHandler, ActivitySubmitHandler>();

        // Phase B2 — Activity feedback / repeat policy / calibration signals
        services.AddScoped<ISubmitActivityFeedbackHandler, ActivityFeedbackHandler>();
        services.AddScoped<IActivityFeedbackPolicyProvider, ActivityFeedbackPolicyProvider>();

        // Pattern Evaluation Engine — skill update (Phase 5), evaluators (Phases 2 & 4) + router (Phase 3)
        services.AddScoped<PatternSkillUpdateService>();
        services.AddScoped<IMultiSkillProgressService, MultiSkillProgressService>();
        services.AddScoped<IPatternEvaluator, ExactMatchEvaluator>();
        services.AddScoped<IPatternEvaluator, KeyedSelectionEvaluator>();
        services.AddScoped<IPatternEvaluator, NoMarkingEvaluator>();
        services.AddScoped<IPatternEvaluator, AiStructuredEvaluator>();
        services.AddScoped<IPatternEvaluator, AiOpenEndedEvaluator>();
        services.AddScoped<IPatternEvaluator, FormIoPatternEvaluator>();
        services.AddScoped<IPatternEvaluationRouter, PatternEvaluationRouter>();

        // STT: Phase 4 (2026-07-15) — OpenAiSpeechToTextService activates when OpenAI:ApiKey /
        // OPENAI_API_KEY is configured; falls back to FakeSpeechToTextService (deterministic
        // placeholder) otherwise, same "real if configured, fake if not" precedent as every other
        // OpenAI-backed provider in this codebase.
        services.AddScoped<FakeSpeechToTextService>();
        services.AddScoped<LinguaCoach.Infrastructure.Speaking.OpenAiSpeechToTextService>();
        services.AddScoped<ISpeechToTextService>(sp =>
        {
            var openAi = sp.GetRequiredService<LinguaCoach.Infrastructure.Speaking.OpenAiSpeechToTextService>();
            return openAi.IsSupported ? openAi : sp.GetRequiredService<FakeSpeechToTextService>();
        });
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

        // Session handlers — Phase I2B: Today is module-only. ISessionGeneratorService/
        // SessionGeneratorService and IPrepareExerciseHandler/ExercisePrepareHandler were deleted
        // along with the legacy generation pipeline; see docs/reviews/2026-07-10-phase-i2b-
        // today-module-only-collapse-review.md.
        services.AddScoped<SessionQueryHandler>();
        services.AddScoped<IGetTodaysSessionHandler>(sp => sp.GetRequiredService<SessionQueryHandler>());
        services.AddScoped<IGetSessionHistoryHandler>(sp => sp.GetRequiredService<SessionQueryHandler>());
        services.AddScoped<SessionLifecycleHandler>();
        services.AddScoped<IStartSessionHandler>(sp => sp.GetRequiredService<SessionLifecycleHandler>());
        services.AddScoped<ICompleteSessionHandler>(sp => sp.GetRequiredService<SessionLifecycleHandler>());
        services.AddScoped<ICompleteExerciseHandler>(sp => sp.GetRequiredService<SessionLifecycleHandler>());
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

        // Phase I2C: the student activity readiness pool (Phase 10M) and its replenishment engine
        // (Phase 10N) were deleted — see docs/reviews/2026-07-10-phase-i2c-readiness-pool-removal-review.md.
        services.AddScoped<Jobs.NotificationDispatchJob>();

        // Practice Gym suggestion service (Phase 10O)
        services.AddScoped<IPracticeGymSuggestionService, LinguaCoach.Infrastructure.PracticeGym.PracticeGymSuggestionService>();

        // Phase H7 — Practice Gym Module Pipeline (deterministic, read-only module selection for
        // Practice Gym suggestions + the one write path for its assignment bookkeeping).
        services.AddScoped<LinguaCoach.Application.PracticeGymModules.IPracticeGymModuleSelectionService,
            LinguaCoach.Infrastructure.PracticeGymModules.PracticeGymModuleSelectionService>();
        services.AddScoped<LinguaCoach.Application.PracticeGymModules.IPracticeGymModuleAssignmentRecorder,
            LinguaCoach.Infrastructure.PracticeGymModules.PracticeGymModuleAssignmentRecorder>();

        // Phase H10 — Exercise Runtime Launch Path / Attempt Bridge (materializes an
        // approved, launch-eligible Exercise into a real LearningActivity, reusing the
        // existing scoring/attempt/ledger pipeline unchanged).
        services.AddScoped<LinguaCoach.Application.ExerciseLaunch.IExerciseLaunchService,
            LinguaCoach.Infrastructure.ExerciseLaunch.ExerciseLaunchService>();

        // Admin runtime settings / feature gate registry (Phase 20B)
        services.AddSingleton<IFeatureGateRegistry, LinguaCoach.Infrastructure.Admin.FeatureGateRegistryService>();
        services.AddScoped<IRuntimeSettingsService, LinguaCoach.Infrastructure.Admin.RuntimeSettingsService>();

        // Student readiness audit + repair (Phase 20D)
        services.AddScoped<LinguaCoach.Application.Admin.StudentReadiness.IStudentReadinessAuditService,
            LinguaCoach.Infrastructure.Admin.StudentReadinessAuditService>();
        services.AddScoped<LinguaCoach.Application.Admin.StudentReadiness.IStudentPilotReadinessRepairService,
            LinguaCoach.Infrastructure.Admin.StudentPilotReadinessRepairService>();

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

        // .NET config binding appends config-bound array items to the class default rather than
        // replacing it, so SkillsToAssess ends up with each skill listed twice (default + config,
        // both "listening, reading, writing, vocabulary, grammar, speaking"). That silently doubled
        // placement item generation and produced duplicate placement_skill_results rows on
        // completion. Deduplicate once here instead of patching every call site.
        services.PostConfigure<LinguaCoach.Application.Placement.PlacementAssessmentOptions>(o =>
            o.SkillsToAssess = o.SkillsToAssess.Distinct().ToArray());
        services.AddScoped<LinguaCoach.Application.Placement.IPlacementScoringService,
            LinguaCoach.Infrastructure.Placement.PlacementScoringService>();
        services.AddScoped<LinguaCoach.Application.Placement.IPlacementSpeakingScorer,
            LinguaCoach.Infrastructure.Placement.PlacementSpeakingScorer>();
        services.AddScoped<LinguaCoach.Application.Placement.IPlacementAssessmentService,
            LinguaCoach.Infrastructure.Placement.PlacementAssessmentService>();

        // Phase 20I-5 — Adaptive placement listening audio
        services.AddScoped<LinguaCoach.Infrastructure.Placement.AdaptivePlacementAudioService>();

        // Phase 20I-4 — Admin-configurable placement item bank
        services.AddScoped<LinguaCoach.Application.Placement.IAdminPlacementItemListQuery,
            LinguaCoach.Infrastructure.Placement.AdminPlacementItemListQueryHandler>();
        services.AddScoped<LinguaCoach.Application.Placement.IAdminPlacementItemGetQuery,
            LinguaCoach.Infrastructure.Placement.AdminPlacementItemGetQueryHandler>();
        services.AddScoped<LinguaCoach.Application.Placement.IAdminAddPlacementItemHandler,
            LinguaCoach.Infrastructure.Placement.AdminAddPlacementItemHandler>();
        services.AddScoped<LinguaCoach.Application.Placement.IAdminUpdatePlacementItemHandler,
            LinguaCoach.Infrastructure.Placement.AdminUpdatePlacementItemHandler>();
        services.AddScoped<LinguaCoach.Application.Placement.IAdminRemovePlacementItemHandler,
            LinguaCoach.Infrastructure.Placement.AdminRemovePlacementItemHandler>();
        services.AddScoped<LinguaCoach.Application.Placement.IAdminPlacementItemReviewHandler,
            LinguaCoach.Infrastructure.Placement.AdminPlacementItemReviewHandler>();
        services.AddScoped<LinguaCoach.Application.Placement.IAdminPlacementItemCalibrationHandler,
            LinguaCoach.Infrastructure.Placement.AdminPlacementItemCalibrationHandler>();

        // Phase E1 — English resource import staging (source registry, import runs, raw
        // records, candidate staging)
        services.AddScoped<LinguaCoach.Application.ResourceImport.IAdminResourceSourceListQuery,
            LinguaCoach.Infrastructure.ResourceImport.AdminResourceSourceListQueryHandler>();
        services.AddScoped<LinguaCoach.Application.ResourceImport.IAdminResourceSourceGetQuery,
            LinguaCoach.Infrastructure.ResourceImport.AdminResourceSourceGetQueryHandler>();
        services.AddScoped<LinguaCoach.Application.ResourceImport.IAdminAddResourceSourceHandler,
            LinguaCoach.Infrastructure.ResourceImport.AdminAddResourceSourceHandler>();
        services.AddScoped<LinguaCoach.Application.ResourceImport.IAdminUpdateResourceSourceHandler,
            LinguaCoach.Infrastructure.ResourceImport.AdminUpdateResourceSourceHandler>();
        services.AddScoped<LinguaCoach.Application.ResourceImport.IAdminResourceSourceApprovalHandler,
            LinguaCoach.Infrastructure.ResourceImport.AdminResourceSourceApprovalHandler>();
        services.AddScoped<LinguaCoach.Application.ResourceImport.IAdminResourceImportRunListQuery,
            LinguaCoach.Infrastructure.ResourceImport.AdminResourceImportRunListQueryHandler>();
        services.AddScoped<LinguaCoach.Application.ResourceImport.IAdminResourceImportRunGetQuery,
            LinguaCoach.Infrastructure.ResourceImport.AdminResourceImportRunGetQueryHandler>();
        services.AddScoped<LinguaCoach.Application.ResourceImport.IAdminResourceRawRecordListQuery,
            LinguaCoach.Infrastructure.ResourceImport.AdminResourceRawRecordListQueryHandler>();
        services.AddScoped<LinguaCoach.Application.ResourceImport.IAdminResourceRawRecordGetQuery,
            LinguaCoach.Infrastructure.ResourceImport.AdminResourceRawRecordGetQueryHandler>();
        services.AddScoped<LinguaCoach.Application.ResourceImport.IAdminResourceCandidateListQuery,
            LinguaCoach.Infrastructure.ResourceImport.AdminResourceCandidateListQueryHandler>();
        services.AddScoped<LinguaCoach.Application.ResourceImport.IAdminResourceCandidateGetQuery,
            LinguaCoach.Infrastructure.ResourceImport.AdminResourceCandidateGetQueryHandler>();
        services.AddScoped<LinguaCoach.Application.ResourceImport.IAdminResourceCandidateNotesHandler,
            LinguaCoach.Infrastructure.ResourceImport.AdminResourceCandidateNotesHandler>();
        services.AddScoped<LinguaCoach.Application.ResourceImport.IAdminResourceCandidateApproveHandler,
            LinguaCoach.Infrastructure.ResourceImport.AdminResourceCandidateApproveHandler>();
        services.AddScoped<LinguaCoach.Application.ResourceImport.IAdminResourceCandidateRejectHandler,
            LinguaCoach.Infrastructure.ResourceImport.AdminResourceCandidateRejectHandler>();
        // Phase 3 — import candidate review workflow.
        services.AddScoped<LinguaCoach.Application.ResourceImport.IAdminResourceCandidateSkipHandler,
            LinguaCoach.Infrastructure.ResourceImport.AdminResourceCandidateSkipHandler>();
        services.AddScoped<LinguaCoach.Application.ResourceImport.IAdminResourceCandidateContentUpdateHandler,
            LinguaCoach.Infrastructure.ResourceImport.AdminResourceCandidateContentUpdateHandler>();
        services.AddScoped<LinguaCoach.Application.ResourceImport.IAdminResourceCandidateReviewSummaryQuery,
            LinguaCoach.Infrastructure.ResourceImport.AdminResourceCandidateReviewSummaryQueryHandler>();
        services.AddScoped<LinguaCoach.Application.ResourceImport.IResourceCandidateBatchActionService,
            LinguaCoach.Infrastructure.ResourceImport.ResourceCandidateBatchActionService>();
        services.AddScoped<LinguaCoach.Application.ResourceImport.IResourceImportService,
            LinguaCoach.Infrastructure.ResourceImport.ResourceImportService>();

        // Phase H3 — Lesson foundation (reviewable teaching/explanation blocks generated from
        // published Resource Bank rows).
        services.AddScoped<LinguaCoach.Application.Lessons.IAdminLessonListQuery,
            LinguaCoach.Infrastructure.Lessons.AdminLessonListQueryHandler>();
        services.AddScoped<LinguaCoach.Application.Lessons.IAdminLessonGetQuery,
            LinguaCoach.Infrastructure.Lessons.AdminLessonGetQueryHandler>();
        services.AddScoped<LinguaCoach.Application.Lessons.IAdminCreateLessonHandler,
            LinguaCoach.Infrastructure.Lessons.AdminCreateLessonHandler>();
        services.AddScoped<LinguaCoach.Application.Lessons.IAdminUpdateLessonHandler,
            LinguaCoach.Infrastructure.Lessons.AdminUpdateLessonHandler>();
        services.AddScoped<LinguaCoach.Application.Lessons.IAdminApproveLessonHandler,
            LinguaCoach.Infrastructure.Lessons.AdminApproveLessonHandler>();
        services.AddScoped<LinguaCoach.Application.Lessons.IAdminRejectLessonHandler,
            LinguaCoach.Infrastructure.Lessons.AdminRejectLessonHandler>();
        services.AddScoped<LinguaCoach.Application.Lessons.IGenerateLessonFromResourcesHandler,
            LinguaCoach.Infrastructure.Lessons.LessonGenerationService>();
        services.AddScoped<LinguaCoach.Application.Lessons.IGenerateLessonFromResourcesWithAiHandler,
            LinguaCoach.Infrastructure.Lessons.AiLessonGenerationService>();
        // Phase K6 — admin archive/unarchive (soft-delete), mirroring the Resource Bank pattern.
        services.AddScoped<LinguaCoach.Application.Lessons.ILessonArchiveHandler,
            LinguaCoach.Infrastructure.Lessons.LessonArchiveHandler>();
        // Phase K8 — "diagnose then AI-repair" for a Lesson missing core teaching content.
        services.AddScoped<LinguaCoach.Application.Lessons.ILessonRepairService,
            LinguaCoach.Infrastructure.Lessons.LessonRepairService>();

        // Phase H4 — Activity foundation (reviewable, editable practice task designs generated
        // from published Resource Bank rows or a Lesson).
        services.AddScoped<LinguaCoach.Application.Exercises.IAdminExerciseListQuery,
            LinguaCoach.Infrastructure.Exercises.AdminExerciseListQueryHandler>();
        services.AddScoped<LinguaCoach.Application.Exercises.IAdminExerciseGetQuery,
            LinguaCoach.Infrastructure.Exercises.AdminExerciseGetQueryHandler>();
        services.AddScoped<LinguaCoach.Application.Exercises.IAdminCreateExerciseHandler,
            LinguaCoach.Infrastructure.Exercises.AdminCreateExerciseHandler>();
        services.AddScoped<LinguaCoach.Application.Exercises.IAdminUpdateExerciseHandler,
            LinguaCoach.Infrastructure.Exercises.AdminUpdateExerciseHandler>();
        services.AddScoped<LinguaCoach.Application.Exercises.IAdminApproveExerciseHandler,
            LinguaCoach.Infrastructure.Exercises.AdminApproveExerciseHandler>();
        services.AddScoped<LinguaCoach.Application.Exercises.IAdminRejectExerciseHandler,
            LinguaCoach.Infrastructure.Exercises.AdminRejectExerciseHandler>();
        services.AddScoped<LinguaCoach.Infrastructure.Exercises.ActivityGenerationService>();
        services.AddScoped<LinguaCoach.Application.Exercises.IGenerateActivityFromLessonHandler>(
            sp => sp.GetRequiredService<LinguaCoach.Infrastructure.Exercises.ActivityGenerationService>());
        services.AddScoped<LinguaCoach.Application.Exercises.IGenerateActivityFromLessonWithAiHandler,
            LinguaCoach.Infrastructure.Exercises.AiExerciseGenerationService>();
        // Phase K5 — "Generate Exercises from Lesson" with an admin-picked count/type per Exercise,
        // auto-linking a Module afterward (registered after IModuleAutoLinkService below).
        services.AddScoped<LinguaCoach.Application.Exercises.IGenerateActivitiesFromLessonHandler,
            LinguaCoach.Infrastructure.Exercises.LessonExerciseBatchGenerationService>();
        // Phase K6 — admin archive/unarchive (soft-delete), mirroring the Resource Bank pattern.
        services.AddScoped<LinguaCoach.Application.Exercises.IExerciseArchiveHandler,
            LinguaCoach.Infrastructure.Exercises.ExerciseArchiveHandler>();
        // Phase K7 — admin "preview as a learner" for a standalone Exercise (deterministic scoring,
        // mirroring Module preview's ComponentAnswerScorer usage).
        services.AddScoped<LinguaCoach.Application.Exercises.IAdminExercisePreviewSubmitHandler,
            LinguaCoach.Infrastructure.Exercises.AdminExercisePreviewService>();
        // Phase K8 — "diagnose then AI-repair" for an Exercise missing Instructions/Description
        // text (never scoring/answer-key/schema — see ExerciseRepairService's doc comment).
        services.AddScoped<LinguaCoach.Application.Exercises.IExerciseRepairService,
            LinguaCoach.Infrastructure.Exercises.ExerciseRepairService>();

        // Phase H5 — Module foundation (reusable, reviewable learning units combining
        // Lessons + Exercises + a module-level feedback plan).
        services.AddScoped<LinguaCoach.Application.Modules.IAdminModuleListQuery,
            LinguaCoach.Infrastructure.Modules.AdminModuleListQueryHandler>();
        services.AddScoped<LinguaCoach.Application.Modules.IAdminModuleGetQuery,
            LinguaCoach.Infrastructure.Modules.AdminModuleGetQueryHandler>();
        services.AddScoped<LinguaCoach.Application.Modules.IAdminCreateModuleHandler,
            LinguaCoach.Infrastructure.Modules.AdminCreateModuleHandler>();
        services.AddScoped<LinguaCoach.Application.Modules.IAdminUpdateModuleHandler,
            LinguaCoach.Infrastructure.Modules.AdminUpdateModuleHandler>();
        services.AddScoped<LinguaCoach.Application.Modules.IAdminApproveModuleHandler,
            LinguaCoach.Infrastructure.Modules.AdminApproveModuleHandler>();
        services.AddScoped<LinguaCoach.Application.Modules.IAdminRejectModuleHandler,
            LinguaCoach.Infrastructure.Modules.AdminRejectModuleHandler>();
        services.AddScoped<LinguaCoach.Infrastructure.Modules.ModuleGenerationService>();
        services.AddScoped<LinguaCoach.Application.Modules.IGenerateModuleFromItemsHandler>(
            sp => sp.GetRequiredService<LinguaCoach.Infrastructure.Modules.ModuleGenerationService>());
        services.AddScoped<LinguaCoach.Application.Modules.IGenerateModuleFromResourceHandler>(
            sp => sp.GetRequiredService<LinguaCoach.Infrastructure.Modules.ModuleGenerationService>());
        services.AddScoped<LinguaCoach.Application.Modules.IGenerateModuleFromLessonHandler>(
            sp => sp.GetRequiredService<LinguaCoach.Infrastructure.Modules.ModuleGenerationService>());
        services.AddScoped<LinguaCoach.Application.Modules.IGenerateModuleFromExerciseHandler>(
            sp => sp.GetRequiredService<LinguaCoach.Infrastructure.Modules.ModuleGenerationService>());
        services.AddScoped<LinguaCoach.Application.Modules.IGenerateModuleFromResourceWithAiHandler,
            LinguaCoach.Infrastructure.Modules.AiModuleGenerationService>();
        // Phase K5 — automatic Module create-or-extend, called after Exercise generation instead
        // of a separate manual "Generate Module" admin action.
        services.AddScoped<LinguaCoach.Application.Modules.IModuleAutoLinkService,
            LinguaCoach.Infrastructure.Modules.ModuleAutoLinkService>();
        services.AddScoped<LinguaCoach.Infrastructure.Modules.AdminModulePreviewService>();
        services.AddScoped<LinguaCoach.Application.Modules.IAdminModulePreviewQuery>(
            sp => sp.GetRequiredService<LinguaCoach.Infrastructure.Modules.AdminModulePreviewService>());
        services.AddScoped<LinguaCoach.Application.Modules.IAdminModulePreviewSubmitHandler>(
            sp => sp.GetRequiredService<LinguaCoach.Infrastructure.Modules.AdminModulePreviewService>());
        // Phase K6 — admin archive/unarchive (soft-delete), mirroring the Resource Bank pattern.
        services.AddScoped<LinguaCoach.Application.Modules.IModuleArchiveHandler,
            LinguaCoach.Infrastructure.Modules.ModuleArchiveHandler>();
        // Phase K8 — "diagnose then AI-repair" for a Module missing its Description.
        services.AddScoped<LinguaCoach.Application.Modules.IModuleRepairService,
            LinguaCoach.Infrastructure.Modules.ModuleRepairService>();

        // Phase H6 (renamed I4 Pass 3) — Today Plan Module Pipeline (deterministic, read-only
        // module selection for Today + the one write path for its assignment bookkeeping).
        services.AddScoped<LinguaCoach.Application.TodayPlanModules.ITodayPlanModuleSelectionService,
            LinguaCoach.Infrastructure.TodayPlanModules.TodayPlanModuleSelectionService>();
        services.AddScoped<LinguaCoach.Application.TodayPlanModules.ITodayPlanModuleAssignmentRecorder,
            LinguaCoach.Infrastructure.TodayPlanModules.TodayPlanModuleAssignmentRecorder>();

        // Phase E2 — AI analysis (advisory), deterministic rule validation, and dedup/fingerprint
        // gates over staged candidates.
        services.AddScoped<LinguaCoach.Application.ResourceImport.IResourceCandidateAnalysisService,
            LinguaCoach.Infrastructure.ResourceImport.ResourceCandidateAnalysisService>();
        services.AddScoped<LinguaCoach.Application.ResourceImport.IResourceCandidateValidationService,
            LinguaCoach.Infrastructure.ResourceImport.ResourceCandidateValidationService>();
        services.AddScoped<LinguaCoach.Application.ResourceImport.IResourceCandidateBatchAnalysisService,
            LinguaCoach.Infrastructure.ResourceImport.ResourceCandidateBatchAnalysisService>();
        // Phase E3 — read-only rendered preview for a staged ResourceCandidate.
        services.AddScoped<LinguaCoach.Application.ResourceImport.IResourceCandidatePreviewService,
            LinguaCoach.Infrastructure.ResourceImport.ResourceCandidatePreviewService>();
        // Phase E4 — publishes an approved, validated ResourceCandidate into its target Cefr* bank table.
        services.AddScoped<LinguaCoach.Application.ResourceImport.IResourceCandidatePublishService,
            LinguaCoach.Infrastructure.ResourceImport.ResourceCandidatePublishService>();
        // Phase E5 — read-only browse/search over the published Cefr* bank tables.
        services.AddScoped<LinguaCoach.Application.ResourceImport.IResourceBankQueryService,
            LinguaCoach.Infrastructure.ResourceImport.ResourceBankQueryService>();
        // Phase K3 — admin archive/unarchive (soft-delete) for Resource Bank rows.
        services.AddScoped<LinguaCoach.Application.ResourceImport.IResourceBankArchiveHandler,
            LinguaCoach.Infrastructure.ResourceImport.ResourceBankArchiveHandler>();
        // Phase K8 — shared AI field-repair helper + per-entity "diagnose then AI-repair" services.
        services.AddScoped<LinguaCoach.Infrastructure.AdminRepair.AdminRepairFieldGenerator>();
        services.AddScoped<LinguaCoach.Application.ResourceImport.IResourceBankRepairService,
            LinguaCoach.Infrastructure.ResourceImport.ResourceBankRepairService>();
        // Phase K5 — admin edit of a published Resource Bank item's content/metadata.
        services.AddScoped<LinguaCoach.Application.ResourceImport.IResourceBankItemUpdateHandler,
            LinguaCoach.Infrastructure.ResourceImport.ResourceBankItemUpdateHandler>();
        // Phase J5c — real audio-file upload/storage for ListeningPassage candidates.
        services.AddScoped<LinguaCoach.Application.ResourceImport.IResourceCandidateAudioService,
            LinguaCoach.Infrastructure.ResourceImport.ResourceCandidateAudioService>();
        // Phase K1 — AI-assisted import column-mapping proposal.
        services.AddScoped<LinguaCoach.Application.ResourceImport.IResourceImportColumnMappingService,
            LinguaCoach.Infrastructure.ResourceImport.ResourceImportColumnMappingService>();

        // Phase 4 (2026-07-15) — large-scale AI import packages. App never crashes on a missing
        // "ImportPackageLimits" config section — sensible defaults are baked into the options class.
        if (configuration is not null)
            services.Configure<LinguaCoach.Infrastructure.ResourceImport.ImportPackageLimitsOptions>(
                configuration.GetSection(LinguaCoach.Infrastructure.ResourceImport.ImportPackageLimitsOptions.SectionName));
        else
            services.Configure<LinguaCoach.Infrastructure.ResourceImport.ImportPackageLimitsOptions>(_ => { });
        services.AddScoped<LinguaCoach.Application.ResourceImport.IZipPackageInspector,
            LinguaCoach.Infrastructure.ResourceImport.ZipPackageInspector>();
        services.AddScoped<LinguaCoach.Application.ResourceImport.IImportPackageUploadService,
            LinguaCoach.Infrastructure.ResourceImport.ImportPackageUploadService>();
        // Phase 4.2 — the canonical entry point for pasted text and/or loose (non-ZIP) files.
        services.AddScoped<LinguaCoach.Application.ResourceImport.IImportPackageSubmissionService,
            LinguaCoach.Infrastructure.ResourceImport.ImportPackageSubmissionService>();
        services.AddScoped<LinguaCoach.Application.ResourceImport.IImportProcessingModeDecisionService,
            LinguaCoach.Infrastructure.ResourceImport.ImportProcessingModeDecisionService>();

        // Mandatory Import Execution Plan addendum (2026-07-15) — plan generation + approval,
        // required before any package (regardless of size) may be processed.
        if (configuration is not null)
            services.Configure<LinguaCoach.Infrastructure.ResourceImport.ImportCostEstimationOptions>(
                configuration.GetSection(LinguaCoach.Infrastructure.ResourceImport.ImportCostEstimationOptions.SectionName));
        else
            services.Configure<LinguaCoach.Infrastructure.ResourceImport.ImportCostEstimationOptions>(_ => { });
        services.AddScoped<LinguaCoach.Application.ResourceImport.IImportExecutionPlanGenerationService,
            LinguaCoach.Infrastructure.ResourceImport.ImportExecutionPlanGenerationService>();
        services.AddScoped<LinguaCoach.Application.ResourceImport.IImportExecutionPlanApprovalService,
            LinguaCoach.Infrastructure.ResourceImport.ImportExecutionPlanApprovalService>();
        services.AddScoped<LinguaCoach.Application.ResourceImport.IImportPackageProcessingService,
            LinguaCoach.Infrastructure.ResourceImport.ImportPackageProcessingService>();
        // Phase 4.3 — the single place ProfileJson is parsed/validated; execution must resolve
        // through this rather than reading ProfileJson directly.
        services.AddScoped<LinguaCoach.Application.ResourceImport.IApprovedImportProfileResolver,
            LinguaCoach.Infrastructure.ResourceImport.ApprovedImportProfileResolver>();

        // Phase 4.4 (Workstream A) — admin plan editing before approval, estimate recalculation,
        // and bounded mapping preview.
        services.AddScoped<LinguaCoach.Application.ResourceImport.IImportPlanEstimateService,
            LinguaCoach.Infrastructure.ResourceImport.ImportPlanEstimateService>();
        services.AddScoped<LinguaCoach.Application.ResourceImport.IImportPlanDraftService,
            LinguaCoach.Infrastructure.ResourceImport.ImportPlanDraftService>();
        services.AddScoped<LinguaCoach.Application.ResourceImport.IImportPlanPreviewService,
            LinguaCoach.Infrastructure.ResourceImport.ImportPlanPreviewService>();
        // Phase 4.4 (Workstream B) — durable, retry-safe STT operation ledger.
        services.AddScoped<LinguaCoach.Application.ResourceImport.IImportSttOperationLedger,
            LinguaCoach.Infrastructure.ResourceImport.ImportSttOperationLedger>();
        // Phase 4.4B — audited, concurrency-checked cost ceiling amendment and controlled resume.
        services.AddScoped<LinguaCoach.Application.ResourceImport.IImportCostCeilingAmendmentService,
            LinguaCoach.Infrastructure.ResourceImport.ImportCostCeilingAmendmentService>();
        // Phase 4.4C — read-only STT operation-ledger visibility for the admin plan page.
        services.AddScoped<LinguaCoach.Application.ResourceImport.IImportSttOperationSummaryQuery,
            LinguaCoach.Infrastructure.ResourceImport.ImportSttOperationSummaryQuery>();
        // Phase 4.4D — durable, retry-safe AI candidate-enrichment operation ledger + its read-only
        // admin visibility, generalizing the STT ledger pattern.
        services.AddScoped<LinguaCoach.Application.ResourceImport.IImportAiEnrichmentOperationLedger,
            LinguaCoach.Infrastructure.ResourceImport.ImportAiEnrichmentOperationLedger>();
        services.AddScoped<LinguaCoach.Application.ResourceImport.IImportAiEnrichmentOperationSummaryQuery,
            LinguaCoach.Infrastructure.ResourceImport.ImportAiEnrichmentOperationSummaryQuery>();
        // Phase 4.4E — real, persisted, reusable audio-duration measurement (replaces the flat
        // five-minute-per-file assumption).
        if (configuration is not null)
            services.Configure<LinguaCoach.Infrastructure.ResourceImport.AudioDurationProbeOptions>(
                configuration.GetSection(LinguaCoach.Infrastructure.ResourceImport.AudioDurationProbeOptions.SectionName));
        else
            services.Configure<LinguaCoach.Infrastructure.ResourceImport.AudioDurationProbeOptions>(_ => { });
        services.AddScoped<LinguaCoach.Application.ResourceImport.IAudioDurationProbe,
            LinguaCoach.Infrastructure.ResourceImport.AudioDurationProbe>();
        services.AddScoped<LinguaCoach.Application.ResourceImport.IImportAssetAudioDurationResolver,
            LinguaCoach.Infrastructure.ResourceImport.ImportAssetAudioDurationResolver>();

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

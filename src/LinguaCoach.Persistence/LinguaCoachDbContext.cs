using LinguaCoach.Domain.Entities;
using LinguaCoach.Persistence.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace LinguaCoach.Persistence;

public sealed class LinguaCoachDbContext : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>
{
    public LinguaCoachDbContext(DbContextOptions<LinguaCoachDbContext> options) : base(options) { }

    public DbSet<Language> Languages => Set<Language>();
    public DbSet<LanguagePair> LanguagePairs => Set<LanguagePair>();
    public DbSet<CareerProfile> CareerProfiles => Set<CareerProfile>();
    public DbSet<StudentProfile> StudentProfiles => Set<StudentProfile>();
    public DbSet<AiPrompt> AiPrompts => Set<AiPrompt>();
    public DbSet<AiUsageLog> AiUsageLogs => Set<AiUsageLog>();
    public DbSet<AiProviderConfig> AiProviderConfigs => Set<AiProviderConfig>();
    public DbSet<AiConfigCategory> AiConfigCategories => Set<AiConfigCategory>();
    public DbSet<AiProviderCredential> AiProviderCredentials => Set<AiProviderCredential>();
    public DbSet<VocabularyEntry> VocabularyEntries => Set<VocabularyEntry>();
    public DbSet<CurriculumWordList> CurriculumWordLists => Set<CurriculumWordList>();
    public DbSet<UserLearningSummary> UserLearningSummaries => Set<UserLearningSummary>();
    public DbSet<StudentSkillProfile> StudentSkillProfiles => Set<StudentSkillProfile>();
    public DbSet<SpeakingScenario> SpeakingScenarios => Set<SpeakingScenario>();
    public DbSet<SpeakingSession> SpeakingSessions => Set<SpeakingSession>();
    public DbSet<SpeakingTurn> SpeakingTurns => Set<SpeakingTurn>();
    public DbSet<WritingScenario> WritingScenarios => Set<WritingScenario>();
    public DbSet<LessonVocabularyLog> LessonVocabularyLogs => Set<LessonVocabularyLog>();
    public DbSet<LearningPath> LearningPaths => Set<LearningPath>();
    public DbSet<LearningModule> LearningModules => Set<LearningModule>();
    public DbSet<LearningActivity> LearningActivities => Set<LearningActivity>();
    public DbSet<ActivityAttempt> ActivityAttempts => Set<ActivityAttempt>();
    public DbSet<StudentVocabularyItem> StudentVocabularyItems => Set<StudentVocabularyItem>();
    public DbSet<PlacementAssessment> PlacementAssessments => Set<PlacementAssessment>();
    public DbSet<LearningSession> LearningSessions => Set<LearningSession>();
    public DbSet<SessionExercise> SessionExercises => Set<SessionExercise>();
    public DbSet<ExercisePatternDefinition> ExercisePatterns => Set<ExercisePatternDefinition>();
    public DbSet<ExerciseTypeDefinition> ExerciseTypeDefinitions => Set<ExerciseTypeDefinition>();
    public DbSet<AudioAsset> AudioAssets => Set<AudioAsset>();
    public DbSet<LessonGenerationSettings> LessonGenerationSettings => Set<LessonGenerationSettings>();
    public DbSet<RuntimeSettingOverride> RuntimeSettingOverrides => Set<RuntimeSettingOverride>();
    public DbSet<StudentResetLog> StudentResetLogs => Set<StudentResetLog>();
    public DbSet<GenerationBatch> GenerationBatches => Set<GenerationBatch>();
    public DbSet<GenerationJobItem> GenerationJobItems => Set<GenerationJobItem>();
    public DbSet<GenerationValidationFailure> GenerationValidationFailures => Set<GenerationValidationFailure>();
    public DbSet<PracticeActivityCache> PracticeActivityCache => Set<PracticeActivityCache>();
    public DbSet<StudentLearningEvent> StudentLearningEvents => Set<StudentLearningEvent>();

    // T47 — Onboarding v2
    public DbSet<StudentFlowTemplate> StudentFlowTemplates => Set<StudentFlowTemplate>();
    public DbSet<StudentFlowTemplateVersion> StudentFlowTemplateVersions => Set<StudentFlowTemplateVersion>();
    public DbSet<StudentFlowSubmission> StudentFlowSubmissions => Set<StudentFlowSubmission>();

    // Phase 10K — Curriculum syllabus foundation
    public DbSet<CurriculumObjective> CurriculumObjectives => Set<CurriculumObjective>();
    public DbSet<ActivityTemplate> ActivityTemplates => Set<ActivityTemplate>();
    public DbSet<CefrResourceSource> CefrResourceSources => Set<CefrResourceSource>();
    public DbSet<CefrDescriptor> CefrDescriptors => Set<CefrDescriptor>();
    public DbSet<CefrVocabularyEntry> CefrVocabularyEntries => Set<CefrVocabularyEntry>();
    public DbSet<CefrGrammarProfileEntry> CefrGrammarProfileEntries => Set<CefrGrammarProfileEntry>();
    public DbSet<CefrReadingReference> CefrReadingReferences => Set<CefrReadingReference>();
    public DbSet<CefrReadingPassage> CefrReadingPassages => Set<CefrReadingPassage>();

    // Phase 10M — Student activity readiness pool
    public DbSet<StudentActivityReadinessItem> StudentActivityReadinessItems => Set<StudentActivityReadinessItem>();

    // Phase B — Repetition/novelty foundation (real content-usage history)
    public DbSet<StudentActivityUsageLog> StudentActivityUsageLogs => Set<StudentActivityUsageLog>();

    // Phase B2 — Activity feedback / repeat policy / calibration signals
    public DbSet<ActivityFeedbackSignal> ActivityFeedbackSignals => Set<ActivityFeedbackSignal>();

    // Phase 12D — Learning Plan orchestrator
    public DbSet<StudentLearningPlan> StudentLearningPlans => Set<StudentLearningPlan>();
    public DbSet<StudentLearningPlanObjective> StudentLearningPlanObjectives => Set<StudentLearningPlanObjective>();

    // Phase 13A — Adaptive Placement Engine
    public DbSet<PlacementAssessmentItem> PlacementAssessmentItems => Set<PlacementAssessmentItem>();
    public DbSet<PlacementSkillResult> PlacementSkillResults => Set<PlacementSkillResult>();

    // Phase 20I-4 — Admin-configurable placement item bank
    public DbSet<PlacementItemDefinition> PlacementItemDefinitions => Set<PlacementItemDefinition>();

    // Phase 10R — Usage governance, token tracking & quota enforcement
    public DbSet<FeatureDefinition> FeatureDefinitions => Set<FeatureDefinition>();
    public DbSet<UsagePolicy> UsagePolicies => Set<UsagePolicy>();
    public DbSet<UsagePolicyRule> UsagePolicyRules => Set<UsagePolicyRule>();
    public DbSet<StudentPolicyAssignment> StudentPolicyAssignments => Set<StudentPolicyAssignment>();
    public DbSet<UsageEvent> UsageEvents => Set<UsageEvent>();
    public DbSet<StudentUsageDaily> StudentUsageDaily => Set<StudentUsageDaily>();
    public DbSet<AdminAuditLog> AdminAuditLogs => Set<AdminAuditLog>();
    public DbSet<AiModelPricingOverride> AiModelPricingOverrides => Set<AiModelPricingOverride>();

    // Phase 10Auth-F — Auth event audit log
    public DbSet<AuthSecurityEvent> AuthSecurityEvents => Set<AuthSecurityEvent>();

    // Phase 10Auth-F-4 — Refresh tokens / session management
    public DbSet<UserRefreshToken> UserRefreshTokens => Set<UserRefreshToken>();

    // Phase 16F — Speaking evaluation foundation
    public DbSet<SpeakingEvaluation> SpeakingEvaluations => Set<SpeakingEvaluation>();
    // Phase 16I — Speaking evaluation mastery signal integration
    public DbSet<SpeakingEvaluationAppliedSignal> SpeakingEvaluationAppliedSignals => Set<SpeakingEvaluationAppliedSignal>();

    // Phase 17A — Writing evaluation foundation
    public DbSet<WritingEvaluation> WritingEvaluations => Set<WritingEvaluation>();
    // Phase 17C — Writing evaluation mastery signal integration
    public DbSet<WritingEvaluationAppliedSignal> WritingEvaluationAppliedSignals => Set<WritingEvaluationAppliedSignal>();

    // Phase E1 — English resource import staging (source → run → raw record → candidate)
    public DbSet<ResourceImportRun> ResourceImportRuns => Set<ResourceImportRun>();
    public DbSet<ResourceRawRecord> ResourceRawRecords => Set<ResourceRawRecord>();
    public DbSet<ResourceCandidate> ResourceCandidates => Set<ResourceCandidate>();

    // Phase H3 — Learn Item foundation (reviewable teaching/explanation blocks generated from
    // published Resource Bank rows; the "Learn" half of a future Module)
    public DbSet<LearnItem> LearnItems => Set<LearnItem>();
    public DbSet<LearnItemResourceLink> LearnItemResourceLinks => Set<LearnItemResourceLink>();

    // Phase H4 — Activity foundation (reviewable, editable practice task designs generated from
    // published Resource Bank rows or a Learn Item; the "Practice" half of a future Module)
    public DbSet<ActivityDefinition> ActivityDefinitions => Set<ActivityDefinition>();
    public DbSet<ActivityResourceLink> ActivityResourceLinks => Set<ActivityResourceLink>();

    // Phase H5 — Module Definition foundation (reusable, reviewable learning units combining
    // Learn Items + Activity Definitions + a module-level feedback plan; not wired into any
    // runtime path — see ModuleDefinition's doc comment for the distinction from LearningModule)
    public DbSet<ModuleDefinition> ModuleDefinitions => Set<ModuleDefinition>();
    public DbSet<ModuleDefinitionLearnItemLink> ModuleDefinitionLearnItemLinks => Set<ModuleDefinitionLearnItemLink>();
    public DbSet<ModuleDefinitionActivityLink> ModuleDefinitionActivityLinks => Set<ModuleDefinitionActivityLink>();

    // Phase H6 — Daily Lesson Module Pipeline (additive bookkeeping: which ModuleDefinition, if
    // any, the deterministic selector chose for a student on a given date)
    public DbSet<StudentDailyModuleAssignment> StudentDailyModuleAssignments => Set<StudentDailyModuleAssignment>();

    // Phase H7 — Practice Gym Module Pipeline (additive bookkeeping: which ModuleDefinition, if
    // any, the deterministic selector suggested to a student)
    public DbSet<StudentPracticeGymModuleAssignment> StudentPracticeGymModuleAssignments => Set<StudentPracticeGymModuleAssignment>();

    // Phase 10W — Enterprise notification platform
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<NotificationOutboxItem> NotificationOutboxItems => Set<NotificationOutboxItem>();
    public DbSet<NotificationTemplate> NotificationTemplates => Set<NotificationTemplate>();
    public DbSet<NotificationPreference> NotificationPreferences => Set<NotificationPreference>();
    public DbSet<NotificationChannelConfig> NotificationChannelConfigs => Set<NotificationChannelConfig>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(LinguaCoachDbContext).Assembly);

        if (Database.ProviderName?.Contains("Npgsql", StringComparison.OrdinalIgnoreCase) == true)
        {
            modelBuilder.Entity<LearningPath>()
                .Property<uint>("xmin")
                .HasColumnName("xmin")
                .HasColumnType("xid")
                .ValueGeneratedOnAddOrUpdate()
                .IsConcurrencyToken();

            modelBuilder.Entity<PracticeActivityCache>()
                .Property<uint>("xmin")
                .HasColumnName("xmin")
                .HasColumnType("xid")
                .ValueGeneratedOnAddOrUpdate()
                .IsConcurrencyToken();

            modelBuilder.Entity<StudentActivityReadinessItem>()
                .Property<uint>("xmin")
                .HasColumnName("xmin")
                .HasColumnType("xid")
                .ValueGeneratedOnAddOrUpdate()
                .IsConcurrencyToken();
        }
    }
}

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
    public DbSet<LearningTrack> LearningTracks => Set<LearningTrack>();
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
    public DbSet<WritingSubmission> WritingSubmissions => Set<WritingSubmission>();
    public DbSet<LessonVocabularyLog> LessonVocabularyLogs => Set<LessonVocabularyLog>();
    public DbSet<LearningPath> LearningPaths => Set<LearningPath>();
    public DbSet<LearningModule> LearningModules => Set<LearningModule>();
    public DbSet<LearningActivity> LearningActivities => Set<LearningActivity>();
    public DbSet<ActivityAttempt> ActivityAttempts => Set<ActivityAttempt>();
    public DbSet<StudentVocabularyItem> StudentVocabularyItems => Set<StudentVocabularyItem>();
    public DbSet<PlacementAssessment> PlacementAssessments => Set<PlacementAssessment>();
    public DbSet<PlacementAnswer> PlacementAnswers => Set<PlacementAnswer>();
    public DbSet<LearningSession> LearningSessions => Set<LearningSession>();
    public DbSet<SessionExercise> SessionExercises => Set<SessionExercise>();
    public DbSet<ExercisePatternDefinition> ExercisePatterns => Set<ExercisePatternDefinition>();
    public DbSet<ExerciseTypeDefinition> ExerciseTypeDefinitions => Set<ExerciseTypeDefinition>();
    public DbSet<AudioAsset> AudioAssets => Set<AudioAsset>();
    public DbSet<LessonGenerationSettings> LessonGenerationSettings => Set<LessonGenerationSettings>();
    public DbSet<StudentResetLog> StudentResetLogs => Set<StudentResetLog>();
    public DbSet<GenerationBatch> GenerationBatches => Set<GenerationBatch>();
    public DbSet<GenerationJobItem> GenerationJobItems => Set<GenerationJobItem>();
    public DbSet<PracticeActivityCache> PracticeActivityCache => Set<PracticeActivityCache>();
    public DbSet<StudentLearningEvent> StudentLearningEvents => Set<StudentLearningEvent>();

    // T47 — Onboarding v2
    public DbSet<OnboardingFlowDefinition> OnboardingFlowDefinitions => Set<OnboardingFlowDefinition>();
    public DbSet<OnboardingStepDefinition> OnboardingStepDefinitions => Set<OnboardingStepDefinition>();
    public DbSet<StudentOnboardingProgress> StudentOnboardingProgress => Set<StudentOnboardingProgress>();
    public DbSet<StudentOnboardingResponse> StudentOnboardingResponses => Set<StudentOnboardingResponse>();

    // Phase 10K — Curriculum syllabus foundation
    public DbSet<CurriculumObjective> CurriculumObjectives => Set<CurriculumObjective>();

    // Phase 10M — Student activity readiness pool
    public DbSet<StudentActivityReadinessItem> StudentActivityReadinessItems => Set<StudentActivityReadinessItem>();

    // Phase 12D — Learning Plan orchestrator
    public DbSet<StudentLearningPlan> StudentLearningPlans => Set<StudentLearningPlan>();
    public DbSet<StudentLearningPlanObjective> StudentLearningPlanObjectives => Set<StudentLearningPlanObjective>();

    // Phase 13A — Adaptive Placement Engine
    public DbSet<PlacementAssessmentItem> PlacementAssessmentItems => Set<PlacementAssessmentItem>();
    public DbSet<PlacementSkillResult> PlacementSkillResults => Set<PlacementSkillResult>();

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

            modelBuilder.Entity<StudentOnboardingProgress>()
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

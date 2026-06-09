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
        }
    }
}

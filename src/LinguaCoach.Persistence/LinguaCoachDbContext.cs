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
    public DbSet<SpeakingScenario> SpeakingScenarios => Set<SpeakingScenario>();
    public DbSet<SpeakingSession> SpeakingSessions => Set<SpeakingSession>();
    public DbSet<SpeakingTurn> SpeakingTurns => Set<SpeakingTurn>();
    public DbSet<WritingSubmission> WritingSubmissions => Set<WritingSubmission>();
    public DbSet<LessonVocabularyLog> LessonVocabularyLogs => Set<LessonVocabularyLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(LinguaCoachDbContext).Assembly);
    }
}

using LinguaCoach.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace LinguaCoach.Persistence;

public sealed class LinguaCoachDbContext : DbContext
{
    public LinguaCoachDbContext(DbContextOptions<LinguaCoachDbContext> options) : base(options) { }

    public DbSet<Language> Languages => Set<Language>();
    public DbSet<LanguagePair> LanguagePairs => Set<LanguagePair>();
    public DbSet<LearningTrack> LearningTracks => Set<LearningTrack>();
    public DbSet<CareerProfile> CareerProfiles => Set<CareerProfile>();
    public DbSet<StudentProfile> StudentProfiles => Set<StudentProfile>();
    public DbSet<AiPrompt> AiPrompts => Set<AiPrompt>();
    public DbSet<AiUsageLog> AiUsageLogs => Set<AiUsageLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(LinguaCoachDbContext).Assembly);
    }
}

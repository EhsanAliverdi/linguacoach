using LinguaCoach.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LinguaCoach.Persistence.Configurations;

internal sealed class SpeakingScenarioConfiguration : IEntityTypeConfiguration<SpeakingScenario>
{
    public void Configure(EntityTypeBuilder<SpeakingScenario> builder)
    {
        builder.ToTable("speaking_scenarios");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired()
            .HasDefaultValueSql("now()");

        builder.Property(e => e.CareerProfileId).HasColumnName("career_profile_id").IsRequired();
        builder.Property(e => e.LanguagePairId).HasColumnName("language_pair_id").IsRequired();
        builder.Property(e => e.Title).HasColumnName("title").HasMaxLength(300).IsRequired();
        builder.Property(e => e.Goal).HasColumnName("goal").HasMaxLength(500).IsRequired();
        builder.Property(e => e.MaxTurns).HasColumnName("max_turns").IsRequired();
        builder.Property(e => e.TargetPhrases).HasColumnName("target_phrases").IsRequired();
        builder.Property(e => e.Rubric).HasColumnName("rubric").IsRequired();
        builder.Property(e => e.DifficultyLevel).HasColumnName("difficulty_level")
            .HasMaxLength(10).IsRequired();

        builder.HasIndex(e => new { e.CareerProfileId, e.LanguagePairId })
            .HasDatabaseName("ix_speaking_scenarios_career_lang");

        builder.HasOne<Domain.Entities.CareerProfile>()
            .WithMany()
            .HasForeignKey(e => e.CareerProfileId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Domain.Entities.LanguagePair>()
            .WithMany()
            .HasForeignKey(e => e.LanguagePairId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

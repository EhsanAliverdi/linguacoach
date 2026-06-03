using LinguaCoach.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LinguaCoach.Persistence.Configurations;

internal sealed class WritingScenarioConfiguration : IEntityTypeConfiguration<WritingScenario>
{
    public void Configure(EntityTypeBuilder<WritingScenario> builder)
    {
        builder.ToTable("writing_scenarios");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired()
            .HasDefaultValueSql("now()");

        builder.Property(e => e.LanguagePairId).HasColumnName("language_pair_id");
        builder.Property(e => e.CareerProfileId).HasColumnName("career_profile_id");
        builder.Property(e => e.Title).HasColumnName("title").HasMaxLength(300).IsRequired();
        builder.Property(e => e.Situation).HasColumnName("situation").IsRequired();
        builder.Property(e => e.LearningGoal).HasColumnName("learning_goal").IsRequired();
        builder.Property(e => e.TargetPhrasesJson).HasColumnName("target_phrases_json")
            .HasColumnType("jsonb").IsRequired();
        builder.Property(e => e.TargetVocabularyJson).HasColumnName("target_vocabulary_json")
            .HasColumnType("jsonb").IsRequired();
        builder.Property(e => e.ExampleText).HasColumnName("example_text").IsRequired();
        builder.Property(e => e.CommonMistakeToAvoid).HasColumnName("common_mistake_to_avoid").IsRequired();
        builder.Property(e => e.Difficulty).HasColumnName("difficulty").HasMaxLength(10).IsRequired();
        builder.Property(e => e.IsActive).HasColumnName("is_active").IsRequired();

        builder.HasIndex(e => e.IsActive)
            .HasDatabaseName("ix_writing_scenarios_active")
            .HasFilter("is_active = true");

        builder.HasOne<LanguagePair>()
            .WithMany()
            .HasForeignKey(e => e.LanguagePairId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne<CareerProfile>()
            .WithMany()
            .HasForeignKey(e => e.CareerProfileId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

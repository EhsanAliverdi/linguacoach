using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LinguaCoach.Persistence.Configurations;

internal sealed class LearningActivityConfiguration : IEntityTypeConfiguration<LearningActivity>
{
    public void Configure(EntityTypeBuilder<LearningActivity> builder)
    {
        builder.ToTable("learning_activities");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired()
            .HasDefaultValueSql("now()");

        builder.Property(e => e.LearningModuleId).HasColumnName("learning_module_id");
        builder.Property(e => e.ActivityType).HasColumnName("activity_type")
            .HasConversion<int>().IsRequired();
        builder.Property(e => e.Source).HasColumnName("source")
            .HasConversion<int>().IsRequired();
        builder.Property(e => e.Title).HasColumnName("title").HasMaxLength(500).IsRequired();
        builder.Property(e => e.Difficulty).HasColumnName("difficulty").HasMaxLength(10).IsRequired();
        builder.Property(e => e.AiGeneratedContentJson).HasColumnName("ai_generated_content_json")
            .HasColumnType("jsonb").IsRequired();
        builder.Property(e => e.SourceWritingScenarioId).HasColumnName("source_writing_scenario_id");
        builder.Property(e => e.ExercisePatternKey).HasColumnName("exercise_pattern_key").HasMaxLength(100);
        builder.Property(e => e.IsActive).HasColumnName("is_active").IsRequired();

        builder.HasIndex(e => new { e.ActivityType, e.IsActive })
            .HasDatabaseName("ix_learning_activities_type_active")
            .HasFilter("is_active = true");

        builder.HasIndex(e => e.Source)
            .HasDatabaseName("ix_learning_activities_source");

        builder.HasMany(e => e.Attempts)
            .WithOne()
            .HasForeignKey(e => e.LearningActivityId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

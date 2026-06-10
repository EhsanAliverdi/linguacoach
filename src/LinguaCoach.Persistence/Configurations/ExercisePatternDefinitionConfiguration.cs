using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LinguaCoach.Persistence.Configurations;

internal sealed class ExercisePatternDefinitionConfiguration
    : IEntityTypeConfiguration<ExercisePatternDefinition>
{
    public void Configure(EntityTypeBuilder<ExercisePatternDefinition> builder)
    {
        builder.ToTable("exercise_patterns");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired()
            .HasDefaultValueSql("now()");

        builder.Property(e => e.Key).HasColumnName("key").HasMaxLength(100).IsRequired();
        builder.HasIndex(e => e.Key)
            .IsUnique()
            .HasDatabaseName("ix_exercise_patterns_key");

        builder.Property(e => e.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
        builder.Property(e => e.PrimarySkill).HasColumnName("primary_skill").HasMaxLength(50).IsRequired();
        builder.Property(e => e.SecondarySkillsJson).HasColumnName("secondary_skills_json").IsRequired();
        builder.Property(e => e.CompatibleKindsJson).HasColumnName("compatible_kinds_json").IsRequired();

        builder.Property(e => e.ActivityType).HasColumnName("activity_type")
            .HasConversion<int>().IsRequired();
        builder.Property(e => e.InteractionMode).HasColumnName("interaction_mode")
            .HasConversion<int>().IsRequired();
        builder.Property(e => e.MarkingMode).HasColumnName("marking_mode")
            .HasConversion<int>().IsRequired();

        builder.Property(e => e.EstimatedMinutes).HasColumnName("estimated_minutes").IsRequired();
        builder.Property(e => e.AiGeneratePromptKey).HasColumnName("ai_generate_prompt_key")
            .HasMaxLength(200).IsRequired();
        builder.Property(e => e.AiEvaluatePromptKey).HasColumnName("ai_evaluate_prompt_key")
            .HasMaxLength(200).IsRequired();

        builder.Property(e => e.RequiresAudio).HasColumnName("requires_audio").IsRequired();
        builder.Property(e => e.WorkplaceContext).HasColumnName("workplace_context").IsRequired();
        builder.Property(e => e.TeachingPurpose).HasColumnName("teaching_purpose").IsRequired();
        builder.Property(e => e.IsActive).HasColumnName("is_active").IsRequired();

        builder.HasIndex(e => e.IsActive)
            .HasDatabaseName("ix_exercise_patterns_active")
            .HasFilter("is_active = true");
    }
}

using LinguaCoach.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LinguaCoach.Persistence.Configurations;

internal sealed class ExerciseTypeDefinitionConfiguration : IEntityTypeConfiguration<ExerciseTypeDefinition>
{
    public void Configure(EntityTypeBuilder<ExerciseTypeDefinition> builder)
    {
        builder.ToTable("exercise_type_definitions");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired().HasDefaultValueSql("now()");
        builder.Property(e => e.UpdatedAt).HasColumnName("updated_at").IsRequired().HasDefaultValueSql("now()");
        builder.Property(e => e.Key).HasColumnName("key").HasMaxLength(100).IsRequired();
        builder.HasIndex(e => e.Key).IsUnique().HasDatabaseName("ix_exercise_type_definitions_key");
        builder.Property(e => e.DisplayName).HasColumnName("display_name").HasMaxLength(200).IsRequired();
        builder.Property(e => e.Description).HasColumnName("description").IsRequired();
        builder.Property(e => e.PrimarySkill).HasColumnName("primary_skill").HasMaxLength(50).IsRequired();
        builder.Property(e => e.SecondarySkillsJson).HasColumnName("secondary_skills_json").IsRequired();
        builder.Property(e => e.Category).HasColumnName("category").HasMaxLength(80).IsRequired();
        builder.Property(e => e.IsEnabled).HasColumnName("is_enabled").IsRequired();
        builder.Property(e => e.ImplementationStatus).HasColumnName("implementation_status").HasMaxLength(40).IsRequired();
        builder.Property(e => e.RendererKey).HasColumnName("renderer_key").HasMaxLength(100).IsRequired();
        builder.Property(e => e.EvaluatorKey).HasColumnName("evaluator_key").HasMaxLength(100).IsRequired();
        builder.Property(e => e.GenerationPromptKey).HasColumnName("generation_prompt_key").HasMaxLength(200).IsRequired();
        builder.Property(e => e.LegacyActivityType).HasColumnName("legacy_activity_type").HasConversion<int?>();
        builder.Property(e => e.ExercisePatternKey).HasColumnName("exercise_pattern_key").HasMaxLength(100);
        builder.Property(e => e.EstimatedDurationMinutes).HasColumnName("estimated_duration_minutes").IsRequired();
        builder.Property(e => e.RequiresAudio).HasColumnName("requires_audio").IsRequired();
        builder.Property(e => e.RequiresImage).HasColumnName("requires_image").IsRequired();
        builder.Property(e => e.SupportsPracticeGym).HasColumnName("supports_practice_gym").IsRequired();
        builder.Property(e => e.SupportsTodayLesson).HasColumnName("supports_today_lesson").IsRequired();
        builder.Ignore(e => e.IsAvailableForGeneration);
        builder.HasIndex(e => new { e.PrimarySkill, e.IsEnabled }).HasDatabaseName("ix_exercise_type_definitions_skill_enabled");
    }
}

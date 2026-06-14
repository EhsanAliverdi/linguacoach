using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LinguaCoach.Persistence.Configurations;

internal sealed class SessionExerciseConfiguration : IEntityTypeConfiguration<SessionExercise>
{
    public void Configure(EntityTypeBuilder<SessionExercise> builder)
    {
        builder.ToTable("session_exercises");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired()
            .HasDefaultValueSql("now()");

        builder.Property(e => e.LearningSessionId).HasColumnName("learning_session_id").IsRequired();
        builder.Property(e => e.Order).HasColumnName("order").IsRequired();
        builder.Property(e => e.ExercisePatternKey).HasColumnName("exercise_pattern_key").HasMaxLength(100).IsRequired();
        builder.Property(e => e.PrimarySkill).HasColumnName("primary_skill").HasMaxLength(100).IsRequired();
        builder.Property(e => e.SecondarySkillsJson).HasColumnName("secondary_skills_json").IsRequired(false);
        builder.Property(e => e.EstimatedMinutes).HasColumnName("estimated_minutes").IsRequired();
        builder.Property(e => e.Instructions).HasColumnName("instructions").IsRequired();
        builder.Property(e => e.LearningActivityId).HasColumnName("learning_activity_id").IsRequired(false);
        builder.Property(e => e.Status).HasColumnName("status")
            .HasConversion<int>().IsRequired();
        builder.Property(e => e.CompletedAtUtc).HasColumnName("completed_at_utc").IsRequired(false);
        builder.Property(e => e.DeletedAtUtc).HasColumnName("deleted_at_utc").IsRequired(false);

        builder.HasQueryFilter(e => e.DeletedAtUtc == null);

        builder.HasIndex(e => new { e.LearningSessionId, e.Order })
            .HasDatabaseName("ix_session_exercises_session_order");

        // Nullable FK to learning_activities — no cascade; activity deletion is independent.
        builder.HasOne<LearningActivity>()
            .WithMany()
            .HasForeignKey(e => e.LearningActivityId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

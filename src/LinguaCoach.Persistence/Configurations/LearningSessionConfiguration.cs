using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LinguaCoach.Persistence.Configurations;

internal sealed class LearningSessionConfiguration : IEntityTypeConfiguration<LearningSession>
{
    public void Configure(EntityTypeBuilder<LearningSession> builder)
    {
        builder.ToTable("learning_sessions");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired()
            .HasDefaultValueSql("now()");

        builder.Property(e => e.LearningModuleId).HasColumnName("learning_module_id").IsRequired();
        builder.Property(e => e.Title).HasColumnName("title").HasMaxLength(500).IsRequired();
        builder.Property(e => e.Topic).HasColumnName("topic").HasMaxLength(500).IsRequired();
        builder.Property(e => e.SessionGoal).HasColumnName("session_goal").IsRequired();
        builder.Property(e => e.DurationMinutes).HasColumnName("duration_minutes").IsRequired();
        builder.Property(e => e.FocusSkill).HasColumnName("focus_skill").HasMaxLength(100).IsRequired();
        builder.Property(e => e.SecondarySkillsJson).HasColumnName("secondary_skills_json").IsRequired(false);
        builder.Property(e => e.Order).HasColumnName("order").IsRequired();
        builder.Property(e => e.Status).HasColumnName("status")
            .HasConversion<int>().IsRequired();
        builder.Property(e => e.StartedAtUtc).HasColumnName("started_at_utc").IsRequired(false);
        builder.Property(e => e.CompletedAtUtc).HasColumnName("completed_at_utc").IsRequired(false);
        builder.Property(e => e.GeneratedFromMemorySnapshotJson)
            .HasColumnName("generated_from_memory_snapshot_json").IsRequired(false);

        builder.HasIndex(e => new { e.LearningModuleId, e.Order })
            .HasDatabaseName("ix_learning_sessions_module_order");

        builder.HasIndex(e => e.Status)
            .HasDatabaseName("ix_learning_sessions_status");

        builder.HasOne<LearningModule>()
            .WithMany()
            .HasForeignKey(e => e.LearningModuleId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(e => e.Exercises)
            .WithOne()
            .HasForeignKey(e => e.LearningSessionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

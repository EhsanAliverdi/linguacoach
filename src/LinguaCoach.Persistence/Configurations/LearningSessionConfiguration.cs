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

        // Background generation / lesson buffer (T10)
        builder.Property(e => e.StudentProfileId).HasColumnName("student_profile_id").IsRequired(false);
        builder.Property(e => e.CourseSequenceNumber).HasColumnName("course_sequence_number").IsRequired(false);
        // Bugfix-D1A: no HasDefaultValue here (deliberately). GenerationStatus.Pending == 0, the
        // enum's CLR default — configuring a DB default here makes EF treat any CLR value equal
        // to the type's default as "unset" and skip sending it in the INSERT, letting the DB
        // default apply instead. That silently discarded MarkGenerationPending() calls made
        // before a new session's first SaveChanges (see LessonBatchGenerationJob), always
        // persisting Ready regardless. New LearningSession instances already default to Ready via
        // the property initializer in code, so no DB-side default is needed for correctness.
        builder.Property(e => e.GenerationStatus).HasColumnName("generation_status")
            .HasConversion<int>().IsRequired();
        builder.Property(e => e.ReadyAtUtc).HasColumnName("ready_at_utc").IsRequired(false);
        builder.Property(e => e.GenerationBatchId).HasColumnName("generation_batch_id").IsRequired(false);
        builder.Property(e => e.DeletedAtUtc).HasColumnName("deleted_at_utc").IsRequired(false);

        builder.HasQueryFilter(e => e.DeletedAtUtc == null);

        builder.HasIndex(e => new { e.LearningModuleId, e.Order })
            .HasDatabaseName("ix_learning_sessions_module_order");

        builder.HasIndex(e => e.Status)
            .HasDatabaseName("ix_learning_sessions_status");

        // Idempotency: one session per student per course sequence number (T10).
        // Filtered to background-generated rows so legacy rows (null student) are unaffected.
        builder.HasIndex(e => new { e.StudentProfileId, e.CourseSequenceNumber })
            .HasDatabaseName("ux_learning_sessions_student_sequence")
            .IsUnique()
            .HasFilter("student_profile_id IS NOT NULL AND course_sequence_number IS NOT NULL");

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

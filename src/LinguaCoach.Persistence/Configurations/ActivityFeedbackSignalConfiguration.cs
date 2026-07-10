using LinguaCoach.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LinguaCoach.Persistence.Configurations;

internal sealed class ActivityFeedbackSignalConfiguration
    : IEntityTypeConfiguration<ActivityFeedbackSignal>
{
    public void Configure(EntityTypeBuilder<ActivityFeedbackSignal> builder)
    {
        builder.ToTable("activity_feedback_signals");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired()
            .HasDefaultValueSql("now()");
        builder.Property(e => e.UpdatedAt).HasColumnName("updated_at").IsRequired();

        builder.Property(e => e.StudentProfileId).HasColumnName("student_profile_id").IsRequired();
        builder.Property(e => e.LearningActivityId).HasColumnName("learning_activity_id").IsRequired();
        builder.Property(e => e.ActivityAttemptId).HasColumnName("activity_attempt_id");
        builder.Property(e => e.StudentActivityUsageLogId).HasColumnName("student_activity_usage_log_id");
        builder.Property(e => e.SourceTemplateId).HasColumnName("source_template_id");
        builder.Property(e => e.SourceBankItemId).HasColumnName("source_bank_item_id");

        builder.Property(e => e.PatternKey).HasColumnName("pattern_key").HasMaxLength(200);
        builder.Property(e => e.Skill).HasColumnName("skill").HasMaxLength(100);
        builder.Property(e => e.Subskill).HasColumnName("subskill").HasMaxLength(128);
        builder.Property(e => e.CefrLevel).HasColumnName("cefr_level").HasMaxLength(10);
        builder.Property(e => e.CurriculumObjectiveKey).HasColumnName("curriculum_objective_key").HasMaxLength(200);

        builder.Property(e => e.DifficultyRating).HasColumnName("difficulty_rating")
            .HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(e => e.ClarityRating).HasColumnName("clarity_rating")
            .HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(e => e.UsefulnessRating).HasColumnName("usefulness_rating")
            .HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(e => e.RepeatPreference).HasColumnName("repeat_preference")
            .HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(e => e.OptionalComment).HasColumnName("optional_comment")
            .HasMaxLength(Domain.Entities.ActivityFeedbackSignal.MaxOptionalCommentLength);

        builder.HasOne<StudentProfile>()
            .WithMany()
            .HasForeignKey(e => e.StudentProfileId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<LearningActivity>()
            .WithMany()
            .HasForeignKey(e => e.LearningActivityId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<ActivityAttempt>()
            .WithMany()
            .HasForeignKey(e => e.ActivityAttemptId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<StudentActivityUsageLog>()
            .WithMany()
            .HasForeignKey(e => e.StudentActivityUsageLogId)
            .OnDelete(DeleteBehavior.Restrict);

        // Note: StudentActivityReadinessItemId column/FK removed in Phase I2C along with the
        // readiness pool — see docs/reviews/2026-07-10-phase-i2c-readiness-pool-removal-review.md.

        // Note: SourceTemplateId no longer has a FK relation — the ActivityTemplate entity/table
        // was removed in Phase I2A (legacy fallback deletion). The column remains as plain data
        // for historical rows; no new writes populate it.

        builder.HasOne<PlacementItemDefinition>()
            .WithMany()
            .HasForeignKey(e => e.SourceBankItemId)
            .OnDelete(DeleteBehavior.Restrict);

        // Idempotency: one feedback signal per (student, attempt) when the attempt is known —
        // resubmitting for the same attempt updates the row instead of duplicating it.
        builder.HasIndex(e => new { e.StudentProfileId, e.ActivityAttemptId })
            .IsUnique()
            .HasFilter("activity_attempt_id IS NOT NULL")
            .HasDatabaseName("ux_feedback_signals_student_attempt");

        // Fallback idempotency key when no attempt is linked: one per (student, activity).
        // Note: EF Core treats a second HasIndex() call on the exact same property set as
        // reconfiguring the same index (not a second index), so this must be the only index
        // over (StudentProfileId, LearningActivityId) — it doubles as the general lookup index.
        builder.HasIndex(e => new { e.StudentProfileId, e.LearningActivityId })
            .IsUnique()
            .HasFilter("activity_attempt_id IS NULL")
            .HasDatabaseName("ux_feedback_signals_student_activity_no_attempt");

        builder.HasIndex(e => new { e.StudentProfileId, e.PatternKey })
            .HasDatabaseName("ix_feedback_signals_student_pattern");
    }
}

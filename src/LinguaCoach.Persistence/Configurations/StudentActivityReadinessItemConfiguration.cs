using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LinguaCoach.Persistence.Configurations;

internal sealed class StudentActivityReadinessItemConfiguration
    : IEntityTypeConfiguration<StudentActivityReadinessItem>
{
    public void Configure(EntityTypeBuilder<StudentActivityReadinessItem> builder)
    {
        builder.ToTable("student_activity_readiness_items");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired()
            .HasDefaultValueSql("now()");
        builder.Property(e => e.UpdatedAt).HasColumnName("updated_at").IsRequired();

        builder.Property(e => e.StudentId).HasColumnName("student_id").IsRequired();
        builder.Property(e => e.Source).HasColumnName("source").IsRequired()
            .HasConversion<string>().HasMaxLength(50);
        builder.Property(e => e.Status).HasColumnName("status").IsRequired()
            .HasConversion<string>().HasMaxLength(50);
        builder.Property(e => e.Priority).HasColumnName("priority").IsRequired().HasDefaultValue(0);

        // Routing snapshot
        builder.Property(e => e.TargetCefrLevel).HasColumnName("target_cefr_level").HasMaxLength(10).IsRequired();
        builder.Property(e => e.OriginalCefrLevelSnapshot).HasColumnName("original_cefr_level_snapshot").HasMaxLength(10);
        builder.Property(e => e.IsLowerLevelContent).HasColumnName("is_lower_level_content").IsRequired();
        builder.Property(e => e.RoutingReason).HasColumnName("routing_reason").IsRequired()
            .HasConversion<string>().HasMaxLength(50);
        builder.Property(e => e.RoutingExplanation).HasColumnName("routing_explanation");
        builder.Property(e => e.CurriculumObjectiveKey).HasColumnName("curriculum_objective_key").HasMaxLength(200);
        builder.Property(e => e.CurriculumObjectiveTitle).HasColumnName("curriculum_objective_title").HasMaxLength(300);
        builder.Property(e => e.PrimarySkill).HasColumnName("primary_skill").HasMaxLength(100);
        builder.Property(e => e.SecondarySkillsJson).HasColumnName("secondary_skills_json").IsRequired()
            .HasDefaultValue("[]");
        builder.Property(e => e.ContextTagsJson).HasColumnName("context_tags_json").IsRequired()
            .HasDefaultValue("[]");
        builder.Property(e => e.FocusTagsJson).HasColumnName("focus_tags_json").IsRequired()
            .HasDefaultValue("[]");
        builder.Property(e => e.PatternKey).HasColumnName("pattern_key").HasMaxLength(200);
        builder.Property(e => e.ActivityType).HasColumnName("activity_type").HasMaxLength(100);
        builder.Property(e => e.DifficultyBand).HasColumnName("difficulty_band").IsRequired().HasDefaultValue(1);
        builder.Property(e => e.RequiresAdminReview).HasColumnName("requires_admin_review").IsRequired()
            .HasDefaultValue(false);

        // Admin approval (Phase 19B, per-item)
        builder.Property(e => e.AdminReviewStatus).HasColumnName("admin_review_status").IsRequired()
            .HasConversion<string>().HasMaxLength(50).HasDefaultValue(AdminReviewStatus.NotRequired);
        builder.Property(e => e.AdminReviewedAtUtc).HasColumnName("admin_reviewed_at_utc");
        builder.Property(e => e.AdminReviewedByUserId).HasColumnName("admin_reviewed_by_user_id");
        builder.Property(e => e.AdminReviewReason).HasColumnName("admin_review_reason").HasMaxLength(500);
        builder.Property(e => e.AdminReviewNotes).HasColumnName("admin_review_notes").HasMaxLength(2000);

        // Preference snapshot
        builder.Property(e => e.PreferredSessionDurationMinutes).HasColumnName("preferred_session_duration_minutes");
        builder.Property(e => e.DifficultyPreference).HasColumnName("difficulty_preference").HasMaxLength(50);
        builder.Property(e => e.SupportLanguageCode).HasColumnName("support_language_code").HasMaxLength(20);
        builder.Property(e => e.SupportLanguageName).HasColumnName("support_language_name").HasMaxLength(100);
        builder.Property(e => e.TranslationHelpPreference).HasColumnName("translation_help_preference").HasMaxLength(50);

        // Linked entity IDs
        builder.Property(e => e.LearningSessionId).HasColumnName("learning_session_id");
        builder.Property(e => e.LearningActivityId).HasColumnName("learning_activity_id");
        builder.Property(e => e.SessionExerciseId).HasColumnName("session_exercise_id");

        // Generation provenance
        builder.Property(e => e.GeneratedBy).HasColumnName("generated_by").HasMaxLength(100);

        // Error info
        builder.Property(e => e.ErrorCode).HasColumnName("error_code").HasMaxLength(100);
        builder.Property(e => e.ErrorMessage).HasColumnName("error_message");
        builder.Property(e => e.AttemptCount).HasColumnName("attempt_count").IsRequired().HasDefaultValue(0);

        // Lifecycle timestamps
        builder.Property(e => e.ReservedAt).HasColumnName("reserved_at");
        builder.Property(e => e.ConsumedAt).HasColumnName("consumed_at");
        builder.Property(e => e.ExpiresAt).HasColumnName("expires_at");
        builder.Property(e => e.StaleAt).HasColumnName("stale_at");
        builder.Property(e => e.LastEvaluatedAtUtc).HasColumnName("last_evaluated_at_utc");

        // Indexes for common query patterns
        builder.HasIndex(e => new { e.StudentId, e.Status, e.Source })
            .HasDatabaseName("ix_readiness_items_student_status_source");

        builder.HasIndex(e => new { e.StudentId, e.Status, e.Priority, e.CreatedAt })
            .HasDatabaseName("ix_readiness_items_student_status_priority");

        builder.HasIndex(e => e.LearningActivityId)
            .HasDatabaseName("ix_readiness_items_activity_id");

        builder.HasIndex(e => e.LearningSessionId)
            .HasDatabaseName("ix_readiness_items_session_id");

        builder.HasIndex(e => new { e.RequiresAdminReview, e.AdminReviewStatus })
            .HasDatabaseName("ix_readiness_items_admin_review_status");

    }
}

using LinguaCoach.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LinguaCoach.Persistence.Configurations;

internal sealed class StudentActivityUsageLogConfiguration
    : IEntityTypeConfiguration<StudentActivityUsageLog>
{
    public void Configure(EntityTypeBuilder<StudentActivityUsageLog> builder)
    {
        builder.ToTable("student_activity_usage_logs");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired()
            .HasDefaultValueSql("now()");

        builder.Property(e => e.StudentProfileId).HasColumnName("student_profile_id").IsRequired();

        builder.Property(e => e.LearningActivityId).HasColumnName("learning_activity_id");
        builder.Property(e => e.StudentActivityReadinessItemId).HasColumnName("student_activity_readiness_item_id");
        builder.Property(e => e.SourceTemplateId).HasColumnName("source_template_id");
        builder.Property(e => e.SourceBankItemId).HasColumnName("source_bank_item_id");

        builder.Property(e => e.PatternKey).HasColumnName("pattern_key").HasMaxLength(200);
        builder.Property(e => e.ActivityType).HasColumnName("activity_type").HasMaxLength(100);
        builder.Property(e => e.Skill).HasColumnName("skill").HasMaxLength(100);
        builder.Property(e => e.Subskill).HasColumnName("subskill").HasMaxLength(128);
        builder.Property(e => e.CefrLevel).HasColumnName("cefr_level").HasMaxLength(10);
        builder.Property(e => e.CurriculumObjectiveKey).HasColumnName("curriculum_objective_key").HasMaxLength(200);

        builder.Property(e => e.ContentFingerprint).HasColumnName("content_fingerprint")
            .HasMaxLength(128).IsRequired();
        builder.Property(e => e.TopicKey).HasColumnName("topic_key").HasMaxLength(300);
        builder.Property(e => e.ScenarioKey).HasColumnName("scenario_key").HasMaxLength(300);
        builder.Property(e => e.PassageKey).HasColumnName("passage_key").HasMaxLength(300);
        builder.Property(e => e.PromptKey).HasColumnName("prompt_key").HasMaxLength(200);
        builder.Property(e => e.ContextTagsJson).HasColumnName("context_tags_json").HasColumnType("jsonb");
        builder.Property(e => e.FocusTagsJson).HasColumnName("focus_tags_json").HasColumnType("jsonb");

        builder.Property(e => e.IsIntentionalReview).HasColumnName("is_intentional_review")
            .IsRequired().HasDefaultValue(false);
        builder.Property(e => e.ReviewReason).HasColumnName("review_reason").HasMaxLength(500);

        builder.Property(e => e.ConsumedAtUtc).HasColumnName("consumed_at_utc").IsRequired();

        builder.HasOne<StudentProfile>()
            .WithMany()
            .HasForeignKey(e => e.StudentProfileId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<LearningActivity>()
            .WithMany()
            .HasForeignKey(e => e.LearningActivityId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<StudentActivityReadinessItem>()
            .WithMany()
            .HasForeignKey(e => e.StudentActivityReadinessItemId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<ActivityTemplate>()
            .WithMany()
            .HasForeignKey(e => e.SourceTemplateId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<PlacementItemDefinition>()
            .WithMany()
            .HasForeignKey(e => e.SourceBankItemId)
            .OnDelete(DeleteBehavior.Restrict);

        // Idempotency: at most one usage log per (student, activity) — a retried/duplicate
        // completion for the same LearningActivity must not create a second row. Partial index
        // (nullable column) so activities not yet linked to a LearningActivityId don't collide.
        builder.HasIndex(e => new { e.StudentProfileId, e.LearningActivityId })
            .IsUnique()
            .HasFilter("learning_activity_id IS NOT NULL")
            .HasDatabaseName("ux_usage_logs_student_activity");

        // Query patterns required by the novelty policy and future reporting.
        builder.HasIndex(e => new { e.StudentProfileId, e.ConsumedAtUtc })
            .HasDatabaseName("ix_usage_logs_student_consumed_at");

        builder.HasIndex(e => new { e.StudentProfileId, e.ContentFingerprint })
            .HasDatabaseName("ix_usage_logs_student_fingerprint");

        builder.HasIndex(e => new { e.StudentProfileId, e.SourceTemplateId })
            .HasDatabaseName("ix_usage_logs_student_template");

        builder.HasIndex(e => new { e.StudentProfileId, e.PatternKey })
            .HasDatabaseName("ix_usage_logs_student_pattern");

        builder.HasIndex(e => new { e.StudentProfileId, e.TopicKey })
            .HasDatabaseName("ix_usage_logs_student_topic");

        builder.HasIndex(e => new { e.StudentProfileId, e.ScenarioKey })
            .HasDatabaseName("ix_usage_logs_student_scenario");
    }
}

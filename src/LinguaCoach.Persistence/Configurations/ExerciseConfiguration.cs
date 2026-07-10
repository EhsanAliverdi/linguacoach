using LinguaCoach.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LinguaCoach.Persistence.Configurations;

internal sealed class ExerciseConfiguration : IEntityTypeConfiguration<Exercise>
{
    public void Configure(EntityTypeBuilder<Exercise> builder)
    {
        builder.ToTable("exercises");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired()
            .HasDefaultValueSql("now()");
        builder.Property(e => e.UpdatedAtUtc).HasColumnName("updated_at_utc").IsRequired();

        builder.Property(e => e.Title).HasColumnName("title").HasMaxLength(300).IsRequired();
        builder.Property(e => e.Description).HasColumnName("description");
        builder.Property(e => e.Instructions).HasColumnName("instructions").IsRequired();

        builder.Property(e => e.ActivityType).HasColumnName("activity_type").HasMaxLength(64).IsRequired();
        builder.Property(e => e.PatternKey).HasColumnName("pattern_key").HasMaxLength(128);
        builder.Property(e => e.RendererType).HasColumnName("renderer_type").HasConversion<string>().HasMaxLength(32).IsRequired();

        builder.Property(e => e.FormSchemaJson).HasColumnName("form_schema_json");
        builder.Property(e => e.AnswerKeyJson).HasColumnName("answer_key_json");
        builder.Property(e => e.ScoringRulesJson).HasColumnName("scoring_rules_json");
        builder.Property(e => e.FeedbackPlanJson).HasColumnName("feedback_plan_json");

        builder.Property(e => e.CefrLevel).HasColumnName("cefr_level").HasMaxLength(10);
        builder.Property(e => e.Skill).HasColumnName("skill").HasMaxLength(100);
        builder.Property(e => e.Subskill).HasColumnName("subskill").HasMaxLength(128);
        builder.Property(e => e.ContextTagsJson).HasColumnName("context_tags_json").HasDefaultValue("[]").IsRequired();
        builder.Property(e => e.FocusTagsJson).HasColumnName("focus_tags_json").HasDefaultValue("[]").IsRequired();
        builder.Property(e => e.DifficultyBand).HasColumnName("difficulty_band");
        builder.Property(e => e.EstimatedMinutes).HasColumnName("estimated_minutes");

        builder.Property(e => e.LessonId).HasColumnName("lesson_id");
        builder.Property(e => e.SourceMode).HasColumnName("source_mode").HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(e => e.GenerationProvider).HasColumnName("generation_provider").HasMaxLength(100);
        builder.Property(e => e.GenerationModel).HasColumnName("generation_model").HasMaxLength(100);

        builder.Property(e => e.ReviewStatus).HasColumnName("review_status").HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(e => e.CreatedByUserId).HasColumnName("created_by_user_id");
        builder.Property(e => e.ReviewedByUserId).HasColumnName("reviewed_by_user_id");
        builder.Property(e => e.ApprovedAtUtc).HasColumnName("approved_at_utc");
        builder.Property(e => e.RejectedAtUtc).HasColumnName("rejected_at_utc");
        builder.Property(e => e.RejectionReason).HasColumnName("rejection_reason");
        builder.Property(e => e.ReviewNotes).HasColumnName("review_notes");

        // No FK constraint to lessons — Lesson has no delete path today, and keeping this
        // a soft reference (like ResourceCandidate.PublishedEntityId) avoids coupling Activity
        // foundation's migration to Lesson's table lifecycle.
        builder.HasIndex(e => e.LessonId).HasDatabaseName("ix_exercises_lesson");
        builder.HasIndex(e => e.ReviewStatus).HasDatabaseName("ix_exercises_review_status");
        builder.HasIndex(e => e.CefrLevel).HasDatabaseName("ix_exercises_cefr_level");
        builder.HasIndex(e => e.Skill).HasDatabaseName("ix_exercises_skill");
        builder.HasIndex(e => e.Subskill).HasDatabaseName("ix_exercises_subskill");
        builder.HasIndex(e => e.ActivityType).HasDatabaseName("ix_exercises_activity_type");
        builder.HasIndex(e => e.CreatedAt).HasDatabaseName("ix_exercises_created_at");
    }
}

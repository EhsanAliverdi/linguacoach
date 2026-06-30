using LinguaCoach.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LinguaCoach.Persistence.Configurations;

internal sealed class WritingEvaluationConfiguration : IEntityTypeConfiguration<WritingEvaluation>
{
    public void Configure(EntityTypeBuilder<WritingEvaluation> builder)
    {
        builder.ToTable("writing_evaluations");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired()
            .HasDefaultValueSql("now()");

        builder.Property(e => e.ActivityAttemptId).HasColumnName("activity_attempt_id").IsRequired();
        builder.Property(e => e.StudentProfileId).HasColumnName("student_profile_id").IsRequired();
        builder.Property(e => e.LearningActivityId).HasColumnName("learning_activity_id").IsRequired();
        builder.Property(e => e.Status).HasColumnName("status").IsRequired();
        builder.Property(e => e.ProviderName).HasColumnName("provider_name").HasMaxLength(100);
        builder.Property(e => e.ModelName).HasColumnName("model_name").HasMaxLength(100);
        builder.Property(e => e.StartedAtUtc).HasColumnName("started_at_utc");
        builder.Property(e => e.CompletedAtUtc).HasColumnName("completed_at_utc");
        builder.Property(e => e.FailedAtUtc).HasColumnName("failed_at_utc");
        builder.Property(e => e.FailureReason).HasColumnName("failure_reason").HasMaxLength(500);
        builder.Property(e => e.OverallScore).HasColumnName("overall_score");
        builder.Property(e => e.GrammarScore).HasColumnName("grammar_score");
        builder.Property(e => e.VocabularyScore).HasColumnName("vocabulary_score");
        builder.Property(e => e.CoherenceScore).HasColumnName("coherence_score");
        builder.Property(e => e.TaskCompletionScore).HasColumnName("task_completion_score");
        builder.Property(e => e.FeedbackText).HasColumnName("feedback_text").HasMaxLength(2000);
        builder.Property(e => e.SuggestedImprovement).HasColumnName("suggested_improvement").HasMaxLength(500);
        builder.Property(e => e.CorrectedText).HasColumnName("corrected_text").HasMaxLength(3000);
        builder.Property(e => e.RetryCount).HasColumnName("retry_count").HasDefaultValue(0);

        builder.HasIndex(e => e.ActivityAttemptId)
            .HasDatabaseName("ix_writing_evaluations_attempt");
        builder.HasIndex(e => e.StudentProfileId)
            .HasDatabaseName("ix_writing_evaluations_student");
        builder.HasIndex(e => e.LearningActivityId)
            .HasDatabaseName("ix_writing_evaluations_activity");
        builder.HasIndex(e => e.Status)
            .HasDatabaseName("ix_writing_evaluations_status");
    }
}

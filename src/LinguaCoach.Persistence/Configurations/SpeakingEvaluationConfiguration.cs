using LinguaCoach.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LinguaCoach.Persistence.Configurations;

internal sealed class SpeakingEvaluationConfiguration : IEntityTypeConfiguration<SpeakingEvaluation>
{
    public void Configure(EntityTypeBuilder<SpeakingEvaluation> builder)
    {
        builder.ToTable("speaking_evaluations");

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
        builder.Property(e => e.Transcript).HasColumnName("transcript").HasMaxLength(4000);
        builder.Property(e => e.OverallScore).HasColumnName("overall_score");
        builder.Property(e => e.FluencyScore).HasColumnName("fluency_score");
        builder.Property(e => e.PronunciationScore).HasColumnName("pronunciation_score");
        builder.Property(e => e.CompletenessScore).HasColumnName("completeness_score");
        builder.Property(e => e.RelevanceScore).HasColumnName("relevance_score");
        builder.Property(e => e.FeedbackText).HasColumnName("feedback_text").HasMaxLength(2000);
        builder.Property(e => e.SuggestedImprovement).HasColumnName("suggested_improvement").HasMaxLength(500);
        builder.Property(e => e.RetryCount).HasColumnName("retry_count").HasDefaultValue(0);

        builder.HasIndex(e => e.ActivityAttemptId)
            .HasDatabaseName("ix_speaking_evaluations_attempt");
        builder.HasIndex(e => e.StudentProfileId)
            .HasDatabaseName("ix_speaking_evaluations_student");
        builder.HasIndex(e => e.Status)
            .HasDatabaseName("ix_speaking_evaluations_status");
    }
}

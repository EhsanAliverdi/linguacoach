using LinguaCoach.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LinguaCoach.Persistence.Configurations;

internal sealed class SpeakingEvaluationAppliedSignalConfiguration
    : IEntityTypeConfiguration<SpeakingEvaluationAppliedSignal>
{
    public void Configure(EntityTypeBuilder<SpeakingEvaluationAppliedSignal> builder)
    {
        builder.ToTable("speaking_evaluation_applied_signals");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired()
            .HasDefaultValueSql("now()");

        builder.Property(e => e.EvaluationId).HasColumnName("evaluation_id").IsRequired();
        builder.Property(e => e.AttemptId).HasColumnName("attempt_id").IsRequired();
        builder.Property(e => e.StudentProfileId).HasColumnName("student_profile_id").IsRequired();
        builder.Property(e => e.ActivityId).HasColumnName("activity_id").IsRequired();
        builder.Property(e => e.SignalType).HasColumnName("signal_type").HasMaxLength(50).IsRequired();
        builder.Property(e => e.Confidence).HasColumnName("confidence").HasMaxLength(20).IsRequired();
        builder.Property(e => e.ScoreUsed).HasColumnName("score_used");
        builder.Property(e => e.SkillAffected).HasColumnName("skill_affected").HasMaxLength(100).IsRequired();
        builder.Property(e => e.AppliedRuleVersion).HasColumnName("applied_rule_version").HasMaxLength(20).IsRequired();
        builder.Property(e => e.DryRunOutcome).HasColumnName("dry_run_outcome").HasMaxLength(100).IsRequired();
        builder.Property(e => e.Reason).HasColumnName("reason").HasMaxLength(500).IsRequired();
        builder.Property(e => e.LearningEventId).HasColumnName("learning_event_id");
        builder.Property(e => e.AppliedAtUtc).HasColumnName("applied_at_utc").IsRequired();

        // One applied signal per evaluation — idempotency enforced at DB level.
        builder.HasIndex(e => e.EvaluationId)
            .IsUnique()
            .HasDatabaseName("ix_speaking_applied_signals_evaluation_unique");

        builder.HasIndex(e => e.StudentProfileId)
            .HasDatabaseName("ix_speaking_applied_signals_student");
    }
}

using LinguaCoach.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LinguaCoach.Persistence.Configurations;

internal sealed class AiUsageLogConfiguration : IEntityTypeConfiguration<AiUsageLog>
{
    public void Configure(EntityTypeBuilder<AiUsageLog> builder)
    {
        builder.ToTable("ai_usage_logs");

        builder.HasKey(l => l.Id);
        builder.Property(l => l.Id).HasColumnName("id");
        builder.Property(l => l.CreatedAt).HasColumnName("created_at").IsRequired()
            .HasDefaultValueSql("now()");

        // StudentProfileId is now nullable (path generation has no profile)
        builder.Property(l => l.StudentProfileId).HasColumnName("student_profile_id").IsRequired(false);

        builder.HasOne<StudentProfile>()
            .WithMany()
            .HasForeignKey(l => l.StudentProfileId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);

        builder.Property(l => l.FeatureKey)
            .HasColumnName("feature_key")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(l => l.ProviderName)
            .HasColumnName("provider_name")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(l => l.ModelName)
            .HasColumnName("model_name")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(l => l.IsFallback).HasColumnName("is_fallback").IsRequired();
        builder.Property(l => l.WasSuccessful).HasColumnName("was_successful").IsRequired();
        builder.Property(l => l.FailureReason).HasColumnName("failure_reason").HasMaxLength(200).IsRequired(false);

        builder.Property(l => l.InputTokens).HasColumnName("input_tokens").IsRequired();
        builder.Property(l => l.OutputTokens).HasColumnName("output_tokens").IsRequired();

        builder.Property(l => l.CostUsd)
            .HasColumnName("cost_usd")
            .HasColumnType("numeric(10,6)")
            .IsRequired();

        builder.Property(l => l.DurationMs).HasColumnName("duration_ms").IsRequired();
        builder.Property(l => l.CorrelationId).HasColumnName("correlation_id").HasMaxLength(50).IsRequired(false);

        builder.HasIndex(l => l.StudentProfileId)
            .HasDatabaseName("ix_ai_usage_logs_student_profile_id");
        builder.HasIndex(l => new { l.StudentProfileId, l.CreatedAt })
            .HasDatabaseName("ix_ai_usage_logs_student_profile_id_created_at");
        builder.HasIndex(l => l.FeatureKey)
            .HasDatabaseName("ix_ai_usage_logs_feature_key");
        builder.HasIndex(l => l.ProviderName)
            .HasDatabaseName("ix_ai_usage_logs_provider_name");
    }
}

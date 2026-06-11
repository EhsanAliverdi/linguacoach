using LinguaCoach.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LinguaCoach.Persistence.Configurations;

internal sealed class GenerationBatchConfiguration : IEntityTypeConfiguration<GenerationBatch>
{
    public void Configure(EntityTypeBuilder<GenerationBatch> builder)
    {
        builder.ToTable("generation_batches");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired()
            .HasDefaultValueSql("now()");

        builder.Property(e => e.StudentProfileId).HasColumnName("student_profile_id").IsRequired();
        builder.Property(e => e.TriggerReason).HasColumnName("trigger_reason").HasConversion<int>().IsRequired();
        builder.Property(e => e.Status).HasColumnName("status").HasConversion<int>().IsRequired();
        builder.Property(e => e.RequestedSessionCount).HasColumnName("requested_session_count").IsRequired();
        builder.Property(e => e.CompletedSessionCount).HasColumnName("completed_session_count").IsRequired();
        builder.Property(e => e.SummarySnapshotJson).HasColumnName("summary_snapshot_json").IsRequired(false);
        builder.Property(e => e.PromptVersion).HasColumnName("prompt_version").HasMaxLength(100).IsRequired(false);
        builder.Property(e => e.ProviderName).HasColumnName("provider_name").HasMaxLength(100).IsRequired(false);
        builder.Property(e => e.ModelName).HasColumnName("model_name").HasMaxLength(200).IsRequired(false);
        builder.Property(e => e.CorrelationId).HasColumnName("correlation_id").HasMaxLength(100).IsRequired(false);
        builder.Property(e => e.StartedAtUtc).HasColumnName("started_at_utc").IsRequired(false);
        builder.Property(e => e.CompletedAtUtc).HasColumnName("completed_at_utc").IsRequired(false);
        builder.Property(e => e.FailureReason).HasColumnName("failure_reason").IsRequired(false);

        builder.HasIndex(e => e.StudentProfileId).HasDatabaseName("ix_generation_batches_student");
        builder.HasIndex(e => e.Status).HasDatabaseName("ix_generation_batches_status");

        builder.HasMany(e => e.Items)
            .WithOne()
            .HasForeignKey(i => i.GenerationBatchId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class GenerationJobItemConfiguration : IEntityTypeConfiguration<GenerationJobItem>
{
    public void Configure(EntityTypeBuilder<GenerationJobItem> builder)
    {
        builder.ToTable("generation_job_items");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired()
            .HasDefaultValueSql("now()");

        builder.Property(e => e.GenerationBatchId).HasColumnName("generation_batch_id").IsRequired();
        builder.Property(e => e.ItemType).HasColumnName("item_type").HasConversion<int>().IsRequired();
        builder.Property(e => e.TargetEntityId).HasColumnName("target_entity_id").IsRequired(false);
        builder.Property(e => e.Status).HasColumnName("status").HasConversion<int>().IsRequired();
        builder.Property(e => e.AttemptCount).HasColumnName("attempt_count").IsRequired();
        builder.Property(e => e.NextRetryAtUtc).HasColumnName("next_retry_at_utc").IsRequired(false);
        builder.Property(e => e.LastError).HasColumnName("last_error").IsRequired(false);
        builder.Property(e => e.StartedAtUtc).HasColumnName("started_at_utc").IsRequired(false);
        builder.Property(e => e.CompletedAtUtc).HasColumnName("completed_at_utc").IsRequired(false);

        builder.HasIndex(e => e.GenerationBatchId).HasDatabaseName("ix_generation_job_items_batch");
        builder.HasIndex(e => e.Status).HasDatabaseName("ix_generation_job_items_status");
    }
}

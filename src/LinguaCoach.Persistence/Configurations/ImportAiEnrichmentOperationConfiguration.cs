using LinguaCoach.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LinguaCoach.Persistence.Configurations;

internal sealed class ImportAiEnrichmentOperationConfiguration : IEntityTypeConfiguration<ImportAiEnrichmentOperation>
{
    public void Configure(EntityTypeBuilder<ImportAiEnrichmentOperation> builder)
    {
        builder.ToTable("import_ai_enrichment_operations");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired().HasDefaultValueSql("now()");

        builder.Property(e => e.ImportPackageId).HasColumnName("import_package_id").IsRequired();
        builder.Property(e => e.ImportProfileId).HasColumnName("import_profile_id").IsRequired();
        builder.Property(e => e.ResourceCandidateId).HasColumnName("resource_candidate_id").IsRequired();
        builder.Property(e => e.LogicalOperationKey).HasColumnName("logical_operation_key").HasMaxLength(500).IsRequired();

        builder.Property(e => e.OperationType).HasColumnName("operation_type").HasMaxLength(50).IsRequired();
        builder.Property(e => e.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(e => e.AttemptNumber).HasColumnName("attempt_number").IsRequired().HasDefaultValue(1);

        builder.Property(e => e.ProviderName).HasColumnName("provider_name").HasMaxLength(100).IsRequired();
        builder.Property(e => e.ModelName).HasColumnName("model_name").HasMaxLength(100);
        builder.Property(e => e.PromptVersion).HasColumnName("prompt_version").HasMaxLength(100).IsRequired();
        builder.Property(e => e.ProcessingMode).HasColumnName("processing_mode").HasMaxLength(50).IsRequired();

        builder.Property(e => e.ResultReferenceJson).HasColumnName("result_reference_json");

        builder.Property(e => e.InputTokens).HasColumnName("input_tokens");
        builder.Property(e => e.OutputTokens).HasColumnName("output_tokens");
        builder.Property(e => e.InputPricePer1KTokensSnapshot).HasColumnName("input_price_per_1k_tokens_snapshot").HasPrecision(12, 6);
        builder.Property(e => e.OutputPricePer1KTokensSnapshot).HasColumnName("output_price_per_1k_tokens_snapshot").HasPrecision(12, 6);
        builder.Property(e => e.CalculatedCost).HasColumnName("calculated_cost").HasPrecision(12, 4);
        builder.Property(e => e.Currency).HasColumnName("currency").HasMaxLength(8).IsRequired().HasDefaultValue("USD");

        builder.Property(e => e.FailureReason).HasColumnName("failure_reason").HasMaxLength(2000);

        builder.Property(e => e.StartedAtUtc).HasColumnName("started_at_utc").IsRequired();
        builder.Property(e => e.CompletedAtUtc).HasColumnName("completed_at_utc");

        builder.HasOne<ImportPackage>()
            .WithMany()
            .HasForeignKey(e => e.ImportPackageId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<ResourceCandidate>()
            .WithMany()
            .HasForeignKey(e => e.ResourceCandidateId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(e => e.ImportPackageId).HasDatabaseName("ix_import_ai_enrichment_operations_package");

        // Phase 4.4D — the actual retry-safety/dedup guarantee, mirroring
        // ux_import_stt_operations_logical_key: exactly one ledger row may ever exist for a given
        // logical operation. Same single-active-worker boundary documented on the STT ledger
        // applies here too (Quartz clustering remains deferred).
        builder.HasIndex(e => e.LogicalOperationKey).IsUnique().HasDatabaseName("ux_import_ai_enrichment_operations_logical_key");
    }
}

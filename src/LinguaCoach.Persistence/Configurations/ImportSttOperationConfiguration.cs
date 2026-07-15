using LinguaCoach.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LinguaCoach.Persistence.Configurations;

internal sealed class ImportSttOperationConfiguration : IEntityTypeConfiguration<ImportSttOperation>
{
    public void Configure(EntityTypeBuilder<ImportSttOperation> builder)
    {
        builder.ToTable("import_stt_operations");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired().HasDefaultValueSql("now()");

        builder.Property(e => e.ImportPackageId).HasColumnName("import_package_id").IsRequired();
        builder.Property(e => e.ImportProfileId).HasColumnName("import_profile_id").IsRequired();
        builder.Property(e => e.ImportAssetId).HasColumnName("import_asset_id").IsRequired();
        builder.Property(e => e.LogicalOperationKey).HasColumnName("logical_operation_key").HasMaxLength(400).IsRequired();

        builder.Property(e => e.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(e => e.AttemptNumber).HasColumnName("attempt_number").IsRequired().HasDefaultValue(1);

        builder.Property(e => e.ProviderName).HasColumnName("provider_name").HasMaxLength(100).IsRequired();
        builder.Property(e => e.ModelName).HasColumnName("model_name").HasMaxLength(100);
        builder.Property(e => e.TranscriptText).HasColumnName("transcript_text");

        builder.Property(e => e.AssumedMinutes).HasColumnName("assumed_minutes").HasPrecision(10, 4).IsRequired();
        builder.Property(e => e.PricePerMinuteSnapshot).HasColumnName("price_per_minute_snapshot").HasPrecision(12, 6);
        builder.Property(e => e.CalculatedCost).HasColumnName("calculated_cost").HasPrecision(12, 4);
        builder.Property(e => e.Currency).HasColumnName("currency").HasMaxLength(8).IsRequired().HasDefaultValue("USD");

        builder.Property(e => e.FailureReason).HasColumnName("failure_reason").HasMaxLength(2000);

        builder.Property(e => e.StartedAtUtc).HasColumnName("started_at_utc").IsRequired();
        builder.Property(e => e.CompletedAtUtc).HasColumnName("completed_at_utc");

        builder.HasOne<ImportPackage>()
            .WithMany()
            .HasForeignKey(e => e.ImportPackageId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<ImportAsset>()
            .WithMany()
            .HasForeignKey(e => e.ImportAssetId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(e => e.ImportPackageId).HasDatabaseName("ix_import_stt_operations_package");

        // Phase 4.4 (B11) — the actual retry-safety/dedup guarantee: exactly one ledger row may
        // ever exist for a given logical operation, mutated in place across attempts (see
        // ImportSttOperation.BeginRetry) rather than accumulating a new row per attempt. This is
        // the "unique database constraint" the spec calls for — it prevents two concurrent workers
        // from both inserting a fresh Pending row for the same (package, asset, checksum) and
        // therefore both calling the provider. It does NOT by itself guarantee strict
        // linearizability across every possible interleaving (see the Phase 4.4 review doc's
        // "operation claiming" section for the documented remaining boundary — this codebase
        // still assumes a single active package-processing worker at a time, consistent with the
        // existing, still-deferred Quartz-clustering limitation).
        builder.HasIndex(e => e.LogicalOperationKey).IsUnique().HasDatabaseName("ux_import_stt_operations_logical_key");
    }
}

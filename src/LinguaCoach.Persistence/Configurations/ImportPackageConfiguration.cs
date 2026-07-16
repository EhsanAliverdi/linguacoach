using LinguaCoach.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LinguaCoach.Persistence.Configurations;

internal sealed class ImportPackageConfiguration : IEntityTypeConfiguration<ImportPackage>
{
    public void Configure(EntityTypeBuilder<ImportPackage> builder)
    {
        builder.ToTable("import_packages");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired().HasDefaultValueSql("now()");

        builder.Property(e => e.CefrResourceSourceId).HasColumnName("cefr_resource_source_id").IsRequired();
        builder.Property(e => e.CreatedByUserId).HasColumnName("created_by_user_id");

        builder.Property(e => e.OriginalArchiveFileName).HasColumnName("original_archive_file_name").HasMaxLength(500).IsRequired();
        builder.Property(e => e.ArchiveStorageKey).HasColumnName("archive_storage_key").HasMaxLength(500);
        builder.Property(e => e.ArchiveChecksum).HasColumnName("archive_checksum").HasMaxLength(128);
        builder.Property(e => e.CompressedSizeBytes).HasColumnName("compressed_size_bytes");

        builder.Property(e => e.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(e => e.ProcessingMode).HasColumnName("processing_mode").HasConversion<string>().HasMaxLength(32);
        builder.Property(e => e.ProcessingModeReason).HasColumnName("processing_mode_reason");

        builder.Property(e => e.ManifestJson).HasColumnName("manifest_json");
        builder.Property(e => e.ApprovedImportProfileId).HasColumnName("approved_import_profile_id");

        builder.Property(e => e.StartedAtUtc).HasColumnName("started_at_utc").IsRequired();
        builder.Property(e => e.CompletedAtUtc).HasColumnName("completed_at_utc");
        builder.Property(e => e.ErrorSummary).HasColumnName("error_summary");
        builder.Property(e => e.Notes).HasColumnName("notes");

        builder.Property(e => e.FilesInspectedCount).HasColumnName("files_inspected_count").IsRequired().HasDefaultValue(0);
        builder.Property(e => e.FilesProcessedCount).HasColumnName("files_processed_count").IsRequired().HasDefaultValue(0);
        builder.Property(e => e.RecordsProcessedCount).HasColumnName("records_processed_count").IsRequired().HasDefaultValue(0);
        builder.Property(e => e.CandidatesCreatedCount).HasColumnName("candidates_created_count").IsRequired().HasDefaultValue(0);
        builder.Property(e => e.CandidatesFailedCount).HasColumnName("candidates_failed_count").IsRequired().HasDefaultValue(0);
        builder.Property(e => e.LastCompletedStageIndex).HasColumnName("last_completed_stage_index").IsRequired().HasDefaultValue(-1);

        // Phase 4.4 — durable running cost total, neutral default (0) for existing rows.
        builder.Property(e => e.AccruedCost).HasColumnName("accrued_cost").HasPrecision(12, 4).IsRequired().HasDefaultValue(0m);
        builder.Property(e => e.AccruedCostCurrency).HasColumnName("accrued_cost_currency").HasMaxLength(8).IsRequired().HasDefaultValue("USD");

        // Phase 4.8 — durable claim/lease. ConcurrencyStamp is a real EF concurrency token (works
        // portably across SQLite and Postgres, unlike a Postgres-only xmin column) — every UPDATE
        // EF issues for this entity includes it in the WHERE clause, so a concurrent claim attempt
        // against a stale value affects zero rows and throws DbUpdateConcurrencyException instead
        // of silently overwriting the winner's claim.
        builder.Property(e => e.ClaimedByWorkerId).HasColumnName("claimed_by_worker_id").HasMaxLength(200);
        builder.Property(e => e.ClaimedAtUtc).HasColumnName("claimed_at_utc");
        builder.Property(e => e.ClaimExpiresAtUtc).HasColumnName("claim_expires_at_utc");
        builder.Property(e => e.ConcurrencyStamp).HasColumnName("concurrency_stamp").IsRequired().IsConcurrencyToken();

        builder.HasOne<CefrResourceSource>()
            .WithMany()
            .HasForeignKey(e => e.CefrResourceSourceId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => e.CefrResourceSourceId).HasDatabaseName("ix_import_packages_source");
        builder.HasIndex(e => e.Status).HasDatabaseName("ix_import_packages_status");
        builder.HasIndex(e => e.ClaimExpiresAtUtc).HasDatabaseName("ix_import_packages_claim_expires");
    }
}

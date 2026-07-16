using LinguaCoach.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LinguaCoach.Persistence.Configurations;

internal sealed class ImportUploadSessionConfiguration : IEntityTypeConfiguration<ImportUploadSession>
{
    public void Configure(EntityTypeBuilder<ImportUploadSession> builder)
    {
        builder.ToTable("import_upload_sessions");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired().HasDefaultValueSql("now()");

        builder.Property(e => e.CefrResourceSourceId).HasColumnName("cefr_resource_source_id").IsRequired();
        builder.Property(e => e.CreatedByUserId).HasColumnName("created_by_user_id");

        builder.Property(e => e.OriginalFileName).HasColumnName("original_file_name").HasMaxLength(500).IsRequired();
        builder.Property(e => e.DeclaredTotalSizeBytes).HasColumnName("declared_total_size_bytes").IsRequired();
        builder.Property(e => e.PartSizeBytes).HasColumnName("part_size_bytes").IsRequired();
        builder.Property(e => e.TotalPartsExpected).HasColumnName("total_parts_expected").IsRequired();
        builder.Property(e => e.DeclaredChecksumSha256).HasColumnName("declared_checksum_sha256").HasMaxLength(64);

        builder.Property(e => e.FinalStorageKey).HasColumnName("final_storage_key").HasMaxLength(500).IsRequired();
        builder.Property(e => e.Notes).HasColumnName("notes");

        builder.Property(e => e.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(e => e.ImportPackageId).HasColumnName("import_package_id");

        builder.Property(e => e.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();
        builder.Property(e => e.ExpiresAtUtc).HasColumnName("expires_at_utc").IsRequired();
        builder.Property(e => e.CompletedAtUtc).HasColumnName("completed_at_utc");
        builder.Property(e => e.AbortedAtUtc).HasColumnName("aborted_at_utc");

        builder.HasOne<CefrResourceSource>()
            .WithMany()
            .HasForeignKey(e => e.CefrResourceSourceId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => e.CefrResourceSourceId).HasDatabaseName("ix_import_upload_sessions_source");
        builder.HasIndex(e => e.Status).HasDatabaseName("ix_import_upload_sessions_status");
        builder.HasIndex(e => e.CreatedByUserId).HasDatabaseName("ix_import_upload_sessions_created_by");
    }
}

internal sealed class ImportUploadSessionPartConfiguration : IEntityTypeConfiguration<ImportUploadSessionPart>
{
    public void Configure(EntityTypeBuilder<ImportUploadSessionPart> builder)
    {
        builder.ToTable("import_upload_session_parts");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired().HasDefaultValueSql("now()");

        builder.Property(e => e.ImportUploadSessionId).HasColumnName("import_upload_session_id").IsRequired();
        builder.Property(e => e.PartNumber).HasColumnName("part_number").IsRequired();
        builder.Property(e => e.SizeBytes).HasColumnName("size_bytes").IsRequired();
        builder.Property(e => e.Sha256Checksum).HasColumnName("sha256_checksum").HasMaxLength(64);
        builder.Property(e => e.StorageKey).HasColumnName("storage_key").HasMaxLength(500).IsRequired();
        builder.Property(e => e.UploadedAtUtc).HasColumnName("uploaded_at_utc").IsRequired();

        builder.HasOne<ImportUploadSession>()
            .WithMany()
            .HasForeignKey(e => e.ImportUploadSessionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(e => new { e.ImportUploadSessionId, e.PartNumber })
            .IsUnique()
            .HasDatabaseName("ux_import_upload_session_parts_session_part");
    }
}

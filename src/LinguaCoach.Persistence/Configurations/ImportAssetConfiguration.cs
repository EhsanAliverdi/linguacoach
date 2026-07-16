using LinguaCoach.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LinguaCoach.Persistence.Configurations;

internal sealed class ImportAssetConfiguration : IEntityTypeConfiguration<ImportAsset>
{
    public void Configure(EntityTypeBuilder<ImportAsset> builder)
    {
        builder.ToTable("import_assets");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired().HasDefaultValueSql("now()");

        builder.Property(e => e.ImportPackageId).HasColumnName("import_package_id").IsRequired();
        builder.Property(e => e.OriginalFileName).HasColumnName("original_file_name").HasMaxLength(500).IsRequired();
        builder.Property(e => e.RelativePath).HasColumnName("relative_path").HasMaxLength(1000).IsRequired();
        builder.Property(e => e.StorageKey).HasColumnName("storage_key").HasMaxLength(500).IsRequired();

        builder.Property(e => e.MimeType).HasColumnName("mime_type").HasMaxLength(150).IsRequired();
        builder.Property(e => e.DetectedMediaType).HasColumnName("detected_media_type").HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(e => e.FileExtension).HasColumnName("file_extension").HasMaxLength(20).IsRequired();

        builder.Property(e => e.CompressedSizeBytes).HasColumnName("compressed_size_bytes");
        builder.Property(e => e.UncompressedSizeBytes).HasColumnName("uncompressed_size_bytes").IsRequired();
        builder.Property(e => e.Checksum).HasColumnName("checksum").HasMaxLength(128).IsRequired();

        builder.Property(e => e.Role).HasColumnName("role").HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(e => e.RoleOrigin).HasColumnName("role_origin").HasConversion<string>().HasMaxLength(32).IsRequired();

        builder.Property(e => e.ProcessingState).HasColumnName("processing_state").HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(e => e.ValidationErrorsJson).HasColumnName("validation_errors_json");
        builder.Property(e => e.ValidationWarningsJson).HasColumnName("validation_warnings_json");

        builder.Property(e => e.UploadedAtUtc).HasColumnName("uploaded_at_utc").IsRequired();
        builder.Property(e => e.ProcessingMetadataJson).HasColumnName("processing_metadata_json");

        // Phase 4.4E — real, persisted, reusable audio-duration measurement.
        builder.Property(e => e.AudioDurationSeconds).HasColumnName("audio_duration_seconds").HasPrecision(10, 3);
        builder.Property(e => e.AudioDurationMeasurementChecksum).HasColumnName("audio_duration_measurement_checksum").HasMaxLength(128);
        builder.Property(e => e.AudioDurationMeasurementStatus).HasColumnName("audio_duration_measurement_status")
            .HasConversion<string>().HasMaxLength(32).IsRequired().HasDefaultValue(LinguaCoach.Domain.Enums.ImportAudioDurationMeasurementStatus.NotMeasured);
        builder.Property(e => e.AudioDurationMeasuredAtUtc).HasColumnName("audio_duration_measured_at_utc");
        builder.Property(e => e.AudioDurationMeasurementError).HasColumnName("audio_duration_measurement_error").HasMaxLength(2000);

        builder.HasOne<ImportPackage>()
            .WithMany()
            .HasForeignKey(e => e.ImportPackageId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(e => e.ImportPackageId).HasDatabaseName("ix_import_assets_package");
        builder.HasIndex(e => e.Role).HasDatabaseName("ix_import_assets_role");
        builder.HasIndex(e => e.Checksum).HasDatabaseName("ix_import_assets_checksum");
    }
}

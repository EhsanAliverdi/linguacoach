using LinguaCoach.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LinguaCoach.Persistence.Configurations;

internal sealed class ResourceRawRecordConfiguration : IEntityTypeConfiguration<ResourceRawRecord>
{
    public void Configure(EntityTypeBuilder<ResourceRawRecord> builder)
    {
        builder.ToTable("resource_raw_records");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired()
            .HasDefaultValueSql("now()");

        builder.Property(e => e.ResourceImportRunId).HasColumnName("resource_import_run_id").IsRequired();
        builder.Property(e => e.ExternalRecordId).HasColumnName("external_record_id").HasMaxLength(300);
        builder.Property(e => e.RawJson).HasColumnName("raw_json");
        builder.Property(e => e.RawText).HasColumnName("raw_text");
        builder.Property(e => e.RawHash).HasColumnName("raw_hash").HasMaxLength(128).IsRequired();
        builder.Property(e => e.DetectedLanguageCode).HasColumnName("detected_language_code").HasMaxLength(10).IsRequired();
        builder.Property(e => e.DetectedFormat).HasColumnName("detected_format").HasMaxLength(16).IsRequired();
        builder.Property(e => e.ExtractionStatus).HasColumnName("extraction_status").HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(e => e.ExtractionWarningsJson).HasColumnName("extraction_warnings_json");

        builder.HasOne<ResourceImportRun>()
            .WithMany()
            .HasForeignKey(e => e.ResourceImportRunId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(e => e.ResourceImportRunId).HasDatabaseName("ix_resource_raw_records_run");
        builder.HasIndex(e => new { e.ResourceImportRunId, e.RawHash }).HasDatabaseName("ix_resource_raw_records_run_hash");
    }
}

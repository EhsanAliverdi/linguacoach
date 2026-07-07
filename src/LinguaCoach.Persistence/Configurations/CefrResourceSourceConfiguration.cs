using LinguaCoach.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LinguaCoach.Persistence.Configurations;

internal sealed class CefrResourceSourceConfiguration : IEntityTypeConfiguration<CefrResourceSource>
{
    public void Configure(EntityTypeBuilder<CefrResourceSource> builder)
    {
        builder.ToTable("cefr_resource_sources");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired()
            .HasDefaultValueSql("now()");

        builder.Property(e => e.Name).HasColumnName("name").HasMaxLength(300).IsRequired();
        builder.Property(e => e.LicenseType).HasColumnName("license_type").HasMaxLength(100).IsRequired();
        builder.Property(e => e.SourceUrl).HasColumnName("source_url").HasMaxLength(500);
        builder.Property(e => e.UsageRestrictionNotes).HasColumnName("usage_restriction_notes");
        builder.Property(e => e.IsImportApproved).HasColumnName("is_import_approved").IsRequired()
            .HasDefaultValue(false);
        builder.Property(e => e.ImportedAtUtc).HasColumnName("imported_at_utc");

        builder.HasIndex(e => e.Name)
            .IsUnique()
            .HasDatabaseName("ix_cefr_resource_sources_name");
    }
}

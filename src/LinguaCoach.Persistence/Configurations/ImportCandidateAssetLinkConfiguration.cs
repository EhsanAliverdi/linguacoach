using LinguaCoach.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LinguaCoach.Persistence.Configurations;

internal sealed class ImportCandidateAssetLinkConfiguration : IEntityTypeConfiguration<ImportCandidateAssetLink>
{
    public void Configure(EntityTypeBuilder<ImportCandidateAssetLink> builder)
    {
        builder.ToTable("import_candidate_asset_links");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired().HasDefaultValueSql("now()");

        builder.Property(e => e.ResourceCandidateId).HasColumnName("resource_candidate_id").IsRequired();
        builder.Property(e => e.ImportAssetId).HasColumnName("import_asset_id").IsRequired();
        builder.Property(e => e.Role).HasColumnName("role").HasConversion<string>().HasMaxLength(32).IsRequired();

        builder.HasOne<ResourceCandidate>()
            .WithMany()
            .HasForeignKey(e => e.ResourceCandidateId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<ImportAsset>()
            .WithMany()
            .HasForeignKey(e => e.ImportAssetId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(e => e.ResourceCandidateId).HasDatabaseName("ix_import_candidate_asset_links_candidate");
        builder.HasIndex(e => e.ImportAssetId).HasDatabaseName("ix_import_candidate_asset_links_asset");
        builder.HasIndex(e => new { e.ResourceCandidateId, e.ImportAssetId }).IsUnique()
            .HasDatabaseName("ux_import_candidate_asset_links_pair");
    }
}

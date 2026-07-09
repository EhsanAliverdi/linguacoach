using LinguaCoach.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LinguaCoach.Persistence.Configurations;

internal sealed class LearnItemResourceLinkConfiguration : IEntityTypeConfiguration<LearnItemResourceLink>
{
    public void Configure(EntityTypeBuilder<LearnItemResourceLink> builder)
    {
        builder.ToTable("learn_item_resource_links");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired()
            .HasDefaultValueSql("now()");

        builder.Property(e => e.LearnItemId).HasColumnName("learn_item_id").IsRequired();
        builder.Property(e => e.ResourceType).HasColumnName("resource_type").HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(e => e.ResourceId).HasColumnName("resource_id").IsRequired();
        builder.Property(e => e.Role).HasColumnName("role").HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(e => e.SnapshotTitle).HasColumnName("snapshot_title").HasMaxLength(500);
        builder.Property(e => e.ContentFingerprint).HasColumnName("content_fingerprint").HasMaxLength(128);

        builder.HasOne<LearnItem>()
            .WithMany()
            .HasForeignKey(e => e.LearnItemId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(e => e.LearnItemId).HasDatabaseName("ix_learn_item_resource_links_learn_item");
        builder.HasIndex(e => new { e.ResourceType, e.ResourceId }).HasDatabaseName("ix_learn_item_resource_links_resource");
    }
}

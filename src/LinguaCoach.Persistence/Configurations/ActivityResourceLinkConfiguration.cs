using LinguaCoach.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LinguaCoach.Persistence.Configurations;

internal sealed class ActivityResourceLinkConfiguration : IEntityTypeConfiguration<ActivityResourceLink>
{
    public void Configure(EntityTypeBuilder<ActivityResourceLink> builder)
    {
        builder.ToTable("activity_resource_links");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired()
            .HasDefaultValueSql("now()");

        builder.Property(e => e.ActivityDefinitionId).HasColumnName("activity_definition_id").IsRequired();
        builder.Property(e => e.ResourceType).HasColumnName("resource_type").HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(e => e.ResourceId).HasColumnName("resource_id").IsRequired();
        builder.Property(e => e.Role).HasColumnName("role").HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(e => e.SnapshotTitle).HasColumnName("snapshot_title").HasMaxLength(500);
        builder.Property(e => e.ContentFingerprint).HasColumnName("content_fingerprint").HasMaxLength(128);

        builder.HasOne<ActivityDefinition>()
            .WithMany()
            .HasForeignKey(e => e.ActivityDefinitionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(e => e.ActivityDefinitionId).HasDatabaseName("ix_activity_resource_links_activity");
        builder.HasIndex(e => new { e.ResourceType, e.ResourceId }).HasDatabaseName("ix_activity_resource_links_resource");
    }
}

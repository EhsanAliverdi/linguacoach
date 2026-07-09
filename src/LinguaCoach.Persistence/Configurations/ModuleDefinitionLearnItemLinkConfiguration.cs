using LinguaCoach.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LinguaCoach.Persistence.Configurations;

internal sealed class ModuleDefinitionLearnItemLinkConfiguration : IEntityTypeConfiguration<ModuleDefinitionLearnItemLink>
{
    public void Configure(EntityTypeBuilder<ModuleDefinitionLearnItemLink> builder)
    {
        builder.ToTable("module_definition_learn_item_links");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired()
            .HasDefaultValueSql("now()");

        builder.Property(e => e.ModuleDefinitionId).HasColumnName("module_definition_id").IsRequired();
        builder.Property(e => e.LearnItemId).HasColumnName("learn_item_id").IsRequired();
        builder.Property(e => e.Role).HasColumnName("role").HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(e => e.SortOrder).HasColumnName("sort_order").IsRequired();
        builder.Property(e => e.SnapshotTitle).HasColumnName("snapshot_title").HasMaxLength(500);

        builder.HasOne<ModuleDefinition>()
            .WithMany()
            .HasForeignKey(e => e.ModuleDefinitionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(e => e.ModuleDefinitionId).HasDatabaseName("ix_module_definition_learn_item_links_module");
        builder.HasIndex(e => e.LearnItemId).HasDatabaseName("ix_module_definition_learn_item_links_learn_item");
    }
}

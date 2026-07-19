using LinguaCoach.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LinguaCoach.Persistence.Configurations;

internal sealed class ModuleSkillGraphNodeLinkConfiguration : IEntityTypeConfiguration<ModuleSkillGraphNodeLink>
{
    public void Configure(EntityTypeBuilder<ModuleSkillGraphNodeLink> builder)
    {
        builder.ToTable("module_skill_graph_node_links");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired()
            .HasDefaultValueSql("now()");

        builder.Property(e => e.ModuleId).HasColumnName("module_id").IsRequired();
        builder.Property(e => e.SkillGraphNodeId).HasColumnName("skill_graph_node_id").IsRequired();
        builder.Property(e => e.Confidence).HasColumnName("confidence");

        // Cascade on the Module side only, following ModuleLessonLinkConfiguration's convention —
        // the SkillGraphNodeId side has no DB-level FK constraint (a node is soft-deactivated via
        // IsActive in practice, never hard-deleted, so no cascade is needed on that side).
        builder.HasOne<Module>()
            .WithMany()
            .HasForeignKey(e => e.ModuleId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(e => e.ModuleId).HasDatabaseName("ix_module_skill_graph_node_links_module");
        builder.HasIndex(e => e.SkillGraphNodeId).HasDatabaseName("ix_module_skill_graph_node_links_node");
        builder.HasIndex(e => new { e.ModuleId, e.SkillGraphNodeId }).IsUnique()
            .HasDatabaseName("ix_module_skill_graph_node_links_module_node");
    }
}

using LinguaCoach.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LinguaCoach.Persistence.Configurations;

internal sealed class SkillGraphPrerequisiteEdgeConfiguration : IEntityTypeConfiguration<SkillGraphPrerequisiteEdge>
{
    public void Configure(EntityTypeBuilder<SkillGraphPrerequisiteEdge> builder)
    {
        builder.ToTable("skill_graph_prerequisite_edges");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired()
            .HasDefaultValueSql("now()");

        builder.Property(e => e.NodeId).HasColumnName("node_id").IsRequired();
        builder.Property(e => e.PrerequisiteNodeId).HasColumnName("prerequisite_node_id").IsRequired();

        // Both sides point at the same table (self-referencing) — no cascade on either FK to avoid
        // a multiple-cascade-path error EF/Postgres would reject; edges are cleaned up explicitly
        // when a node is deleted (nodes are soft-deleted via IsActive in practice, not hard-deleted).
        builder.HasOne<SkillGraphNode>()
            .WithMany()
            .HasForeignKey(e => e.NodeId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<SkillGraphNode>()
            .WithMany()
            .HasForeignKey(e => e.PrerequisiteNodeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => e.NodeId).HasDatabaseName("ix_skill_graph_prerequisite_edges_node");
        builder.HasIndex(e => e.PrerequisiteNodeId).HasDatabaseName("ix_skill_graph_prerequisite_edges_prerequisite");
        builder.HasIndex(e => new { e.NodeId, e.PrerequisiteNodeId }).IsUnique()
            .HasDatabaseName("ix_skill_graph_prerequisite_edges_node_prerequisite");
    }
}

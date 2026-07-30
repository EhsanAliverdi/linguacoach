using LinguaCoach.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LinguaCoach.Persistence.Configurations;

internal sealed class SkillGraphNodeConfiguration : IEntityTypeConfiguration<SkillGraphNode>
{
    public void Configure(EntityTypeBuilder<SkillGraphNode> builder)
    {
        builder.ToTable("skill_graph_nodes");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired()
            .HasDefaultValueSql("now()");

        builder.Property(e => e.Key).HasColumnName("key").HasMaxLength(200).IsRequired();
        builder.Property(e => e.Title).HasColumnName("title").HasMaxLength(300).IsRequired();
        builder.Property(e => e.Description).HasColumnName("description").IsRequired();
        builder.Property(e => e.CefrLevel).HasColumnName("cefr_level").HasMaxLength(2).IsRequired();
        builder.Property(e => e.Skill).HasColumnName("skill").HasMaxLength(50).IsRequired();
        builder.Property(e => e.Subskill).HasColumnName("subskill").HasMaxLength(50);
        builder.Property(e => e.DifficultyBand).HasColumnName("difficulty_band").IsRequired();
        builder.Property(e => e.DescriptionForAi).HasColumnName("description_for_ai");
        builder.Property(e => e.ReviewStatus).HasColumnName("review_status").HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(e => e.ReviewedByUserId).HasColumnName("reviewed_by_user_id");
        builder.Property(e => e.ApprovedAtUtc).HasColumnName("approved_at_utc");
        builder.Property(e => e.RejectedAtUtc).HasColumnName("rejected_at_utc");
        builder.Property(e => e.RejectionReason).HasColumnName("rejection_reason").HasMaxLength(1000);
        builder.Property(e => e.IsActive).HasColumnName("is_active").IsRequired();
        builder.Property(e => e.UpdatedAtUtc).HasColumnName("updated_at_utc").IsRequired();
        builder.Property(e => e.ContextTagsJson).HasColumnName("context_tags_json").HasDefaultValue("[]").IsRequired();
        builder.Property(e => e.FocusTagsJson).HasColumnName("focus_tags_json").HasDefaultValue("[]").IsRequired();
        builder.Property(e => e.ParentNodeId).HasColumnName("parent_node_id");

        // Phase GSG-1 (2026-07-31) — CEFR provenance/confidence, node type, routing eligibility.
        builder.Property(e => e.CefrConfidence).HasColumnName("cefr_confidence").HasConversion<string>().HasMaxLength(32).IsRequired()
            .HasDefaultValue(Domain.Enums.CefrConfidence.Unknown);
        builder.Property(e => e.CefrSource).HasColumnName("cefr_source").HasMaxLength(50);
        builder.Property(e => e.NodeType).HasColumnName("node_type").HasConversion<string>().HasMaxLength(32);
        builder.Property(e => e.RoutingEligible).HasColumnName("routing_eligible").IsRequired().HasDefaultValue(false);

        builder.HasIndex(e => e.Key).IsUnique().HasDatabaseName("ix_skill_graph_nodes_key");
        builder.HasIndex(e => new { e.CefrLevel, e.Skill, e.IsActive }).HasDatabaseName("ix_skill_graph_nodes_cefr_skill_active");
        builder.HasIndex(e => e.ReviewStatus).HasDatabaseName("ix_skill_graph_nodes_review_status");
        builder.HasIndex(e => e.ParentNodeId).HasDatabaseName("ix_skill_graph_nodes_parent_node_id");
        builder.HasIndex(e => e.RoutingEligible).HasDatabaseName("ix_skill_graph_nodes_routing_eligible");
        builder.HasIndex(e => e.NodeType).HasDatabaseName("ix_skill_graph_nodes_node_type");

        // Self-referencing, same no-cascade convention SkillGraphPrerequisiteEdge already uses —
        // deleting/deactivating a container while it still has children must be an explicit,
        // reviewed action (reparent or deactivate the children first), never a silent cascade.
        builder.HasOne<SkillGraphNode>()
            .WithMany()
            .HasForeignKey(e => e.ParentNodeId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

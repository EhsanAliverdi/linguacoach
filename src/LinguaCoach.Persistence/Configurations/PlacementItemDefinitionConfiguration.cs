using LinguaCoach.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LinguaCoach.Persistence.Configurations;

internal sealed class PlacementItemDefinitionConfiguration : IEntityTypeConfiguration<PlacementItemDefinition>
{
    public void Configure(EntityTypeBuilder<PlacementItemDefinition> builder)
    {
        builder.ToTable("placement_item_definitions");

        builder.HasKey(i => i.Id);
        builder.Property(i => i.Id).HasColumnName("id");
        builder.Property(i => i.CreatedAt).HasColumnName("created_at").IsRequired()
            .HasDefaultValueSql("now()");

        builder.Property(i => i.Skill).HasColumnName("skill").IsRequired().HasMaxLength(50);
        builder.Property(i => i.CefrLevel).HasColumnName("cefr_level").IsRequired().HasMaxLength(10);
        builder.Property(i => i.Subskill).HasColumnName("subskill").HasMaxLength(128);
        builder.Property(i => i.ItemOrder).HasColumnName("item_order").IsRequired();
        builder.Property(i => i.IsEnabled).HasColumnName("is_enabled").IsRequired();
        builder.Property(i => i.FormIoSchemaJson).HasColumnName("form_io_schema_json").HasColumnType("jsonb");
        builder.Property(i => i.ScoringRulesJson).HasColumnName("scoring_rules_json").HasColumnType("jsonb");
        builder.Property(i => i.AuthoringSchemaJson).HasColumnName("authoring_schema_json").HasColumnType("jsonb");
        builder.Property(i => i.ScoringRulesVersion).HasColumnName("scoring_rules_version").IsRequired()
            .HasDefaultValue(0);
        builder.Property(i => i.RendererKind).HasColumnName("renderer_kind").HasConversion<string>().HasMaxLength(20).IsRequired()
            .HasDefaultValue(LinguaCoach.Domain.Enums.FormRendererKind.FormIo);

        // Calibration (Phase 7 — AI Bank-First Teaching Architecture)
        builder.Property(i => i.DifficultyBand).HasColumnName("difficulty_band").IsRequired().HasDefaultValue(1);
        builder.Property(i => i.DiscriminationIndex).HasColumnName("discrimination_index");
        builder.Property(i => i.CalibrationSampleSize).HasColumnName("calibration_sample_size");
        builder.Property(i => i.EvidenceWeight).HasColumnName("evidence_weight").IsRequired().HasDefaultValue(1.0);
        builder.Property(i => i.ReviewStatus).HasColumnName("review_status").IsRequired()
            .HasConversion<string>().HasMaxLength(50).HasDefaultValue(LinguaCoach.Domain.Enums.AdminReviewStatus.NotRequired);
        builder.Property(i => i.ItemVersion).HasColumnName("item_version").IsRequired().HasDefaultValue(1);
        builder.Property(i => i.PreviousVersionId).HasColumnName("previous_version_id");

        builder.HasOne<PlacementItemDefinition>()
            .WithMany()
            .HasForeignKey(i => i.PreviousVersionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(i => new { i.Skill, i.CefrLevel, i.IsEnabled })
            .HasDatabaseName("ix_placement_item_definitions_skill_level_enabled");

        builder.HasIndex(i => i.ReviewStatus)
            .HasDatabaseName("ix_placement_item_definitions_review_status");
    }
}

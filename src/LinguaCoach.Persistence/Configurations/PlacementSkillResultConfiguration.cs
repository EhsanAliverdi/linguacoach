using LinguaCoach.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LinguaCoach.Persistence.Configurations;

internal sealed class PlacementSkillResultConfiguration : IEntityTypeConfiguration<PlacementSkillResult>
{
    public void Configure(EntityTypeBuilder<PlacementSkillResult> builder)
    {
        builder.ToTable("placement_skill_results");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired();

        builder.Property(e => e.PlacementAssessmentId).HasColumnName("placement_assessment_id").IsRequired();
        builder.Property(e => e.Skill).HasColumnName("skill").HasMaxLength(100).IsRequired();
        builder.Property(e => e.EstimatedCefrLevel).HasColumnName("estimated_cefr_level").HasMaxLength(10).IsRequired();
        builder.Property(e => e.Confidence).HasColumnName("confidence").IsRequired();
        builder.Property(e => e.EvidenceCount).HasColumnName("evidence_count").IsRequired();
        builder.Property(e => e.Strengths).HasColumnName("strengths").HasMaxLength(500);
        builder.Property(e => e.Weaknesses).HasColumnName("weaknesses").HasMaxLength(500);
        builder.Property(e => e.RecommendedStartingObjectiveKeys)
            .HasColumnName("recommended_starting_objective_keys").HasMaxLength(1000);

        builder.HasOne<PlacementAssessment>()
            .WithMany()
            .HasForeignKey(e => e.PlacementAssessmentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(e => e.PlacementAssessmentId)
            .HasDatabaseName("ix_placement_skill_results_assessment_id");
    }
}

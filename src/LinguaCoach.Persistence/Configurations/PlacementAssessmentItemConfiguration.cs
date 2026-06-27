using LinguaCoach.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LinguaCoach.Persistence.Configurations;

internal sealed class PlacementAssessmentItemConfiguration : IEntityTypeConfiguration<PlacementAssessmentItem>
{
    public void Configure(EntityTypeBuilder<PlacementAssessmentItem> builder)
    {
        builder.ToTable("placement_assessment_items");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired();

        builder.Property(e => e.PlacementAssessmentId).HasColumnName("placement_assessment_id").IsRequired();
        builder.Property(e => e.Skill).HasColumnName("skill").HasMaxLength(100).IsRequired();
        builder.Property(e => e.TargetCefrLevel).HasColumnName("target_cefr_level").HasMaxLength(10).IsRequired();
        builder.Property(e => e.ItemType).HasColumnName("item_type").HasMaxLength(50).IsRequired();
        builder.Property(e => e.Prompt).HasColumnName("prompt").HasMaxLength(2000).IsRequired();
        builder.Property(e => e.Response).HasColumnName("response").HasMaxLength(2000);
        builder.Property(e => e.Score).HasColumnName("score");
        builder.Property(e => e.IsCorrect).HasColumnName("is_correct");
        builder.Property(e => e.EvaluatedAtUtc).HasColumnName("evaluated_at_utc");
        builder.Property(e => e.ItemOrder).HasColumnName("item_order").IsRequired();
        builder.Property(e => e.CorrectAnswer).HasColumnName("correct_answer").HasMaxLength(500);
        builder.Property(e => e.EvaluationNotes).HasColumnName("evaluation_notes").HasMaxLength(1000);
        builder.Property(e => e.DurationSeconds).HasColumnName("duration_seconds");

        builder.HasOne<PlacementAssessment>()
            .WithMany(a => a.Items)
            .HasForeignKey(e => e.PlacementAssessmentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(e => e.PlacementAssessmentId)
            .HasDatabaseName("ix_placement_assessment_items_assessment_id");
    }
}

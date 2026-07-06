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
        builder.Property(e => e.SourceItemDefinitionId).HasColumnName("source_item_definition_id");
        builder.Property(e => e.Skill).HasColumnName("skill").HasMaxLength(100).IsRequired();
        builder.Property(e => e.TargetCefrLevel).HasColumnName("target_cefr_level").HasMaxLength(10).IsRequired();
        builder.Property(e => e.ItemType).HasColumnName("item_type").HasMaxLength(50).IsRequired();
        builder.Property(e => e.Prompt).HasColumnName("prompt").HasMaxLength(2000).IsRequired();
        builder.Property(e => e.Score).HasColumnName("score");
        builder.Property(e => e.IsCorrect).HasColumnName("is_correct");
        builder.Property(e => e.EvaluatedAtUtc).HasColumnName("evaluated_at_utc");
        builder.Property(e => e.ItemOrder).HasColumnName("item_order").IsRequired();
        builder.Property(e => e.DurationSeconds).HasColumnName("duration_seconds");
        builder.Property(e => e.AudioStorageKey).HasColumnName("audio_storage_key").HasMaxLength(500);
        builder.Property(e => e.AudioContentType).HasColumnName("audio_content_type").HasMaxLength(100);
        builder.Property(e => e.FormIoSchemaJson).HasColumnName("form_io_schema_json").HasColumnType("jsonb");
        builder.Property(e => e.ScoringRulesJsonSnapshot).HasColumnName("scoring_rules_json_snapshot").HasColumnType("jsonb");
        builder.Property(e => e.ScoringRulesVersionSnapshot).HasColumnName("scoring_rules_version_snapshot");
        builder.Property(e => e.SubmissionDataJson).HasColumnName("submission_data_json").HasColumnType("jsonb");
        builder.Property(e => e.NormalizedAnswerJson).HasColumnName("normalized_answer_json").HasColumnType("jsonb");

        builder.HasOne<PlacementAssessment>()
            .WithMany(a => a.Items)
            .HasForeignKey(e => e.PlacementAssessmentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(e => e.PlacementAssessmentId)
            .HasDatabaseName("ix_placement_assessment_items_assessment_id");
    }
}

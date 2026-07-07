using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LinguaCoach.Persistence.Configurations;

internal sealed class ActivityTemplateConfiguration : IEntityTypeConfiguration<ActivityTemplate>
{
    public void Configure(EntityTypeBuilder<ActivityTemplate> builder)
    {
        builder.ToTable("activity_templates");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired()
            .HasDefaultValueSql("now()");

        builder.Property(e => e.Key).HasColumnName("key").HasMaxLength(200).IsRequired();
        builder.Property(e => e.VersionNumber).HasColumnName("version_number").IsRequired()
            .HasDefaultValue(1);
        builder.Property(e => e.PreviousVersionId).HasColumnName("previous_version_id");

        builder.Property(e => e.Skill).HasColumnName("skill").HasMaxLength(100).IsRequired();
        builder.Property(e => e.Subskill).HasColumnName("subskill").HasMaxLength(128);
        builder.Property(e => e.CefrLevel).HasColumnName("cefr_level").HasMaxLength(10).IsRequired();

        builder.Property(e => e.ContextTagsJson).HasColumnName("context_tags_json").IsRequired()
            .HasDefaultValue("[]");
        builder.Property(e => e.FocusTagsJson).HasColumnName("focus_tags_json").IsRequired()
            .HasDefaultValue("[]");
        builder.Property(e => e.CurriculumObjectiveKey).HasColumnName("curriculum_objective_key").HasMaxLength(200);

        builder.Property(e => e.ActivityType).HasColumnName("activity_type").HasMaxLength(100).IsRequired();
        builder.Property(e => e.PatternKey).HasColumnName("pattern_key").HasMaxLength(200);

        builder.Property(e => e.FormIoBaseSchemaJson).HasColumnName("form_io_base_schema_json").HasColumnType("jsonb");
        builder.Property(e => e.GenerationInstructions).HasColumnName("generation_instructions");
        builder.Property(e => e.ScoringModelJson).HasColumnName("scoring_model_json").HasColumnType("jsonb");
        builder.Property(e => e.ValidationRulesJson).HasColumnName("validation_rules_json").HasColumnType("jsonb");

        builder.Property(e => e.ReviewStatus).HasColumnName("review_status").IsRequired()
            .HasConversion<string>().HasMaxLength(50).HasDefaultValue(AdminReviewStatus.NotRequired);
        builder.Property(e => e.IsPublished).HasColumnName("is_published").IsRequired()
            .HasDefaultValue(false);

        builder.Property(e => e.EstimatedDurationSeconds).HasColumnName("estimated_duration_seconds");
        builder.Property(e => e.AssetRequirementsJson).HasColumnName("asset_requirements_json");

        builder.HasOne<ActivityTemplate>()
            .WithMany()
            .HasForeignKey(e => e.PreviousVersionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => new { e.Key, e.VersionNumber })
            .IsUnique()
            .HasDatabaseName("ix_activity_templates_key_version");

        builder.HasIndex(e => new { e.Skill, e.CefrLevel, e.IsPublished })
            .HasDatabaseName("ix_activity_templates_skill_level_published");

        builder.HasIndex(e => e.ReviewStatus)
            .HasDatabaseName("ix_activity_templates_review_status");
    }
}

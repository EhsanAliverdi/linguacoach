using LinguaCoach.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LinguaCoach.Persistence.Configurations;

internal sealed class CurriculumObjectiveConfiguration : IEntityTypeConfiguration<CurriculumObjective>
{
    public void Configure(EntityTypeBuilder<CurriculumObjective> builder)
    {
        builder.ToTable("curriculum_objectives");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired()
            .HasDefaultValueSql("now()");

        builder.Property(e => e.Key).HasColumnName("key").HasMaxLength(200).IsRequired();
        builder.Property(e => e.Title).HasColumnName("title").HasMaxLength(300).IsRequired();
        builder.Property(e => e.Description).HasColumnName("description").IsRequired();
        builder.Property(e => e.CefrLevel).HasColumnName("cefr_level").HasMaxLength(10).IsRequired();
        builder.Property(e => e.PrimarySkill).HasColumnName("primary_skill").HasMaxLength(100).IsRequired();
        builder.Property(e => e.SecondarySkillsJson).HasColumnName("secondary_skills_json").IsRequired()
            .HasDefaultValue("[]");
        builder.Property(e => e.ContextTagsJson).HasColumnName("context_tags_json").IsRequired()
            .HasDefaultValue("[]");
        builder.Property(e => e.FocusTagsJson).HasColumnName("focus_tags_json").IsRequired()
            .HasDefaultValue("[]");
        builder.Property(e => e.PrerequisiteKeysJson).HasColumnName("prerequisite_keys_json").IsRequired()
            .HasDefaultValue("[]");
        builder.Property(e => e.RecommendedOrder).HasColumnName("recommended_order").IsRequired();
        builder.Property(e => e.DifficultyBand).HasColumnName("difficulty_band").IsRequired();
        builder.Property(e => e.IsActive).HasColumnName("is_active").IsRequired();
        builder.Property(e => e.IsReviewable).HasColumnName("is_reviewable").IsRequired();
        builder.Property(e => e.IsExamInspired).HasColumnName("is_exam_inspired").IsRequired();
        builder.Property(e => e.TeachingNotes).HasColumnName("teaching_notes");

        builder.HasIndex(e => e.Key)
            .IsUnique()
            .HasDatabaseName("ix_curriculum_objectives_key");

        builder.HasIndex(e => new { e.CefrLevel, e.PrimarySkill, e.IsActive })
            .HasDatabaseName("ix_curriculum_objectives_cefr_skill_active");
    }
}

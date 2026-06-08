using LinguaCoach.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LinguaCoach.Persistence.Configurations;

internal sealed class PlacementAssessmentConfiguration : IEntityTypeConfiguration<PlacementAssessment>
{
    public void Configure(EntityTypeBuilder<PlacementAssessment> builder)
    {
        builder.ToTable("placement_assessments");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired();

        builder.Property(e => e.StudentProfileId).HasColumnName("student_profile_id").IsRequired();
        builder.Property(e => e.Status).HasColumnName("status").IsRequired();
        builder.Property(e => e.StartedAtUtc).HasColumnName("started_at_utc");
        builder.Property(e => e.CompletedAtUtc).HasColumnName("completed_at_utc");
        builder.Property(e => e.CurrentSectionKey).HasColumnName("current_section_key")
            .HasMaxLength(50).IsRequired();
        builder.Property(e => e.ResultJson).HasColumnName("result_json");
        builder.Property(e => e.OverallEstimatedLevel).HasColumnName("overall_estimated_level").HasMaxLength(5);
        builder.Property(e => e.SkillLevelsJson).HasColumnName("skill_levels_json");
        builder.Property(e => e.UpdatedAtUtc).HasColumnName("updated_at_utc").IsRequired();

        builder.HasMany(e => e.Answers)
            .WithOne()
            .HasForeignKey(a => a.PlacementAssessmentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(e => e.Answers).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasOne<StudentProfile>()
            .WithMany()
            .HasForeignKey(e => e.StudentProfileId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(e => e.StudentProfileId)
            .HasDatabaseName("ix_placement_assessments_student_profile_id");
    }
}

using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LinguaCoach.Persistence.Configurations;

internal sealed class StudentLearningPlanConfiguration : IEntityTypeConfiguration<StudentLearningPlan>
{
    public void Configure(EntityTypeBuilder<StudentLearningPlan> builder)
    {
        builder.ToTable("student_learning_plans");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired()
            .HasDefaultValueSql("now()");

        builder.Property(e => e.StudentProfileId).HasColumnName("student_profile_id").IsRequired();
        builder.Property(e => e.CefrLevelSnapshot).HasColumnName("cefr_level_snapshot")
            .HasMaxLength(10).IsRequired();
        builder.Property(e => e.Status).HasColumnName("status").IsRequired()
            .HasConversion<string>().HasMaxLength(30);
        builder.Property(e => e.RegenerationReason).HasColumnName("regeneration_reason")
            .HasMaxLength(200).IsRequired();
        builder.Property(e => e.RegenerationCount).HasColumnName("regeneration_count")
            .IsRequired().HasDefaultValue(0);
        builder.Property(e => e.LastEvaluatedAt).HasColumnName("last_evaluated_at");
        builder.Property(e => e.PlannedLessonCount).HasColumnName("planned_lesson_count")
            .IsRequired().HasDefaultValue(10);

        builder.HasMany(e => e.Objectives)
            .WithOne()
            .HasForeignKey(o => o.StudentLearningPlanId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(e => new { e.StudentProfileId, e.Status })
            .HasDatabaseName("ix_learning_plans_student_status");

        builder.HasIndex(e => e.StudentProfileId)
            .HasDatabaseName("ix_learning_plans_student");
    }
}

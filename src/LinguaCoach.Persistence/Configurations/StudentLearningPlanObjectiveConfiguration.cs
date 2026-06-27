using LinguaCoach.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LinguaCoach.Persistence.Configurations;

internal sealed class StudentLearningPlanObjectiveConfiguration
    : IEntityTypeConfiguration<StudentLearningPlanObjective>
{
    public void Configure(EntityTypeBuilder<StudentLearningPlanObjective> builder)
    {
        builder.ToTable("student_learning_plan_objectives");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired()
            .HasDefaultValueSql("now()");

        builder.Property(e => e.StudentLearningPlanId).HasColumnName("student_learning_plan_id").IsRequired();
        builder.Property(e => e.ObjectiveKey).HasColumnName("objective_key").HasMaxLength(200).IsRequired();
        builder.Property(e => e.CefrLevel).HasColumnName("cefr_level").HasMaxLength(10).IsRequired();
        builder.Property(e => e.Skill).HasColumnName("skill").HasMaxLength(100).IsRequired();
        builder.Property(e => e.Context).HasColumnName("context").HasMaxLength(100).IsRequired();
        builder.Property(e => e.Title).HasColumnName("title").HasMaxLength(300);
        builder.Property(e => e.Priority).HasColumnName("priority").IsRequired().HasDefaultValue(0);
        builder.Property(e => e.Source).HasColumnName("source").HasMaxLength(100).IsRequired();
        builder.Property(e => e.Status).HasColumnName("status").IsRequired()
            .HasConversion<string>().HasMaxLength(30);
        builder.Property(e => e.PlannedOrder).HasColumnName("planned_order");
        builder.Property(e => e.IsReview).HasColumnName("is_review").IsRequired().HasDefaultValue(false);
        builder.Property(e => e.IsBlocked).HasColumnName("is_blocked").IsRequired().HasDefaultValue(false);
        builder.Property(e => e.BlockedByObjectiveKey).HasColumnName("blocked_by_objective_key").HasMaxLength(200);
        builder.Property(e => e.LastEvaluatedAt).HasColumnName("last_evaluated_at");

        builder.HasIndex(e => new { e.StudentLearningPlanId, e.Status })
            .HasDatabaseName("ix_plan_objectives_plan_status");

        builder.HasIndex(e => new { e.StudentLearningPlanId, e.PlannedOrder })
            .HasDatabaseName("ix_plan_objectives_plan_order");
    }
}

using LinguaCoach.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LinguaCoach.Persistence.Configurations;

internal sealed class StudentGoalWeightConfiguration : IEntityTypeConfiguration<StudentGoalWeight>
{
    public void Configure(EntityTypeBuilder<StudentGoalWeight> builder)
    {
        builder.ToTable("student_goal_weights");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired()
            .HasDefaultValueSql("now()");

        builder.Property(e => e.StudentId).HasColumnName("student_id").IsRequired();
        builder.Property(e => e.GoalTag).HasColumnName("goal_tag").HasMaxLength(50).IsRequired();
        builder.Property(e => e.Weight).HasColumnName("weight").IsRequired();
        builder.Property(e => e.Source).HasColumnName("source").HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(e => e.UpdatedAtUtc).HasColumnName("updated_at_utc").IsRequired();

        builder.HasOne<StudentProfile>()
            .WithMany()
            .HasForeignKey(e => e.StudentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(e => e.StudentId).HasDatabaseName("ix_student_goal_weights_student");
        builder.HasIndex(e => new { e.StudentId, e.GoalTag }).IsUnique()
            .HasDatabaseName("ix_student_goal_weights_student_tag");
    }
}

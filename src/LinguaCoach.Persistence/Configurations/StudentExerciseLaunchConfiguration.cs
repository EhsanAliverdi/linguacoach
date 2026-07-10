using LinguaCoach.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LinguaCoach.Persistence.Configurations;

internal sealed class StudentExerciseLaunchConfiguration : IEntityTypeConfiguration<StudentExerciseLaunch>
{
    public void Configure(EntityTypeBuilder<StudentExerciseLaunch> builder)
    {
        builder.ToTable("student_exercise_launches");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired()
            .HasDefaultValueSql("now()");

        builder.Property(e => e.StudentId).HasColumnName("student_id").IsRequired();
        builder.Property(e => e.ModuleId).HasColumnName("module_id").IsRequired();
        builder.Property(e => e.ExerciseId).HasColumnName("exercise_id").IsRequired();
        builder.Property(e => e.LessonId).HasColumnName("lesson_id");
        builder.Property(e => e.LearningActivityId).HasColumnName("learning_activity_id").IsRequired();
        builder.Property(e => e.Source).HasColumnName("source").HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(e => e.LaunchedAt).HasColumnName("launched_at").IsRequired();

        // Restrict-delete FKs so a launch history row can never silently lose the content it
        // points at — mirrors StudentDailyModuleAssignment/StudentPracticeGymModuleAssignment's
        // convention for FKs to reusable/admin-authored content.
        builder.HasOne<Module>()
            .WithMany()
            .HasForeignKey(e => e.ModuleId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Exercise>()
            .WithMany()
            .HasForeignKey(e => e.ExerciseId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Lesson>()
            .WithMany()
            .HasForeignKey(e => e.LessonId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<LearningActivity>()
            .WithMany()
            .HasForeignKey(e => e.LearningActivityId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => new { e.StudentId, e.LaunchedAt })
            .HasDatabaseName("ix_exercise_launches_student_launched");
        builder.HasIndex(e => e.ModuleId)
            .HasDatabaseName("ix_exercise_launches_module");
        builder.HasIndex(e => e.ExerciseId)
            .HasDatabaseName("ix_exercise_launches_activity");
        builder.HasIndex(e => e.LearningActivityId)
            .HasDatabaseName("ix_exercise_launches_learning_activity")
            .IsUnique();
    }
}

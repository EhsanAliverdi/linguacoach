using LinguaCoach.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LinguaCoach.Persistence.Configurations;

internal sealed class StudentActivityDefinitionLaunchConfiguration : IEntityTypeConfiguration<StudentActivityDefinitionLaunch>
{
    public void Configure(EntityTypeBuilder<StudentActivityDefinitionLaunch> builder)
    {
        builder.ToTable("student_activity_definition_launches");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired()
            .HasDefaultValueSql("now()");

        builder.Property(e => e.StudentId).HasColumnName("student_id").IsRequired();
        builder.Property(e => e.ModuleDefinitionId).HasColumnName("module_definition_id").IsRequired();
        builder.Property(e => e.ActivityDefinitionId).HasColumnName("activity_definition_id").IsRequired();
        builder.Property(e => e.LearnItemId).HasColumnName("learn_item_id");
        builder.Property(e => e.LearningActivityId).HasColumnName("learning_activity_id").IsRequired();
        builder.Property(e => e.Source).HasColumnName("source").HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(e => e.LaunchedAt).HasColumnName("launched_at").IsRequired();

        // Restrict-delete FKs so a launch history row can never silently lose the content it
        // points at — mirrors StudentDailyModuleAssignment/StudentPracticeGymModuleAssignment's
        // convention for FKs to reusable/admin-authored content.
        builder.HasOne<ModuleDefinition>()
            .WithMany()
            .HasForeignKey(e => e.ModuleDefinitionId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ActivityDefinition>()
            .WithMany()
            .HasForeignKey(e => e.ActivityDefinitionId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<LearnItem>()
            .WithMany()
            .HasForeignKey(e => e.LearnItemId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<LearningActivity>()
            .WithMany()
            .HasForeignKey(e => e.LearningActivityId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => new { e.StudentId, e.LaunchedAt })
            .HasDatabaseName("ix_activity_definition_launches_student_launched");
        builder.HasIndex(e => e.ModuleDefinitionId)
            .HasDatabaseName("ix_activity_definition_launches_module");
        builder.HasIndex(e => e.ActivityDefinitionId)
            .HasDatabaseName("ix_activity_definition_launches_activity");
        builder.HasIndex(e => e.LearningActivityId)
            .HasDatabaseName("ix_activity_definition_launches_learning_activity")
            .IsUnique();
    }
}

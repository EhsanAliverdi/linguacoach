using LinguaCoach.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LinguaCoach.Persistence.Configurations;

internal sealed class StudentDailyModuleAssignmentConfiguration : IEntityTypeConfiguration<StudentDailyModuleAssignment>
{
    public void Configure(EntityTypeBuilder<StudentDailyModuleAssignment> builder)
    {
        builder.ToTable("student_daily_module_assignments");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired()
            .HasDefaultValueSql("now()");

        builder.Property(e => e.StudentId).HasColumnName("student_id").IsRequired();
        builder.Property(e => e.ModuleDefinitionId).HasColumnName("module_definition_id");
        builder.Property(e => e.AssignedForDate).HasColumnName("assigned_for_date").HasColumnType("date").IsRequired();
        builder.Property(e => e.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(e => e.SelectionReason).HasColumnName("selection_reason");
        builder.Property(e => e.FallbackReason).HasColumnName("fallback_reason");
        builder.Property(e => e.EstimatedMinutes).HasColumnName("estimated_minutes");
        builder.Property(e => e.ConsumedAt).HasColumnName("consumed_at");

        // Restrict-delete FK to ModuleDefinition mirrors StudentActivityReadinessItem's
        // SourceTemplateId/SourceBankItemId convention for FKs to reusable/admin-authored content.
        builder.HasOne<ModuleDefinition>()
            .WithMany()
            .HasForeignKey(e => e.ModuleDefinitionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => new { e.StudentId, e.AssignedForDate })
            .HasDatabaseName("ix_daily_module_assignments_student_date");
        builder.HasIndex(e => new { e.StudentId, e.ModuleDefinitionId })
            .HasDatabaseName("ix_daily_module_assignments_student_module");
        builder.HasIndex(e => e.Status).HasDatabaseName("ix_daily_module_assignments_status");
    }
}

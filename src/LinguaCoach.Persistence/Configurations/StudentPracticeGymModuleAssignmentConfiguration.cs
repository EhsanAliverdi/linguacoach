using LinguaCoach.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LinguaCoach.Persistence.Configurations;

internal sealed class StudentPracticeGymModuleAssignmentConfiguration : IEntityTypeConfiguration<StudentPracticeGymModuleAssignment>
{
    public void Configure(EntityTypeBuilder<StudentPracticeGymModuleAssignment> builder)
    {
        builder.ToTable("student_practice_gym_module_assignments");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired()
            .HasDefaultValueSql("now()");

        builder.Property(e => e.StudentId).HasColumnName("student_id").IsRequired();
        builder.Property(e => e.ModuleId).HasColumnName("module_id");
        builder.Property(e => e.SuggestedAt).HasColumnName("suggested_at").IsRequired();
        builder.Property(e => e.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(e => e.SelectionReason).HasColumnName("selection_reason");
        builder.Property(e => e.FallbackReason).HasColumnName("fallback_reason");
        builder.Property(e => e.SelectedAt).HasColumnName("selected_at");
        builder.Property(e => e.DismissedAt).HasColumnName("dismissed_at");
        builder.Property(e => e.ConsumedAt).HasColumnName("consumed_at");

        // Restrict-delete FK to Module mirrors H6's StudentDailyModuleAssignment
        // convention for FKs to reusable/admin-authored content.
        builder.HasOne<Module>()
            .WithMany()
            .HasForeignKey(e => e.ModuleId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => new { e.StudentId, e.SuggestedAt })
            .HasDatabaseName("ix_pg_module_assignments_student_suggested");
        builder.HasIndex(e => new { e.StudentId, e.ModuleId })
            .HasDatabaseName("ix_pg_module_assignments_student_module");
        builder.HasIndex(e => e.Status).HasDatabaseName("ix_pg_module_assignments_status");
    }
}

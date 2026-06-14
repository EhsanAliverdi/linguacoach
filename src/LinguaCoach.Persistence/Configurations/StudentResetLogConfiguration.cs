using LinguaCoach.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LinguaCoach.Persistence.Configurations;

internal sealed class StudentResetLogConfiguration : IEntityTypeConfiguration<StudentResetLog>
{
    public void Configure(EntityTypeBuilder<StudentResetLog> builder)
    {
        builder.ToTable("student_reset_logs");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired()
            .HasDefaultValueSql("now()");

        builder.Property(e => e.StudentProfileId).HasColumnName("student_profile_id").IsRequired();
        builder.Property(e => e.AdminUserId).HasColumnName("admin_user_id").IsRequired();
        builder.Property(e => e.PreviousStage).HasColumnName("previous_stage")
            .HasConversion<int>().IsRequired();
        builder.Property(e => e.NewStage).HasColumnName("new_stage")
            .HasConversion<int>().IsRequired();
        builder.Property(e => e.ClearedItemsJson).HasColumnName("cleared_items_json")
            .HasColumnType("jsonb").IsRequired();
        builder.Property(e => e.Reason).HasColumnName("reason").HasMaxLength(2000).IsRequired();
        builder.Property(e => e.CorrelationId).HasColumnName("correlation_id").HasMaxLength(100).IsRequired();
        builder.Property(e => e.PerformedAtUtc).HasColumnName("performed_at_utc").IsRequired();

        builder.HasIndex(e => e.StudentProfileId)
            .HasDatabaseName("ix_student_reset_logs_student");

        builder.HasIndex(e => e.AdminUserId)
            .HasDatabaseName("ix_student_reset_logs_admin");

        builder.HasOne<StudentProfile>()
            .WithMany()
            .HasForeignKey(e => e.StudentProfileId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

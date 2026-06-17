using LinguaCoach.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LinguaCoach.Persistence.Configurations;

internal sealed class StudentOnboardingResponseConfiguration : IEntityTypeConfiguration<StudentOnboardingResponse>
{
    public void Configure(EntityTypeBuilder<StudentOnboardingResponse> builder)
    {
        builder.ToTable("student_onboarding_responses");

        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).HasColumnName("id");
        builder.Property(r => r.CreatedAt).HasColumnName("created_at").IsRequired()
            .HasDefaultValueSql("now()");

        builder.Property(r => r.ProgressId).HasColumnName("progress_id").IsRequired();
        builder.Property(r => r.StepKey).HasColumnName("step_key").IsRequired().HasMaxLength(100);
        builder.Property(r => r.AnswerJson).HasColumnName("answer_json").IsRequired().HasColumnType("jsonb");
        builder.Property(r => r.SubmittedAt).HasColumnName("submitted_at").IsRequired();

        // Unique per progress + step: one response per step per student (no edit history).
        builder.HasIndex(r => new { r.ProgressId, r.StepKey })
            .IsUnique()
            .HasDatabaseName("ix_student_onboarding_responses_progress_step_key");

        builder.HasIndex(r => r.ProgressId)
            .HasDatabaseName("ix_student_onboarding_responses_progress_id");
    }
}

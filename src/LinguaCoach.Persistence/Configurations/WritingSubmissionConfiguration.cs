using LinguaCoach.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LinguaCoach.Persistence.Configurations;

internal sealed class WritingSubmissionConfiguration : IEntityTypeConfiguration<WritingSubmission>
{
    public void Configure(EntityTypeBuilder<WritingSubmission> builder)
    {
        builder.ToTable("writing_submissions");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired()
            .HasDefaultValueSql("now()");

        builder.Property(e => e.StudentProfileId).HasColumnName("student_profile_id").IsRequired();
        builder.Property(e => e.ScenarioId).HasColumnName("scenario_id");

        builder.HasOne<WritingScenario>()
            .WithMany()
            .HasForeignKey(e => e.ScenarioId)
            .OnDelete(DeleteBehavior.SetNull);
        builder.Property(e => e.ScenarioTitle).HasColumnName("scenario_title").HasMaxLength(300).IsRequired();
        builder.Property(e => e.OriginalText).HasColumnName("original_text").IsRequired();
        builder.Property(e => e.CorrectedText).HasColumnName("corrected_text").IsRequired();
        builder.Property(e => e.FeedbackJson).HasColumnName("feedback_json").HasColumnType("jsonb").IsRequired();
        builder.Property(e => e.Score).HasColumnName("score");
        builder.Property(e => e.PromptKey).HasColumnName("prompt_key").HasMaxLength(200).IsRequired();

        builder.HasIndex(e => new { e.StudentProfileId, e.CreatedAt })
            .HasDatabaseName("ix_writing_submissions_student_created");

        builder.HasOne<StudentProfile>()
            .WithMany()
            .HasForeignKey(e => e.StudentProfileId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

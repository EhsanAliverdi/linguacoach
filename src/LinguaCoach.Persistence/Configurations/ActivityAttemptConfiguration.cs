using LinguaCoach.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LinguaCoach.Persistence.Configurations;

internal sealed class ActivityAttemptConfiguration : IEntityTypeConfiguration<ActivityAttempt>
{
    public void Configure(EntityTypeBuilder<ActivityAttempt> builder)
    {
        builder.ToTable("activity_attempts");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired()
            .HasDefaultValueSql("now()");

        builder.Property(e => e.StudentProfileId).HasColumnName("student_profile_id").IsRequired();
        builder.Property(e => e.LearningActivityId).HasColumnName("learning_activity_id").IsRequired();
        builder.Property(e => e.SubmittedContent).HasColumnName("submitted_content").IsRequired();
        builder.Property(e => e.AudioUrl).HasColumnName("audio_url").HasMaxLength(2000);
        builder.Property(e => e.AudioStorageKey).HasColumnName("audio_storage_key").HasMaxLength(500);
        builder.Property(e => e.FeedbackJson).HasColumnName("feedback_json")
            .HasColumnType("jsonb").IsRequired();
        builder.Property(e => e.Score).HasColumnName("score");
        builder.Property(e => e.SubmittedAnswerJson).HasColumnName("submitted_answer_json")
            .HasColumnType("jsonb");
        builder.Property(e => e.EvaluationResultJson).HasColumnName("evaluation_result_json")
            .HasColumnType("jsonb");
        builder.Property(e => e.MaxScore).HasColumnName("max_score");
        builder.Property(e => e.Percentage).HasColumnName("percentage");
        builder.Property(e => e.Passed).HasColumnName("passed");
        builder.Property(e => e.Completed).HasColumnName("completed");
        builder.Property(e => e.MarkingMode).HasColumnName("marking_mode");
        builder.Property(e => e.PromptKey).HasColumnName("prompt_key").HasMaxLength(200).IsRequired();

        builder.HasIndex(e => e.StudentProfileId)
            .HasDatabaseName("ix_activity_attempts_student");

        builder.HasIndex(e => e.LearningActivityId)
            .HasDatabaseName("ix_activity_attempts_activity");

        builder.HasOne<StudentProfile>()
            .WithMany()
            .HasForeignKey(e => e.StudentProfileId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

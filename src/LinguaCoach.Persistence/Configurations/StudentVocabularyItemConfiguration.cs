using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LinguaCoach.Persistence.Configurations;

internal sealed class StudentVocabularyItemConfiguration : IEntityTypeConfiguration<StudentVocabularyItem>
{
    public void Configure(EntityTypeBuilder<StudentVocabularyItem> builder)
    {
        builder.ToTable("student_vocabulary_items");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired()
            .HasDefaultValueSql("now()");

        builder.Property(e => e.StudentProfileId).HasColumnName("student_profile_id").IsRequired();
        builder.Property(e => e.SourceActivityAttemptId).HasColumnName("source_activity_attempt_id");
        builder.Property(e => e.SourceLearningActivityId).HasColumnName("source_learning_activity_id");

        builder.Property(e => e.Term).HasColumnName("term").HasMaxLength(300).IsRequired();
        builder.Property(e => e.SuggestedPhrase).HasColumnName("suggested_phrase").HasMaxLength(500);
        builder.Property(e => e.MeaningOrExplanation).HasColumnName("meaning_or_explanation").HasMaxLength(1000).IsRequired();
        builder.Property(e => e.ExampleSentence).HasColumnName("example_sentence").HasMaxLength(500);
        builder.Property(e => e.Category).HasColumnName("category").HasMaxLength(50).IsRequired();

        builder.Property(e => e.Status).HasColumnName("status").IsRequired()
            .HasConversion<string>();
        builder.Property(e => e.Source).HasColumnName("source").IsRequired()
            .HasConversion<string>();

        builder.Property(e => e.SeenCount).HasColumnName("seen_count").IsRequired();
        builder.Property(e => e.StrengthScore).HasColumnName("strength_score").IsRequired();
        builder.Property(e => e.LastSeenAtUtc).HasColumnName("last_seen_at_utc");
        builder.Property(e => e.NextReviewAtUtc).HasColumnName("next_review_at_utc");
        builder.Property(e => e.UpdatedAt).HasColumnName("updated_at").IsRequired();

        // Unique constraint: one entry per student + term + category
        builder.HasIndex(e => new { e.StudentProfileId, e.Term, e.Category })
            .HasDatabaseName("ix_student_vocabulary_items_student_term_cat")
            .IsUnique();

        builder.HasIndex(e => new { e.StudentProfileId, e.Status })
            .HasDatabaseName("ix_student_vocabulary_items_student_status");

        builder.HasOne<StudentProfile>()
            .WithMany()
            .HasForeignKey(e => e.StudentProfileId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

using LinguaCoach.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LinguaCoach.Persistence.Configurations;

internal sealed class VocabularyEntryConfiguration : IEntityTypeConfiguration<VocabularyEntry>
{
    public void Configure(EntityTypeBuilder<VocabularyEntry> builder)
    {
        builder.ToTable("vocabulary_entries");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired()
            .HasDefaultValueSql("now()");

        builder.Property(e => e.StudentProfileId).HasColumnName("student_profile_id").IsRequired();
        builder.Property(e => e.LanguagePairId).HasColumnName("language_pair_id").IsRequired();
        builder.Property(e => e.Word).HasColumnName("word").HasMaxLength(300).IsRequired();
        builder.Property(e => e.Definition).HasColumnName("definition").IsRequired();
        builder.Property(e => e.Status).HasColumnName("status").IsRequired();

        builder.Property(e => e.RecognitionCount).HasColumnName("recognition_count").IsRequired();
        builder.Property(e => e.RecallCount).HasColumnName("recall_count").IsRequired();
        builder.Property(e => e.UsageCount).HasColumnName("usage_count").IsRequired();
        builder.Property(e => e.ExposureCount).HasColumnName("exposure_count").IsRequired();
        builder.Property(e => e.CorrectCount).HasColumnName("correct_count").IsRequired();
        builder.Property(e => e.IncorrectCount).HasColumnName("incorrect_count").IsRequired();

        builder.Property(e => e.LastSeen).HasColumnName("last_seen");
        builder.Property(e => e.LastPractised).HasColumnName("last_practised");
        builder.Property(e => e.NextReviewDate).HasColumnName("next_review_date");

        builder.Property(e => e.EaseFactor).HasColumnName("ease_factor").IsRequired();
        builder.Property(e => e.RepetitionCount).HasColumnName("repetition_count").IsRequired();
        builder.Property(e => e.MasteryScore).HasColumnName("mastery_score").IsRequired();
        builder.Property(e => e.UpdatedAt).HasColumnName("updated_at").IsRequired();

        builder.HasIndex(e => new { e.StudentProfileId, e.LanguagePairId })
            .HasDatabaseName("ix_vocabulary_entries_student_lang");

        builder.HasIndex(e => new { e.StudentProfileId, e.Status })
            .HasDatabaseName("ix_vocabulary_entries_student_status");

        builder.HasIndex(e => new { e.StudentProfileId, e.NextReviewDate })
            .HasDatabaseName("ix_vocabulary_entries_student_next_review");

        builder.HasOne<Domain.Entities.StudentProfile>()
            .WithMany()
            .HasForeignKey(e => e.StudentProfileId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Domain.Entities.LanguagePair>()
            .WithMany()
            .HasForeignKey(e => e.LanguagePairId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

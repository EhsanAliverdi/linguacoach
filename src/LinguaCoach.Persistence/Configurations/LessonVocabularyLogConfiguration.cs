using LinguaCoach.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LinguaCoach.Persistence.Configurations;

internal sealed class LessonVocabularyLogConfiguration : IEntityTypeConfiguration<LessonVocabularyLog>
{
    public void Configure(EntityTypeBuilder<LessonVocabularyLog> builder)
    {
        builder.ToTable("lesson_vocabulary_logs");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired()
            .HasDefaultValueSql("now()");

        builder.Property(e => e.StudentProfileId).HasColumnName("student_profile_id").IsRequired();
        builder.Property(e => e.VocabularyEntryId).HasColumnName("vocabulary_entry_id").IsRequired();
        builder.Property(e => e.LessonNumber).HasColumnName("lesson_number").IsRequired();
        builder.Property(e => e.OccurredAt).HasColumnName("occurred_at").IsRequired();

        builder.HasIndex(e => new { e.StudentProfileId, e.LessonNumber })
            .HasDatabaseName("ix_lesson_vocabulary_logs_student_lesson");

        builder.HasIndex(e => new { e.StudentProfileId, e.OccurredAt })
            .HasDatabaseName("ix_lesson_vocabulary_logs_student_occurred");

        builder.HasOne<StudentProfile>()
            .WithMany()
            .HasForeignKey(e => e.StudentProfileId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<VocabularyEntry>()
            .WithMany()
            .HasForeignKey(e => e.VocabularyEntryId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

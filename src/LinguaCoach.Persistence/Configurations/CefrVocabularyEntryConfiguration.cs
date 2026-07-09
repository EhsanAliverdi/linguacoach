using LinguaCoach.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LinguaCoach.Persistence.Configurations;

internal sealed class CefrVocabularyEntryConfiguration : IEntityTypeConfiguration<CefrVocabularyEntry>
{
    public void Configure(EntityTypeBuilder<CefrVocabularyEntry> builder)
    {
        builder.ToTable("cefr_vocabulary_entries");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired()
            .HasDefaultValueSql("now()");

        builder.Property(e => e.SourceId).HasColumnName("source_id").IsRequired();
        builder.Property(e => e.Word).HasColumnName("word").HasMaxLength(200).IsRequired();
        builder.Property(e => e.CefrLevel).HasColumnName("cefr_level").HasMaxLength(10).IsRequired();
        builder.Property(e => e.PartOfSpeech).HasColumnName("part_of_speech").HasMaxLength(50);
        builder.Property(e => e.Notes).HasColumnName("notes");

        // Phase E9 — published selection metadata. Context/focus tags are stored as text (not jsonb)
        // so the query service's `.Contains("\"tag\"")` filter translates to a portable SQL LIKE on
        // both PostgreSQL and the SQLite test provider — the same proven pattern CurriculumObjective
        // uses. Nullable for backward compatibility with pre-E9 rows.
        builder.Property(e => e.Subskill).HasColumnName("subskill").HasMaxLength(128);
        builder.Property(e => e.DifficultyBand).HasColumnName("difficulty_band");
        builder.Property(e => e.ContextTagsJson).HasColumnName("context_tags_json");
        builder.Property(e => e.FocusTagsJson).HasColumnName("focus_tags_json");

        builder.HasOne<CefrResourceSource>()
            .WithMany()
            .HasForeignKey(e => e.SourceId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => new { e.Word, e.CefrLevel })
            .HasDatabaseName("ix_cefr_vocabulary_entries_word_level");
    }
}

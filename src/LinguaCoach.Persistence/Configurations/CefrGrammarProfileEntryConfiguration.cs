using LinguaCoach.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LinguaCoach.Persistence.Configurations;

internal sealed class CefrGrammarProfileEntryConfiguration : IEntityTypeConfiguration<CefrGrammarProfileEntry>
{
    public void Configure(EntityTypeBuilder<CefrGrammarProfileEntry> builder)
    {
        builder.ToTable("cefr_grammar_profile_entries");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired()
            .HasDefaultValueSql("now()");

        builder.Property(e => e.SourceId).HasColumnName("source_id").IsRequired();
        builder.Property(e => e.CefrLevel).HasColumnName("cefr_level").HasMaxLength(10).IsRequired();
        builder.Property(e => e.GrammarPoint).HasColumnName("grammar_point").HasMaxLength(200).IsRequired();
        builder.Property(e => e.Description).HasColumnName("description");

        // Phase E9 — published selection metadata (see CefrVocabularyEntryConfiguration for the
        // text-vs-jsonb rationale). Nullable for backward compatibility.
        builder.Property(e => e.Subskill).HasColumnName("subskill").HasMaxLength(128);
        builder.Property(e => e.DifficultyBand).HasColumnName("difficulty_band");
        builder.Property(e => e.ContextTagsJson).HasColumnName("context_tags_json");
        builder.Property(e => e.FocusTagsJson).HasColumnName("focus_tags_json");

        builder.HasOne<CefrResourceSource>()
            .WithMany()
            .HasForeignKey(e => e.SourceId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => new { e.CefrLevel, e.GrammarPoint })
            .HasDatabaseName("ix_cefr_grammar_profile_entries_level_point");
    }
}

using LinguaCoach.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LinguaCoach.Persistence.Configurations;

internal sealed class CefrReadingPassageConfiguration : IEntityTypeConfiguration<CefrReadingPassage>
{
    public void Configure(EntityTypeBuilder<CefrReadingPassage> builder)
    {
        builder.ToTable("cefr_reading_passages");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired()
            .HasDefaultValueSql("now()");

        builder.Property(e => e.SourceId).HasColumnName("source_id").IsRequired();
        builder.Property(e => e.Title).HasColumnName("title").HasMaxLength(500).IsRequired();
        builder.Property(e => e.PassageText).HasColumnName("passage_text").IsRequired();
        builder.Property(e => e.Summary).HasColumnName("summary");
        builder.Property(e => e.CefrLevel).HasColumnName("cefr_level").HasMaxLength(10).IsRequired();
        builder.Property(e => e.DifficultyBand).HasColumnName("difficulty_band");
        builder.Property(e => e.PrimarySkill).HasColumnName("primary_skill").HasMaxLength(100).IsRequired();
        builder.Property(e => e.Subskill).HasColumnName("subskill").HasMaxLength(128);
        builder.Property(e => e.TopicTagsJson).HasColumnName("topic_tags_json").HasColumnType("jsonb");
        builder.Property(e => e.ContextTagsJson).HasColumnName("context_tags_json").HasColumnType("jsonb");
        builder.Property(e => e.FocusTagsJson).HasColumnName("focus_tags_json").HasColumnType("jsonb");
        builder.Property(e => e.WordCount).HasColumnName("word_count").IsRequired();
        builder.Property(e => e.EstimatedReadingMinutes).HasColumnName("estimated_reading_minutes").IsRequired();
        builder.Property(e => e.AttributionText).HasColumnName("attribution_text");
        builder.Property(e => e.ContentFingerprint).HasColumnName("content_fingerprint").HasMaxLength(200);
        builder.Property(e => e.QualityScore).HasColumnName("quality_score");
        builder.Property(e => e.UpdatedAtUtc).HasColumnName("updated_at_utc").IsRequired();

        builder.HasOne<CefrResourceSource>()
            .WithMany()
            .HasForeignKey(e => e.SourceId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => e.CefrLevel)
            .HasDatabaseName("ix_cefr_reading_passages_level");
        builder.HasIndex(e => e.SourceId)
            .HasDatabaseName("ix_cefr_reading_passages_source");
        builder.HasIndex(e => e.ContentFingerprint)
            .HasDatabaseName("ix_cefr_reading_passages_fingerprint");
        builder.HasIndex(e => e.CreatedAt)
            .HasDatabaseName("ix_cefr_reading_passages_created_at");
    }
}

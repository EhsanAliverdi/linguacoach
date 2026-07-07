using LinguaCoach.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LinguaCoach.Persistence.Configurations;

internal sealed class CefrReadingReferenceConfiguration : IEntityTypeConfiguration<CefrReadingReference>
{
    public void Configure(EntityTypeBuilder<CefrReadingReference> builder)
    {
        builder.ToTable("cefr_reading_references");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired()
            .HasDefaultValueSql("now()");

        builder.Property(e => e.SourceId).HasColumnName("source_id").IsRequired();
        builder.Property(e => e.CefrLevel).HasColumnName("cefr_level").HasMaxLength(10).IsRequired();
        builder.Property(e => e.TextType).HasColumnName("text_type").HasMaxLength(100);
        builder.Property(e => e.DifficultyNotes).HasColumnName("difficulty_notes");
        builder.Property(e => e.ReferenceExcerpt).HasColumnName("reference_excerpt");

        builder.HasOne<CefrResourceSource>()
            .WithMany()
            .HasForeignKey(e => e.SourceId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => e.CefrLevel)
            .HasDatabaseName("ix_cefr_reading_references_level");
    }
}

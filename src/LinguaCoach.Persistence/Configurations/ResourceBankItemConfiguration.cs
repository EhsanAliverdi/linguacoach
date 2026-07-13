using LinguaCoach.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LinguaCoach.Persistence.Configurations;

internal sealed class ResourceBankItemConfiguration : IEntityTypeConfiguration<ResourceBankItem>
{
    public void Configure(EntityTypeBuilder<ResourceBankItem> builder)
    {
        builder.ToTable("resource_bank_items");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired()
            .HasDefaultValueSql("now()");

        builder.Property(e => e.Type).HasColumnName("type").IsRequired();
        builder.Property(e => e.SourceId).HasColumnName("source_id").IsRequired();
        builder.Property(e => e.CefrLevel).HasColumnName("cefr_level").HasMaxLength(10).IsRequired();
        builder.Property(e => e.Subskill).HasColumnName("subskill").HasMaxLength(128);
        builder.Property(e => e.DifficultyBand).HasColumnName("difficulty_band");
        // Plain text, not jsonb — matches the 3-of-4 legacy tables' convention so the portable
        // .Contains LIKE-filter pattern works identically on PostgreSQL and SQLite-in-memory tests.
        builder.Property(e => e.ContextTagsJson).HasColumnName("context_tags_json");
        builder.Property(e => e.FocusTagsJson).HasColumnName("focus_tags_json");
        builder.Property(e => e.ContentFingerprint).HasColumnName("content_fingerprint").HasMaxLength(200);
        builder.Property(e => e.ContentJson).HasColumnName("content_json").IsRequired();
        builder.Property(e => e.UpdatedAt).HasColumnName("updated_at");
        builder.Property(e => e.IsArchived).HasColumnName("is_archived").IsRequired().HasDefaultValue(false);

        builder.HasOne<CefrResourceSource>()
            .WithMany()
            .HasForeignKey(e => e.SourceId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => e.CefrLevel).HasDatabaseName("ix_resource_bank_items_level");
        builder.HasIndex(e => e.SourceId).HasDatabaseName("ix_resource_bank_items_source");
        builder.HasIndex(e => e.ContentFingerprint).HasDatabaseName("ix_resource_bank_items_fingerprint");
        builder.HasIndex(e => e.CreatedAt).HasDatabaseName("ix_resource_bank_items_created_at");
        builder.HasIndex(e => new { e.Type, e.CefrLevel }).HasDatabaseName("ix_resource_bank_items_type_level");
    }
}

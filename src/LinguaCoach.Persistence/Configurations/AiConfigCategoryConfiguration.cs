using LinguaCoach.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LinguaCoach.Persistence.Configurations;

internal sealed class AiConfigCategoryConfiguration : IEntityTypeConfiguration<AiConfigCategory>
{
    public void Configure(EntityTypeBuilder<AiConfigCategory> builder)
    {
        builder.ToTable("ai_config_categories");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired()
            .HasDefaultValueSql("now()");

        builder.Property(e => e.CategoryKey).HasColumnName("category_key").HasMaxLength(100).IsRequired();
        builder.Property(e => e.DisplayName).HasColumnName("display_name").HasMaxLength(200).IsRequired();
        builder.Property(e => e.ProviderName).HasColumnName("provider_name").HasMaxLength(50).IsRequired(false);
        builder.Property(e => e.ModelName).HasColumnName("model_name").HasMaxLength(200).IsRequired(false);
        builder.Property(e => e.VoiceName).HasColumnName("voice_name").HasMaxLength(100).IsRequired(false);
        builder.Property(e => e.UpdatedAt).HasColumnName("updated_at").IsRequired();

        builder.HasIndex(e => e.CategoryKey)
            .IsUnique()
            .HasDatabaseName("ix_ai_config_categories_category_key");
    }
}

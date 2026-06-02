using LinguaCoach.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LinguaCoach.Persistence.Configurations;

internal sealed class AiProviderConfigConfiguration : IEntityTypeConfiguration<AiProviderConfig>
{
    public void Configure(EntityTypeBuilder<AiProviderConfig> builder)
    {
        builder.ToTable("ai_provider_configs");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired()
            .HasDefaultValueSql("now()");

        builder.Property(e => e.FeatureKey).HasColumnName("feature_key").HasMaxLength(100).IsRequired();
        builder.Property(e => e.ProviderName).HasColumnName("provider_name").HasMaxLength(50).IsRequired();
        builder.Property(e => e.ModelName).HasColumnName("model_name").HasMaxLength(100).IsRequired();
        // Nullable — null means "fall back to environment variable". Stored as text; encrypt at the infrastructure layer.
        builder.Property(e => e.ApiKey).HasColumnName("api_key").HasMaxLength(500).IsRequired(false);
        builder.Property(e => e.UpdatedAt).HasColumnName("updated_at").IsRequired();

        builder.HasIndex(e => e.FeatureKey)
            .IsUnique()
            .HasDatabaseName("ix_ai_provider_configs_feature_key");
    }
}

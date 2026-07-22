using LinguaCoach.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LinguaCoach.Persistence.Configurations;

internal sealed class AiModelPricingOverrideConfiguration : IEntityTypeConfiguration<AiModelPricingOverride>
{
    public void Configure(EntityTypeBuilder<AiModelPricingOverride> builder)
    {
        builder.ToTable("ai_model_pricing_overrides");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired()
            .HasDefaultValueSql("now()");

        builder.Property(e => e.ProviderName).HasColumnName("provider_name").HasMaxLength(100).IsRequired();
        builder.Property(e => e.ModelName).HasColumnName("model_name").HasMaxLength(200).IsRequired();
        builder.Property(e => e.InputPricePer1KTokens).HasColumnName("input_price_per_1k_tokens")
            .HasColumnType("numeric(12,8)").IsRequired();
        builder.Property(e => e.OutputPricePer1KTokens).HasColumnName("output_price_per_1k_tokens")
            .HasColumnType("numeric(12,8)").IsRequired();
        builder.Property(e => e.InputPricePer1KCharacters).HasColumnName("input_price_per_1k_characters")
            .HasColumnType("numeric(12,8)").IsRequired(false);
        builder.Property(e => e.Currency).HasColumnName("currency").HasMaxLength(10).IsRequired();
        builder.Property(e => e.IsActive).HasColumnName("is_active").IsRequired();
        builder.Property(e => e.EffectiveFromUtc).HasColumnName("effective_from_utc").IsRequired();
        builder.Property(e => e.EffectiveToUtc).HasColumnName("effective_to_utc").IsRequired(false);
        builder.Property(e => e.Notes).HasColumnName("notes").HasMaxLength(500).IsRequired(false);
        builder.Property(e => e.UpdatedAtUtc).HasColumnName("updated_at_utc").IsRequired(false);
        builder.Property(e => e.CreatedByAdminUserId).HasColumnName("created_by_admin_user_id").IsRequired(false);
        builder.Property(e => e.UpdatedByAdminUserId).HasColumnName("updated_by_admin_user_id").IsRequired(false);

        builder.HasIndex(e => new { e.ProviderName, e.ModelName })
            .HasDatabaseName("ix_ai_model_pricing_overrides_provider_model");
        builder.HasIndex(e => new { e.IsActive, e.EffectiveFromUtc })
            .HasDatabaseName("ix_ai_model_pricing_overrides_active_from");
    }
}

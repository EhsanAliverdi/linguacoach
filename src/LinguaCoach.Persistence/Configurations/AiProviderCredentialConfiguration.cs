using LinguaCoach.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LinguaCoach.Persistence.Configurations;

internal sealed class AiProviderCredentialConfiguration : IEntityTypeConfiguration<AiProviderCredential>
{
    public void Configure(EntityTypeBuilder<AiProviderCredential> builder)
    {
        builder.ToTable("ai_provider_credentials");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired()
            .HasDefaultValueSql("now()");

        builder.Property(e => e.ProviderName).HasColumnName("provider_name").HasMaxLength(50).IsRequired();
        builder.Property(e => e.ApiKey).HasColumnName("api_key").HasMaxLength(500).IsRequired(false);
        builder.Property(e => e.UpdatedAt).HasColumnName("updated_at").IsRequired();

        // Per-model test results stored as JSON. text works on both PostgreSQL and SQLite.
        builder.Property(e => e.ModelTests)
            .HasColumnName("model_tests")
            .HasColumnType("text")
            .HasConversion(
                v => System.Text.Json.JsonSerializer.Serialize(v, (System.Text.Json.JsonSerializerOptions?)null),
                v => System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, ModelTestResult>>(v, (System.Text.Json.JsonSerializerOptions?)null)
                     ?? new Dictionary<string, ModelTestResult>());

        builder.HasIndex(e => e.ProviderName)
            .IsUnique()
            .HasDatabaseName("ix_ai_provider_credentials_provider_name");
    }
}

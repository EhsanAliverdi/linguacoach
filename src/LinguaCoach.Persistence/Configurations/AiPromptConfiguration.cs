using LinguaCoach.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LinguaCoach.Persistence.Configurations;

internal sealed class AiPromptConfiguration : IEntityTypeConfiguration<AiPrompt>
{
    public void Configure(EntityTypeBuilder<AiPrompt> builder)
    {
        builder.ToTable("ai_prompts");

        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).HasColumnName("id");
        builder.Property(p => p.CreatedAt).HasColumnName("created_at").IsRequired()
            .HasDefaultValueSql("now()");

        builder.Property(p => p.Key)
            .HasColumnName("key")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(p => p.Content)
            .HasColumnName("content")
            .IsRequired();

        builder.Property(p => p.Version).HasColumnName("version").IsRequired();
        builder.Property(p => p.IsActive).HasColumnName("is_active").IsRequired();

        builder.HasIndex(p => new { p.Key, p.Version })
            .IsUnique()
            .HasDatabaseName("ix_ai_prompts_key_version");
    }
}

using LinguaCoach.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LinguaCoach.Persistence.Configurations;

internal sealed class LanguagePairConfiguration : IEntityTypeConfiguration<LanguagePair>
{
    public void Configure(EntityTypeBuilder<LanguagePair> builder)
    {
        builder.ToTable("language_pairs");

        builder.HasKey(lp => lp.Id);
        builder.Property(lp => lp.Id).HasColumnName("id");
        builder.Property(lp => lp.CreatedAt).HasColumnName("created_at").IsRequired()
            .HasDefaultValueSql("now()");

        builder.Property(lp => lp.SourceLanguageId).HasColumnName("source_language_id").IsRequired();
        builder.Property(lp => lp.TargetLanguageId).HasColumnName("target_language_id").IsRequired();
        builder.Property(lp => lp.IsActive).HasColumnName("is_active").IsRequired();

        builder.HasOne(lp => lp.SourceLanguage)
            .WithMany()
            .HasForeignKey(lp => lp.SourceLanguageId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(lp => lp.TargetLanguage)
            .WithMany()
            .HasForeignKey(lp => lp.TargetLanguageId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(lp => lp.TargetLanguageId)
            .HasDatabaseName("ix_language_pairs_target_language_id");

        builder.HasIndex(lp => new { lp.SourceLanguageId, lp.TargetLanguageId })
            .IsUnique()
            .HasDatabaseName("ix_language_pairs_source_target");

        builder.HasData(new
        {
            Id = Seed.SeedData.FaEnPairId,
            SourceLanguageId = Seed.SeedData.PersianId,
            TargetLanguageId = Seed.SeedData.EnglishId,
            IsActive = true,
            CreatedAt = Seed.SeedData.SeedDate
        });
    }
}

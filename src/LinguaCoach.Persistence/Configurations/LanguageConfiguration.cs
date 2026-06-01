using LinguaCoach.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LinguaCoach.Persistence.Configurations;

internal sealed class LanguageConfiguration : IEntityTypeConfiguration<Language>
{
    public void Configure(EntityTypeBuilder<Language> builder)
    {
        builder.ToTable("languages");

        builder.HasKey(l => l.Id);
        builder.Property(l => l.Id).HasColumnName("id");
        builder.Property(l => l.CreatedAt).HasColumnName("created_at").IsRequired()
            .HasDefaultValueSql("now()");

        builder.Property(l => l.Code)
            .HasColumnName("code")
            .HasMaxLength(2)
            .IsRequired();

        builder.Property(l => l.Name)
            .HasColumnName("name")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(l => l.Direction)
            .HasColumnName("direction")
            .IsRequired();

        builder.HasIndex(l => l.Code).IsUnique().HasDatabaseName("ix_languages_code");

        builder.HasData(
            new { Id = Seed.SeedData.PersianId, Code = "fa", Name = "Persian", Direction = Domain.Enums.LanguageDirection.Rtl, CreatedAt = Seed.SeedData.SeedDate },
            new { Id = Seed.SeedData.EnglishId, Code = "en", Name = "English", Direction = Domain.Enums.LanguageDirection.Ltr, CreatedAt = Seed.SeedData.SeedDate }
        );
    }
}

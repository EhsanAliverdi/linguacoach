using LinguaCoach.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LinguaCoach.Persistence.Configurations;

internal sealed class CareerProfileConfiguration : IEntityTypeConfiguration<CareerProfile>
{
    public void Configure(EntityTypeBuilder<CareerProfile> builder)
    {
        builder.ToTable("career_profiles");

        builder.HasKey(cp => cp.Id);
        builder.Property(cp => cp.Id).HasColumnName("id");
        builder.Property(cp => cp.CreatedAt).HasColumnName("created_at").IsRequired()
            .HasDefaultValueSql("now()");

        builder.Property(cp => cp.Name)
            .HasColumnName("name")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(cp => cp.Description)
            .HasColumnName("description")
            .HasMaxLength(1000)
            .IsRequired();

        builder.Property(cp => cp.LanguagePairId).HasColumnName("language_pair_id").IsRequired();

        builder.HasOne(cp => cp.LanguagePair)
            .WithMany()
            .HasForeignKey(cp => cp.LanguagePairId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(cp => cp.LanguagePairId)
            .HasDatabaseName("ix_career_profiles_language_pair_id");

        builder.HasData(new
        {
            Id = Seed.SeedData.DocumentControllerProfileId,
            Name = "Document Controller",
            Description = "Role-specific English for document control professionals in construction and engineering projects.",
            LanguagePairId = Seed.SeedData.FaEnPairId,
            CreatedAt = Seed.SeedData.SeedDate
        });
    }
}

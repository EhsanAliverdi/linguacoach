using LinguaCoach.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LinguaCoach.Persistence.Configurations;

internal sealed class LearningTrackConfiguration : IEntityTypeConfiguration<LearningTrack>
{
    public void Configure(EntityTypeBuilder<LearningTrack> builder)
    {
        builder.ToTable("learning_tracks");

        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).HasColumnName("id");
        builder.Property(t => t.CreatedAt).HasColumnName("created_at").IsRequired()
            .HasDefaultValueSql("now()");

        builder.Property(t => t.Name)
            .HasColumnName("name")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(t => t.Description)
            .HasColumnName("description")
            .HasMaxLength(1000)
            .IsRequired();

        builder.Property(t => t.LanguagePairId).HasColumnName("language_pair_id").IsRequired();

        builder.HasOne(t => t.LanguagePair)
            .WithMany()
            .HasForeignKey(t => t.LanguagePairId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(t => t.LanguagePairId)
            .HasDatabaseName("ix_learning_tracks_language_pair_id");

        builder.HasData(new
        {
            Id = Seed.SeedData.WorkplaceEnglishTrackId,
            Name = "Workplace English",
            Description = "Role-specific English for professional workplace communication.",
            LanguagePairId = Seed.SeedData.FaEnPairId,
            CreatedAt = Seed.SeedData.SeedDate
        });
    }
}

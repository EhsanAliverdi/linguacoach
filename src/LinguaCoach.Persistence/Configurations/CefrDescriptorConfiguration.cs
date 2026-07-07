using LinguaCoach.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LinguaCoach.Persistence.Configurations;

internal sealed class CefrDescriptorConfiguration : IEntityTypeConfiguration<CefrDescriptor>
{
    public void Configure(EntityTypeBuilder<CefrDescriptor> builder)
    {
        builder.ToTable("cefr_descriptors");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired()
            .HasDefaultValueSql("now()");

        builder.Property(e => e.SourceId).HasColumnName("source_id").IsRequired();
        builder.Property(e => e.CefrLevel).HasColumnName("cefr_level").HasMaxLength(10).IsRequired();
        builder.Property(e => e.Skill).HasColumnName("skill").HasMaxLength(100).IsRequired();
        builder.Property(e => e.Subskill).HasColumnName("subskill").HasMaxLength(128);
        builder.Property(e => e.CanDoStatement).HasColumnName("can_do_statement").IsRequired();
        builder.Property(e => e.Citation).HasColumnName("citation").HasMaxLength(500);

        builder.HasOne<CefrResourceSource>()
            .WithMany()
            .HasForeignKey(e => e.SourceId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => new { e.CefrLevel, e.Skill })
            .HasDatabaseName("ix_cefr_descriptors_level_skill");
    }
}

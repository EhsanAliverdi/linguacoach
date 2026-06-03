using LinguaCoach.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LinguaCoach.Persistence.Configurations;

internal sealed class LearningModuleConfiguration : IEntityTypeConfiguration<LearningModule>
{
    public void Configure(EntityTypeBuilder<LearningModule> builder)
    {
        builder.ToTable("learning_modules");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired()
            .HasDefaultValueSql("now()");

        builder.Property(e => e.LearningPathId).HasColumnName("learning_path_id").IsRequired();
        builder.Property(e => e.Title).HasColumnName("title").HasMaxLength(500).IsRequired();
        builder.Property(e => e.Description).HasColumnName("description").IsRequired();
        builder.Property(e => e.Order).HasColumnName("order").IsRequired();

        builder.HasIndex(e => new { e.LearningPathId, e.Order })
            .HasDatabaseName("ix_learning_modules_path_order");

        builder.HasMany(e => e.Activities)
            .WithOne()
            .HasForeignKey(e => e.LearningModuleId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

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
        builder.Property(e => e.FocusSkill).HasColumnName("focus_skill").HasMaxLength(200).IsRequired(false);
        builder.Property(e => e.Reason).HasColumnName("reason").IsRequired(false);
        builder.Property(e => e.Difficulty).HasColumnName("difficulty").HasMaxLength(50).IsRequired(false);
        builder.Property(e => e.FingerprintJson).HasColumnName("fingerprint_json").IsRequired(false);
        builder.Property(e => e.Order).HasColumnName("order").IsRequired();
        builder.Property(e => e.CompletedAt).HasColumnName("completed_at").IsRequired(false);

        builder.HasIndex(e => new { e.LearningPathId, e.Order })
            .HasDatabaseName("ix_learning_modules_path_order");

        builder.HasMany(e => e.Activities)
            .WithOne()
            .HasForeignKey(e => e.LearningModuleId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

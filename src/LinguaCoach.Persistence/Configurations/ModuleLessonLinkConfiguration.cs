using LinguaCoach.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LinguaCoach.Persistence.Configurations;

internal sealed class ModuleLessonLinkConfiguration : IEntityTypeConfiguration<ModuleLessonLink>
{
    public void Configure(EntityTypeBuilder<ModuleLessonLink> builder)
    {
        builder.ToTable("module_lesson_links");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired()
            .HasDefaultValueSql("now()");

        builder.Property(e => e.ModuleId).HasColumnName("module_id").IsRequired();
        builder.Property(e => e.LessonId).HasColumnName("lesson_id").IsRequired();
        builder.Property(e => e.Role).HasColumnName("role").HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(e => e.SortOrder).HasColumnName("sort_order").IsRequired();
        builder.Property(e => e.SnapshotTitle).HasColumnName("snapshot_title").HasMaxLength(500);

        builder.HasOne<Module>()
            .WithMany()
            .HasForeignKey(e => e.ModuleId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(e => e.ModuleId).HasDatabaseName("ix_module_lesson_links_module");
        builder.HasIndex(e => e.LessonId).HasDatabaseName("ix_module_lesson_links_lesson");
    }
}

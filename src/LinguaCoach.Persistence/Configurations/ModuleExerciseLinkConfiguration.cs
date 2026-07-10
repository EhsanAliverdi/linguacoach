using LinguaCoach.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LinguaCoach.Persistence.Configurations;

internal sealed class ModuleExerciseLinkConfiguration : IEntityTypeConfiguration<ModuleExerciseLink>
{
    public void Configure(EntityTypeBuilder<ModuleExerciseLink> builder)
    {
        builder.ToTable("module_exercise_links");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired()
            .HasDefaultValueSql("now()");

        builder.Property(e => e.ModuleId).HasColumnName("module_id").IsRequired();
        builder.Property(e => e.ExerciseId).HasColumnName("exercise_id").IsRequired();
        builder.Property(e => e.Role).HasColumnName("role").HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(e => e.SortOrder).HasColumnName("sort_order").IsRequired();
        builder.Property(e => e.Required).HasColumnName("required").IsRequired().HasDefaultValue(true);
        builder.Property(e => e.SnapshotTitle).HasColumnName("snapshot_title").HasMaxLength(500);

        builder.HasOne<Module>()
            .WithMany()
            .HasForeignKey(e => e.ModuleId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(e => e.ModuleId).HasDatabaseName("ix_module_exercise_links_module");
        builder.HasIndex(e => e.ExerciseId).HasDatabaseName("ix_module_exercise_links_activity");
    }
}

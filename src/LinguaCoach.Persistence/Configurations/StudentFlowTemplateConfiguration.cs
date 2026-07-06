using LinguaCoach.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LinguaCoach.Persistence.Configurations;

internal sealed class StudentFlowTemplateConfiguration : IEntityTypeConfiguration<StudentFlowTemplate>
{
    public void Configure(EntityTypeBuilder<StudentFlowTemplate> builder)
    {
        builder.ToTable("student_flow_templates");

        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).HasColumnName("id");
        builder.Property(t => t.CreatedAt).HasColumnName("created_at").IsRequired().HasDefaultValueSql("now()");

        builder.Property(t => t.FlowKind).HasColumnName("flow_kind").HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(t => t.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
        builder.Property(t => t.Description).HasColumnName("description").HasMaxLength(1000);
        builder.Property(t => t.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(t => t.ActiveVersionId).HasColumnName("active_version_id");
        builder.Property(t => t.UpdatedAt).HasColumnName("updated_at").IsRequired();

        builder.HasIndex(t => t.FlowKind).HasDatabaseName("ix_student_flow_templates_flow_kind");

        builder.HasMany(t => t.Versions)
            .WithOne(v => v.Template)
            .HasForeignKey(v => v.TemplateId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

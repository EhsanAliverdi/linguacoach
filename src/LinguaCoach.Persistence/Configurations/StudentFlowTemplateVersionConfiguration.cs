using LinguaCoach.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LinguaCoach.Persistence.Configurations;

internal sealed class StudentFlowTemplateVersionConfiguration : IEntityTypeConfiguration<StudentFlowTemplateVersion>
{
    public void Configure(EntityTypeBuilder<StudentFlowTemplateVersion> builder)
    {
        builder.ToTable("student_flow_template_versions");

        builder.HasKey(v => v.Id);
        builder.Property(v => v.Id).HasColumnName("id");
        builder.Property(v => v.CreatedAt).HasColumnName("created_at").IsRequired().HasDefaultValueSql("now()");

        builder.Property(v => v.TemplateId).HasColumnName("template_id").IsRequired();
        builder.Property(v => v.VersionNumber).HasColumnName("version_number").IsRequired();
        builder.Property(v => v.FormIoSchemaJson).HasColumnName("form_io_schema_json").HasColumnType("jsonb").IsRequired();
        builder.Property(v => v.ScoringRulesJson).HasColumnName("scoring_rules_json").HasColumnType("jsonb");
        builder.Property(v => v.RendererKind).HasColumnName("renderer_kind").HasConversion<string>().HasMaxLength(20).IsRequired()
            .HasDefaultValue(LinguaCoach.Domain.Enums.FormRendererKind.FormIo);
        builder.Property(v => v.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(v => v.CreatedByAdminId).HasColumnName("created_by_admin_id").IsRequired();
        builder.Property(v => v.PublishedAt).HasColumnName("published_at");
        builder.Property(v => v.UpdatedAt).HasColumnName("updated_at").IsRequired();

        builder.HasIndex(v => new { v.TemplateId, v.VersionNumber })
            .IsUnique()
            .HasDatabaseName("ix_student_flow_template_versions_template_version");

        builder.HasIndex(v => new { v.TemplateId, v.Status })
            .HasDatabaseName("ix_student_flow_template_versions_template_status");
    }
}

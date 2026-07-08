using LinguaCoach.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LinguaCoach.Persistence.Configurations;

internal sealed class ResourceImportRunConfiguration : IEntityTypeConfiguration<ResourceImportRun>
{
    public void Configure(EntityTypeBuilder<ResourceImportRun> builder)
    {
        builder.ToTable("resource_import_runs");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired()
            .HasDefaultValueSql("now()");

        builder.Property(e => e.CefrResourceSourceId).HasColumnName("cefr_resource_source_id").IsRequired();
        builder.Property(e => e.StartedAtUtc).HasColumnName("started_at_utc").IsRequired();
        builder.Property(e => e.CompletedAtUtc).HasColumnName("completed_at_utc");
        builder.Property(e => e.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(e => e.ImportedByUserId).HasColumnName("imported_by_user_id");
        builder.Property(e => e.ImportMode).HasColumnName("import_mode").HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(e => e.FileName).HasColumnName("file_name").HasMaxLength(300).IsRequired();
        builder.Property(e => e.FileHash).HasColumnName("file_hash").HasMaxLength(128).IsRequired();
        builder.Property(e => e.SourceVersion).HasColumnName("source_version").HasMaxLength(100);
        builder.Property(e => e.ParserVersion).HasColumnName("parser_version").HasMaxLength(20).IsRequired();
        builder.Property(e => e.AiModelUsed).HasColumnName("ai_model_used").HasMaxLength(100);
        builder.Property(e => e.TotalRecordCount).HasColumnName("total_record_count").IsRequired().HasDefaultValue(0);
        builder.Property(e => e.SucceededCount).HasColumnName("succeeded_count").IsRequired().HasDefaultValue(0);
        builder.Property(e => e.RejectedCount).HasColumnName("rejected_count").IsRequired().HasDefaultValue(0);
        builder.Property(e => e.WarningCount).HasColumnName("warning_count").IsRequired().HasDefaultValue(0);
        builder.Property(e => e.ErrorSummary).HasColumnName("error_summary");
        builder.Property(e => e.Notes).HasColumnName("notes");

        builder.HasOne<CefrResourceSource>()
            .WithMany()
            .HasForeignKey(e => e.CefrResourceSourceId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => e.CefrResourceSourceId).HasDatabaseName("ix_resource_import_runs_source");
        builder.HasIndex(e => e.Status).HasDatabaseName("ix_resource_import_runs_status");
    }
}

using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LinguaCoach.Persistence.Configurations;

internal sealed class NotificationTemplateConfiguration : IEntityTypeConfiguration<NotificationTemplate>
{
    public void Configure(EntityTypeBuilder<NotificationTemplate> builder)
    {
        builder.ToTable("notification_templates");

        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).HasColumnName("id");
        builder.Property(t => t.CreatedAt).HasColumnName("created_at").IsRequired()
            .HasDefaultValueSql("now()");

        builder.Property(t => t.TemplateKey).HasColumnName("template_key").HasMaxLength(100).IsRequired();
        builder.Property(t => t.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
        builder.Property(t => t.Subject).HasColumnName("subject").HasMaxLength(300).IsRequired(false);
        builder.Property(t => t.Title).HasColumnName("title").HasMaxLength(200).IsRequired(false);
        builder.Property(t => t.Body).HasColumnName("body").HasMaxLength(4000).IsRequired();
        builder.Property(t => t.IsActive).HasColumnName("is_active").IsRequired();
        builder.Property(t => t.Version).HasColumnName("version").IsRequired();
        builder.Property(t => t.SupportedVariablesJson).HasColumnName("supported_variables_json").HasMaxLength(2000).IsRequired(false);
        builder.Property(t => t.Description).HasColumnName("description").HasMaxLength(500).IsRequired(false);
        builder.Property(t => t.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();
        builder.Property(t => t.UpdatedAtUtc).HasColumnName("updated_at_utc").IsRequired(false);

        builder.Property(t => t.Channel)
            .HasColumnName("channel")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(t => t.Category)
            .HasColumnName("category")
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(t => t.Severity)
            .HasColumnName("severity")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.HasIndex(t => new { t.TemplateKey, t.Channel, t.IsActive })
            .HasDatabaseName("ix_notification_templates_key_channel_active");

        builder.HasIndex(t => t.IsActive)
            .HasDatabaseName("ix_notification_templates_active");
    }
}

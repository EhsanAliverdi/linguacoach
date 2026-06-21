using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LinguaCoach.Persistence.Configurations;

internal sealed class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.ToTable("notifications");

        builder.HasKey(n => n.Id);
        builder.Property(n => n.Id).HasColumnName("id");
        builder.Property(n => n.CreatedAt).HasColumnName("created_at").IsRequired()
            .HasDefaultValueSql("now()");

        builder.Property(n => n.RecipientUserId).HasColumnName("recipient_user_id").IsRequired();

        builder.Property(n => n.Title).HasColumnName("title").HasMaxLength(200).IsRequired();
        builder.Property(n => n.Body).HasColumnName("body").HasMaxLength(2000).IsRequired();

        builder.Property(n => n.Category)
            .HasColumnName("category")
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(n => n.Severity)
            .HasColumnName("severity")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(n => n.Channel)
            .HasColumnName("channel")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(n => n.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(n => n.DeepLinkUrl).HasColumnName("deep_link_url").HasMaxLength(500).IsRequired(false);
        builder.Property(n => n.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();
        builder.Property(n => n.ReadAtUtc).HasColumnName("read_at_utc").IsRequired(false);
        builder.Property(n => n.ExpiresAtUtc).HasColumnName("expires_at_utc").IsRequired(false);
        builder.Property(n => n.MetadataJson).HasColumnName("metadata_json").HasMaxLength(4000).IsRequired(false);

        builder.HasIndex(n => new { n.RecipientUserId, n.CreatedAtUtc })
            .HasDatabaseName("ix_notifications_recipient_created");
        builder.HasIndex(n => new { n.RecipientUserId, n.ReadAtUtc })
            .HasDatabaseName("ix_notifications_recipient_read");
        builder.HasIndex(n => new { n.Channel, n.Status })
            .HasDatabaseName("ix_notifications_channel_status");
    }
}

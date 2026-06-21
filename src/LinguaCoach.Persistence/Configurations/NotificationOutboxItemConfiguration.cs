using LinguaCoach.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LinguaCoach.Persistence.Configurations;

internal sealed class NotificationOutboxItemConfiguration : IEntityTypeConfiguration<NotificationOutboxItem>
{
    public void Configure(EntityTypeBuilder<NotificationOutboxItem> builder)
    {
        builder.ToTable("notification_outbox_items");

        builder.HasKey(o => o.Id);
        builder.Property(o => o.Id).HasColumnName("id");
        builder.Property(o => o.CreatedAt).HasColumnName("created_at").IsRequired()
            .HasDefaultValueSql("now()");

        builder.Property(o => o.NotificationId).HasColumnName("notification_id").IsRequired(false);
        builder.Property(o => o.RecipientUserId).HasColumnName("recipient_user_id").IsRequired();

        builder.Property(o => o.Channel)
            .HasColumnName("channel")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(o => o.PayloadJson).HasColumnName("payload_json").IsRequired();

        builder.Property(o => o.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(o => o.AttemptCount).HasColumnName("attempt_count").IsRequired();
        builder.Property(o => o.NextAttemptAtUtc).HasColumnName("next_attempt_at_utc").IsRequired(false);
        builder.Property(o => o.LastAttemptAtUtc).HasColumnName("last_attempt_at_utc").IsRequired(false);
        builder.Property(o => o.LastError).HasColumnName("last_error").HasMaxLength(1000).IsRequired(false);
        builder.Property(o => o.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();
        builder.Property(o => o.ProcessedAtUtc).HasColumnName("processed_at_utc").IsRequired(false);

        builder.HasIndex(o => new { o.Status, o.NextAttemptAtUtc })
            .HasDatabaseName("ix_notification_outbox_status_next_attempt");
        builder.HasIndex(o => o.RecipientUserId)
            .HasDatabaseName("ix_notification_outbox_recipient");
        builder.HasIndex(o => o.NotificationId)
            .HasDatabaseName("ix_notification_outbox_notification_id");
    }
}

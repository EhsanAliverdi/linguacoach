using LinguaCoach.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LinguaCoach.Persistence.Configurations;

public sealed class NotificationChannelConfigConfiguration
    : IEntityTypeConfiguration<NotificationChannelConfig>
{
    public void Configure(EntityTypeBuilder<NotificationChannelConfig> builder)
    {
        builder.ToTable("NotificationChannelConfigs");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Channel).IsRequired().HasMaxLength(32);
        builder.HasIndex(x => x.Channel).IsUnique();

        builder.Property(x => x.Provider).HasMaxLength(64);
        builder.Property(x => x.FromAddress).HasMaxLength(256);
        builder.Property(x => x.FromDisplayName).HasMaxLength(128);
        builder.Property(x => x.Host).HasMaxLength(256);
        builder.Property(x => x.Username).HasMaxLength(256);
        builder.Property(x => x.SenderId).HasMaxLength(64);

        // Encrypted secret — long enough for any AES-256 + base64 value.
        builder.Property(x => x.SecretEncrypted).HasMaxLength(1024);

        builder.Property(x => x.CreatedAtUtc).IsRequired();
        builder.Property(x => x.UpdatedAtUtc);
        builder.Property(x => x.UpdatedByAdminUserId);
    }
}

using LinguaCoach.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LinguaCoach.Persistence.Configurations;

public sealed class UserRefreshTokenConfiguration : IEntityTypeConfiguration<UserRefreshToken>
{
    public void Configure(EntityTypeBuilder<UserRefreshToken> builder)
    {
        builder.ToTable("UserRefreshTokens");
        builder.HasKey(t => t.Id);

        builder.Property(t => t.TokenHash).HasMaxLength(64).IsRequired();
        builder.Property(t => t.IpAddress).HasMaxLength(64);
        builder.Property(t => t.UserAgent).HasMaxLength(512);
        builder.Property(t => t.DeviceDescription).HasMaxLength(256);
        builder.Property(t => t.CorrelationId).HasMaxLength(64);
        builder.Property(t => t.RevocationReason).HasMaxLength(64);

        // Fast lookup by hash — primary query path for refresh/logout
        builder.HasIndex(t => t.TokenHash).IsUnique();
        // Active session queries by user
        builder.HasIndex(t => new { t.UserId, t.RevokedAtUtc });
    }
}

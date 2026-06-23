using LinguaCoach.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LinguaCoach.Persistence.Configurations;

public sealed class AuthSecurityEventConfiguration : IEntityTypeConfiguration<AuthSecurityEvent>
{
    public void Configure(EntityTypeBuilder<AuthSecurityEvent> builder)
    {
        builder.ToTable("AuthSecurityEvents");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.EventType).IsRequired().HasConversion<string>().HasMaxLength(64);
        builder.Property(x => x.Outcome).IsRequired().HasConversion<string>().HasMaxLength(32);
        builder.Property(x => x.EmailOrUserName).HasMaxLength(256);
        builder.Property(x => x.FailureReasonCode).HasMaxLength(64);
        builder.Property(x => x.IpAddress).HasMaxLength(64);
        builder.Property(x => x.UserAgent).HasMaxLength(512);
        builder.Property(x => x.CorrelationId).HasMaxLength(64);
        // MetadataJson intentionally unbounded — caller must keep it minimal and safe
        builder.HasIndex(x => new { x.UserId, x.OccurredAtUtc });
        builder.HasIndex(x => new { x.EventType, x.OccurredAtUtc });
        builder.HasIndex(x => x.OccurredAtUtc);
    }
}

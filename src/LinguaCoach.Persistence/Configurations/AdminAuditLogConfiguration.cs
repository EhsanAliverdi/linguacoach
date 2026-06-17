using LinguaCoach.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LinguaCoach.Persistence.Configurations;

public sealed class AdminAuditLogConfiguration : IEntityTypeConfiguration<AdminAuditLog>
{
    public void Configure(EntityTypeBuilder<AdminAuditLog> builder)
    {
        builder.ToTable("AdminAuditLogs");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Action).IsRequired().HasMaxLength(100);
        builder.Property(x => x.EntityType).IsRequired().HasMaxLength(100);
        builder.Property(x => x.EntityId).HasMaxLength(200);
        builder.Property(x => x.Reason).HasMaxLength(500);
        builder.Property(x => x.CorrelationId).HasMaxLength(200);
        builder.HasIndex(x => new { x.ActorAdminUserId, x.CreatedAt });
        builder.HasIndex(x => new { x.TargetStudentId, x.CreatedAt });
    }
}

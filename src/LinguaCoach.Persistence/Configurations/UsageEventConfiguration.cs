using LinguaCoach.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LinguaCoach.Persistence.Configurations;

public sealed class UsageEventConfiguration : IEntityTypeConfiguration<UsageEvent>
{
    public void Configure(EntityTypeBuilder<UsageEvent> builder)
    {
        builder.ToTable("UsageEvents");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.FeatureKey).IsRequired().HasMaxLength(100);
        builder.Property(x => x.UnitType).HasConversion<string>().HasMaxLength(50);
        builder.Property(x => x.Provider).HasMaxLength(100);
        builder.Property(x => x.Model).HasMaxLength(200);
        builder.Property(x => x.Currency).HasMaxLength(10);
        builder.Property(x => x.EstimatedCost).HasColumnType("decimal(18,6)");
        builder.Property(x => x.RequestId).HasMaxLength(200);
        builder.Property(x => x.CorrelationId).HasMaxLength(200);
        builder.Property(x => x.ErrorCode).HasMaxLength(100);
        builder.Property(x => x.ErrorMessage).HasMaxLength(1000);
        builder.HasOne<StudentProfile>().WithMany().HasForeignKey(x => x.StudentProfileId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(x => new { x.StudentProfileId, x.CreatedAt });
        builder.HasIndex(x => new { x.StudentProfileId, x.FeatureKey, x.CreatedAt });
    }
}

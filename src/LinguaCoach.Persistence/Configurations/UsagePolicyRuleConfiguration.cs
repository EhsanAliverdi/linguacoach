using LinguaCoach.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LinguaCoach.Persistence.Configurations;

public sealed class UsagePolicyRuleConfiguration : IEntityTypeConfiguration<UsagePolicyRule>
{
    public void Configure(EntityTypeBuilder<UsagePolicyRule> builder)
    {
        builder.ToTable("UsagePolicyRules");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.FeatureKey).IsRequired().HasMaxLength(100);
        builder.Property(x => x.EnforcementMode).HasConversion<string>().HasMaxLength(50);
        builder.Property(x => x.UnitType).HasConversion<string>().HasMaxLength(50);
        builder.Property(x => x.DailyCostLimit).HasColumnType("decimal(18,6)");
        builder.Property(x => x.MonthlyCostLimit).HasColumnType("decimal(18,6)");
        builder.HasIndex(x => new { x.UsagePolicyId, x.FeatureKey });
    }
}

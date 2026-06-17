using LinguaCoach.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LinguaCoach.Persistence.Configurations;

public sealed class UsagePolicyConfiguration : IEntityTypeConfiguration<UsagePolicy>
{
    public void Configure(EntityTypeBuilder<UsagePolicy> builder)
    {
        builder.ToTable("UsagePolicies");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).IsRequired().HasMaxLength(200);
        builder.Property(x => x.Description).HasMaxLength(1000);
        builder.Property(x => x.ScopeType).HasConversion<string>().HasMaxLength(50);
        builder.HasMany(x => x.Rules).WithOne(r => r.UsagePolicy).HasForeignKey(r => r.UsagePolicyId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(x => x.StudentAssignments).WithOne(a => a.UsagePolicy).HasForeignKey(a => a.UsagePolicyId).OnDelete(DeleteBehavior.Restrict);
    }
}

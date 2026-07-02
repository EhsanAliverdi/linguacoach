using LinguaCoach.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LinguaCoach.Persistence.Configurations;

public sealed class RuntimeSettingOverrideConfiguration : IEntityTypeConfiguration<RuntimeSettingOverride>
{
    public void Configure(EntityTypeBuilder<RuntimeSettingOverride> builder)
    {
        builder.ToTable("RuntimeSettingOverrides");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Key).IsRequired().HasMaxLength(200);
        builder.Property(x => x.ValueJson).IsRequired().HasMaxLength(2000);
        builder.Property(x => x.DataType).IsRequired().HasMaxLength(50);
        builder.Property(x => x.Reason).HasMaxLength(500);
        builder.HasIndex(x => x.Key).IsUnique();
    }
}

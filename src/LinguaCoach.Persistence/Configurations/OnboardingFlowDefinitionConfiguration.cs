using LinguaCoach.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LinguaCoach.Persistence.Configurations;

internal sealed class OnboardingFlowDefinitionConfiguration : IEntityTypeConfiguration<OnboardingFlowDefinition>
{
    public void Configure(EntityTypeBuilder<OnboardingFlowDefinition> builder)
    {
        builder.ToTable("onboarding_flow_definitions");

        builder.HasKey(f => f.Id);
        builder.Property(f => f.Id).HasColumnName("id");
        builder.Property(f => f.CreatedAt).HasColumnName("created_at").IsRequired()
            .HasDefaultValueSql("now()");

        builder.Property(f => f.Name).HasColumnName("name").IsRequired().HasMaxLength(200);
        builder.Property(f => f.IsActive).HasColumnName("is_active").IsRequired();
        builder.Property(f => f.Version).HasColumnName("version").IsRequired();

        // Enforce: only one active flow at a time.
        builder.HasIndex(f => f.IsActive)
            .IsUnique()
            .HasFilter("is_active = true")
            .HasDatabaseName("ix_onboarding_flow_definitions_single_active");

        builder.HasMany(f => f.Steps)
            .WithOne(s => s.FlowDefinition)
            .HasForeignKey(s => s.FlowDefinitionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(f => f.Steps).HasField("_steps").UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

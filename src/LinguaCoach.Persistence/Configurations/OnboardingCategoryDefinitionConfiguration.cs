using LinguaCoach.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LinguaCoach.Persistence.Configurations;

internal sealed class OnboardingCategoryDefinitionConfiguration : IEntityTypeConfiguration<OnboardingCategoryDefinition>
{
    public void Configure(EntityTypeBuilder<OnboardingCategoryDefinition> builder)
    {
        builder.ToTable("onboarding_category_definitions");

        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).HasColumnName("id");
        builder.Property(c => c.CreatedAt).HasColumnName("created_at").IsRequired().HasDefaultValueSql("now()");

        builder.Property(c => c.FlowDefinitionId).HasColumnName("flow_definition_id").IsRequired();
        builder.Property(c => c.Name).HasColumnName("name").IsRequired().HasMaxLength(150);
        builder.Property(c => c.Description).HasColumnName("description").HasMaxLength(500);
        builder.Property(c => c.CategoryOrder).HasColumnName("category_order").IsRequired();
        builder.Property(c => c.IsEnabled).HasColumnName("is_enabled").IsRequired();

        builder.HasIndex(c => new { c.FlowDefinitionId, c.CategoryOrder })
            .HasDatabaseName("ix_onboarding_category_definitions_flow_order");
    }
}

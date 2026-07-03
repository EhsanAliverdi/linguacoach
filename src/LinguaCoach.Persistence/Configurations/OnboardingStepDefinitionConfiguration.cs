using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LinguaCoach.Persistence.Configurations;

internal sealed class OnboardingStepDefinitionConfiguration : IEntityTypeConfiguration<OnboardingStepDefinition>
{
    public void Configure(EntityTypeBuilder<OnboardingStepDefinition> builder)
    {
        builder.ToTable("onboarding_step_definitions");

        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).HasColumnName("id");
        builder.Property(s => s.CreatedAt).HasColumnName("created_at").IsRequired()
            .HasDefaultValueSql("now()");

        builder.Property(s => s.FlowDefinitionId).HasColumnName("flow_definition_id").IsRequired();
        builder.Property(s => s.StepKey).HasColumnName("step_key").IsRequired().HasMaxLength(100);
        builder.Property(s => s.Title).HasColumnName("title").IsRequired().HasMaxLength(200);
        builder.Property(s => s.Description).HasColumnName("description").HasMaxLength(2000);
        builder.Property(s => s.StepType).HasColumnName("step_type").IsRequired()
            .HasConversion<string>();
        builder.Property(s => s.RequirementType).HasColumnName("requirement_type").IsRequired()
            .HasConversion<string>();
        builder.Property(s => s.StepOrder).HasColumnName("step_order").IsRequired();
        builder.Property(s => s.IsEnabled).HasColumnName("is_enabled").IsRequired();
        builder.Property(s => s.OptionsJson).HasColumnName("options_json").HasColumnType("jsonb");
        builder.Property(s => s.ValidationMetadataJson).HasColumnName("validation_metadata_json").HasColumnType("jsonb");
        // Typed mapping: stored as string for readability.
        builder.Property(s => s.AnswerMapping).HasColumnName("answer_mapping").IsRequired()
            .HasConversion<string>();
        // Assessment metadata (correct answers, scoring weights) — server-side only, never exposed to students.
        builder.Property(s => s.AssessmentMetadataJson).HasColumnName("assessment_metadata_json").HasColumnType("jsonb");
        builder.Property(s => s.ContentJson).HasColumnName("content_json").HasColumnType("jsonb");
        builder.Ignore(s => s.Content);

        // Unique step key per flow.
        builder.HasIndex(s => new { s.FlowDefinitionId, s.StepKey })
            .IsUnique()
            .HasDatabaseName("ix_onboarding_step_definitions_flow_step_key");

        builder.HasIndex(s => new { s.FlowDefinitionId, s.StepOrder })
            .HasDatabaseName("ix_onboarding_step_definitions_flow_step_order");
    }
}

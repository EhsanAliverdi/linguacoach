using System.Text.Json;
using LinguaCoach.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LinguaCoach.Persistence.Configurations;

internal sealed class StudentOnboardingProgressConfiguration : IEntityTypeConfiguration<StudentOnboardingProgress>
{
    public void Configure(EntityTypeBuilder<StudentOnboardingProgress> builder)
    {
        builder.ToTable("student_onboarding_progress");

        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).HasColumnName("id");
        builder.Property(p => p.CreatedAt).HasColumnName("created_at").IsRequired()
            .HasDefaultValueSql("now()");

        builder.Property(p => p.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(p => p.FlowDefinitionId).HasColumnName("flow_definition_id").IsRequired();
        builder.Property(p => p.CurrentStepKey).HasColumnName("current_step_key").HasMaxLength(100);
        builder.Property(p => p.CompletedStepKeys)
            .HasColumnName("completed_step_keys")
            .HasColumnType("jsonb")
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null) ?? new List<string>());
        builder.Property(p => p.PercentageComplete).HasColumnName("percentage_complete").IsRequired();
        builder.Property(p => p.StartedAt).HasColumnName("started_at").IsRequired();
        builder.Property(p => p.CompletedAt).HasColumnName("completed_at");
        builder.Property(p => p.IsComplete).HasColumnName("is_complete").IsRequired();
        builder.Property(p => p.PreliminaryCefrLevel).HasColumnName("preliminary_cefr_level").HasMaxLength(2);
        // Optimistic concurrency token (xmin) registered in OnModelCreating, matching existing LearningPath/PracticeActivityCache pattern.

        // One progress record per student — enforced at DB level.
        builder.HasIndex(p => p.UserId)
            .IsUnique()
            .HasDatabaseName("ix_student_onboarding_progress_user_id");

        builder.HasOne(p => p.FlowDefinition)
            .WithMany()
            .HasForeignKey(p => p.FlowDefinitionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(p => p.Responses)
            .WithOne(r => r.Progress)
            .HasForeignKey(r => r.ProgressId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

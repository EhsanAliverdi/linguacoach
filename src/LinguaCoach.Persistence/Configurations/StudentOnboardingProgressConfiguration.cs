using System.Text.Json;
using LinguaCoach.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
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
        // Bug found live 2026-07-03: RecordStepCompleted() mutates this List<string> in place
        // (.Add(stepKey)), so without an explicit ValueComparer, EF's default reference-equality
        // change tracking never sees a change on the same List instance and silently never
        // persists it — every V2 onboarding completion failed with "Required steps not
        // completed" listing every step, because completed_step_keys was always []. This was
        // never caught before because V2 had never been driven end-to-end by a real student.
        builder.Property(p => p.CompletedStepKeys)
            .HasColumnName("completed_step_keys")
            .HasColumnType("jsonb")
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null) ?? new List<string>())
            .Metadata.SetValueComparer(new ValueComparer<List<string>>(
                (a, b) => a!.SequenceEqual(b!),
                v => v.Aggregate(0, (hash, item) => HashCode.Combine(hash, item.GetHashCode())),
                v => v.ToList()));
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

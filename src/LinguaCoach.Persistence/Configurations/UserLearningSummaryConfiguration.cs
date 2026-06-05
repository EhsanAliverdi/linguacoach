using LinguaCoach.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LinguaCoach.Persistence.Configurations;

internal sealed class UserLearningSummaryConfiguration : IEntityTypeConfiguration<UserLearningSummary>
{
    public void Configure(EntityTypeBuilder<UserLearningSummary> builder)
    {
        builder.ToTable("user_learning_summaries");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired()
            .HasDefaultValueSql("now()");

        builder.Property(e => e.StudentProfileId).HasColumnName("student_profile_id").IsRequired();
        builder.Property(e => e.RecentWeaknesses).HasColumnName("recent_weaknesses")
            .HasMaxLength(200).IsRequired();
        builder.Property(e => e.RecentProgress).HasColumnName("recent_progress")
            .HasMaxLength(200).IsRequired();
        builder.Property(e => e.JourneySummary).HasColumnName("journey_summary").IsRequired(false);
        builder.Property(e => e.StrongSkillsJson).HasColumnName("strong_skills_json").IsRequired().HasDefaultValue("[]");
        builder.Property(e => e.WeakSkillsJson).HasColumnName("weak_skills_json").IsRequired().HasDefaultValue("[]");
        builder.Property(e => e.RecurringMistakesJson).HasColumnName("recurring_mistakes_json").IsRequired().HasDefaultValue("[]");
        builder.Property(e => e.CoveredScenariosJson).HasColumnName("covered_scenarios_json").IsRequired().HasDefaultValue("[]");
        builder.Property(e => e.NextFocusJson).HasColumnName("next_focus_json").IsRequired().HasDefaultValue("[]");
        builder.Property(e => e.UpdatedAt).HasColumnName("updated_at").IsRequired();

        // One summary record per student.
        builder.HasIndex(e => e.StudentProfileId)
            .IsUnique()
            .HasDatabaseName("ix_user_learning_summaries_student_profile_id");

        builder.HasOne<Domain.Entities.StudentProfile>()
            .WithMany()
            .HasForeignKey(e => e.StudentProfileId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

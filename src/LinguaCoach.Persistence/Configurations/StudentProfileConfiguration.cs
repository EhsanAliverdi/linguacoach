using LinguaCoach.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LinguaCoach.Persistence.Configurations;

internal sealed class StudentProfileConfiguration : IEntityTypeConfiguration<StudentProfile>
{
    public void Configure(EntityTypeBuilder<StudentProfile> builder)
    {
        builder.ToTable("student_profiles");

        builder.HasKey(sp => sp.Id);
        builder.Property(sp => sp.Id).HasColumnName("id");
        builder.Property(sp => sp.CreatedAt).HasColumnName("created_at").IsRequired();

        builder.Property(sp => sp.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(sp => sp.OnboardingStatus).HasColumnName("onboarding_status").IsRequired();
        builder.Property(sp => sp.LastCompletedStep).HasColumnName("last_completed_step").IsRequired();
        builder.Property(sp => sp.LanguagePairId).HasColumnName("language_pair_id");
        builder.Property(sp => sp.LearningTrackId).HasColumnName("learning_track_id");
        builder.Property(sp => sp.CareerProfileId).HasColumnName("career_profile_id");
        builder.Property(sp => sp.SkillFocus).HasColumnName("skill_focus");

        builder.HasOne(sp => sp.LanguagePair)
            .WithMany()
            .HasForeignKey(sp => sp.LanguagePairId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(sp => sp.LearningTrack)
            .WithMany()
            .HasForeignKey(sp => sp.LearningTrackId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(sp => sp.CareerProfile)
            .WithMany()
            .HasForeignKey(sp => sp.CareerProfileId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(sp => sp.UserId)
            .IsUnique()
            .HasDatabaseName("ix_student_profiles_user_id");

        builder.HasIndex(sp => new { sp.UserId, sp.OnboardingStatus })
            .HasDatabaseName("ix_student_profiles_user_id_onboarding_status");
    }
}

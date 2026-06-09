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
        builder.Property(sp => sp.CreatedAt).HasColumnName("created_at").IsRequired()
            .HasDefaultValueSql("now()");

        builder.Property(sp => sp.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(sp => sp.OnboardingStatus).HasColumnName("onboarding_status").IsRequired();
        builder.Property(sp => sp.LastCompletedStep).HasColumnName("last_completed_step").IsRequired();
        builder.Property(sp => sp.LanguagePairId).HasColumnName("language_pair_id");
        builder.Property(sp => sp.LearningTrackId).HasColumnName("learning_track_id");
        builder.Property(sp => sp.CareerProfileId).HasColumnName("career_profile_id");
        builder.Property(sp => sp.SkillFocus).HasColumnName("skill_focus");
        builder.Property(sp => sp.CefrLevel).HasColumnName("cefr_level").HasMaxLength(2);

        // T29 — lifecycle & placement fields
        builder.Property(sp => sp.LifecycleStage).HasColumnName("lifecycle_stage").IsRequired();
        builder.Property(sp => sp.ProfessionalExperienceLevel).HasColumnName("professional_experience_level");
        builder.Property(sp => sp.RoleFamiliarity).HasColumnName("role_familiarity");
        builder.Property(sp => sp.WorkplaceSeniority).HasColumnName("workplace_seniority");
        builder.Property(sp => sp.PreferredSessionDurationMinutes).HasColumnName("preferred_session_duration_minutes");

        // T30 — admin-created profile fields
        builder.Property(sp => sp.FirstName).HasColumnName("first_name").HasMaxLength(100);
        builder.Property(sp => sp.LastName).HasColumnName("last_name").HasMaxLength(100);
        builder.Property(sp => sp.DisplayName).HasColumnName("display_name").HasMaxLength(150);
        builder.Property(sp => sp.CareerContext).HasColumnName("career_context").HasMaxLength(500);
        builder.Property(sp => sp.LearningGoal).HasColumnName("learning_goal").HasMaxLength(500);

        // T31 — student-set onboarding goal fields
        builder.Property(sp => sp.LearningGoalDescription).HasColumnName("learning_goal_description").HasMaxLength(1000);
        builder.Property(sp => sp.DifficultSituationsText).HasColumnName("difficult_situations_text").HasMaxLength(1000);

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

        builder.HasIndex(sp => sp.LanguagePairId)
            .HasDatabaseName("ix_student_profiles_language_pair_id");

        builder.HasIndex(sp => sp.LearningTrackId)
            .HasDatabaseName("ix_student_profiles_learning_track_id");

        builder.HasIndex(sp => sp.CareerProfileId)
            .HasDatabaseName("ix_student_profiles_career_profile_id");

        builder.HasIndex(sp => sp.UserId)
            .IsUnique()
            .HasDatabaseName("ix_student_profiles_user_id");

        builder.HasIndex(sp => new { sp.UserId, sp.OnboardingStatus })
            .HasDatabaseName("ix_student_profiles_user_id_onboarding_status");
    }
}

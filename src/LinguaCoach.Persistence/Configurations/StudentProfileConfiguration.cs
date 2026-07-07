using System.Text.Json;
using LinguaCoach.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
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

        // T46 — student-editable learning preferences (Phase 10G)
        builder.Property(sp => sp.PreferredName).HasColumnName("preferred_name").HasMaxLength(100);
        builder.Property(sp => sp.SupportLanguageCode).HasColumnName("support_language_code").HasMaxLength(10);
        builder.Property(sp => sp.SupportLanguageName).HasColumnName("support_language_name").HasMaxLength(100);
        builder.Property(sp => sp.TranslationHelpPreference).HasColumnName("translation_help_preference");
        // ValueComparer added defensively alongside the identical CompletedStepKeys fix
        // (StudentOnboardingProgressConfiguration) — these two currently only get reassigned
        // via UpdateLearningPreferences's `.ToList()` (a new instance, so EF's default
        // reference-equality tracking happens to work), but any future in-place mutation
        // (e.g. `.Add(...)`) would silently stop persisting without this.
        builder.Property(sp => sp.LearningGoals)
            .HasColumnName("learning_goals")
            .HasColumnType("jsonb")
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null) ?? new List<string>())
            .Metadata.SetValueComparer(new ValueComparer<List<string>>(
                (a, b) => a!.SequenceEqual(b!),
                v => v.Aggregate(0, (hash, item) => HashCode.Combine(hash, item.GetHashCode())),
                v => v.ToList()));
        builder.Property(sp => sp.CustomLearningGoal).HasColumnName("custom_learning_goal").HasMaxLength(200);
        builder.Property(sp => sp.FocusAreas)
            .HasColumnName("focus_areas")
            .HasColumnType("jsonb")
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null) ?? new List<string>())
            .Metadata.SetValueComparer(new ValueComparer<List<string>>(
                (a, b) => a!.SequenceEqual(b!),
                v => v.Aggregate(0, (hash, item) => HashCode.Combine(hash, item.GetHashCode())),
                v => v.ToList()));
        builder.Property(sp => sp.CustomFocusArea).HasColumnName("custom_focus_area").HasMaxLength(200);
        builder.Property(sp => sp.DifficultyPreference).HasColumnName("difficulty_preference");
        builder.Property(sp => sp.LearningPreferencesUpdatedAt).HasColumnName("learning_preferences_updated_at");

        builder.HasOne(sp => sp.LanguagePair)
            .WithMany()
            .HasForeignKey(sp => sp.LanguagePairId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(sp => sp.CareerProfile)
            .WithMany()
            .HasForeignKey(sp => sp.CareerProfileId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(sp => sp.LanguagePairId)
            .HasDatabaseName("ix_student_profiles_language_pair_id");

        builder.HasIndex(sp => sp.CareerProfileId)
            .HasDatabaseName("ix_student_profiles_career_profile_id");

        builder.HasIndex(sp => sp.UserId)
            .IsUnique()
            .HasDatabaseName("ix_student_profiles_user_id");

        builder.HasIndex(sp => new { sp.UserId, sp.OnboardingStatus })
            .HasDatabaseName("ix_student_profiles_user_id_onboarding_status");
    }
}

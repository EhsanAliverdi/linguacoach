using LinguaCoach.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LinguaCoach.Persistence.Configurations;

internal sealed class StudentLearningEventConfiguration : IEntityTypeConfiguration<StudentLearningEvent>
{
    public void Configure(EntityTypeBuilder<StudentLearningEvent> builder)
    {
        builder.ToTable("student_learning_events");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired()
            .HasDefaultValueSql("now()");

        builder.Property(e => e.StudentProfileId).HasColumnName("student_profile_id").IsRequired();
        builder.Property(e => e.Source).HasColumnName("source").HasConversion<string>().HasMaxLength(50).IsRequired();
        builder.Property(e => e.Outcome).HasColumnName("outcome").HasConversion<string>().HasMaxLength(50).IsRequired();

        builder.Property(e => e.ActivityId).HasColumnName("activity_id");
        builder.Property(e => e.SessionId).HasColumnName("session_id");
        builder.Property(e => e.SessionExerciseId).HasColumnName("session_exercise_id");
        builder.Property(e => e.ActivityAttemptId).HasColumnName("activity_attempt_id");

        builder.Property(e => e.ExerciseType).HasColumnName("exercise_type").HasMaxLength(100);
        builder.Property(e => e.PatternKey).HasColumnName("pattern_key").HasMaxLength(100);
        builder.Property(e => e.PrimarySkill).HasColumnName("primary_skill").HasMaxLength(100);
        builder.Property(e => e.SecondarySkillsJson).HasColumnName("secondary_skills_json");
        builder.Property(e => e.LearningGoalContext).HasColumnName("learning_goal_context").HasMaxLength(200);
        builder.Property(e => e.CefrLevelAtEvent).HasColumnName("cefr_level_at_event").HasMaxLength(10);

        builder.Property(e => e.ConceptsTaughtJson).HasColumnName("concepts_taught_json");
        builder.Property(e => e.ConceptsPractisedJson).HasColumnName("concepts_practised_json");
        builder.Property(e => e.MistakeTagsJson).HasColumnName("mistake_tags_json");

        builder.Property(e => e.Score).HasColumnName("score");
        builder.Property(e => e.NormalizedScore).HasColumnName("normalized_score");
        builder.Property(e => e.OccurredAtUtc).HasColumnName("occurred_at_utc").IsRequired();
        builder.Property(e => e.MetadataJson).HasColumnName("metadata_json");

        builder.HasIndex(e => e.StudentProfileId)
            .HasDatabaseName("ix_student_learning_events_student");
        builder.HasIndex(e => new { e.StudentProfileId, e.OccurredAtUtc })
            .HasDatabaseName("ix_student_learning_events_student_time");
        builder.HasIndex(e => new { e.StudentProfileId, e.PatternKey })
            .HasDatabaseName("ix_student_learning_events_student_pattern");

        builder.HasOne<StudentProfile>()
            .WithMany()
            .HasForeignKey(e => e.StudentProfileId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

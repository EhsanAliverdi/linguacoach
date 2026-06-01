using LinguaCoach.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LinguaCoach.Persistence.Configurations;

internal sealed class SpeakingSessionConfiguration : IEntityTypeConfiguration<SpeakingSession>
{
    public void Configure(EntityTypeBuilder<SpeakingSession> builder)
    {
        builder.ToTable("speaking_sessions");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired()
            .HasDefaultValueSql("now()");

        builder.Property(e => e.StudentProfileId).HasColumnName("student_profile_id").IsRequired();
        builder.Property(e => e.ScenarioId).HasColumnName("scenario_id").IsRequired();
        builder.Property(e => e.Status).HasColumnName("status").IsRequired();
        builder.Property(e => e.CefrLevel).HasColumnName("cefr_level").HasMaxLength(10).IsRequired();
        builder.Property(e => e.CareerContext).HasColumnName("career_context").HasMaxLength(200).IsRequired();
        builder.Property(e => e.MaxTurns).HasColumnName("max_turns").IsRequired();
        builder.Property(e => e.CurrentTurn).HasColumnName("current_turn").IsRequired();
        builder.Property(e => e.OverallScore).HasColumnName("overall_score");
        builder.Property(e => e.SessionSummary).HasColumnName("session_summary").HasMaxLength(200);
        builder.Property(e => e.StartedAt).HasColumnName("started_at");
        builder.Property(e => e.CompletedAt).HasColumnName("completed_at");

        builder.HasIndex(e => new { e.StudentProfileId, e.Status })
            .HasDatabaseName("ix_speaking_sessions_student_status");

        builder.HasIndex(e => e.ScenarioId)
            .HasDatabaseName("ix_speaking_sessions_scenario_id");

        builder.HasOne<Domain.Entities.StudentProfile>()
            .WithMany()
            .HasForeignKey(e => e.StudentProfileId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Domain.Entities.SpeakingScenario>()
            .WithMany()
            .HasForeignKey(e => e.ScenarioId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

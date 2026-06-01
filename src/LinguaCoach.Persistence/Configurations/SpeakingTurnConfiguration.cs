using LinguaCoach.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LinguaCoach.Persistence.Configurations;

internal sealed class SpeakingTurnConfiguration : IEntityTypeConfiguration<SpeakingTurn>
{
    public void Configure(EntityTypeBuilder<SpeakingTurn> builder)
    {
        builder.ToTable("speaking_turns");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired()
            .HasDefaultValueSql("now()");

        builder.Property(e => e.SpeakingSessionId).HasColumnName("speaking_session_id").IsRequired();
        builder.Property(e => e.TurnNumber).HasColumnName("turn_number").IsRequired();
        builder.Property(e => e.AiQuestion).HasColumnName("ai_question").IsRequired();
        builder.Property(e => e.UserTranscript).HasColumnName("user_transcript");

        // userAudioUrl is nullable — audio upload not implemented in MVP.
        builder.Property(e => e.UserAudioUrl).HasColumnName("user_audio_url").HasMaxLength(1000);

        builder.Property(e => e.AiReply).HasColumnName("ai_reply").IsRequired();
        builder.Property(e => e.FeedbackJson).HasColumnName("feedback_json")
            .HasColumnType("jsonb").IsRequired();
        builder.Property(e => e.MistakesJson).HasColumnName("mistakes_json")
            .HasColumnType("jsonb").IsRequired();

        builder.Property(e => e.PronunciationScore).HasColumnName("pronunciation_score");
        builder.Property(e => e.GrammarScore).HasColumnName("grammar_score");
        builder.Property(e => e.VocabularyScore).HasColumnName("vocabulary_score");
        builder.Property(e => e.FluencyScore).HasColumnName("fluency_score");
        builder.Property(e => e.TurnSummary).HasColumnName("turn_summary").HasMaxLength(150);

        builder.HasIndex(e => new { e.SpeakingSessionId, e.TurnNumber })
            .IsUnique()
            .HasDatabaseName("ix_speaking_turns_session_turn");

        builder.HasOne<Domain.Entities.SpeakingSession>()
            .WithMany()
            .HasForeignKey(e => e.SpeakingSessionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

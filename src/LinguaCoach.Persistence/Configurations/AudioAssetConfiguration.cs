using LinguaCoach.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LinguaCoach.Persistence.Configurations;

internal sealed class AudioAssetConfiguration : IEntityTypeConfiguration<AudioAsset>
{
    public void Configure(EntityTypeBuilder<AudioAsset> builder)
    {
        builder.ToTable("audio_assets");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired()
            .HasDefaultValueSql("now()");

        builder.Property(e => e.StudentProfileId).HasColumnName("student_profile_id").IsRequired();
        builder.Property(e => e.LearningSessionId).HasColumnName("learning_session_id").IsRequired(false);
        builder.Property(e => e.LearningActivityId).HasColumnName("learning_activity_id").IsRequired(false);
        builder.Property(e => e.ActivityAttemptId).HasColumnName("activity_attempt_id").IsRequired(false);
        builder.Property(e => e.AssetType).HasColumnName("asset_type").HasConversion<int>().IsRequired();
        builder.Property(e => e.ObjectKey).HasColumnName("object_key").HasMaxLength(1000).IsRequired();
        builder.Property(e => e.ContentType).HasColumnName("content_type").HasMaxLength(100).IsRequired();
        builder.Property(e => e.DurationSeconds).HasColumnName("duration_seconds").IsRequired(false);
        builder.Property(e => e.TranscriptHash).HasColumnName("transcript_hash").HasMaxLength(128).IsRequired(false);
        builder.Property(e => e.SpeakerProfileHash).HasColumnName("speaker_profile_hash").HasMaxLength(128).IsRequired(false);
        builder.Property(e => e.SpeakerProfileJson).HasColumnName("speaker_profile_json").IsRequired(false);
        builder.Property(e => e.ProviderName).HasColumnName("provider_name").HasMaxLength(100).IsRequired(false);
        builder.Property(e => e.ModelName).HasColumnName("model_name").HasMaxLength(200).IsRequired(false);
        builder.Property(e => e.GenerationStatus).HasColumnName("generation_status").HasConversion<int>().IsRequired();
        builder.Property(e => e.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();

        builder.HasIndex(e => e.StudentProfileId).HasDatabaseName("ix_audio_assets_student");
        builder.HasIndex(e => e.LearningActivityId).HasDatabaseName("ix_audio_assets_activity");
        builder.HasIndex(e => e.ActivityAttemptId).HasDatabaseName("ix_audio_assets_attempt");
        builder.HasIndex(e => e.GenerationStatus).HasDatabaseName("ix_audio_assets_status");

        // TTS idempotency fingerprint (T10).
        builder.HasIndex(e => new
            {
                e.LearningActivityId,
                e.TranscriptHash,
                e.SpeakerProfileHash,
                e.ProviderName,
                e.ModelName
            })
            .HasDatabaseName("ux_audio_assets_tts_fingerprint")
            .IsUnique()
            .HasFilter("learning_activity_id IS NOT NULL AND transcript_hash IS NOT NULL");
    }
}

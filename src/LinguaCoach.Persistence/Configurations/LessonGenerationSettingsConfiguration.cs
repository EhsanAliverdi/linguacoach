using LinguaCoach.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LinguaCoach.Persistence.Configurations;

internal sealed class LessonGenerationSettingsConfiguration : IEntityTypeConfiguration<LessonGenerationSettings>
{
    public void Configure(EntityTypeBuilder<LessonGenerationSettings> builder)
    {
        builder.ToTable("lesson_generation_settings");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired()
            .HasDefaultValueSql("now()");

        builder.Property(e => e.ReadyLessonBufferSize).HasColumnName("ready_lesson_buffer_size").IsRequired();
        builder.Property(e => e.RefillThreshold).HasColumnName("refill_threshold").IsRequired();
        builder.Property(e => e.RefillBatchSize).HasColumnName("refill_batch_size").IsRequired();
        builder.Property(e => e.MaxGenerationAttempts).HasColumnName("max_generation_attempts").IsRequired();
        builder.Property(e => e.GenerationTimeoutSeconds).HasColumnName("generation_timeout_seconds").IsRequired();
        builder.Property(e => e.TtsTimeoutSeconds).HasColumnName("tts_timeout_seconds").IsRequired();
        builder.Property(e => e.MaxConcurrentGenerationJobs).HasColumnName("max_concurrent_generation_jobs").IsRequired();
        builder.Property(e => e.MaxConcurrentTtsJobs).HasColumnName("max_concurrent_tts_jobs").IsRequired();
        builder.Property(e => e.EnableBackgroundGeneration).HasColumnName("enable_background_generation").IsRequired();
        builder.Property(e => e.EnableTtsGeneration).HasColumnName("enable_tts_generation").IsRequired();
        builder.Property(e => e.PracticeGymReadyExercisesPerType).HasColumnName("practice_gym_ready_exercises_per_type").IsRequired();
        builder.Property(e => e.PracticeGymRefillThresholdPerType).HasColumnName("practice_gym_refill_threshold_per_type").IsRequired();
        builder.Property(e => e.PracticeGymRefillCountPerType).HasColumnName("practice_gym_refill_count_per_type").IsRequired();
        builder.Property(e => e.UpdatedAtUtc).HasColumnName("updated_at_utc").IsRequired();
    }
}

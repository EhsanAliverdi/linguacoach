using LinguaCoach.Domain.Common;

namespace LinguaCoach.Domain.Entities;

/// <summary>
/// Single-row install-wide settings that drive the lesson buffer, generation throttles,
/// and Practice Gym cache. Defaults match the sprint product-owner policy.
/// </summary>
public sealed class LessonGenerationSettings : BaseEntity
{
    public int ReadyLessonBufferSize { get; private set; } = 5;
    public int RefillThreshold { get; private set; } = 1;
    public int RefillBatchSize { get; private set; } = 4;
    public int MaxGenerationAttempts { get; private set; } = 2;
    public int GenerationTimeoutSeconds { get; private set; } = 120;
    public int TtsTimeoutSeconds { get; private set; } = 60;
    public int MaxConcurrentGenerationJobs { get; private set; } = 2;
    public int MaxConcurrentTtsJobs { get; private set; } = 2;
    public bool EnableBackgroundGeneration { get; private set; } = true;
    public bool EnableTtsGeneration { get; private set; } = true;
    public int PracticeGymReadyExercisesPerType { get; private set; } = 10;
    public int PracticeGymRefillThresholdPerType { get; private set; } = 3;
    public int PracticeGymRefillCountPerType { get; private set; } = 7;

    public DateTime UpdatedAtUtc { get; private set; } = DateTime.UtcNow;

    // EF + seeder use this default-constructed instance.
    public LessonGenerationSettings() { }

    /// <summary>
    /// Updates the mutable settings with validation:
    /// positive integers, refill threshold strictly below buffer size, sane caps.
    /// </summary>
    public void Update(
        int readyLessonBufferSize,
        int refillThreshold,
        int refillBatchSize,
        int maxGenerationAttempts,
        int generationTimeoutSeconds,
        int ttsTimeoutSeconds,
        int maxConcurrentGenerationJobs,
        int maxConcurrentTtsJobs,
        bool enableBackgroundGeneration,
        bool enableTtsGeneration,
        int practiceGymReadyExercisesPerType,
        int practiceGymRefillThresholdPerType,
        int practiceGymRefillCountPerType)
    {
        if (readyLessonBufferSize < 1) throw new ArgumentException("ReadyLessonBufferSize must be positive.");
        if (refillThreshold < 0) throw new ArgumentException("RefillThreshold must be non-negative.");
        if (refillThreshold >= readyLessonBufferSize) throw new ArgumentException("RefillThreshold must be lower than ReadyLessonBufferSize.");
        if (refillBatchSize < 1) throw new ArgumentException("RefillBatchSize must be positive.");
        if (maxGenerationAttempts < 1) throw new ArgumentException("MaxGenerationAttempts must be positive.");
        if (generationTimeoutSeconds < 1) throw new ArgumentException("GenerationTimeoutSeconds must be positive.");
        if (ttsTimeoutSeconds < 1) throw new ArgumentException("TtsTimeoutSeconds must be positive.");
        if (maxConcurrentGenerationJobs < 1) throw new ArgumentException("MaxConcurrentGenerationJobs must be positive.");
        if (maxConcurrentTtsJobs < 1) throw new ArgumentException("MaxConcurrentTtsJobs must be positive.");
        if (practiceGymReadyExercisesPerType < 1) throw new ArgumentException("PracticeGymReadyExercisesPerType must be positive.");
        if (practiceGymRefillThresholdPerType < 0) throw new ArgumentException("PracticeGymRefillThresholdPerType must be non-negative.");
        if (practiceGymRefillThresholdPerType >= practiceGymReadyExercisesPerType) throw new ArgumentException("PracticeGymRefillThresholdPerType must be lower than PracticeGymReadyExercisesPerType.");
        if (practiceGymRefillCountPerType < 1) throw new ArgumentException("PracticeGymRefillCountPerType must be positive.");

        ReadyLessonBufferSize = readyLessonBufferSize;
        RefillThreshold = refillThreshold;
        RefillBatchSize = refillBatchSize;
        MaxGenerationAttempts = maxGenerationAttempts;
        GenerationTimeoutSeconds = generationTimeoutSeconds;
        TtsTimeoutSeconds = ttsTimeoutSeconds;
        MaxConcurrentGenerationJobs = maxConcurrentGenerationJobs;
        MaxConcurrentTtsJobs = maxConcurrentTtsJobs;
        EnableBackgroundGeneration = enableBackgroundGeneration;
        EnableTtsGeneration = enableTtsGeneration;
        PracticeGymReadyExercisesPerType = practiceGymReadyExercisesPerType;
        PracticeGymRefillThresholdPerType = practiceGymRefillThresholdPerType;
        PracticeGymRefillCountPerType = practiceGymRefillCountPerType;
        UpdatedAtUtc = DateTime.UtcNow;
    }
}

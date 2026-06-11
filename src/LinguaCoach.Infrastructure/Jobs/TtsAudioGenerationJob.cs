using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Infrastructure.Activity;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Quartz;

namespace LinguaCoach.Infrastructure.Jobs;

/// <summary>
/// Generates listening audio for ListeningComprehension activities in a generation batch's
/// sessions, stores it through IFileStorageService (via ListeningAudioService), and records
/// an AudioAsset with TranscriptHash + SpeakerProfileHash for idempotency.
///
/// Idempotent: if an AudioAsset with the same activity + transcript + speaker-profile +
/// provider + model fingerprint already exists, it is reused — audio is not regenerated.
/// </summary>
[DisallowConcurrentExecution]
public sealed class TtsAudioGenerationJob : IJob
{
    public const string JobName = "tts-audio-generation";
    public const string BatchIdKey = "batchId";

    private readonly LinguaCoachDbContext _db;
    private readonly ListeningAudioService _listeningAudio;
    private readonly ILogger<TtsAudioGenerationJob> _logger;

    public TtsAudioGenerationJob(
        LinguaCoachDbContext db,
        ListeningAudioService listeningAudio,
        ILogger<TtsAudioGenerationJob> logger)
    {
        _db = db;
        _listeningAudio = listeningAudio;
        _logger = logger;
    }

    public static async Task TriggerAsync(IScheduler scheduler, Guid batchId, CancellationToken ct)
    {
        var job = JobBuilder.Create<TtsAudioGenerationJob>()
            .WithIdentity($"{JobName}-{batchId:N}-{Guid.NewGuid():N}")
            .UsingJobData(BatchIdKey, batchId.ToString())
            .Build();
        await scheduler.ScheduleJob(job, TriggerBuilder.Create().StartNow().Build(), ct);
    }

    public async Task Execute(IJobExecutionContext context)
    {
        var ct = context.CancellationToken;
        var batchId = Guid.Parse(context.MergedJobDataMap.GetString(BatchIdKey)!);

        var settings = await _db.LessonGenerationSettings.AsNoTracking().FirstOrDefaultAsync(ct);
        if (settings is not null && !settings.EnableTtsGeneration)
        {
            _logger.LogInformation("TtsAudioGenerationJob: TTS generation disabled — skipping BatchId={BatchId}.", batchId);
            return;
        }

        // Listening activities for this batch's sessions.
        var sessionIds = await _db.LearningSessions
            .Where(s => s.GenerationBatchId == batchId)
            .Select(s => new { s.Id, s.StudentProfileId, s.LearningModuleId })
            .ToListAsync(ct);

        foreach (var s in sessionIds)
        {
            if (s.StudentProfileId is null) continue;

            var activityIds = await _db.SessionExercises
                .Where(e => e.LearningSessionId == s.Id && e.LearningActivityId != null)
                .Select(e => e.LearningActivityId!.Value)
                .ToListAsync(ct);

            foreach (var activityId in activityIds)
            {
                var activity = await _db.LearningActivities
                    .FirstOrDefaultAsync(a => a.Id == activityId
                                           && a.ActivityType == ActivityType.ListeningComprehension, ct);
                if (activity is null) continue;

                await GenerateForActivityAsync(activity, s.StudentProfileId.Value, s.Id, ct);
            }
        }
    }

    public async Task GenerateForActivityAsync(
        LearningActivity activity, Guid studentProfileId, Guid? learningSessionId, CancellationToken ct)
    {
        var content = ListeningAudioService.Parse(activity.AiGeneratedContentJson);
        var transcript = content.AudioScript ?? string.Empty;
        if (string.IsNullOrWhiteSpace(transcript)) return;

        var transcriptHash = GenerationHashing.Sha256(transcript);
        // Speaker profile fingerprint must be stable across runs — derive it from the activity's
        // structured speaker metadata, NOT from any post-generation audio Voice field.
        var speakerProfileJson = $"{content.SpeakerRole}|{content.ListenerRole}|{content.Difficulty}";
        var speakerProfileHash = GenerationHashing.Sha256(speakerProfileJson);

        // Idempotency: reuse an existing ready asset with the same fingerprint.
        var existing = await _db.AudioAssets.FirstOrDefaultAsync(
            a => a.LearningActivityId == activity.Id
              && a.TranscriptHash == transcriptHash
              && a.SpeakerProfileHash == speakerProfileHash
              && a.GenerationStatus == GenerationStatus.Ready, ct);
        if (existing is not null)
        {
            _logger.LogInformation(
                "TtsAudioGenerationJob: reusing AudioAsset {AssetId} for activity {ActivityId} (idempotent).",
                existing.Id, activity.Id);
            return;
        }

        // Generate audio (stores bytes via IFileStorageService and writes StorageKey into the JSON).
        await _listeningAudio.EnsureAudioAsync(activity, "en", ct);
        await _db.SaveChangesAsync(ct);

        var storageKey = _listeningAudio.GetStorageKey(activity);
        if (string.IsNullOrWhiteSpace(storageKey))
        {
            _logger.LogWarning("TtsAudioGenerationJob: audio unavailable for activity {ActivityId}.", activity.Id);
            return;
        }

        var refreshed = ListeningAudioService.Parse(activity.AiGeneratedContentJson);
        var asset = new AudioAsset(
            studentProfileId,
            AssetType.ListeningTts,
            storageKey,
            refreshed.Audio?.ContentType ?? "audio/wav",
            learningSessionId: learningSessionId,
            learningActivityId: activity.Id,
            transcriptHash: transcriptHash,
            speakerProfileHash: speakerProfileHash,
            speakerProfileJson: speakerProfileJson,
            providerName: refreshed.Audio?.Provider,
            generationStatus: GenerationStatus.Ready);
        _db.AudioAssets.Add(asset);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "TtsAudioGenerationJob: created AudioAsset {AssetId} for activity {ActivityId}.", asset.Id, activity.Id);
    }
}

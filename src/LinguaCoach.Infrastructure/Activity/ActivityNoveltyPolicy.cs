using LinguaCoach.Application.Activity;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace LinguaCoach.Infrastructure.Activity;

/// <summary>See <see cref="IActivityNoveltyPolicy"/>.</summary>
public sealed class ActivityNoveltyPolicy : IActivityNoveltyPolicy
{
    private readonly LinguaCoachDbContext _db;
    private readonly NoveltyPolicySettings _settings;

    public ActivityNoveltyPolicy(LinguaCoachDbContext db, IOptions<NoveltyPolicySettings> settings)
    {
        _db = db;
        _settings = settings.Value;
    }

    public async Task<ActivityNoveltyResult> CheckAsync(ActivityNoveltyCheckRequest request, CancellationToken ct = default)
    {
        if (request.StudentProfileId == Guid.Empty)
            throw new ArgumentException("StudentProfileId is required.", nameof(request));
        if (string.IsNullOrWhiteSpace(request.ContentFingerprint))
            throw new ArgumentException("ContentFingerprint is required.", nameof(request));

        // Intentional review/remediation repeats are allowed by design — no cooldown applies.
        if (request.IsIntentionalReview)
            return ActivityNoveltyResult.Allow();

        var now = request.NowUtc ?? DateTime.UtcNow;

        // 1. Exact content fingerprint — the strongest signal, checked first.
        var fingerprintCutoff = now.AddDays(-_settings.FingerprintCooldownDays);
        var fingerprintMatch = await _db.StudentActivityUsageLogs
            .AsNoTracking()
            .Where(l => l.StudentProfileId == request.StudentProfileId
                     && l.ContentFingerprint == request.ContentFingerprint
                     && l.ConsumedAtUtc >= fingerprintCutoff)
            .OrderByDescending(l => l.ConsumedAtUtc)
            .Select(l => new { l.Id, l.ConsumedAtUtc, l.ContentFingerprint })
            .FirstOrDefaultAsync(ct);

        if (fingerprintMatch is not null)
        {
            return new ActivityNoveltyResult(
                Allowed: false,
                Reason: NoveltyBlockReason.SameFingerprintTooRecent,
                BlockingUsageLogId: fingerprintMatch.Id,
                CooldownUntilUtc: fingerprintMatch.ConsumedAtUtc.AddDays(_settings.FingerprintCooldownDays),
                MatchedFingerprint: fingerprintMatch.ContentFingerprint);
        }

        // 2. Same source template (bank-first path only — SourceTemplateId is null for legacy
        // freeform-generated content, so this check is inert for it).
        if (request.SourceTemplateId.HasValue)
        {
            var templateCutoff = now.AddDays(-_settings.TemplateCooldownDays);
            var templateMatch = await _db.StudentActivityUsageLogs
                .AsNoTracking()
                .Where(l => l.StudentProfileId == request.StudentProfileId
                         && l.SourceTemplateId == request.SourceTemplateId
                         && l.ConsumedAtUtc >= templateCutoff)
                .OrderByDescending(l => l.ConsumedAtUtc)
                .Select(l => new { l.Id, l.ConsumedAtUtc, l.SourceTemplateId })
                .FirstOrDefaultAsync(ct);

            if (templateMatch is not null)
            {
                return new ActivityNoveltyResult(
                    Allowed: false,
                    Reason: NoveltyBlockReason.SameTemplateTooRecent,
                    BlockingUsageLogId: templateMatch.Id,
                    CooldownUntilUtc: templateMatch.ConsumedAtUtc.AddDays(_settings.TemplateCooldownDays),
                    MatchedTemplateId: templateMatch.SourceTemplateId);
            }
        }

        // 3. Same topic key, if present.
        if (!string.IsNullOrWhiteSpace(request.TopicKey))
        {
            var topicCutoff = now.AddDays(-_settings.TopicCooldownDays);
            var topicMatch = await _db.StudentActivityUsageLogs
                .AsNoTracking()
                .Where(l => l.StudentProfileId == request.StudentProfileId
                         && l.TopicKey == request.TopicKey
                         && l.ConsumedAtUtc >= topicCutoff)
                .OrderByDescending(l => l.ConsumedAtUtc)
                .Select(l => new { l.Id, l.ConsumedAtUtc, l.TopicKey })
                .FirstOrDefaultAsync(ct);

            if (topicMatch is not null)
            {
                return new ActivityNoveltyResult(
                    Allowed: false,
                    Reason: NoveltyBlockReason.SameTopicTooRecent,
                    BlockingUsageLogId: topicMatch.Id,
                    CooldownUntilUtc: topicMatch.ConsumedAtUtc.AddDays(_settings.TopicCooldownDays),
                    MatchedTopicKey: topicMatch.TopicKey);
            }
        }

        // 4. Same scenario key, if present.
        if (!string.IsNullOrWhiteSpace(request.ScenarioKey))
        {
            var scenarioCutoff = now.AddDays(-_settings.ScenarioCooldownDays);
            var scenarioMatch = await _db.StudentActivityUsageLogs
                .AsNoTracking()
                .Where(l => l.StudentProfileId == request.StudentProfileId
                         && l.ScenarioKey == request.ScenarioKey
                         && l.ConsumedAtUtc >= scenarioCutoff)
                .OrderByDescending(l => l.ConsumedAtUtc)
                .Select(l => new { l.Id, l.ConsumedAtUtc })
                .FirstOrDefaultAsync(ct);

            if (scenarioMatch is not null)
            {
                return new ActivityNoveltyResult(
                    Allowed: false,
                    Reason: NoveltyBlockReason.SameScenarioTooRecent,
                    BlockingUsageLogId: scenarioMatch.Id,
                    CooldownUntilUtc: scenarioMatch.ConsumedAtUtc.AddDays(_settings.ScenarioCooldownDays));
            }
        }

        return ActivityNoveltyResult.Allow();
    }
}

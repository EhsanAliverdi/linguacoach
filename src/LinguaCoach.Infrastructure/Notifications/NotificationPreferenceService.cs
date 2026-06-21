using LinguaCoach.Application.Notifications;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LinguaCoach.Infrastructure.Notifications;

public sealed class NotificationPreferenceService : INotificationPreferenceService
{
    // Default enabled state when no explicit preference row exists.
    // InApp: on for all non-SMS channels. Email: on for all. SMS: always off (deferred).
    private static bool DefaultEnabled(NotificationCategory category, NotificationChannel channel)
    {
        if (channel == NotificationChannel.Sms) return false;
        return true;
    }

    private readonly LinguaCoachDbContext _db;

    public NotificationPreferenceService(LinguaCoachDbContext db) => _db = db;

    public async Task<bool> IsChannelEnabledAsync(
        Guid userId,
        NotificationCategory category,
        NotificationChannel channel,
        CancellationToken ct = default)
    {
        // Required categories always deliver regardless of preference.
        if (NotificationPreference.IsRequired(category)) return true;

        // SMS is deferred — never enabled.
        if (channel == NotificationChannel.Sms) return false;

        var pref = await _db.NotificationPreferences
            .AsNoTracking()
            .FirstOrDefaultAsync(p =>
                p.UserId == userId &&
                p.Category == category &&
                p.Channel == channel, ct);

        return pref?.IsEnabled ?? DefaultEnabled(category, channel);
    }

    public async Task<IReadOnlyList<NotificationPreferenceItem>> GetPreferencesAsync(
        Guid userId,
        CancellationToken ct = default)
    {
        var stored = await _db.NotificationPreferences
            .AsNoTracking()
            .Where(p => p.UserId == userId)
            .ToListAsync(ct);

        var result = new List<NotificationPreferenceItem>();

        // Return a row for every category x channel combination (except SMS — show as locked off).
        foreach (var category in Enum.GetValues<NotificationCategory>())
        {
            foreach (var channel in Enum.GetValues<NotificationChannel>())
            {
                bool required = NotificationPreference.IsRequired(category);
                bool sms = channel == NotificationChannel.Sms;

                var row = stored.FirstOrDefault(p => p.Category == category && p.Channel == channel);
                bool isEnabled = required || (sms ? false : (row?.IsEnabled ?? DefaultEnabled(category, channel)));

                result.Add(new NotificationPreferenceItem(category, channel, isEnabled, required || sms));
            }
        }

        return result;
    }

    public async Task UpdatePreferencesAsync(
        Guid userId,
        IEnumerable<UpdateNotificationPreferenceRequest> preferences,
        CancellationToken ct = default)
    {
        var stored = await _db.NotificationPreferences
            .Where(p => p.UserId == userId)
            .ToListAsync(ct);

        foreach (var req in preferences)
        {
            // Required categories are always enabled — silently enforce.
            bool effectiveEnabled = NotificationPreference.IsRequired(req.Category) || req.IsEnabled;

            var existing = stored.FirstOrDefault(p =>
                p.Category == req.Category && p.Channel == req.Channel);

            if (existing is not null)
            {
                existing.SetEnabled(effectiveEnabled);
            }
            else
            {
                _db.NotificationPreferences.Add(
                    NotificationPreference.Create(userId, req.Category, req.Channel, effectiveEnabled));
            }
        }

        await _db.SaveChangesAsync(ct);
    }
}

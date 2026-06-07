using LinguaCoach.Application.Vocabulary;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LinguaCoach.Infrastructure.Vocabulary;

public sealed class GetVocabularyHandler : IGetVocabularyHandler
{
    private readonly LinguaCoachDbContext _db;

    public GetVocabularyHandler(LinguaCoachDbContext db) => _db = db;

    public async Task<IReadOnlyList<StudentVocabularyItemDto>> HandleAsync(
        GetVocabularyQuery query, CancellationToken ct = default)
    {
        var profile = await _db.StudentProfiles
            .FirstOrDefaultAsync(p => p.UserId == query.UserId, ct)
            ?? throw new InvalidOperationException("Student profile not found.");

        var q = _db.StudentVocabularyItems
            .Where(v => v.StudentProfileId == profile.Id);

        if (!string.IsNullOrWhiteSpace(query.Status)
            && Enum.TryParse<VocabularyItemStatus>(query.Status, ignoreCase: true, out var statusEnum))
        {
            q = q.Where(v => v.Status == statusEnum);
        }

        if (!string.IsNullOrWhiteSpace(query.Category))
        {
            var cat = query.Category.Trim().ToLowerInvariant();
            q = q.Where(v => v.Category == cat);
        }

        var items = await q
            .OrderByDescending(v => v.CreatedAt)
            .ToListAsync(ct);

        if (items.Count == 0) return [];

        // Load source activity titles
        var activityIds = items
            .Where(i => i.SourceLearningActivityId.HasValue)
            .Select(i => i.SourceLearningActivityId!.Value)
            .Distinct()
            .ToList();

        var activityMap = activityIds.Count > 0
            ? await _db.LearningActivities
                .Where(a => activityIds.Contains(a.Id))
                .Select(a => new ActivityInfo(a.Id, a.Title, a.LearningModuleId))
                .ToDictionaryAsync(a => a.Id, ct)
            : new Dictionary<Guid, ActivityInfo>();

        var moduleIds = activityMap.Values
            .Where(a => a.LearningModuleId.HasValue)
            .Select(a => a.LearningModuleId!.Value)
            .Distinct()
            .ToList();

        var moduleMap = moduleIds.Count > 0
            ? await _db.LearningModules
                .Where(m => moduleIds.Contains(m.Id))
                .Select(m => new { m.Id, m.Title })
                .ToDictionaryAsync(m => m.Id, m => m.Title, ct)
            : new Dictionary<Guid, string>();

        return items.Select(i =>
        {
            string? actTitle = null;
            string? modTitle = null;

            if (i.SourceLearningActivityId.HasValue
                && activityMap.TryGetValue(i.SourceLearningActivityId.Value, out var act))
            {
                actTitle = act.Title;
                if (act.LearningModuleId.HasValue)
                    moduleMap.TryGetValue(act.LearningModuleId.Value, out modTitle);
            }

            return new StudentVocabularyItemDto(
                Id: i.Id,
                Term: i.Term,
                SuggestedPhrase: i.SuggestedPhrase,
                MeaningOrExplanation: i.MeaningOrExplanation,
                ExampleSentence: i.ExampleSentence,
                Category: i.Category,
                Status: i.Status.ToString(),
                Source: i.Source.ToString(),
                SeenCount: i.SeenCount,
                LastSeenAtUtc: i.LastSeenAtUtc,
                NextReviewAtUtc: i.NextReviewAtUtc,
                CreatedAt: i.CreatedAt,
                SourceActivityTitle: actTitle,
                SourceModuleTitle: modTitle);
        }).ToList();
    }

    private sealed record ActivityInfo(Guid Id, string Title, Guid? LearningModuleId);
}

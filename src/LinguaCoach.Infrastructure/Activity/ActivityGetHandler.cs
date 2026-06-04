using System.Text.Json;
using LinguaCoach.Application.Activity;
using LinguaCoach.Application.LearningPath;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Infrastructure.Progress;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LinguaCoach.Infrastructure.Activity;

/// <summary>
/// Returns the next activity for a student.
/// Primary path: AI generates a fresh activity.
/// Fallback path: returns a SystemFallback activity from DB if AI fails or is unavailable.
/// Never throws a 500 — fallback is always available if seed data is present.
/// </summary>
public sealed class ActivityGetHandler : IGetNextActivityHandler
{
    private const int CompletionThreshold = 3;

    private readonly LinguaCoachDbContext _db;
    private readonly IAiActivityGenerator _aiGenerator;
    private readonly ILearningPathGenerator _pathGenerator;
    private readonly StudentProgressService _progress;
    private readonly ILogger<ActivityGetHandler> _logger;

    public ActivityGetHandler(
        LinguaCoachDbContext db,
        IAiActivityGenerator aiGenerator,
        ILearningPathGenerator pathGenerator,
        StudentProgressService progress,
        ILogger<ActivityGetHandler> logger)
    {
        _db = db;
        _aiGenerator = aiGenerator;
        _pathGenerator = pathGenerator;
        _progress = progress;
        _logger = logger;
    }

    public async Task<ActivityDto> HandleAsync(GetNextActivityQuery query, CancellationToken ct = default)
    {
        var profile = await _db.StudentProfiles
            .Include(p => p.LanguagePair)
                .ThenInclude(lp => lp!.SourceLanguage)
            .Include(p => p.LanguagePair)
                .ThenInclude(lp => lp!.TargetLanguage)
            .Include(p => p.CareerProfile)
            .FirstOrDefaultAsync(p => p.UserId == query.UserId, ct)
            ?? throw new InvalidOperationException("Student profile not found.");

        if (profile.OnboardingStatus != Domain.Enums.OnboardingStatus.Complete)
            throw new InvalidOperationException("Activity requires completed onboarding.");

        var activityType = query.PreferredType ?? ActivityType.WritingScenario;

        // Resolve active learning path + current module (lazy-generate if missing).
        var (currentModuleId, topicHint) = await ResolveCurrentModuleAsync(profile.UserId, profile.Id, ct);

        // Detect focus area from recent feedback to guide AI generation.
        var focusArea = await _progress.GetCurrentFocusAreaAsync(profile.Id, ct);
        var recentMistakes = StudentProgressService.BuildRecentMistakesSummary(focusArea);

        // Primary path — AI generation.
        try
        {
            var context = new ActivityGenerationContext(
                ActivityType: activityType,
                CefrLevel: profile.CefrLevel ?? "B1",
                CareerContext: profile.CareerProfile?.Name ?? "General",
                LanguagePairCode: BuildPairCode(profile.LanguagePair),
                SourceLanguageName: profile.LanguagePair?.SourceLanguage?.Name ?? "Persian",
                TargetLanguageName: profile.LanguagePair?.TargetLanguage?.Name ?? "English",
                TopicHint: topicHint,
                RecentMistakesSummary: recentMistakes);

            var contentJson = await _aiGenerator.GenerateActivityContentAsync(context, ct);

            var cefrLevel = profile.CefrLevel ?? "B1";
            var title = ExtractTitle(contentJson, activityType);

            var activity = new Domain.Entities.LearningActivity(
                activityType: activityType,
                source: ActivitySource.AiGenerated,
                title: title,
                difficulty: cefrLevel,
                aiGeneratedContentJson: contentJson,
                learningModuleId: currentModuleId);

            _db.LearningActivities.Add(activity);
            await _db.SaveChangesAsync(ct);

            return MapToDto(activity);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "AI activity generation failed for user {UserId}, activityType {Type}. Returning SystemFallback.",
                query.UserId, activityType);
        }

        // Fallback path — return a seeded SystemFallback activity.
        var fallbacks = await _db.LearningActivities
            .Where(a => a.ActivityType == activityType
                     && a.Source == ActivitySource.SystemFallback
                     && a.IsActive)
            .ToListAsync(ct);

        var fallback = fallbacks.Count > 0
            ? fallbacks[Random.Shared.Next(fallbacks.Count)]
            : null;

        if (fallback is null)
            throw new InvalidOperationException(
                $"No SystemFallback activity found for type {activityType}. Ensure seed data has run.");

        return MapToDto(fallback);
    }

    private async Task<(Guid? ModuleId, string? TopicHint)> ResolveCurrentModuleAsync(
        Guid userId, Guid studentProfileId, CancellationToken ct)
    {
        try
        {
            var path = await _db.LearningPaths
                .Include(p => p.Modules)
                .FirstOrDefaultAsync(p => p.StudentProfileId == studentProfileId && p.IsActive, ct);

            if (path is null)
            {
                // Lazy generation: student has no path yet (e.g. existing test account).
                _logger.LogInformation(
                    "No active LearningPath for profile {ProfileId}. Generating default path lazily.",
                    studentProfileId);
                await _pathGenerator.GenerateAsync(new Application.LearningPath.GenerateLearningPathCommand(userId), ct);

                path = await _db.LearningPaths
                    .Include(p => p.Modules)
                    .FirstOrDefaultAsync(p => p.StudentProfileId == studentProfileId && p.IsActive, ct);
            }

            if (path is null || path.Modules.Count == 0)
                return (null, null);

            var modules = path.Modules.OrderBy(m => m.Order).ToList();
            var moduleIds = modules.Select(m => m.Id).ToList();

            var completedCounts = await _db.ActivityAttempts
                .Where(a => a.StudentProfileId == studentProfileId)
                .Join(_db.LearningActivities.Where(la => la.LearningModuleId.HasValue && moduleIds.Contains(la.LearningModuleId!.Value)),
                      attempt => attempt.LearningActivityId,
                      activity => activity.Id,
                      (attempt, activity) => activity.LearningModuleId!.Value)
                .GroupBy(moduleId => moduleId)
                .Select(g => new { ModuleId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.ModuleId, x => x.Count, ct);

            var current = modules.FirstOrDefault(m =>
                completedCounts.GetValueOrDefault(m.Id, 0) < CompletionThreshold)
                ?? modules.Last();

            var hint = $"{current.Title}: {current.Description}";
            return (current.Id, hint);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to resolve current module for profile {ProfileId}. Proceeding without module context.", studentProfileId);
            return (null, null);
        }
    }

    private static string BuildPairCode(Domain.Entities.LanguagePair? pair)
    {
        if (pair is null) return "fa-en";
        var src = pair.SourceLanguage?.Code ?? "fa";
        var tgt = pair.TargetLanguage?.Code ?? "en";
        return $"{src}-{tgt}";
    }

    private static ActivityDto MapToDto(Domain.Entities.LearningActivity activity)
    {
        WritingContent? wc = null;
        if (activity.ActivityType == ActivityType.WritingScenario)
        {
            try
            {
                wc = JsonSerializer.Deserialize<WritingContent>(
                    activity.AiGeneratedContentJson,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            catch { /* leave null, safe defaults below */ }
        }

        return new ActivityDto(
            ActivityId: activity.Id,
            ActivityType: activity.ActivityType,
            Source: activity.Source,
            Title: activity.Title,
            Difficulty: activity.Difficulty,
            Situation: wc?.Situation,
            LearningGoal: wc?.LearningGoal,
            TargetPhrases: wc?.TargetPhrases ?? [],
            TargetVocabulary: wc?.TargetVocabulary ?? [],
            ExampleText: wc?.ExampleText,
            CommonMistakeToAvoid: wc?.CommonMistakeToAvoid,
            InstructionInSourceLanguage: wc?.InstructionInSourceLanguage);
    }

    private static string ExtractTitle(string contentJson, ActivityType type)
    {
        try
        {
            using var doc = JsonDocument.Parse(contentJson);
            if (doc.RootElement.TryGetProperty("title", out var t) && t.ValueKind == JsonValueKind.String)
                return t.GetString() ?? $"AI {type} activity";
            if (doc.RootElement.TryGetProperty("situation", out var s) && s.ValueKind == JsonValueKind.String)
            {
                var sit = s.GetString() ?? string.Empty;
                return sit.Length > 100 ? sit[..100] + "…" : sit;
            }
        }
        catch { /* ignore */ }
        return $"AI {type} activity";
    }

    private sealed class WritingContent
    {
        public string? Situation { get; set; }
        public string? LearningGoal { get; set; }
        public string[]? TargetPhrases { get; set; }
        public string[]? TargetVocabulary { get; set; }
        public string? ExampleText { get; set; }
        public string? CommonMistakeToAvoid { get; set; }
        public string? InstructionInSourceLanguage { get; set; }
    }
}

using System.Text.Json;
using System.Text.Json.Serialization;
using LinguaCoach.Application.Ai;
using LinguaCoach.Application.LearningPath;
using LinguaCoach.Application.Memory;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.ValueObjects;
using LinguaCoach.Infrastructure.Ai;
using LinguaCoach.Infrastructure.Progress;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LinguaCoach.Infrastructure.Memory;

public sealed class StudentMemoryService : IStudentMemoryService, IStudentMemoryQuery
{
    private const string PromptKey = "student_memory_update";

    private static readonly Dictionary<string, string> SkillLabels = new(StringComparer.OrdinalIgnoreCase)
    {
        ["grammar_accuracy"] = "Grammar accuracy",
        ["formal_tone"] = "Formal workplace tone",
        ["sentence_clarity"] = "Sentence clarity",
        ["message_structure"] = "Message structure",
        ["workplace_vocabulary"] = "Workplace vocabulary",
        ["concise_writing"] = "Concise writing",
        ["softening_language"] = "Softening language",
        ["summarising_information"] = "Summarising information",
        ["clarifying_questions"] = "Clarifying questions",
        ["escalation_language"] = "Escalation language",
    };

    private readonly LinguaCoachDbContext _db;
    private readonly IAiContextBuilder _contextBuilder;
    private readonly AiExecutionService _aiExecution;
    private readonly StudentProgressService _progress;
    private readonly ILogger<StudentMemoryService> _logger;

    public StudentMemoryService(
        LinguaCoachDbContext db,
        IAiContextBuilder contextBuilder,
        AiExecutionService aiExecution,
        StudentProgressService progress,
        ILogger<StudentMemoryService> logger)
    {
        _db = db;
        _contextBuilder = contextBuilder;
        _aiExecution = aiExecution;
        _progress = progress;
        _logger = logger;
    }

    public async Task<UserLearningSummary> GetOrCreateWithBootstrapAsync(Guid studentProfileId, CancellationToken ct = default)
    {
        var existing = await _db.UserLearningSummaries.FirstOrDefaultAsync(x => x.StudentProfileId == studentProfileId, ct);
        if (existing is not null) return existing;

        var summary = new UserLearningSummary(studentProfileId);
        var focus = await _progress.GetCurrentFocusAreaAsync(studentProfileId, ct);
        if (focus is not null)
        {
            summary.ApplyDelta(new MemoryUpdateDelta(
                JourneySummaryDelta: $"Recent practice suggests the next useful focus is {focus.FriendlyLabel}.",
                NewStrengths: [],
                NewWeaknesses: [focus.FriendlyLabel],
                RecurringMistakesToAdd: [],
                CoveredScenariosToAdd: [],
                WeakSkillKeys: [MapCategoryToSkillKey(focus.Category)],
                StrongSkillKeys: [],
                RecommendedNextFocus: [focus.FriendlyLabel]));
        }

        _db.UserLearningSummaries.Add(summary);
        await UpsertSkillProfilesAsync(studentProfileId, focus is null ? [] : [MapCategoryToSkillKey(focus.Category)], [], ct);
        await _db.SaveChangesAsync(ct);
        return summary;
    }

    public async Task UpdateMemoryAsync(ActivityMemoryUpdateRequest request, CancellationToken ct = default)
    {
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromSeconds(8));

            var summary = await GetOrCreateWithBootstrapAsync(request.StudentProfile.Id, timeout.Token);
            var contextJson = await BuildMemoryUpdateContextJsonAsync(request, summary, timeout.Token);
            var aiRequest = await _contextBuilder.BuildAsync(PromptKey, new Dictionary<string, string>
            {
                ["memoryUpdateContext"] = contextJson
            }, timeout.Token);

            var response = await _aiExecution.ExecuteWithFallbackAsync(
                PromptKey, aiRequest, request.StudentProfile.Id, request.CorrelationId, timeout.Token);

            var delta = ParseDelta(response);
            summary.ApplyDelta(delta);
            await UpsertSkillProfilesAsync(request.StudentProfile.Id, delta.WeakSkillKeys, delta.StrongSkillKeys, timeout.Token);
            await _db.SaveChangesAsync(timeout.Token);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Student memory update failed StudentProfileId={StudentProfileId} ActivityAttemptId={AttemptId} CorrelationId={CorrelationId}",
                request.StudentProfile.Id, request.Attempt.Id, request.CorrelationId);
        }
    }

    public async Task<string> BuildAdaptiveContextJsonAsync(Guid studentProfileId, int moduleCount, CancellationToken ct = default)
    {
        var summary = await GetOrCreateWithBootstrapAsync(studentProfileId, ct);
        var skills = await _db.StudentSkillProfiles
            .Where(x => x.StudentProfileId == studentProfileId)
            .OrderBy(x => x.SkillKey)
            .Select(x => new { x.SkillKey, x.SkillLabel, x.IsWeak })
            .ToListAsync(ct);

        var path = await _db.LearningPaths
            .Include(p => p.Modules)
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.StudentProfileId == studentProfileId && p.IsActive, ct);

        var existingFingerprints = path?.Modules
            .Where(m => !string.IsNullOrWhiteSpace(m.FingerprintJson))
            .Select(m => SafeDeserialize(m.FingerprintJson!))
            .Where(x => x is not null)
            .ToList() ?? [];

        var profile = await _db.StudentProfiles
            .Include(p => p.CareerProfile)
            .FirstAsync(p => p.Id == studentProfileId, ct);

        var payload = new
        {
            studentProfile = new
            {
                cefrLevel = profile.CefrLevel ?? "B1",
                careerProfile = profile.CareerProfile?.Name ?? "General workplace"
            },
            memory = ToMemoryObject(summary),
            skillProfile = skills,
            existingFingerprints,
            curriculumMap = WorkplaceWritingCurriculumMap.Levels,
            moduleCount = Math.Clamp(moduleCount, 3, 5)
        };

        return JsonSerializer.Serialize(payload);
    }

    public async Task<StudentLearningMemoryDto> GetForUserAsync(Guid userId, CancellationToken ct = default)
    {
        var profile = await _db.StudentProfiles.FirstOrDefaultAsync(p => p.UserId == userId, ct)
            ?? throw new InvalidOperationException("Student profile not found.");
        return await GetForStudentProfileAsync(profile.Id, ct);
    }

    public async Task<StudentLearningMemoryDto> GetForStudentProfileAsync(Guid studentProfileId, CancellationToken ct = default)
    {
        var exists = await _db.StudentProfiles.AnyAsync(p => p.Id == studentProfileId, ct);
        if (!exists) throw new InvalidOperationException("Student profile not found.");

        var summary = await GetOrCreateWithBootstrapAsync(studentProfileId, ct);
        var skills = await _db.StudentSkillProfiles
            .Where(x => x.StudentProfileId == studentProfileId)
            .OrderBy(x => x.SkillKey)
            .Select(x => new StudentSkillProfileDto(x.SkillKey, x.SkillLabel, x.IsWeak))
            .ToListAsync(ct);

        return new StudentLearningMemoryDto(
            summary.JourneySummary,
            JsonStringList.Read(summary.StrongSkillsJson),
            JsonStringList.Read(summary.WeakSkillsJson),
            JsonStringList.Read(summary.RecurringMistakesJson),
            JsonStringList.Read(summary.NextFocusJson),
            JsonStringList.Read(summary.CoveredScenariosJson).Count,
            skills);
    }

    private async Task<string> BuildMemoryUpdateContextJsonAsync(ActivityMemoryUpdateRequest request, UserLearningSummary summary, CancellationToken ct)
    {
        var skills = await _db.StudentSkillProfiles
            .Where(x => x.StudentProfileId == request.StudentProfile.Id)
            .Select(x => new { x.SkillKey, x.SkillLabel, x.IsWeak })
            .ToListAsync(ct);

        var feedback = ExtractCompactFeedback(request.FeedbackJson);
        var payload = new
        {
            studentProfile = new
            {
                cefrLevel = request.StudentProfile.CefrLevel ?? "B1",
                careerProfile = request.StudentProfile.CareerProfile?.Name ?? "General workplace"
            },
            currentMemory = ToMemoryObject(summary),
            skillProfile = skills,
            activityMetadata = new
            {
                moduleTitle = request.Module?.Title,
                activityType = request.Activity.ActivityType.ToString(),
                scenario = ExtractScenarioLabel(request.Activity.AiGeneratedContentJson)
            },
            evaluationFeedback = feedback,
            score = request.Score
        };

        return JsonSerializer.Serialize(payload);
    }

    private static object ToMemoryObject(UserLearningSummary summary) => new
    {
        journeySummary = summary.JourneySummary,
        strongSkills = JsonStringList.Read(summary.StrongSkillsJson),
        weakSkills = JsonStringList.Read(summary.WeakSkillsJson),
        recurringMistakes = JsonStringList.Read(summary.RecurringMistakesJson),
        coveredScenarios = JsonStringList.Read(summary.CoveredScenariosJson),
        nextRecommendedFocus = JsonStringList.Read(summary.NextFocusJson)
    };

    private async Task UpsertSkillProfilesAsync(Guid studentProfileId, IReadOnlyList<string> weakKeys, IReadOnlyList<string> strongKeys, CancellationToken ct)
    {
        var keys = weakKeys.Concat(strongKeys)
            .Select(StudentSkillProfile.NormaliseSkillKey)
            .Where(SkillLabels.ContainsKey)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (keys.Count == 0) return;

        var existing = await _db.StudentSkillProfiles
            .Where(x => x.StudentProfileId == studentProfileId && keys.Contains(x.SkillKey))
            .ToDictionaryAsync(x => x.SkillKey, StringComparer.OrdinalIgnoreCase, ct);

        foreach (var key in keys)
        {
            var isWeak = weakKeys.Select(StudentSkillProfile.NormaliseSkillKey).Contains(key, StringComparer.OrdinalIgnoreCase)
                && !strongKeys.Select(StudentSkillProfile.NormaliseSkillKey).Contains(key, StringComparer.OrdinalIgnoreCase);

            if (existing.TryGetValue(key, out var profile))
            {
                profile.MarkWeak(isWeak);
            }
            else
            {
                _db.StudentSkillProfiles.Add(new StudentSkillProfile(studentProfileId, key, SkillLabels[key], isWeak));
            }
        }
    }

    private static MemoryUpdateDelta ParseDelta(string raw)
    {
        var cleaned = CleanJson(raw);
        var payload = JsonSerializer.Deserialize<MemoryUpdateDeltaPayload>(cleaned, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }) ?? new MemoryUpdateDeltaPayload();

        return new MemoryUpdateDelta(
            payload.JourneySummaryDelta,
            payload.NewStrengths ?? [],
            payload.NewWeaknesses ?? [],
            payload.RecurringMistakesToAdd ?? [],
            payload.CoveredScenariosToAdd ?? [],
            payload.WeakSkillKeys ?? [],
            payload.StrongSkillKeys ?? [],
            payload.RecommendedNextFocus ?? []);
    }

    private static object ExtractCompactFeedback(string feedbackJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(feedbackJson);
            var root = doc.RootElement;
            return new
            {
                overallScore = root.TryGetProperty("overallScore", out var score) ? score.Clone() : default,
                coachSummary = root.TryGetProperty("coachSummary", out var summary) ? summary.GetString() : null,
                changes = root.TryGetProperty("changes", out var changes) && changes.ValueKind == JsonValueKind.Array
                    ? changes.EnumerateArray().Take(5).Select(c => new
                    {
                        category = c.TryGetProperty("category", out var cat) ? cat.GetString() : null,
                        severity = c.TryGetProperty("severity", out var sev) ? sev.GetString() : null,
                        reason = c.TryGetProperty("reason", out var reason) ? reason.GetString() : null
                    }).ToList()
                    : []
            };
        }
        catch
        {
            return new { };
        }
    }

    private static string? ExtractScenarioLabel(string contentJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(contentJson);
            var root = doc.RootElement;
            foreach (var key in new[] { "situation", "scenario", "task", "learningGoal" })
            {
                if (root.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.String)
                    return value.GetString();
            }
        }
        catch { }
        return null;
    }

    private static object? SafeDeserialize(string json)
    {
        try { return JsonSerializer.Deserialize<object>(json); }
        catch { return null; }
    }

    private static string CleanJson(string raw)
    {
        var cleaned = raw.Trim();
        if (!cleaned.StartsWith("```")) return cleaned;
        var firstNewline = cleaned.IndexOf('\n');
        var lastFence = cleaned.LastIndexOf("```", StringComparison.Ordinal);
        return firstNewline > 0 && lastFence > firstNewline
            ? cleaned[(firstNewline + 1)..lastFence].Trim()
            : cleaned;
    }

    private static string MapCategoryToSkillKey(string category) => category.ToLowerInvariant() switch
    {
        "grammar" => "grammar_accuracy",
        "vocabulary" => "workplace_vocabulary",
        "tone" => "formal_tone",
        "clarity" => "sentence_clarity",
        "structure" => "message_structure",
        _ => "message_structure"
    };
}

internal sealed class MemoryUpdateDeltaPayload
{
    [JsonPropertyName("journeySummaryDelta")] public string? JourneySummaryDelta { get; set; }
    [JsonPropertyName("newStrengths")] public List<string>? NewStrengths { get; set; }
    [JsonPropertyName("newWeaknesses")] public List<string>? NewWeaknesses { get; set; }
    [JsonPropertyName("recurringMistakesToAdd")] public List<string>? RecurringMistakesToAdd { get; set; }
    [JsonPropertyName("coveredScenariosToAdd")] public List<string>? CoveredScenariosToAdd { get; set; }
    [JsonPropertyName("weakSkillKeys")] public List<string>? WeakSkillKeys { get; set; }
    [JsonPropertyName("strongSkillKeys")] public List<string>? StrongSkillKeys { get; set; }
    [JsonPropertyName("recommendedNextFocus")] public List<string>? RecommendedNextFocus { get; set; }
}

internal static class WorkplaceWritingCurriculumMap
{
    public static readonly object[] Levels =
    [
        new { level = "B1", skills = new[] { "simple work updates", "requesting information", "explaining problems", "confirming details", "writing short emails", "summarising simply" } },
        new { level = "B1+", skills = new[] { "explaining delays professionally", "softening requests", "writing follow-up emails", "reporting progress", "clarifying misunderstandings", "summarising meeting outcomes" } },
        new { level = "B2", skills = new[] { "escalating diplomatically", "writing recommendations", "challenging decisions politely", "writing incident reports", "comparing options", "negotiating deadlines" } },
    ];
}

using System.Text.Json;
using LinguaCoach.Application.Curriculum;
using LinguaCoach.Application.Modules;
using LinguaCoach.Application.SkillGraph;
using LinguaCoach.Domain.Constants;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LinguaCoach.Infrastructure.SkillGraph;

/// <summary>See <see cref="ISkillGraphRoutingService"/> for the full rationale.</summary>
public sealed class SkillGraphRoutingService : ISkillGraphRoutingService
{
    private static readonly string[] CoreCefrLevels =
        [CefrLevelConstants.A1, CefrLevelConstants.A2, CefrLevelConstants.B1,
         CefrLevelConstants.B2, CefrLevelConstants.C1, CefrLevelConstants.C2];

    private readonly LinguaCoachDbContext _db;
    private readonly ILogger<SkillGraphRoutingService> _logger;

    public SkillGraphRoutingService(LinguaCoachDbContext db, ILogger<SkillGraphRoutingService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public string NormalizeCefrLevel(string? rawLevel)
    {
        if (string.IsNullOrWhiteSpace(rawLevel))
            return CefrLevelConstants.A1;

        var trimmed = rawLevel.Trim().ToUpperInvariant();
        var core = trimmed.TrimEnd('+', '-', '*');

        if (CefrLevelConstants.IsValid(core))
            return core;

        foreach (var level in CoreCefrLevels)
        {
            if (trimmed.StartsWith(level, StringComparison.OrdinalIgnoreCase))
                return level;
        }

        _logger.LogWarning("SkillGraphRoutingService: unrecognised CEFR level '{RawLevel}', defaulting to A1.", rawLevel);
        return CefrLevelConstants.A1;
    }

    public async Task<SkillGraphRoutingRecommendation> RecommendAsync(
        SkillGraphRoutingRequest request, CancellationToken ct = default)
    {
        var normalizedLevel = NormalizeCefrLevel(request.CurrentCefrLevel);
        var contextTags = CurriculumContextMapper.MapFromResolvedContext(request.ResolvedLearningGoalContext);
        var focusAreas = ResolveFocusAreas(request);
        var preferredBand = ResolveDifficultyBand(request.DifficultyPreference);

        var candidates = await GetNodeCandidatesAsync(normalizedLevel, request.PrimarySkill, ct);
        var isLower = false;

        if (candidates.Count == 0 && request.AllowReviewOrScaffold)
        {
            var lowerLevel = GetOneLevelDown(normalizedLevel);
            if (lowerLevel is not null)
            {
                candidates = await GetNodeCandidatesAsync(lowerLevel, request.PrimarySkill, ct);
                isLower = true;
            }
        }

        if (candidates.Count == 0)
        {
            _logger.LogInformation(
                "SkillGraphRoutingService: fallback routing Level={Level} Source={Source}",
                normalizedLevel, request.Source);
            return BuildFallback(request, normalizedLevel, contextTags, preferredBand);
        }

        var best = SelectBestCandidate(candidates, preferredBand, contextTags, focusAreas);
        var reason = isLower
            ? SkillGraphRoutingReason.Review
            : best.HasEligibleContent ? SkillGraphRoutingReason.ContentBacked : SkillGraphRoutingReason.Normal;

        _logger.LogInformation(
            "SkillGraphRoutingService: routing Level={Level} NodeKey={Key} Reason={Reason} Source={Source}",
            normalizedLevel, best.Key, reason, request.Source);

        return BuildRecommendation(request, normalizedLevel, best, preferredBand, contextTags, reason, isLower);
    }

    private async Task<List<NodeCandidate>> GetNodeCandidatesAsync(string cefrLevel, string? skill, CancellationToken ct)
    {
        var query = _db.SkillGraphNodes.AsNoTracking()
            .Where(n => n.ReviewStatus == AdminReviewStatus.Approved && n.IsActive && n.CefrLevel == cefrLevel);

        if (!string.IsNullOrWhiteSpace(skill))
        {
            var normSkill = skill.ToLowerInvariant();
            query = query.Where(n => n.Skill == normSkill);
        }

        var nodes = await query.ToListAsync(ct);
        if (nodes.Count == 0) return [];

        var nodeIds = nodes.Select(n => n.Id).ToList();
        var eligibleLinks = await _db.ModuleSkillGraphNodeLinks.AsNoTracking()
            .Where(l => nodeIds.Contains(l.SkillGraphNodeId))
            .Join(_db.Modules.AsNoTracking().Where(ModuleEligibility.AvailableForNewStudentDeliveryExpr),
                l => l.ModuleId, m => m.Id, (l, m) => new { l.SkillGraphNodeId, m.ContextTagsJson })
            .ToListAsync(ct);

        var linksByNode = eligibleLinks.ToLookup(x => x.SkillGraphNodeId);

        return nodes.Select(n =>
        {
            var nodeLinks = linksByNode[n.Id].ToList();
            var contextTags = nodeLinks
                .SelectMany(x => SafeParseStringArray(x.ContextTagsJson))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            return new NodeCandidate(n.Key, n.Title, n.CefrLevel, n.Skill, n.DifficultyBand, nodeLinks.Count > 0, contextTags);
        }).ToList();
    }

    private static NodeCandidate SelectBestCandidate(
        IReadOnlyList<NodeCandidate> candidates, int preferredBand,
        IReadOnlyList<string> contextTags, IReadOnlyList<string> focusAreas) =>
        candidates
            .OrderByDescending(c => c.HasEligibleContent)
            .ThenByDescending(c => ContextOverlapScore(c, contextTags, focusAreas))
            .ThenBy(c => Math.Abs(c.DifficultyBand - preferredBand))
            .ThenBy(c => c.Key, StringComparer.OrdinalIgnoreCase)
            .First();

    private static int ContextOverlapScore(
        NodeCandidate candidate, IReadOnlyList<string> contextTags, IReadOnlyList<string> focusAreas) =>
        candidate.ContextTags.Count(t =>
            contextTags.Contains(t, StringComparer.OrdinalIgnoreCase) || focusAreas.Contains(t, StringComparer.OrdinalIgnoreCase));

    private static SkillGraphRoutingRecommendation BuildRecommendation(
        SkillGraphRoutingRequest request, string normalizedLevel, NodeCandidate node, int difficultyBand,
        IReadOnlyList<string> contextTags, SkillGraphRoutingReason reason, bool isLower)
    {
        var targetLevel = isLower ? node.CefrLevel : normalizedLevel;
        return new SkillGraphRoutingRecommendation(
            TargetCefrLevel: targetLevel,
            PrimarySkill: node.Skill,
            NodeKey: node.Key,
            NodeTitle: node.Title,
            ContextTags: contextTags,
            DifficultyBand: difficultyBand,
            RoutingReason: reason,
            IsLowerLevelContent: isLower,
            Source: request.Source,
            Explanation: isLower
                ? $"Lower-level content ({targetLevel} vs student {normalizedLevel}): {reason}"
                : $"Exact-level match at {targetLevel}: {reason}");
    }

    private static SkillGraphRoutingRecommendation BuildFallback(
        SkillGraphRoutingRequest request, string normalizedLevel, IReadOnlyList<string> contextTags, int difficultyBand)
    {
        var safeContextTags = contextTags.Contains(CurriculumContextTagConstants.Workplace, StringComparer.OrdinalIgnoreCase)
            ? contextTags.Where(t => !t.Equals(CurriculumContextTagConstants.Workplace, StringComparison.OrdinalIgnoreCase)).ToList()
            : (IReadOnlyList<string>)contextTags;

        if (safeContextTags.Count == 0)
            safeContextTags = [CurriculumContextTagConstants.GeneralEnglish];

        return new SkillGraphRoutingRecommendation(
            TargetCefrLevel: normalizedLevel,
            PrimarySkill: request.PrimarySkill,
            NodeKey: null,
            NodeTitle: null,
            ContextTags: safeContextTags,
            DifficultyBand: difficultyBand,
            RoutingReason: SkillGraphRoutingReason.Fallback,
            IsLowerLevelContent: false,
            Source: request.Source,
            Explanation: $"No skill-graph node found for level {normalizedLevel}; fallback to general_english");
    }

    private static IReadOnlyList<string> ResolveFocusAreas(SkillGraphRoutingRequest request)
    {
        var areas = (request.FocusAreas ?? [])
            .Where(f => !string.IsNullOrWhiteSpace(f))
            .ToList();

        if (!string.IsNullOrWhiteSpace(request.CustomFocusArea))
            areas.Add(request.CustomFocusArea.Trim());

        return areas;
    }

    private static int ResolveDifficultyBand(string? difficultyPreference) =>
        difficultyPreference?.ToLowerInvariant() switch
        {
            "gentle" => 1,
            "challenging" => 4,
            _ => 2
        };

    private static string? GetOneLevelDown(string cefrLevel)
    {
        var idx = Array.IndexOf(CoreCefrLevels, cefrLevel);
        return idx > 0 ? CoreCefrLevels[idx - 1] : null;
    }

    private static List<string> SafeParseStringArray(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try
        {
            return JsonSerializer.Deserialize<List<string>>(json) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private sealed record NodeCandidate(
        string Key, string Title, string CefrLevel, string Skill, int DifficultyBand,
        bool HasEligibleContent, IReadOnlyList<string> ContextTags);
}

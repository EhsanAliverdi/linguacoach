using System.Text.Json;
using LinguaCoach.Application.AdminRepair;
using LinguaCoach.Application.SkillGraph;
using LinguaCoach.Domain.Constants;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Infrastructure.AdminRepair;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LinguaCoach.Infrastructure.SkillGraph;

/// <summary>
/// Sprint 14.1 — diagnoses and AI-repairs a SkillGraphNode missing ContextTags/FocusTags, mirroring
/// <see cref="Modules.ModuleRepairService"/>'s exact shape. Unlike Module's repair (blocked once
/// Approved), tag repair is NOT gated on ReviewStatus — see <see cref="SkillGraphNode.UpdateTags"/>'s
/// doc comment: almost every existing node is already Approved from the Sprint 1 bulk-approval
/// sweep, so gating on approval would make backfilling tags onto the real dataset impossible.
/// </summary>
public sealed class SkillGraphNodeRepairService : ISkillGraphNodeRepairService
{
    private readonly LinguaCoachDbContext _db;
    private readonly AdminRepairFieldGenerator _fieldGenerator;

    public SkillGraphNodeRepairService(LinguaCoachDbContext db, AdminRepairFieldGenerator fieldGenerator)
    {
        _db = db;
        _fieldGenerator = fieldGenerator;
    }

    public async Task<IReadOnlyList<DiagnosticIssue>> DiagnoseAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await _db.SkillGraphNodes.AsNoTracking().FirstOrDefaultAsync(n => n.Id == id, ct)
            ?? throw new InvalidOperationException($"Skill-graph node '{id}' was not found.");
        return Diagnose(entity);
    }

    public async Task<SkillGraphNodeRepairResult> RepairAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await _db.SkillGraphNodes.FirstOrDefaultAsync(n => n.Id == id, ct)
            ?? throw new InvalidOperationException($"Skill-graph node '{id}' was not found.");

        var issues = Diagnose(entity);
        var fixable = issues.Where(i => i.AutoFixable).ToList();
        if (fixable.Count == 0)
            throw new InvalidOperationException("Nothing to repair — no auto-fixable issues were found on this node.");

        var (fixedIssues, providerName, modelName) = await ApplyRepairAsync(entity, issues, ct);
        await _db.SaveChangesAsync(ct);

        var dto = ToDto(entity);
        var remaining = issues.Where(i => !fixedIssues.Contains(i)).ToList();
        return new SkillGraphNodeRepairResult(dto, fixedIssues, remaining, providerName, modelName);
    }

    public async Task<IssuesSummary> GetIssuesSummaryAsync(CancellationToken ct = default)
    {
        var entities = await _db.SkillGraphNodes.AsNoTracking().Where(n => n.IsActive).ToListAsync(ct);
        var withIssues = entities.Count(e => Diagnose(e).Any(i => i.AutoFixable));
        return new IssuesSummary(entities.Count, withIssues);
    }

    public async Task<IReadOnlyList<RepairableItemSummary>> ListWithIssuesAsync(CancellationToken ct = default)
    {
        var entities = await _db.SkillGraphNodes.AsNoTracking().Where(n => n.IsActive).ToListAsync(ct);
        return entities
            .Where(e => Diagnose(e).Any(i => i.AutoFixable))
            .Select(e => new RepairableItemSummary(e.Id, e.Title))
            .ToList();
    }

    public async Task<BulkRepairResult> RepairAllAsync(CancellationToken ct = default)
    {
        var entities = await _db.SkillGraphNodes.Where(n => n.IsActive).ToListAsync(ct);

        var errors = new List<string>();
        var withIssues = 0;
        var repaired = 0;

        foreach (var entity in entities)
        {
            var issues = Diagnose(entity);
            if (!issues.Any(i => i.AutoFixable)) continue;
            withIssues++;
            try
            {
                await ApplyRepairAsync(entity, issues, ct);
                repaired++;
            }
            catch (Exception ex)
            {
                errors.Add($"{entity.Title}: {ex.Message}");
            }
        }

        if (repaired > 0)
            await _db.SaveChangesAsync(ct);

        return new BulkRepairResult(entities.Count, withIssues, repaired, withIssues - repaired, errors);
    }

    private async Task<(List<DiagnosticIssue> FixedIssues, string? ProviderName, string? ModelName)> ApplyRepairAsync(
        SkillGraphNode entity, List<DiagnosticIssue> issues, CancellationToken ct)
    {
        string? providerName = null;
        string? modelName = null;
        var fixedIssues = new List<DiagnosticIssue>();
        var context = $"Title: \"{entity.Title}\". Skill: {entity.Skill}. Subskill: {entity.Subskill ?? "unspecified"}. CEFR level: {entity.CefrLevel}.";
        var vocabulary = string.Join(", ", CurriculumContextTagConstants.All);

        string? contextTagsJson = null;
        if (issues.Any(i => i.Code == "missing_context_tags"))
        {
            var field = await _fieldGenerator.GenerateFieldAsync(
                "skill-graph competency node",
                $"1-3 comma-separated real-world context tags for this node, chosen ONLY from this exact list (no other words, no explanation): {vocabulary}",
                context, ct);
            var tags = ParseAndValidateTags(field.Value);
            if (tags.Count > 0)
            {
                contextTagsJson = JsonSerializer.Serialize(tags);
                (providerName, modelName) = (field.ProviderName, field.ModelName);
                fixedIssues.Add(issues.First(i => i.Code == "missing_context_tags"));
            }
        }

        string? focusTagsJson = null;
        if (issues.Any(i => i.Code == "missing_focus_tags"))
        {
            var field = await _fieldGenerator.GenerateFieldAsync(
                "skill-graph competency node",
                $"1-2 comma-separated finer-grained focus tags for this node, chosen ONLY from this exact list (no other words, no explanation): {vocabulary}",
                context, ct);
            var tags = ParseAndValidateTags(field.Value);
            if (tags.Count > 0)
            {
                focusTagsJson = JsonSerializer.Serialize(tags);
                (providerName, modelName) = (field.ProviderName, field.ModelName);
                fixedIssues.Add(issues.First(i => i.Code == "missing_focus_tags"));
            }
        }

        if (contextTagsJson is not null || focusTagsJson is not null)
            entity.UpdateTags(contextTagsJson, focusTagsJson);

        return (fixedIssues, providerName, modelName);
    }

    /// <summary>The shared field generator only returns free text, so the AI is asked for a
    /// comma-separated list and every candidate is validated against the real vocabulary before
    /// being trusted — an AI-hallucinated tag is silently dropped, never applied.</summary>
    private static List<string> ParseAndValidateTags(string rawValue) =>
        rawValue
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(t => t.Trim().ToLowerInvariant().Replace(' ', '_'))
            .Where(CurriculumContextTagConstants.IsValid)
            .Distinct()
            .Take(3)
            .ToList();

    private static List<DiagnosticIssue> Diagnose(SkillGraphNode entity)
    {
        var issues = new List<DiagnosticIssue>();
        if (IsEmptyTagArray(entity.ContextTagsJson))
            issues.Add(new DiagnosticIssue("missing_context_tags", "Missing context tags (e.g. workplace, travel).", true));
        if (IsEmptyTagArray(entity.FocusTagsJson))
            issues.Add(new DiagnosticIssue("missing_focus_tags", "Missing focus tags.", true));
        return issues;
    }

    private static bool IsEmptyTagArray(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return true;
        try
        {
            var tags = JsonSerializer.Deserialize<List<string>>(json);
            return tags is null || tags.Count == 0;
        }
        catch (JsonException) { return true; }
    }

    private static SkillGraphNodeDto ToDto(SkillGraphNode entity) => new(
        entity.Id, entity.Key, entity.Title, entity.CefrLevel, entity.Skill, entity.Subskill,
        ParseTagsOrEmpty(entity.ContextTagsJson), ParseTagsOrEmpty(entity.FocusTagsJson));

    private static List<string> ParseTagsOrEmpty(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try { return JsonSerializer.Deserialize<List<string>>(json) ?? []; }
        catch (JsonException) { return []; }
    }
}

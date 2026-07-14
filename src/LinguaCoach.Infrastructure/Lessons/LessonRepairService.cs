using LinguaCoach.Application.AdminRepair;
using LinguaCoach.Application.Lessons;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Infrastructure.AdminRepair;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LinguaCoach.Infrastructure.Lessons;

/// <summary>
/// Phase K8/K9 — diagnoses and AI-repairs a Lesson missing its core teaching content (Body and/or
/// Examples). Blocked once Approved, same policy as <see cref="Lesson.UpdateDraft"/> — reject
/// first to reopen editing.
/// </summary>
public sealed class LessonRepairService : ILessonRepairService
{
    private readonly LinguaCoachDbContext _db;
    private readonly AdminRepairFieldGenerator _fieldGenerator;

    public LessonRepairService(LinguaCoachDbContext db, AdminRepairFieldGenerator fieldGenerator)
    {
        _db = db;
        _fieldGenerator = fieldGenerator;
    }

    public async Task<IReadOnlyList<DiagnosticIssue>> DiagnoseAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await _db.Lessons.AsNoTracking().FirstOrDefaultAsync(l => l.Id == id, ct)
            ?? throw new LessonValidationException($"Lesson '{id}' was not found.");
        return Diagnose(entity);
    }

    public async Task<LessonRepairResult> RepairAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await _db.Lessons.FirstOrDefaultAsync(l => l.Id == id, ct)
            ?? throw new LessonValidationException($"Lesson '{id}' was not found.");

        var issues = Diagnose(entity);
        var fixable = issues.Where(i => i.AutoFixable).ToList();
        if (fixable.Count == 0)
            throw new LessonValidationException("Nothing to repair — no auto-fixable issues were found on this Lesson.");
        if (entity.ReviewStatus == Domain.Enums.AdminReviewStatus.Approved)
            throw new LessonValidationException($"Cannot repair Lesson '{entity.Title}': it is already approved. Reject it first to reopen editing.");

        var (fixedIssues, providerName, modelName) = await ApplyRepairAsync(entity, issues, ct);
        await _db.SaveChangesAsync(ct);

        var links = await _db.LessonResourceLinks.Where(l => l.LessonId == id).ToListAsync(ct);
        var dto = LessonMappers.ToDto(entity, links);
        var remaining = issues.Where(i => !fixedIssues.Contains(i)).ToList();
        return new LessonRepairResult(dto, fixedIssues, remaining, providerName, modelName);
    }

    public async Task<IssuesSummary> GetIssuesSummaryAsync(CancellationToken ct = default)
    {
        var entities = await _db.Lessons.AsNoTracking().Where(l => !l.IsArchived).ToListAsync(ct);
        var withIssues = entities.Count(e => Diagnose(e).Any(i => i.AutoFixable));
        return new IssuesSummary(entities.Count, withIssues);
    }

    public async Task<IReadOnlyList<RepairableItemSummary>> ListWithIssuesAsync(CancellationToken ct = default)
    {
        var entities = await _db.Lessons.AsNoTracking().Where(l => !l.IsArchived).ToListAsync(ct);
        return entities
            .Where(e => Diagnose(e).Any(i => i.AutoFixable))
            .Select(e => new RepairableItemSummary(e.Id, e.Title))
            .ToList();
    }

    public async Task<BulkRepairResult> RepairAllAsync(CancellationToken ct = default)
    {
        var entities = await _db.Lessons.Where(l => !l.IsArchived).ToListAsync(ct);
        var errors = new List<string>();
        var withIssues = 0;
        var repaired = 0;

        foreach (var entity in entities)
        {
            var issues = Diagnose(entity);
            if (!issues.Any(i => i.AutoFixable)) continue;
            withIssues++;
            if (entity.ReviewStatus == Domain.Enums.AdminReviewStatus.Approved)
            {
                errors.Add($"{entity.Title}: already approved, reject first.");
                continue;
            }
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
        Lesson entity, List<DiagnosticIssue> issues, CancellationToken ct)
    {
        string? providerName = null;
        string? modelName = null;
        var fixedIssues = new List<DiagnosticIssue>();

        var body = entity.Body;
        var examplesJson = entity.ExamplesJson;

        if (issues.Any(i => i.Code == "missing_body"))
        {
            var field = await _fieldGenerator.GenerateFieldAsync(
                "Lesson", "a short teaching explanation (2-4 sentences) of the topic",
                $"Title: \"{entity.Title}\". Skill: {entity.Skill ?? "unspecified"}. CEFR level: {entity.CefrLevel ?? "unspecified"}.", ct);
            body = field.Value;
            (providerName, modelName) = (field.ProviderName, field.ModelName);
            fixedIssues.Add(issues.First(i => i.Code == "missing_body"));
        }

        if (issues.Any(i => i.Code == "missing_examples"))
        {
            var field = await _fieldGenerator.GenerateFieldAsync(
                "Lesson", "one short example sentence illustrating the topic, as a single plain sentence (no quotes, no list formatting)",
                $"Title: \"{entity.Title}\". Body: {Truncate(body)} CEFR level: {entity.CefrLevel ?? "unspecified"}.", ct);
            examplesJson = System.Text.Json.JsonSerializer.Serialize(new[] { field.Value });
            (providerName, modelName) = (field.ProviderName, field.ModelName);
            fixedIssues.Add(issues.First(i => i.Code == "missing_examples"));
        }

        entity.UpdateDraft(
            entity.Title, body, examplesJson, entity.CommonMistakesJson, entity.UsageNotes,
            entity.CefrLevel, entity.Skill, entity.Subskill, entity.ContextTagsJson, entity.FocusTagsJson,
            entity.DifficultyBand, entity.EstimatedMinutes);

        return (fixedIssues, providerName, modelName);
    }

    private static List<DiagnosticIssue> Diagnose(Lesson entity)
    {
        var issues = new List<DiagnosticIssue>();
        if (string.IsNullOrWhiteSpace(entity.Body) || entity.Body.Trim().Length < 10)
            issues.Add(new DiagnosticIssue("missing_body", "Missing or too-short teaching content (Body).", true));
        if (IsEmptyArray(entity.ExamplesJson))
            issues.Add(new DiagnosticIssue("missing_examples", "No examples.", true));
        return issues;
    }

    private static bool IsEmptyArray(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return true;
        try
        {
            var arr = System.Text.Json.JsonSerializer.Deserialize<List<string>>(json);
            return arr is not { Count: > 0 };
        }
        catch (System.Text.Json.JsonException)
        {
            return true;
        }
    }

    private static string Truncate(string value) => value.Length <= 500 ? value : value[..500];
}

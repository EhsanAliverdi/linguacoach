using LinguaCoach.Application.AdminRepair;
using LinguaCoach.Application.Exercises;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Infrastructure.AdminRepair;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LinguaCoach.Infrastructure.Exercises;

/// <summary>
/// Phase K8/K9 — diagnoses and AI-repairs an Exercise missing its Instructions/Description text
/// only. Never touches FormSchemaJson/AnswerKeyJson/ScoringRulesJson — a missing or broken
/// Form.io schema or scoring rule is flagged, not auto-fixed; those are correctness-critical and
/// must never be silently AI-guessed. Blocked once Approved, same policy as
/// <see cref="Exercise.UpdateDraft"/>.
/// </summary>
public sealed class ExerciseRepairService : IExerciseRepairService
{
    private readonly LinguaCoachDbContext _db;
    private readonly AdminRepairFieldGenerator _fieldGenerator;

    public ExerciseRepairService(LinguaCoachDbContext db, AdminRepairFieldGenerator fieldGenerator)
    {
        _db = db;
        _fieldGenerator = fieldGenerator;
    }

    public async Task<IReadOnlyList<DiagnosticIssue>> DiagnoseAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await _db.Exercises.AsNoTracking().FirstOrDefaultAsync(a => a.Id == id, ct)
            ?? throw new ExerciseValidationException($"Exercise '{id}' was not found.");
        return Diagnose(entity);
    }

    public async Task<ExerciseRepairResult> RepairAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await _db.Exercises.FirstOrDefaultAsync(a => a.Id == id, ct)
            ?? throw new ExerciseValidationException($"Exercise '{id}' was not found.");

        var issues = Diagnose(entity);
        var fixable = issues.Where(i => i.AutoFixable).ToList();
        if (fixable.Count == 0)
            throw new ExerciseValidationException("Nothing to repair — no auto-fixable issues were found on this Exercise.");
        if (entity.ReviewStatus == Domain.Enums.AdminReviewStatus.Approved)
            throw new ExerciseValidationException($"Cannot repair Exercise '{entity.Title}': it is already approved. Reject it first to reopen editing.");

        var (fixedIssues, providerName, modelName) = await ApplyRepairAsync(entity, issues, ct);
        await _db.SaveChangesAsync(ct);

        var links = await _db.ExerciseResourceLinks.Where(l => l.ExerciseId == id).ToListAsync(ct);
        var dto = ExerciseMappers.ToDto(entity, links);
        var remaining = issues.Where(i => !fixedIssues.Contains(i)).ToList();
        return new ExerciseRepairResult(dto, fixedIssues, remaining, providerName, modelName);
    }

    public async Task<IssuesSummary> GetIssuesSummaryAsync(CancellationToken ct = default)
    {
        var entities = await _db.Exercises.AsNoTracking().Where(a => !a.IsArchived).ToListAsync(ct);
        var withIssues = entities.Count(e => Diagnose(e).Any(i => i.AutoFixable));
        return new IssuesSummary(entities.Count, withIssues);
    }

    public async Task<IReadOnlyList<RepairableItemSummary>> ListWithIssuesAsync(CancellationToken ct = default)
    {
        var entities = await _db.Exercises.AsNoTracking().Where(a => !a.IsArchived).ToListAsync(ct);
        return entities
            .Where(e => Diagnose(e).Any(i => i.AutoFixable))
            .Select(e => new RepairableItemSummary(e.Id, e.Title))
            .ToList();
    }

    public async Task<BulkRepairResult> RepairAllAsync(CancellationToken ct = default)
    {
        var entities = await _db.Exercises.Where(a => !a.IsArchived).ToListAsync(ct);
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
        Exercise entity, List<DiagnosticIssue> issues, CancellationToken ct)
    {
        string? providerName = null;
        string? modelName = null;
        var fixedIssues = new List<DiagnosticIssue>();
        var instructions = entity.Instructions;

        if (issues.Any(i => i.Code == "missing_instructions"))
        {
            var field = await _fieldGenerator.GenerateFieldAsync(
                "Exercise", "a short student-facing instruction sentence explaining what to do",
                $"Title: \"{entity.Title}\". Activity type: {entity.ActivityType}. CEFR level: {entity.CefrLevel ?? "unspecified"}.", ct);
            instructions = field.Value;
            (providerName, modelName) = (field.ProviderName, field.ModelName);
            fixedIssues.Add(issues.First(i => i.Code == "missing_instructions"));
        }

        entity.UpdateDraft(
            entity.Title, instructions, entity.Description, entity.FormSchemaJson, entity.AnswerKeyJson,
            entity.ScoringRulesJson, entity.FeedbackPlanJson, entity.CefrLevel, entity.Skill, entity.Subskill,
            entity.ContextTagsJson, entity.FocusTagsJson, entity.DifficultyBand, entity.EstimatedMinutes);

        return (fixedIssues, providerName, modelName);
    }

    private static List<DiagnosticIssue> Diagnose(Exercise entity)
    {
        var issues = new List<DiagnosticIssue>();
        if (string.IsNullOrWhiteSpace(entity.Instructions) || entity.Instructions.Trim().Length < 5)
            issues.Add(new DiagnosticIssue("missing_instructions", "Missing student-facing instructions.", true));
        if (string.IsNullOrWhiteSpace(entity.FormSchemaJson))
            issues.Add(new DiagnosticIssue("missing_form_schema", "No Form.io schema — cannot be auto-fixed, edit the form manually.", false));
        if (string.IsNullOrWhiteSpace(entity.ScoringRulesJson) || string.IsNullOrWhiteSpace(entity.AnswerKeyJson))
            issues.Add(new DiagnosticIssue("missing_scoring_data", "Missing answer key/scoring rules — cannot be auto-fixed, correctness-critical data is never AI-guessed.", false));
        return issues;
    }
}

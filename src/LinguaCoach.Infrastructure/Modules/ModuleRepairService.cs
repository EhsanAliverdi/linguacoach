using LinguaCoach.Application.AdminRepair;
using LinguaCoach.Application.Modules;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Infrastructure.AdminRepair;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LinguaCoach.Infrastructure.Modules;

/// <summary>
/// Phase K8/K9 — diagnoses and AI-repairs a Module missing its Description. A missing linked
/// Lesson/Exercise is flagged (not auto-fixable — that needs real content, not generated text).
/// Blocked once Approved, same policy as <see cref="Module.UpdateDraft"/>.
/// </summary>
public sealed class ModuleRepairService : IModuleRepairService
{
    private readonly LinguaCoachDbContext _db;
    private readonly AdminRepairFieldGenerator _fieldGenerator;

    public ModuleRepairService(LinguaCoachDbContext db, AdminRepairFieldGenerator fieldGenerator)
    {
        _db = db;
        _fieldGenerator = fieldGenerator;
    }

    public async Task<IReadOnlyList<DiagnosticIssue>> DiagnoseAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await _db.Modules.AsNoTracking().FirstOrDefaultAsync(m => m.Id == id, ct)
            ?? throw new ModuleValidationException($"Module '{id}' was not found.");
        var hasLessonLink = await _db.ModuleLessonLinks.AsNoTracking().AnyAsync(l => l.ModuleId == id, ct);
        var hasExerciseLink = await _db.ModuleExerciseLinks.AsNoTracking().AnyAsync(l => l.ModuleId == id, ct);
        return Diagnose(entity, hasLessonLink, hasExerciseLink);
    }

    public async Task<ModuleRepairResult> RepairAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await _db.Modules.FirstOrDefaultAsync(m => m.Id == id, ct)
            ?? throw new ModuleValidationException($"Module '{id}' was not found.");
        var hasLessonLink = await _db.ModuleLessonLinks.AsNoTracking().AnyAsync(l => l.ModuleId == id, ct);
        var hasExerciseLink = await _db.ModuleExerciseLinks.AsNoTracking().AnyAsync(l => l.ModuleId == id, ct);

        var issues = Diagnose(entity, hasLessonLink, hasExerciseLink);
        var fixable = issues.Where(i => i.AutoFixable).ToList();
        if (fixable.Count == 0)
            throw new ModuleValidationException("Nothing to repair — no auto-fixable issues were found on this Module.");
        if (entity.ReviewStatus == Domain.Enums.AdminReviewStatus.Approved)
            throw new ModuleValidationException($"Cannot repair Module '{entity.Title}': it is already approved. Reject it first to reopen editing.");

        var (fixedIssues, providerName, modelName) = await ApplyRepairAsync(entity, issues, ct);
        await _db.SaveChangesAsync(ct);

        var lessonLinks = await _db.ModuleLessonLinks.Where(l => l.ModuleId == id).ToListAsync(ct);
        var exerciseLinks = await _db.ModuleExerciseLinks.Where(l => l.ModuleId == id).ToListAsync(ct);
        var dto = ModuleMappers.ToDto(entity, lessonLinks, exerciseLinks);
        var remaining = issues.Where(i => !fixedIssues.Contains(i)).ToList();
        return new ModuleRepairResult(dto, fixedIssues, remaining, providerName, modelName);
    }

    public async Task<IssuesSummary> GetIssuesSummaryAsync(CancellationToken ct = default)
    {
        var entities = await _db.Modules.AsNoTracking().Where(m => !m.IsArchived).ToListAsync(ct);
        var ids = entities.Select(e => e.Id).ToList();
        var lessonLinkedIds = await _db.ModuleLessonLinks.AsNoTracking().Where(l => ids.Contains(l.ModuleId)).Select(l => l.ModuleId).Distinct().ToListAsync(ct);
        var exerciseLinkedIds = await _db.ModuleExerciseLinks.AsNoTracking().Where(l => ids.Contains(l.ModuleId)).Select(l => l.ModuleId).Distinct().ToListAsync(ct);
        var lessonLinkedSet = lessonLinkedIds.ToHashSet();
        var exerciseLinkedSet = exerciseLinkedIds.ToHashSet();

        var withIssues = entities.Count(e => Diagnose(e, lessonLinkedSet.Contains(e.Id), exerciseLinkedSet.Contains(e.Id)).Any(i => i.AutoFixable));
        return new IssuesSummary(entities.Count, withIssues);
    }

    public async Task<IReadOnlyList<RepairableItemSummary>> ListWithIssuesAsync(CancellationToken ct = default)
    {
        var entities = await _db.Modules.AsNoTracking().Where(m => !m.IsArchived).ToListAsync(ct);
        var ids = entities.Select(e => e.Id).ToList();
        var lessonLinkedSet = (await _db.ModuleLessonLinks.AsNoTracking().Where(l => ids.Contains(l.ModuleId)).Select(l => l.ModuleId).Distinct().ToListAsync(ct)).ToHashSet();
        var exerciseLinkedSet = (await _db.ModuleExerciseLinks.AsNoTracking().Where(l => ids.Contains(l.ModuleId)).Select(l => l.ModuleId).Distinct().ToListAsync(ct)).ToHashSet();

        return entities
            .Where(e => Diagnose(e, lessonLinkedSet.Contains(e.Id), exerciseLinkedSet.Contains(e.Id)).Any(i => i.AutoFixable))
            .Select(e => new RepairableItemSummary(e.Id, e.Title))
            .ToList();
    }

    public async Task<BulkRepairResult> RepairAllAsync(CancellationToken ct = default)
    {
        var entities = await _db.Modules.Where(m => !m.IsArchived).ToListAsync(ct);
        var ids = entities.Select(e => e.Id).ToList();
        var lessonLinkedSet = (await _db.ModuleLessonLinks.AsNoTracking().Where(l => ids.Contains(l.ModuleId)).Select(l => l.ModuleId).Distinct().ToListAsync(ct)).ToHashSet();
        var exerciseLinkedSet = (await _db.ModuleExerciseLinks.AsNoTracking().Where(l => ids.Contains(l.ModuleId)).Select(l => l.ModuleId).Distinct().ToListAsync(ct)).ToHashSet();

        var errors = new List<string>();
        var withIssues = 0;
        var repaired = 0;

        foreach (var entity in entities)
        {
            var issues = Diagnose(entity, lessonLinkedSet.Contains(entity.Id), exerciseLinkedSet.Contains(entity.Id));
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
        Module entity, List<DiagnosticIssue> issues, CancellationToken ct)
    {
        string? providerName = null;
        string? modelName = null;
        var fixedIssues = new List<DiagnosticIssue>();
        var description = entity.Description;

        if (issues.Any(i => i.Code == "missing_description"))
        {
            var field = await _fieldGenerator.GenerateFieldAsync(
                "Module", "a one-sentence summary of what a student learns and practices in this module",
                $"Title: \"{entity.Title}\". Skill: {entity.Skill ?? "unspecified"}. CEFR level: {entity.CefrLevel ?? "unspecified"}.", ct);
            description = field.Value;
            (providerName, modelName) = (field.ProviderName, field.ModelName);
            fixedIssues.Add(issues.First(i => i.Code == "missing_description"));
        }

        entity.UpdateDraft(
            entity.Title, description, entity.CefrLevel, entity.Skill, entity.Subskill,
            entity.ContextTagsJson, entity.FocusTagsJson, entity.DifficultyBand, entity.EstimatedMinutes, entity.FeedbackPlanJson);

        return (fixedIssues, providerName, modelName);
    }

    private static List<DiagnosticIssue> Diagnose(Module entity, bool hasLessonLink, bool hasExerciseLink)
    {
        var issues = new List<DiagnosticIssue>();
        if (string.IsNullOrWhiteSpace(entity.Description))
            issues.Add(new DiagnosticIssue("missing_description", "Missing a description.", true));
        if (!hasLessonLink)
            issues.Add(new DiagnosticIssue("no_lesson_linked", "No Lesson is linked — cannot be auto-fixed, link a Lesson manually.", false));
        if (!hasExerciseLink)
            issues.Add(new DiagnosticIssue("no_exercise_linked", "No Exercise is linked — cannot be auto-fixed, link an Exercise manually.", false));
        return issues;
    }
}

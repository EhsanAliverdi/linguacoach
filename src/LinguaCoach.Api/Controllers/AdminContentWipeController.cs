using LinguaCoach.Domain.Enums;
using LinguaCoach.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LinguaCoach.Api.Controllers;

/// <summary>
/// Full content reseed (2026-07-28) — a guarded, dev-only, one-time hard-delete of every content
/// entity (Resource Bank, Modules, Lessons, Exercises, Skill Graph) so the platform can be rebuilt
/// from the real CEFR-J/Octanove grammar+vocabulary source data instead of the original 600-node
/// hand-authored seed. Explicitly a departure from this codebase's normal "archive/deactivate,
/// never hard-delete" convention — authorized by the user specifically for this dev-environment
/// reseed (no real student data at stake); this is not a pattern to reuse for anything else.
///
/// Same confirmation-gated shape as <c>AdminSkillGraphController.BatchReject</c>'s Approved-node
/// guard: a first call without <c>confirm:true</c> only reports what WOULD be deleted (counts, no
/// mutation); the admin must resubmit with <c>confirm:true</c> to actually execute. Runs inside a
/// single transaction — if anything fails partway, nothing is left half-deleted.
/// </summary>
[ApiController]
[Route("api/admin/content-wipe")]
[Authorize(Roles = nameof(UserRole.Admin))]
public sealed class AdminContentWipeController : ControllerBase
{
    private readonly LinguaCoachDbContext _db;

    public AdminContentWipeController(LinguaCoachDbContext db) => _db = db;

    [HttpPost]
    public async Task<IActionResult> Wipe([FromBody] ContentWipeRequest request, CancellationToken ct)
    {
        var counts = await CountAllAsync(ct);

        if (!request.Confirm)
            return Ok(new ContentWipeResponse(RequiresConfirmation: true, Deleted: null, Counts: counts));

        // Deletion order respects every Restrict FK in the schema (verified against the real EF
        // configurations, not assumed): student assignment/launch rows reference Module/Lesson/
        // Exercise/LearningActivity with Restrict, so they must go first. Module/Lesson/Exercise
        // deletion cascades their own join tables (ModuleLessonLink/ModuleExerciseLink/
        // ModuleSkillGraphNodeLink/LessonResourceLink/ExerciseResourceLink) automatically — no
        // manual deletion needed for those. SkillGraphPrerequisiteEdge -> SkillGraphNode is
        // Restrict, so edges go before nodes. ResourceImportRun cascades ResourceRawRecord ->
        // ResourceCandidate; ResourceImportRun and ResourceBankItem are both Restrict against
        // CefrResourceSource, so both must go before it. ImportPackage and ImportUploadSession are
        // ALSO Restrict against CefrResourceSource (found the hard way — the real DB has 4
        // ImportPackage rows this deletion set's own scope description didn't name, same "hidden
        // structural blocker" pattern as the student assignment tables above); ImportPackage's own
        // children (ImportAiEnrichmentOperation, ImportAsset, ImportCostCeilingAmendment,
        // ImportProfile, ImportSttOperation) all cascade from it automatically, and
        // ImportUploadSessionPart cascades from ImportUploadSession.
        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        await _db.StudentExerciseLaunches.ExecuteDeleteAsync(ct);
        await _db.StudentPracticeGymModuleAssignments.ExecuteDeleteAsync(ct);
        await _db.StudentTodayPlanModuleAssignments.ExecuteDeleteAsync(ct);

        await _db.Modules.ExecuteDeleteAsync(ct);
        await _db.Lessons.ExecuteDeleteAsync(ct);
        await _db.Exercises.ExecuteDeleteAsync(ct);

        await _db.SkillGraphPrerequisiteEdges.ExecuteDeleteAsync(ct);
        await _db.SkillGraphNodes.ExecuteDeleteAsync(ct);

        await _db.ResourceBankItems.ExecuteDeleteAsync(ct);
        await _db.ResourceImportRuns.ExecuteDeleteAsync(ct);
        await _db.ImportPackages.ExecuteDeleteAsync(ct);
        await _db.ImportUploadSessions.ExecuteDeleteAsync(ct);
        await _db.CefrResourceSources.ExecuteDeleteAsync(ct);

        await tx.CommitAsync(ct);

        var remaining = await CountAllAsync(ct);
        return Ok(new ContentWipeResponse(RequiresConfirmation: false, Deleted: counts, Counts: remaining));
    }

    [HttpGet("preview")]
    public async Task<IActionResult> Preview(CancellationToken ct)
        => Ok(await CountAllAsync(ct));

    private async Task<ContentWipeCounts> CountAllAsync(CancellationToken ct) => new(
        StudentExerciseLaunches: await _db.StudentExerciseLaunches.CountAsync(ct),
        StudentPracticeGymModuleAssignments: await _db.StudentPracticeGymModuleAssignments.CountAsync(ct),
        StudentTodayPlanModuleAssignments: await _db.StudentTodayPlanModuleAssignments.CountAsync(ct),
        Modules: await _db.Modules.CountAsync(ct),
        Lessons: await _db.Lessons.CountAsync(ct),
        Exercises: await _db.Exercises.CountAsync(ct),
        SkillGraphPrerequisiteEdges: await _db.SkillGraphPrerequisiteEdges.CountAsync(ct),
        SkillGraphNodes: await _db.SkillGraphNodes.CountAsync(ct),
        ResourceBankItems: await _db.ResourceBankItems.CountAsync(ct),
        ResourceImportRuns: await _db.ResourceImportRuns.CountAsync(ct),
        ImportPackages: await _db.ImportPackages.CountAsync(ct),
        ImportUploadSessions: await _db.ImportUploadSessions.CountAsync(ct),
        CefrResourceSources: await _db.CefrResourceSources.CountAsync(ct));
}

public sealed record ContentWipeRequest(bool Confirm);

public sealed record ContentWipeResponse(bool RequiresConfirmation, ContentWipeCounts? Deleted, ContentWipeCounts Counts);

public sealed record ContentWipeCounts(
    int StudentExerciseLaunches,
    int StudentPracticeGymModuleAssignments,
    int StudentTodayPlanModuleAssignments,
    int Modules,
    int Lessons,
    int Exercises,
    int SkillGraphPrerequisiteEdges,
    int SkillGraphNodes,
    int ResourceBankItems,
    int ResourceImportRuns,
    int ImportPackages,
    int ImportUploadSessions,
    int CefrResourceSources);

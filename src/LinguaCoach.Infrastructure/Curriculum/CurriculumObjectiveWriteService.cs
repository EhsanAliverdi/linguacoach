using System.Text.Json;
using LinguaCoach.Application.Curriculum;
using LinguaCoach.Application.Learning;
using LinguaCoach.Domain.Constants;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LinguaCoach.Infrastructure.Curriculum;

/// <summary>
/// Admin write service for CurriculumObjective CRUD.
/// Validates all business rules before persisting.
/// Does not delete objectives — only activates/deactivates (soft lifecycle).
/// Does not mutate student data.
/// </summary>
public sealed class CurriculumObjectiveWriteService : ICurriculumObjectiveWriteService
{
    private readonly LinguaCoachDbContext _db;
    private readonly ICurriculumRoutingService _routingService;
    private readonly ILearningGoalContextResolver _goalResolver;
    private readonly ILogger<CurriculumObjectiveWriteService> _logger;

    public CurriculumObjectiveWriteService(
        LinguaCoachDbContext db,
        ICurriculumRoutingService routingService,
        ILearningGoalContextResolver goalResolver,
        ILogger<CurriculumObjectiveWriteService> logger)
    {
        _db = db;
        _routingService = routingService;
        _goalResolver = goalResolver;
        _logger = logger;
    }

    public async Task<AdminCurriculumObjectiveDto> CreateAsync(
        AdminCurriculumObjectiveUpsertRequest request, CancellationToken ct = default)
    {
        var validationError = await ValidateRequestAsync(request, existingKey: null, ct);
        if (validationError is not null)
            throw new InvalidOperationException(validationError);

        var objective = new CurriculumObjective(
            key: request.Key.Trim(),
            title: request.Title,
            description: request.Description,
            cefrLevel: request.CefrLevel,
            primarySkill: request.PrimarySkill,
            secondarySkillsJson: SerializeList(request.SecondarySkills),
            contextTagsJson: SerializeList(request.ContextTags),
            focusTagsJson: SerializeList(request.FocusTags),
            prerequisiteKeysJson: SerializeList(request.PrerequisiteObjectiveKeys),
            recommendedOrder: request.RecommendedOrder,
            difficultyBand: request.DifficultyBand,
            isActive: request.IsActive,
            isReviewable: request.IsReviewable,
            isExamInspired: request.IsExamInspired,
            teachingNotes: request.TeachingNotes,
            examplePrompts: request.ExamplePrompts);

        _db.CurriculumObjectives.Add(objective);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("AdminCurriculumObjectiveWriteService: created objective Key={Key}", objective.Key);
        return AdminCurriculumObjectiveDto.From(objective);
    }

    public async Task<AdminCurriculumObjectiveDto> UpdateAsync(
        string key, AdminCurriculumObjectiveUpsertRequest request, CancellationToken ct = default)
    {
        var objective = await _db.CurriculumObjectives
            .FirstOrDefaultAsync(o => o.Key == key, ct)
            ?? throw new KeyNotFoundException($"Curriculum objective '{key}' not found.");

        var validationError = await ValidateRequestAsync(request, existingKey: key, ct);
        if (validationError is not null)
            throw new InvalidOperationException(validationError);

        objective.AdminUpdate(
            title: request.Title,
            description: request.Description,
            cefrLevel: request.CefrLevel,
            primarySkill: request.PrimarySkill,
            secondarySkillsJson: SerializeList(request.SecondarySkills),
            contextTagsJson: SerializeList(request.ContextTags),
            focusTagsJson: SerializeList(request.FocusTags),
            prerequisiteKeysJson: SerializeList(request.PrerequisiteObjectiveKeys),
            recommendedOrder: request.RecommendedOrder,
            difficultyBand: request.DifficultyBand,
            isReviewable: request.IsReviewable,
            isExamInspired: request.IsExamInspired,
            teachingNotes: request.TeachingNotes,
            examplePrompts: request.ExamplePrompts);

        // Handle IsActive separately so admin can toggle it via PUT too.
        if (request.IsActive && !objective.IsActive) objective.Activate();
        else if (!request.IsActive && objective.IsActive) objective.Deactivate();

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("AdminCurriculumObjectiveWriteService: updated objective Key={Key}", key);
        return AdminCurriculumObjectiveDto.From(objective);
    }

    public async Task<AdminCurriculumObjectiveDto> ActivateAsync(string key, CancellationToken ct = default)
    {
        var objective = await _db.CurriculumObjectives
            .FirstOrDefaultAsync(o => o.Key == key, ct)
            ?? throw new KeyNotFoundException($"Curriculum objective '{key}' not found.");

        objective.Activate();
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("AdminCurriculumObjectiveWriteService: activated objective Key={Key}", key);
        return AdminCurriculumObjectiveDto.From(objective);
    }

    public async Task<AdminCurriculumObjectiveDto> DeactivateAsync(string key, CancellationToken ct = default)
    {
        var objective = await _db.CurriculumObjectives
            .FirstOrDefaultAsync(o => o.Key == key, ct)
            ?? throw new KeyNotFoundException($"Curriculum objective '{key}' not found.");

        objective.Deactivate();
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("AdminCurriculumObjectiveWriteService: deactivated objective Key={Key}", key);
        return AdminCurriculumObjectiveDto.From(objective);
    }

    public async Task<AdminRoutingPreviewResult> PreviewRoutingAsync(
        AdminRoutingPreviewRequest request, CancellationToken ct = default)
    {
        // Resolve student context if StudentId provided.
        string? cefrLevel = request.CefrLevelOverride;
        ResolvedLearningGoalContext? goalContext = null;

        if (request.StudentId.HasValue && string.IsNullOrWhiteSpace(cefrLevel))
        {
            var profile = await _db.StudentProfiles
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.UserId == request.StudentId.Value, ct);
            cefrLevel = profile?.CefrLevel ?? "A1";
            if (profile is not null)
            {
                goalContext = _goalResolver.Resolve(profile, new LearningGoalResolutionContext
                {
                    Source = "admin_preview"
                });
            }
        }

        // Build routing request from admin overrides.
        if (request.LearningGoals?.Count > 0 || request.FocusAreas?.Count > 0)
        {
            var overrideGoal = string.Join(", ", (request.LearningGoals ?? []).Concat(request.FocusAreas ?? []));
            goalContext = new ResolvedLearningGoalContext
            {
                PrimaryGoalKey = request.LearningGoals?.FirstOrDefault(),
                GoalLabels = string.Join(", ", request.LearningGoals ?? []),
                FocusAreaKeys = string.Join(", ", request.FocusAreas ?? []),
                ContextSummary = overrideGoal,
                Source = "admin_preview"
            };
        }

        var routingRequest = new CurriculumRoutingRequest
        {
            StudentId = request.StudentId ?? Guid.Empty,
            CurrentCefrLevel = cefrLevel,
            PrimarySkill = request.PrimarySkill,
            Source = request.Source ?? "admin_preview",
            ResolvedLearningGoalContext = goalContext,
            LearningGoals = request.LearningGoals ?? [],
            FocusAreas = request.FocusAreas ?? [],
            DifficultyPreference = request.DifficultyPreference,
            AllowReviewOrScaffold = request.AllowReviewOrScaffold,
            Mode = request.Mode
            // MasteredObjectiveKeys intentionally not set: admin preview does not apply
            // student-specific mastery filtering. See warning added below.
        };

        var recommendation = await _routingService.RecommendAsync(routingRequest, ct);

        var warnings = new List<string>
        {
            "Student-specific mastery filtering is not applied in generic preview."
        };
        if (recommendation.IsLowerLevelContent)
            warnings.Add($"Lower-level content selected ({recommendation.TargetCefrLevel} vs requested {cefrLevel}).");
        if (recommendation.RoutingReason == Domain.Enums.RoutingReason.Fallback)
            warnings.Add("No matching curriculum objective found — fallback to general_english.");
        if (recommendation.ContextTags.Contains("workplace", StringComparer.OrdinalIgnoreCase) &&
            !(request.LearningGoals?.Any(g => g.Contains("workplace", StringComparison.OrdinalIgnoreCase)) ?? false))
            warnings.Add("Workplace context was resolved from student profile goals.");

        return new AdminRoutingPreviewResult(
            TargetCefrLevel: recommendation.TargetCefrLevel,
            CurriculumObjectiveKey: recommendation.CurriculumObjectiveKey,
            CurriculumObjectiveTitle: recommendation.CurriculumObjectiveTitle,
            ContextTags: recommendation.ContextTags,
            FocusTags: recommendation.FocusTags,
            DifficultyBand: recommendation.DifficultyBand,
            RoutingReason: recommendation.RoutingReason.ToString(),
            IsLowerLevelContent: recommendation.IsLowerLevelContent,
            Explanation: recommendation.Explanation,
            FallbackUsed: recommendation.RoutingReason == Domain.Enums.RoutingReason.Fallback,
            NoExactObjectiveFound: recommendation.CurriculumObjectiveKey is null,
            Warnings: warnings);
    }

    private async Task<string?> ValidateRequestAsync(
        AdminCurriculumObjectiveUpsertRequest request,
        string? existingKey,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Key))
            return "Key is required.";

        if (!IsValidSlugKey(request.Key))
            return $"Key '{request.Key}' must be a lowercase slug (letters, digits, dots, underscores, hyphens only).";

        if (string.IsNullOrWhiteSpace(request.Title))
            return "Title is required.";

        if (!CefrLevelConstants.IsValid(request.CefrLevel))
            return $"Invalid CEFR level '{request.CefrLevel}'. Must be one of: {string.Join(", ", CefrLevelConstants.All)}.";

        if (!CurriculumSkillConstants.IsValid(request.PrimarySkill))
            return $"Invalid primary skill '{request.PrimarySkill}'. Must be one of: {string.Join(", ", CurriculumSkillConstants.All)}.";

        foreach (var skill in request.SecondarySkills)
        {
            if (!CurriculumSkillConstants.IsValid(skill))
                return $"Invalid secondary skill '{skill}'.";
        }

        foreach (var tag in request.ContextTags)
        {
            if (!CurriculumContextTagConstants.IsValid(tag))
                return $"Invalid context tag '{tag}'.";
        }

        if (request.DifficultyBand is < 1 or > 5)
            return "DifficultyBand must be between 1 and 5.";

        if (request.RecommendedOrder < 0)
            return "RecommendedOrder must be >= 0.";

        // Duplicate key check (create only).
        if (existingKey is null)
        {
            var exists = await _db.CurriculumObjectives
                .AnyAsync(o => o.Key == request.Key.Trim(), ct);
            if (exists)
                return $"Curriculum objective with key '{request.Key}' already exists.";
        }

        // Self-prerequisite.
        if (request.PrerequisiteObjectiveKeys.Any(p =>
            string.Equals(p, request.Key.Trim(), StringComparison.OrdinalIgnoreCase)))
            return "An objective cannot list itself as a prerequisite.";

        // Dangling prerequisites.
        foreach (var prereqKey in request.PrerequisiteObjectiveKeys)
        {
            var prereqExists = await _db.CurriculumObjectives
                .AnyAsync(o => o.Key == prereqKey, ct);
            if (!prereqExists)
                return $"Prerequisite objective '{prereqKey}' does not exist.";
        }

        return null;
    }

    private static bool IsValidSlugKey(string key) =>
        !string.IsNullOrWhiteSpace(key) &&
        key.All(c => char.IsLetterOrDigit(c) || c == '.' || c == '_' || c == '-') &&
        key == key.ToLowerInvariant();

    private static string SerializeList(IReadOnlyList<string>? list) =>
        list is null || list.Count == 0
            ? "[]"
            : JsonSerializer.Serialize(list);
}

using System.Text.Json;
using LinguaCoach.Application.Ai;
using LinguaCoach.Application.LearningPath;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LinguaCoach.Infrastructure.LearningPath;

/// <summary>
/// Generates or retrieves the active LearningPath for a student.
/// Primary: AI generates a 5-module personalised path.
/// Fallback: DefaultPathFactory produces a hard-coded safe path — never throws.
/// </summary>
public sealed class AiLearningPathGeneratorHandler : ILearningPathGenerator
{
    private const string PromptKey = "learning_path_generate";
    private const int DefaultModuleCount = 5;
    private const int CompletionThreshold = 3;

    private readonly LinguaCoachDbContext _db;
    private readonly IAiContextBuilder _contextBuilder;
    private readonly IAiProviderResolver _aiProviderResolver;
    private readonly ILogger<AiLearningPathGeneratorHandler> _logger;

    public AiLearningPathGeneratorHandler(
        LinguaCoachDbContext db,
        IAiContextBuilder contextBuilder,
        IAiProviderResolver aiProviderResolver,
        ILogger<AiLearningPathGeneratorHandler> logger)
    {
        _db = db;
        _contextBuilder = contextBuilder;
        _aiProviderResolver = aiProviderResolver;
        _logger = logger;
    }

    public async Task<LearningPathDto> GenerateAsync(
        GenerateLearningPathCommand command,
        CancellationToken ct = default)
    {
        var profile = await _db.StudentProfiles
            .Include(p => p.CareerProfile)
            .Include(p => p.LanguagePair)
                .ThenInclude(lp => lp!.SourceLanguage)
            .Include(p => p.LanguagePair)
                .ThenInclude(lp => lp!.TargetLanguage)
            .FirstOrDefaultAsync(p => p.UserId == command.UserId, ct)
            ?? throw new InvalidOperationException("Student profile not found.");

        // Return existing active path if one exists already.
        var existingPath = await _db.LearningPaths
            .Include(p => p.Modules)
            .FirstOrDefaultAsync(p => p.StudentProfileId == profile.Id && p.IsActive, ct);

        if (existingPath is not null)
            return await BuildDtoAsync(existingPath, profile.Id, ct);

        var careerContext = profile.CareerProfile?.Name ?? "General workplace";
        var cefrLevel = profile.CefrLevel ?? "B1";
        var sourceLang = profile.LanguagePair?.SourceLanguage?.Name ?? "Persian";
        var targetLang = profile.LanguagePair?.TargetLanguage?.Name ?? "English";
        var skillFocus = profile.SkillFocus?.ToString() ?? "workplace communication";

        Domain.Entities.LearningPath path;
        List<LearningModule> modules;

        try
        {
            (path, modules) = await GenerateViaAiAsync(
                profile.Id, careerContext, cefrLevel, sourceLang, targetLang, skillFocus, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "AI path generation failed for user {UserId}. Using DefaultPathFactory.",
                command.UserId);
            path = DefaultPathFactory.Create(profile.Id, careerContext, cefrLevel);
            _db.LearningPaths.Add(path);
            await _db.SaveChangesAsync(ct);
            modules = DefaultPathFactory.CreateModules(path.Id).ToList();
        }

        _db.LearningModules.AddRange(modules);
        await _db.SaveChangesAsync(ct);

        // Reload with modules navigation populated.
        var saved = await _db.LearningPaths
            .Include(p => p.Modules)
            .FirstAsync(p => p.Id == path.Id, ct);

        return await BuildDtoAsync(saved, profile.Id, ct);
    }

    private async Task<(Domain.Entities.LearningPath Path, List<LearningModule> Modules)> GenerateViaAiAsync(
        Guid studentProfileId,
        string careerContext,
        string cefrLevel,
        string sourceLang,
        string targetLang,
        string skillFocus,
        CancellationToken ct)
    {
        var variables = new Dictionary<string, string>
        {
            ["careerContext"] = careerContext,
            ["cefrLevel"] = cefrLevel,
            ["sourceLanguageName"] = sourceLang,
            ["targetLanguageName"] = targetLang,
            ["skillFocus"] = skillFocus,
            ["moduleCount"] = DefaultModuleCount.ToString(),
        };

        var selection = _aiProviderResolver.ResolveWritingFeedbackProvider();
        var aiRequest = await _contextBuilder.BuildAsync(PromptKey, variables, ct);
        aiRequest = aiRequest with { ModelHint = selection.ModelName, ApiKeyOverride = selection.ApiKeyOverride };

        var aiResponse = await selection.Provider.CompleteAsync(aiRequest, ct);

        _logger.LogDebug("AI path generation: provider={Provider}, in={In}, out={Out}",
            selection.ProviderName, aiResponse.InputTokens, aiResponse.OutputTokens);

        var parsed = ParseAiResponse(aiResponse.ResponseJson);

        var path = new Domain.Entities.LearningPath(
            studentProfileId,
            parsed.PathTitle,
            learnerContextSummary: $"{careerContext} at {cefrLevel}, skill focus: {skillFocus}");

        _db.LearningPaths.Add(path);
        await _db.SaveChangesAsync(ct);

        var modules = parsed.Modules
            .Select(m => new LearningModule(path.Id, m.Title, m.Description, m.Order))
            .ToList();

        return (path, modules);
    }

    private async Task<LearningPathDto> BuildDtoAsync(
        Domain.Entities.LearningPath path,
        Guid studentProfileId,
        CancellationToken ct)
    {
        var modules = path.Modules.OrderBy(m => m.Order).ToList();

        // Count completed attempts per module.
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

        // Current module = lowest-order module with fewer than threshold completed attempts.
        LearningModule? currentModule = modules
            .FirstOrDefault(m => (completedCounts.GetValueOrDefault(m.Id, 0)) < CompletionThreshold)
            ?? modules.LastOrDefault();

        int modulesCompleted = modules.Count(m =>
            completedCounts.GetValueOrDefault(m.Id, 0) >= CompletionThreshold);

        var moduleDtos = modules.Select(m =>
        {
            int completed = completedCounts.GetValueOrDefault(m.Id, 0);
            return new LearningModuleDto(
                ModuleId: m.Id,
                Title: m.Title,
                Description: m.Description,
                Order: m.Order,
                CompletedActivities: completed,
                TotalActivities: CompletionThreshold,
                IsCurrent: currentModule is not null && m.Id == currentModule.Id);
        }).ToList();

        var currentDto = currentModule is null ? null
            : moduleDtos.First(m => m.ModuleId == currentModule.Id);

        return new LearningPathDto(
            PathId: path.Id,
            Title: path.Title,
            IsActive: path.IsActive,
            CurrentModule: currentDto,
            ModulesCompleted: modulesCompleted,
            TotalModules: modules.Count,
            Modules: moduleDtos);
    }

    private static ParsedAiPath ParseAiResponse(string raw)
    {
        var cleaned = raw.Trim();
        if (cleaned.StartsWith("```"))
        {
            var firstNewline = cleaned.IndexOf('\n');
            var lastFence = cleaned.LastIndexOf("```");
            if (firstNewline > 0 && lastFence > firstNewline)
                cleaned = cleaned[(firstNewline + 1)..lastFence].Trim();
        }

        using var doc = JsonDocument.Parse(cleaned);
        var root = doc.RootElement;

        var pathTitle = root.GetProperty("pathTitle").GetString()
            ?? throw new InvalidOperationException("AI path response missing pathTitle.");

        var modulesEl = root.GetProperty("modules");
        var modules = new List<ParsedModule>();
        foreach (var m in modulesEl.EnumerateArray())
        {
            modules.Add(new ParsedModule(
                Title: m.GetProperty("title").GetString() ?? "Module",
                Description: m.TryGetProperty("description", out var d) ? d.GetString() ?? "" : "",
                Order: m.TryGetProperty("order", out var o) ? o.GetInt32() : modules.Count + 1));
        }

        if (modules.Count == 0)
            throw new InvalidOperationException("AI path response contains no modules.");

        return new ParsedAiPath(pathTitle, modules);
    }

    private sealed record ParsedAiPath(string PathTitle, List<ParsedModule> Modules);
    private sealed record ParsedModule(string Title, string Description, int Order);
}

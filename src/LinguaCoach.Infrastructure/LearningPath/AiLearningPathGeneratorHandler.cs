using System.Text.Json;
using LinguaCoach.Application.Ai;
using LinguaCoach.Application.LearningPath;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Infrastructure.Ai;
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
    private readonly LinguaCoachDbContext _db;
    private readonly IAiContextBuilder _contextBuilder;
    private readonly AiExecutionService _aiExecution;
    private readonly LearningPathDtoBuilder _dtoBuilder;
    private readonly ILogger<AiLearningPathGeneratorHandler> _logger;

    public AiLearningPathGeneratorHandler(
        LinguaCoachDbContext db,
        IAiContextBuilder contextBuilder,
        AiExecutionService aiExecution,
        LearningPathDtoBuilder dtoBuilder,
        ILogger<AiLearningPathGeneratorHandler> logger)
    {
        _db = db;
        _contextBuilder = contextBuilder;
        _aiExecution = aiExecution;
        _dtoBuilder = dtoBuilder;
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
            return await _dtoBuilder.BuildAsync(existingPath, profile.Id, ct);

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

        return await _dtoBuilder.BuildAsync(saved, profile.Id, ct);
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

        var aiRequest = await _contextBuilder.BuildAsync(PromptKey, variables, ct);
        var response = await _aiExecution.ExecuteWithFallbackAsync(
            PromptKey, aiRequest, studentProfileId, correlationId: null, ct);
        var parsed = ParseAiResponse(response);

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

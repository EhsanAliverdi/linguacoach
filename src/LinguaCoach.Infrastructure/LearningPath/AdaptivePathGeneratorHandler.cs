using System.Text.Json;
using System.Text.Json.Serialization;
using LinguaCoach.Application.Ai;
using LinguaCoach.Application.LearningPath;
using LinguaCoach.Application.Memory;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Infrastructure.Ai;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LinguaCoach.Infrastructure.LearningPath;

public sealed class AdaptivePathGeneratorHandler : IAdaptivePathGenerator
{
    private const string PromptKey = "learning_path_generate_adaptive";
    private const int ModuleCount = 4;

    private readonly LinguaCoachDbContext _db;
    private readonly IAiContextBuilder _contextBuilder;
    private readonly AiExecutionService _aiExecution;
    private readonly IStudentMemoryService _memoryService;
    private readonly LearningPathDtoBuilder _dtoBuilder;
    private readonly ILogger<AdaptivePathGeneratorHandler> _logger;

    public AdaptivePathGeneratorHandler(
        LinguaCoachDbContext db,
        IAiContextBuilder contextBuilder,
        AiExecutionService aiExecution,
        IStudentMemoryService memoryService,
        LearningPathDtoBuilder dtoBuilder,
        ILogger<AdaptivePathGeneratorHandler> logger)
    {
        _db = db;
        _contextBuilder = contextBuilder;
        _aiExecution = aiExecution;
        _memoryService = memoryService;
        _dtoBuilder = dtoBuilder;
        _logger = logger;
    }

    public async Task<LearningPathDto> GenerateNextAsync(GenerateNextModulesCommand command, CancellationToken ct = default)
    {
        var profile = await _db.StudentProfiles.FirstOrDefaultAsync(p => p.UserId == command.UserId, ct)
            ?? throw new InvalidOperationException("Student profile not found.");

        var path = await _db.LearningPaths
            .Include(p => p.Modules)
            .FirstOrDefaultAsync(p => p.StudentProfileId == profile.Id
                && p.IsActive
                && (!command.PathId.HasValue || p.Id == command.PathId.Value), ct)
            ?? throw new InvalidOperationException("Active learning path not found.");

        var initialModuleCount = path.Modules.Count;
        var contextJson = await _memoryService.BuildAdaptiveContextJsonAsync(profile.Id, ModuleCount, ct);
        var aiRequest = await _contextBuilder.BuildAsync(PromptKey, new Dictionary<string, string>
        {
            ["adaptiveGenerationContext"] = contextJson
        }, ct);

        var response = await _aiExecution.ExecuteAsync(PromptKey, aiRequest, profile.Id, null, ct);
        var parsed = ParseResponse(response);
        var existingTitles = path.Modules.Select(m => m.Title).ToList();
        var existingFingerprints = path.Modules.Select(m => ModuleFingerprint.TryParse(m.FingerprintJson)).Where(f => f is not null).ToList()!;
        var newModules = new List<LearningModule>();
        var nextOrder = path.Modules.Count == 0 ? 1 : path.Modules.Max(m => m.Order) + 1;

        foreach (var module in parsed.Modules.OrderBy(m => m.Order).Take(5))
        {
            var fingerprint = module.Fingerprint;
            if (IsDuplicateTitle(existingTitles.Concat(newModules.Select(m => m.Title)), module.Title)
                || (fingerprint is not null && existingFingerprints.Concat(newModules.Select(m => ModuleFingerprint.TryParse(m.FingerprintJson)).Where(f => f is not null)!).Any(f => f!.IsDuplicateOf(fingerprint))))
            {
                _logger.LogInformation("Skipped duplicate adaptive module Title={Title} StudentProfileId={StudentProfileId}", module.Title, profile.Id);
                continue;
            }

            var entity = new LearningModule(path.Id, module.Title, module.Description, nextOrder++);
            entity.SetAdaptiveMetadata(module.FocusSkill, module.Reason, module.Difficulty,
                fingerprint is null ? null : JsonSerializer.Serialize(fingerprint));
            newModules.Add(entity);
        }

        if (newModules.Count > 0)
        {
            var currentCount = await _db.LearningModules.CountAsync(m => m.LearningPathId == path.Id, ct);
            if (currentCount != initialModuleCount)
                throw new DbUpdateConcurrencyException("Path was updated concurrently.");

            _db.LearningModules.AddRange(newModules);
            await _db.SaveChangesAsync(ct);
        }

        var saved = await _db.LearningPaths.Include(p => p.Modules).FirstAsync(p => p.Id == path.Id, ct);
        return await _dtoBuilder.BuildAsync(saved, profile.Id, ct);
    }

    private static AdaptivePathResponse ParseResponse(string raw)
    {
        var cleaned = raw.Trim();
        if (cleaned.StartsWith("```"))
        {
            var firstNewline = cleaned.IndexOf('\n');
            var lastFence = cleaned.LastIndexOf("```", StringComparison.Ordinal);
            if (firstNewline > 0 && lastFence > firstNewline)
                cleaned = cleaned[(firstNewline + 1)..lastFence].Trim();
        }

        return JsonSerializer.Deserialize<AdaptivePathResponse>(cleaned, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }) ?? throw new InvalidOperationException("Adaptive path response was empty.");
    }

    private static bool IsDuplicateTitle(IEnumerable<string> existingTitles, string candidate)
    {
        var candidateKey = TitleKey(candidate);
        return existingTitles.Any(t => TitleKey(t) == candidateKey);
    }

    private static string TitleKey(string title)
        => string.Join(' ', title.ToLowerInvariant()
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Take(6));
}

internal sealed class AdaptivePathResponse
{
    [JsonPropertyName("journeySummary")] public string? JourneySummary { get; set; }
    [JsonPropertyName("modules")] public List<AdaptiveModulePayload> Modules { get; set; } = [];
}

internal sealed class AdaptiveModulePayload
{
    [JsonPropertyName("order")] public int Order { get; set; }
    [JsonPropertyName("title")] public string Title { get; set; } = "Recommended module";
    [JsonPropertyName("description")] public string Description { get; set; } = "";
    [JsonPropertyName("focusSkill")] public string? FocusSkill { get; set; }
    [JsonPropertyName("reason")] public string? Reason { get; set; }
    [JsonPropertyName("difficulty")] public string? Difficulty { get; set; }
    [JsonPropertyName("fingerprint")] public ModuleFingerprint? Fingerprint { get; set; }
    [JsonPropertyName("avoidsRepeating")] public List<string> AvoidsRepeating { get; set; } = [];
}

internal sealed record ModuleFingerprint(
    string? CommunicationMode,
    string? ScenarioType,
    string? Audience,
    string? Tone,
    string? Difficulty,
    string? GrammarFocus,
    string? VocabularyTheme)
{
    public static ModuleFingerprint? TryParse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            return JsonSerializer.Deserialize<ModuleFingerprint>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch { return null; }
    }

    public bool IsDuplicateOf(ModuleFingerprint other)
        => Same(ScenarioType, other.ScenarioType)
            && Same(Audience, other.Audience)
            && Same(CommunicationMode, other.CommunicationMode);

    private static bool Same(string? left, string? right)
        => !string.IsNullOrWhiteSpace(left)
            && !string.IsNullOrWhiteSpace(right)
            && string.Equals(left.Trim(), right.Trim(), StringComparison.OrdinalIgnoreCase);
}

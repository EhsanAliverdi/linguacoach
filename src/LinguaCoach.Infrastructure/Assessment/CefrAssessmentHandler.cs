using System.Text.Json;
using System.Text.Json.Serialization;
using LinguaCoach.Application.Assessment;
using LinguaCoach.Application.Ai;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LinguaCoach.Infrastructure.Assessment;

public sealed class CefrAssessmentHandler : ICefrAssessmentHandler
{
    private const string PromptKey = "cefr.assessment.v1";

    private readonly LinguaCoachDbContext _db;
    private readonly IAiContextBuilder _contextBuilder;
    private readonly IAiProvider _aiProvider;
    private readonly ILogger<CefrAssessmentHandler> _logger;

    public CefrAssessmentHandler(
        LinguaCoachDbContext db,
        IAiContextBuilder contextBuilder,
        IAiProvider aiProvider,
        ILogger<CefrAssessmentHandler> logger)
    {
        _db = db;
        _contextBuilder = contextBuilder;
        _aiProvider = aiProvider;
        _logger = logger;
    }

    public async Task<CefrAssessmentResult> HandleAsync(CefrAssessmentCommand command, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(command.StudentSample))
            throw new ArgumentException("Student sample text is required.", nameof(command));
        if (command.StudentSample.Length > 2000)
            throw new ArgumentException("Student sample must be at most 2000 characters.", nameof(command));

        var profile = await _db.StudentProfiles
            .Include(p => p.LanguagePair)
                .ThenInclude(lp => lp!.SourceLanguage)
            .Include(p => p.LanguagePair)
                .ThenInclude(lp => lp!.TargetLanguage)
            .Include(p => p.CareerProfile)
            .FirstOrDefaultAsync(p => p.UserId == command.UserId, ct)
            ?? throw new InvalidOperationException("Student profile not found.");

        if (profile.OnboardingStatus != Domain.Enums.OnboardingStatus.Complete)
            throw new InvalidOperationException("CEFR assessment requires completed onboarding.");

        var variables = new Dictionary<string, string>
        {
            ["sourceLanguageName"] = profile.LanguagePair?.SourceLanguage?.Name ?? "Persian",
            ["targetLanguageName"] = profile.LanguagePair?.TargetLanguage?.Name ?? "English",
            ["careerProfile"] = profile.CareerProfile?.Name ?? "Document Controller",
            ["studentSample"] = command.StudentSample,
        };

        var aiRequest = await _contextBuilder.BuildAsync(PromptKey, variables, ct);
        var aiResponse = await _aiProvider.CompleteAsync(aiRequest, ct);

        // Log cost immediately — before any parsing.
        var modelName = string.IsNullOrEmpty(aiResponse.ModelName) ? "unknown" : aiResponse.ModelName;
        _db.AiUsageLogs.Add(new AiUsageLog(
            profile.Id, "cefr_assessment", _aiProvider.ProviderName, modelName,
            isFallback: false, wasSuccessful: true, failureReason: null,
            aiResponse.InputTokens, aiResponse.OutputTokens, aiResponse.CostUsd,
            durationMs: 0, correlationId: null));
        await _db.SaveChangesAsync(ct);

        var parsed = ParseResponse(aiResponse.ResponseJson);

        // Persist the assessed level on the student profile.
        profile.SetCefrLevel(parsed.Level);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("CEFR assessment complete for student {Id}: {Level}", profile.Id, parsed.Level);

        return new CefrAssessmentResult(
            parsed.Level,
            parsed.Rationale ?? string.Empty,
            parsed.Strengths ?? [],
            parsed.AreasForImprovement ?? []);
    }

    public static CefrAssessmentPayload ParseResponse(string responseJson)
    {
        var cleaned = responseJson.Trim();
        if (cleaned.StartsWith("```"))
        {
            var firstNewline = cleaned.IndexOf('\n');
            var lastFence = cleaned.LastIndexOf("```");
            if (firstNewline > 0 && lastFence > firstNewline)
                cleaned = cleaned[(firstNewline + 1)..lastFence].Trim();
        }

        try
        {
            var result = JsonSerializer.Deserialize<CefrAssessmentPayload>(cleaned,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (result is null || string.IsNullOrEmpty(result.Level))
                throw new InvalidOperationException("AI response did not include a CEFR level.");

            var validLevels = new[] { "A1", "A2", "B1", "B2", "C1", "C2" };
            var level = result.Level.Trim().ToUpperInvariant();
            if (!validLevels.Contains(level))
                throw new InvalidOperationException($"AI returned invalid CEFR level '{result.Level}'.");

            result.Level = level;
            return result;
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException($"AI response was not valid JSON: {ex.Message}", ex);
        }
    }
}

public sealed class CefrAssessmentPayload
{
    [JsonPropertyName("level")]
    public string Level { get; set; } = string.Empty;

    [JsonPropertyName("rationale")]
    public string? Rationale { get; set; }

    [JsonPropertyName("strengths")]
    public List<string>? Strengths { get; set; }

    [JsonPropertyName("areasForImprovement")]
    public List<string>? AreasForImprovement { get; set; }
}

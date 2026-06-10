using System.Text.Json;
using LinguaCoach.Application.Activity;
using LinguaCoach.Application.Ai;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Infrastructure.Ai;
using LinguaCoach.Persistence;
using Microsoft.Extensions.Logging;

namespace LinguaCoach.Infrastructure.Activity;

/// <summary>
/// Implements IAiActivityGenerator for supported AI-generated activity types.
/// </summary>
public sealed class AiActivityGeneratorHandler : IAiActivityGenerator
{
    private const string GenerateWritingPromptKey = "activity_generate_writing";
    private const string GenerateListeningPromptKey = "activity_generate_listening";
    private const string GenerateSpeakingRolePlayPromptKey = "activity_generate_speaking_roleplay";
    private const string EvaluateWritingPromptKey = "activity_evaluate_writing";
    private const string EvaluateSpeakingRolePlayPromptKey = SpeakingRolePlayEvaluator.EvaluatePromptKey;

    private readonly LinguaCoachDbContext _db;
    private readonly IAiContextBuilder _contextBuilder;
    private readonly AiExecutionService _aiExecution;
    private readonly ILogger<AiActivityGeneratorHandler> _logger;

    public AiActivityGeneratorHandler(
        LinguaCoachDbContext db,
        IAiContextBuilder contextBuilder,
        AiExecutionService aiExecution,
        ILogger<AiActivityGeneratorHandler> logger)
    {
        _db = db;
        _contextBuilder = contextBuilder;
        _aiExecution = aiExecution;
        _logger = logger;
    }

    public async Task<string> GenerateActivityContentAsync(
        ActivityGenerationContext context,
        CancellationToken ct = default)
    {
        // VocabularyPractice is supported when driven by a pattern's OverridePromptKey.
        // Bare VocabularyPractice (no override) is handled by VocabularyPracticeGenerator, not here.
        var isPatternDriven = !string.IsNullOrWhiteSpace(context.OverridePromptKey);
        if (context.ActivityType is not ActivityType.WritingScenario
            and not ActivityType.ListeningComprehension
            and not ActivityType.SpeakingRolePlay
            && !(context.ActivityType == ActivityType.VocabularyPractice && isPatternDriven))
            throw new NotSupportedException(
                $"AI generation for {context.ActivityType} is not yet implemented.");

        var variables = new Dictionary<string, string>
        {
            ["cefrLevel"] = context.CefrLevel,
            ["careerContext"] = context.CareerContext,
            ["sourceLanguageName"] = context.SourceLanguageName,
            ["targetLanguageName"] = context.TargetLanguageName,
            ["recentMistakes"] = context.RecentMistakesSummary ?? "none",
            ["topicHint"] = context.TopicHint ?? "workplace communication",
        };

        // Pattern-aware: use the override prompt key from the ExercisePatternDefinition if provided.
        // Fall back to legacy broad ActivityType routing otherwise.
        var promptKey = !string.IsNullOrWhiteSpace(context.OverridePromptKey)
            ? context.OverridePromptKey
            : context.ActivityType switch
            {
                ActivityType.ListeningComprehension => GenerateListeningPromptKey,
                ActivityType.SpeakingRolePlay       => GenerateSpeakingRolePlayPromptKey,
                _                                   => GenerateWritingPromptKey,
            };

        var aiRequest = await _contextBuilder.BuildAsync(promptKey, variables, ct);

        var response = await _aiExecution.ExecuteWithFallbackAsync(
            promptKey, aiRequest, studentProfileId: null, correlationId: null, ct);

        var cleaned = CleanJson(response);
        switch (context.ActivityType)
        {
            case ActivityType.ListeningComprehension:
                ValidateListeningActivityJson(cleaned);
                break;
            case ActivityType.VocabularyPractice when isPatternDriven:
                // Pattern-specific shapes vary — only validate parseable JSON.
                ValidateIsJson(cleaned);
                break;
            case ActivityType.SpeakingRolePlay:
                ValidateSpeakingRolePlayJson(cleaned);
                break;
            default:
                ValidateWritingActivityJson(cleaned);
                break;
        }
        return cleaned;
    }

    public async Task<string> EvaluateAttemptAsync(
        ActivityEvaluationContext context,
        CancellationToken ct = default)
    {
        if (context.ActivityType is not ActivityType.WritingScenario
            and not ActivityType.SpeakingRolePlay)
            throw new NotSupportedException(
                $"AI evaluation for {context.ActivityType} is not yet implemented.");

        var variables = new Dictionary<string, string>
        {
            ["activityContent"] = context.ActivityContentJson,
            ["studentSubmission"] = context.StudentSubmission,
            ["cefrLevel"] = context.CefrLevel,
            ["careerContext"] = context.CareerContext,
            ["sourceLanguageName"] = context.SourceLanguageName,
            ["targetLanguageName"] = context.TargetLanguageName,
        };

        var evalPromptKey = context.ActivityType == ActivityType.SpeakingRolePlay
            ? EvaluateSpeakingRolePlayPromptKey
            : EvaluateWritingPromptKey;

        var aiRequest = await _contextBuilder.BuildAsync(evalPromptKey, variables, ct);

        var response = await _aiExecution.ExecuteWithFallbackAsync(
            evalPromptKey, aiRequest, studentProfileId: null, correlationId: null, ct);

        return CleanJson(response);
    }

    private static void ValidateIsJson(string json)
    {
        try { JsonDocument.Parse(json); }
        catch (JsonException ex)
        {
            throw new AiResponseValidationException($"AI response is not valid JSON: {ex.Message}");
        }
    }

    private static string CleanJson(string raw)
    {
        var cleaned = raw.Trim();
        if (cleaned.StartsWith("```"))
        {
            var firstNewline = cleaned.IndexOf('\n');
            var lastFence = cleaned.LastIndexOf("```");
            if (firstNewline > 0 && lastFence > firstNewline)
                cleaned = cleaned[(firstNewline + 1)..lastFence].Trim();
        }
        return cleaned;
    }

    private static void ValidateWritingActivityJson(string json)
    {
        // Validate the JSON has the expected writing activity shape.
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        if (!root.TryGetProperty("situation", out _) && !root.TryGetProperty("learningGoal", out _))
            throw new AiResponseValidationException(
                "AI writing activity response missing required fields (situation, learningGoal).");
    }

    private static void ValidateSpeakingRolePlayJson(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        if (!root.TryGetProperty("scenario", out _) && !root.TryGetProperty("speakingGoal", out _))
            throw new AiResponseValidationException(
                "AI speaking activity response missing required fields (scenario, speakingGoal).");
    }

    private static void ValidateListeningActivityJson(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        if (!root.TryGetProperty("audioScript", out var script) || script.ValueKind != JsonValueKind.String)
            throw new AiResponseValidationException("AI listening activity response missing audioScript.");
        if (!root.TryGetProperty("questions", out var questions) || questions.ValueKind != JsonValueKind.Array || questions.GetArrayLength() == 0)
            throw new AiResponseValidationException("AI listening activity response missing questions.");
    }
}

using System.Text.Json;
using System.Text.Json.Serialization;
using LinguaCoach.Application.Ai;
using LinguaCoach.Application.Speaking;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Infrastructure.Ai;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LinguaCoach.Infrastructure.Speaking;

public sealed class SpeakingSessionHandler : ICreateSpeakingSessionHandler, ISubmitSpeakingTurnHandler
{
    private const string PromptKey = "speaking.turn.v1";

    private readonly LinguaCoachDbContext _db;
    private readonly IAiContextBuilder _contextBuilder;
    private readonly IAiProvider _aiProvider;
    private readonly ILogger<SpeakingSessionHandler> _logger;

    public SpeakingSessionHandler(
        LinguaCoachDbContext db,
        IAiContextBuilder contextBuilder,
        IAiProvider aiProvider,
        ILogger<SpeakingSessionHandler> logger)
    {
        _db = db;
        _contextBuilder = contextBuilder;
        _aiProvider = aiProvider;
        _logger = logger;
    }

    // ── Create session ────────────────────────────────────────────────────────

    public async Task<SpeakingSessionDto> HandleAsync(CreateSpeakingSessionCommand command, CancellationToken ct = default)
    {
        var profile = await _db.StudentProfiles
            .Include(p => p.LanguagePair)
                .ThenInclude(lp => lp!.SourceLanguage)
            .Include(p => p.CareerProfile)
            .FirstOrDefaultAsync(p => p.UserId == command.UserId, ct)
            ?? throw new InvalidOperationException("Student profile not found.");

        if (profile.OnboardingStatus != Domain.Enums.OnboardingStatus.Complete)
            throw new InvalidOperationException("Speaking sessions require completed onboarding.");

        var scenario = await _db.SpeakingScenarios
            .FirstOrDefaultAsync(s => s.Id == command.ScenarioId
                && s.LanguagePairId == profile.LanguagePairId, ct)
            ?? throw new InvalidOperationException("Speaking scenario not found or does not match your language pair.");

        var cefrLevel = profile.CefrLevel ?? "B1";
        var careerContext = profile.CareerProfile?.Name ?? "Document Controller";

        var session = new SpeakingSession(
            profile.Id,
            scenario.Id,
            cefrLevel,
            careerContext,
            scenario.MaxTurns);

        session.Start();
        _db.SpeakingSessions.Add(session);
        await _db.SaveChangesAsync(ct);

        // Generate the first AI question for turn 1.
        var firstQuestion = await GenerateFirstQuestionAsync(profile, scenario, session, ct);

        // Store turn 1 with the AI question (no transcript yet — student hasn't spoken).
        session.AdvanceTurn();
        var firstTurn = new SpeakingTurn(session.Id, session.CurrentTurn, firstQuestion);
        _db.SpeakingTurns.Add(firstTurn);
        await _db.SaveChangesAsync(ct);

        return new SpeakingSessionDto(
            session.Id,
            scenario.Title,
            scenario.Goal,
            scenario.MaxTurns,
            firstQuestion);
    }

    // ── Submit turn ───────────────────────────────────────────────────────────

    public async Task<SpeakingTurnResultDto> HandleAsync(SubmitSpeakingTurnCommand command, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(command.UserTranscript))
            throw new ArgumentException("User transcript is required.", nameof(command));
        if (command.UserTranscript.Length > 2000)
            throw new ArgumentException("Transcript must be at most 2000 characters.", nameof(command));

        var profile = await _db.StudentProfiles
            .Include(p => p.LanguagePair)
                .ThenInclude(lp => lp!.SourceLanguage)
            .Include(p => p.LanguagePair)
                .ThenInclude(lp => lp!.TargetLanguage)
            .Include(p => p.CareerProfile)
            .FirstOrDefaultAsync(p => p.UserId == command.UserId, ct)
            ?? throw new InvalidOperationException("Student profile not found.");

        var session = await _db.SpeakingSessions
            .FirstOrDefaultAsync(s => s.Id == command.SessionId && s.StudentProfileId == profile.Id, ct)
            ?? throw new InvalidOperationException("Speaking session not found.");

        if (session.Status != Domain.Enums.SpeakingSessionStatus.InProgress)
            throw new InvalidOperationException("Session is not in progress.");

        var scenario = await _db.SpeakingScenarios
            .FirstOrDefaultAsync(s => s.Id == session.ScenarioId, ct)
            ?? throw new InvalidOperationException("Scenario not found.");

        // Get the current open turn (the one with no transcript yet).
        var currentTurn = await _db.SpeakingTurns
            .Where(t => t.SpeakingSessionId == session.Id && t.UserTranscript == null)
            .OrderBy(t => t.TurnNumber)
            .FirstOrDefaultAsync(ct)
            ?? throw new InvalidOperationException("No open turn found for this session.");

        // Previous turn summary for compact context.
        var prevTurnSummary = await _db.SpeakingTurns
            .Where(t => t.SpeakingSessionId == session.Id && t.TurnNumber < currentTurn.TurnNumber)
            .OrderByDescending(t => t.TurnNumber)
            .Select(t => t.TurnSummary)
            .FirstOrDefaultAsync(ct) ?? string.Empty;

        var variables = new Dictionary<string, string>
        {
            ["sourceLanguageName"] = LanguageSupportResolver.ResolveSourceLanguageName(profile),
            ["cefrLevel"] = session.CefrLevel,
            ["careerContext"] = session.CareerContext,
            ["scenarioGoal"] = scenario.Goal,
            ["previousTurnSummary"] = prevTurnSummary,
            ["userTranscript"] = command.UserTranscript,
        };

        var aiRequest = await _contextBuilder.BuildAsync(PromptKey, variables, ct);
        var aiResponse = await _aiProvider.CompleteAsync(aiRequest, ct);

        // Stage AI usage log — saved atomically with the turn response below.
        _db.AiUsageLogs.Add(new AiUsageLog(
            profile.Id, "speaking_turn", _aiProvider.ProviderName,
            string.IsNullOrEmpty(aiResponse.ModelName) ? "unknown" : aiResponse.ModelName,
            isFallback: false, wasSuccessful: true, failureReason: null,
            aiResponse.InputTokens, aiResponse.OutputTokens, aiResponse.CostUsd,
            durationMs: 0, correlationId: null));

        var parsed = ParseTurnResponse(aiResponse.ResponseJson);

        currentTurn.RecordResponse(
            command.UserTranscript,
            parsed.AiReply ?? "Thank you. Let's continue.",
            aiResponse.ResponseJson,
            JsonSerializer.Serialize(parsed.Mistakes ?? []),
            null,  // pronunciationScore — no audio in MVP
            parsed.GrammarScore,
            parsed.VocabularyScore,
            parsed.FluencyScore,
            parsed.TurnSummary);

        await _db.SaveChangesAsync(ct);  // atomic: usage + turn response

        // Determine if session is complete (last turn just submitted).
        var isLastTurn = session.CurrentTurn >= session.MaxTurns;
        double? overallScore = null;

        if (isLastTurn)
        {
            // Calculate overall score as average of all grammar scores.
            var allTurns = await _db.SpeakingTurns
                .Where(t => t.SpeakingSessionId == session.Id && t.GrammarScore.HasValue)
                .ToListAsync(ct);
            if (allTurns.Count == 0)
            {
                _logger.LogWarning("No scored turns found for session {SessionId}; using fallback score 50", session.Id);
                overallScore = 50.0;
            }
            else
            {
                overallScore = Math.Round(allTurns.Average(t =>
                    (t.GrammarScore!.Value
                     + (t.VocabularyScore ?? t.GrammarScore.Value)
                     + (t.FluencyScore ?? t.GrammarScore.Value)) / 3.0), 1);
            }

            var summary = $"Completed speaking session: {scenario.Title}. Score: {overallScore:F0}/100.";
            if (summary.Length > UserLearningSummary.MaxSummaryLength)
                summary = summary[..UserLearningSummary.MaxSummaryLength];

            var sessionSummary = parsed.TurnSummary?.Length > 200
                ? parsed.TurnSummary[..200]
                : parsed.TurnSummary;
            session.Complete(overallScore.Value, sessionSummary);
            await UpdateLearningSummaryAsync(profile.Id, summary, parsed.Mistakes ?? [], ct);
            await _db.SaveChangesAsync(ct);
        }
        else
        {
            // Advance to next turn and create the next turn row with the AI reply as the next question.
            session.AdvanceTurn();
            var nextTurn = new SpeakingTurn(session.Id, session.CurrentTurn, parsed.AiReply ?? "Please continue.");
            _db.SpeakingTurns.Add(nextTurn);
            await _db.SaveChangesAsync(ct);
        }

        return new SpeakingTurnResultDto(
            currentTurn.TurnNumber,
            parsed.AiReply ?? string.Empty,
            parsed.Feedback ?? string.Empty,
            parsed.Mistakes ?? [],
            parsed.GrammarScore,
            parsed.VocabularyScore,
            parsed.FluencyScore,
            isLastTurn,
            overallScore);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task<string> GenerateFirstQuestionAsync(
        StudentProfile profile,
        SpeakingScenario scenario,
        SpeakingSession session,
        CancellationToken ct)
    {
        var variables = new Dictionary<string, string>
        {
            ["sourceLanguageName"] = LanguageSupportResolver.ResolveSourceLanguageName(profile),
            ["cefrLevel"] = session.CefrLevel,
            ["careerContext"] = session.CareerContext,
            ["scenarioGoal"] = scenario.Goal,
            ["previousTurnSummary"] = string.Empty,
            ["userTranscript"] = "[SESSION START — ask the opening question to begin the scenario]",
        };

        var aiRequest = await _contextBuilder.BuildAsync(PromptKey, variables, ct);
        var aiResponse = await _aiProvider.CompleteAsync(aiRequest, ct);

        _db.AiUsageLogs.Add(new AiUsageLog(
            profile.Id, "speaking_turn", _aiProvider.ProviderName,
            string.IsNullOrEmpty(aiResponse.ModelName) ? "unknown" : aiResponse.ModelName,
            isFallback: false, wasSuccessful: true, failureReason: null,
            aiResponse.InputTokens, aiResponse.OutputTokens, aiResponse.CostUsd,
            durationMs: 0, correlationId: null));
        await _db.SaveChangesAsync(ct);

        try
        {
            var parsed = ParseTurnResponse(aiResponse.ResponseJson);
            return parsed.AiReply ?? "Good morning. Could you please introduce yourself and explain why you are calling today?";
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "AI response parsing failed for opening question (session {SessionId}); using fallback", session.Id);
            return "Good morning. Could you please introduce yourself and explain why you are calling today?";
        }
    }

    private async Task UpdateLearningSummaryAsync(
        Guid studentProfileId, string progress, IReadOnlyList<string> mistakes, CancellationToken ct)
    {
        var summary = await _db.UserLearningSummaries
            .FirstOrDefaultAsync(s => s.StudentProfileId == studentProfileId, ct);

        if (summary is null)
        {
            summary = new UserLearningSummary(studentProfileId);
            _db.UserLearningSummaries.Add(summary);
        }

        var weaknesses = mistakes.Count > 0
            ? string.Join("; ", mistakes.Take(3))
            : string.Empty;

        if (weaknesses.Length > UserLearningSummary.MaxSummaryLength)
            weaknesses = weaknesses[..UserLearningSummary.MaxSummaryLength];

        summary.Update(weaknesses, progress);
    }

    public static SpeakingTurnPayload ParseTurnResponse(string responseJson)
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
            var result = JsonSerializer.Deserialize<SpeakingTurnPayload>(cleaned,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            return result ?? new SpeakingTurnPayload();
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException($"AI response was not valid JSON: {ex.Message}", ex);
        }
    }
}

public sealed class SpeakingTurnPayload
{
    [JsonPropertyName("aiReply")]
    public string? AiReply { get; set; }

    [JsonPropertyName("pronunciationScore")]
    public double? PronunciationScore { get; set; }

    [JsonPropertyName("grammarScore")]
    public double? GrammarScore { get; set; }

    [JsonPropertyName("vocabularyScore")]
    public double? VocabularyScore { get; set; }

    [JsonPropertyName("fluencyScore")]
    public double? FluencyScore { get; set; }

    [JsonPropertyName("feedback")]
    public string? Feedback { get; set; }

    [JsonPropertyName("mistakes")]
    public List<string>? Mistakes { get; set; }

    [JsonPropertyName("turnSummary")]
    public string? TurnSummary { get; set; }
}

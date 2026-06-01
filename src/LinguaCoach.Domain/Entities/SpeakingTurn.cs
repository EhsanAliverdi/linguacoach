using LinguaCoach.Domain.Common;

namespace LinguaCoach.Domain.Entities;

/// <summary>
/// Records one turn within a SpeakingSession.
/// Stores the AI question, student transcript (browser STT — no audio bytes in MVP),
/// AI reply, per-skill scores, and structured feedback/mistakes as JSON.
/// </summary>
public sealed class SpeakingTurn : BaseEntity
{
    public Guid SpeakingSessionId { get; private set; }
    public int TurnNumber { get; private set; }

    // The AI prompt/question for this turn.
    public string AiQuestion { get; private set; }

    // Browser Web Speech API transcript. Null if student skipped or STT failed.
    public string? UserTranscript { get; private set; }

    // userAudioUrl: nullable/future — audio upload is not implemented in MVP.
    public string? UserAudioUrl { get; private set; }

    // AI's conversational reply for the next turn prompt.
    public string AiReply { get; private set; }

    // Structured feedback stored as JSON (serialised by persistence layer).
    public string FeedbackJson { get; private set; }

    // Mistakes stored as a JSON array (serialised by persistence layer).
    public string MistakesJson { get; private set; }

    public double? PronunciationScore { get; private set; }
    public double? GrammarScore { get; private set; }
    public double? VocabularyScore { get; private set; }
    public double? FluencyScore { get; private set; }

    // Compact summary used as context in the next turn (max 150 chars).
    public string? TurnSummary { get; private set; }

    private SpeakingTurn()
    {
        AiQuestion = string.Empty;
        AiReply = string.Empty;
        FeedbackJson = "{}";
        MistakesJson = "[]";
    }

    public SpeakingTurn(
        Guid speakingSessionId,
        int turnNumber,
        string aiQuestion)
    {
        if (speakingSessionId == Guid.Empty) throw new ArgumentException("SpeakingSessionId must not be empty.", nameof(speakingSessionId));
        if (turnNumber < 1) throw new ArgumentOutOfRangeException(nameof(turnNumber), "TurnNumber must be >= 1.");
        if (string.IsNullOrWhiteSpace(aiQuestion)) throw new ArgumentException("AiQuestion is required.", nameof(aiQuestion));

        SpeakingSessionId = speakingSessionId;
        TurnNumber = turnNumber;
        AiQuestion = aiQuestion.Trim();
        AiReply = string.Empty;
        FeedbackJson = "{}";
        MistakesJson = "[]";
    }

    public void RecordResponse(
        string? userTranscript,
        string aiReply,
        string feedbackJson,
        string mistakesJson,
        double? pronunciationScore,
        double? grammarScore,
        double? vocabularyScore,
        double? fluencyScore,
        string? turnSummary)
    {
        if (string.IsNullOrWhiteSpace(aiReply)) throw new ArgumentException("AiReply is required.", nameof(aiReply));

        UserTranscript = userTranscript?.Trim();
        AiReply = aiReply.Trim();
        FeedbackJson = string.IsNullOrWhiteSpace(feedbackJson) ? "{}" : feedbackJson;
        MistakesJson = string.IsNullOrWhiteSpace(mistakesJson) ? "[]" : mistakesJson;
        PronunciationScore = pronunciationScore;
        GrammarScore = grammarScore;
        VocabularyScore = vocabularyScore;
        FluencyScore = fluencyScore;
        TurnSummary = turnSummary?.Trim();
    }
}

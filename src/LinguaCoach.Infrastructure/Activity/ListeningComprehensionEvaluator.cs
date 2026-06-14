using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using LinguaCoach.Application.Activity;

namespace LinguaCoach.Infrastructure.Activity;

public sealed class ListeningComprehensionEvaluator
{
    public (string FeedbackJson, double Score) Evaluate(
        string contentJson,
        IReadOnlyList<ListeningAnswerDto> answers,
        string? responseText)
    {
        var content = JsonSerializer.Deserialize<ListeningContent>(contentJson,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new ListeningContent();

        var questionResults = new List<ListeningQuestionFeedback>();
        var answerMap = answers.ToDictionary(a => a.QuestionId, a => a.Answer ?? string.Empty, StringComparer.OrdinalIgnoreCase);

        foreach (var q in content.Questions ?? [])
        {
            var studentAnswer = answerMap.GetValueOrDefault(q.Id ?? string.Empty, string.Empty);
            var score = ScoreAnswer(studentAnswer, q.ExpectedAnswer ?? string.Empty);
            questionResults.Add(new ListeningQuestionFeedback
            {
                QuestionId = q.Id ?? string.Empty,
                Question = q.Question ?? string.Empty,
                StudentAnswer = studentAnswer,
                ExpectedAnswerSummary = q.ExpectedAnswer ?? string.Empty,
                IsCorrect = score >= 0.85,
                Score = Math.Round(score * 100, 0),
                Feedback = score >= 0.85
                    ? "You found the key information."
                    : score >= 0.45
                        ? "You understood part of the message. Check the expected detail."
                        : "Review the transcript and listen for this detail next time."
            });
        }

        var questionScore = questionResults.Count == 0
            ? 0
            : questionResults.Average(r => r.Score);
        var responseScore = string.IsNullOrWhiteSpace(content.ResponseTask?.Prompt)
            ? 100
            : ScoreResponse(responseText, content.ResponseTask.ExpectedFocus);
        var overall = questionResults.Count == 0
            ? responseScore
            : Math.Round(questionScore * 0.75 + responseScore * 0.25, 0);

        var payload = new ListeningFeedbackPayload
        {
            OverallScore = overall,
            CoachSummary = overall >= 80
                ? "You understood the main workplace message and responded professionally."
                : overall >= 60
                    ? "You understood some important details. Focus on the exact action and deadline next time."
                    : "This message needs another careful pass. Use the transcript to find the task, timing, and reason.",
            QuestionFeedback = questionResults,
            Transcript = content.AudioScript ?? string.Empty,
            ResponseFeedback = BuildResponseFeedback(content.ResponseTask, responseText, responseScore),
            MiniLesson = "In workplace listening, first identify the action, the reason, and any time or deadline.",
            NextImprovementStep = "Read the transcript once, underline the requested action and deadline, then answer again without looking."
        };

        return (JsonSerializer.Serialize(payload, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        }), overall);
    }

    private static double ScoreAnswer(string answer, string expected)
    {
        var actualTokens = Tokenize(answer);
        var expectedTokens = Tokenize(expected);
        if (expectedTokens.Count == 0) return 0;
        if (Normalize(answer).Contains(Normalize(expected), StringComparison.OrdinalIgnoreCase)) return 1;
        var matched = expectedTokens.Count(t => actualTokens.Contains(t));
        return Math.Clamp((double)matched / expectedTokens.Count, 0, 1);
    }

    private static string? BuildResponseFeedback(ListeningResponseTask? responseTask, string? responseText, double responseScore)
    {
        if (string.IsNullOrWhiteSpace(responseTask?.Prompt)) return null;

        if (string.IsNullOrWhiteSpace(responseText))
            return "Add a short reply confirming what you will do.";

        var focusTokens = Tokenize(responseTask.ExpectedFocus ?? string.Empty);
        var responseTokens = Tokenize(responseText);
        var missing = focusTokens.Where(t => !responseTokens.Contains(t)).ToList();

        if (focusTokens.Count > 0 && missing.Count > 0)
        {
            return $"Your reply is missing some key points from the message: {string.Join(", ", missing)}. " +
                   "Mention these, confirm the requested action and timeline, and keep a polite workplace tone.";
        }

        if (responseScore >= 80)
            return "Your reply covers the key points from the message in a clear, professional tone.";

        return "Your reply should confirm the task, mention the timeline if relevant, and keep a polite workplace tone.";
    }

    private static double ScoreResponse(string? responseText, string? expectedFocus)
    {
        if (string.IsNullOrWhiteSpace(responseText)) return 0;
        var responseTokens = Tokenize(responseText);
        var focusTokens = Tokenize(expectedFocus ?? string.Empty);
        if (focusTokens.Count == 0) return responseTokens.Count >= 5 ? 80 : 50;
        var matched = focusTokens.Count(t => responseTokens.Contains(t));
        return Math.Clamp(50 + matched * 10, 50, 100);
    }

    private static HashSet<string> Tokenize(string value) =>
        Normalize(value).Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(t => t.Length > 2)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

    private static string Normalize(string value) =>
        Regex.Replace(value.ToLowerInvariant(), "[^a-z0-9 ]", " ").Trim();

    private sealed class ListeningContent
    {
        public string? AudioScript { get; set; }
        public List<ListeningQuestion>? Questions { get; set; }
        public ListeningResponseTask? ResponseTask { get; set; }
    }

    private sealed class ListeningQuestion
    {
        public string? Id { get; set; }
        public string? Question { get; set; }
        public string? ExpectedAnswer { get; set; }
    }

    private sealed class ListeningResponseTask
    {
        public string? Prompt { get; set; }
        public string? ExpectedFocus { get; set; }
    }

    private sealed class ListeningFeedbackPayload
    {
        [JsonPropertyName("overallScore")] public double OverallScore { get; set; }
        [JsonPropertyName("coachSummary")] public string? CoachSummary { get; set; }
        [JsonPropertyName("questionFeedback")] public List<ListeningQuestionFeedback> QuestionFeedback { get; set; } = [];
        [JsonPropertyName("transcript")] public string? Transcript { get; set; }
        [JsonPropertyName("responseFeedback")] public string? ResponseFeedback { get; set; }
        [JsonPropertyName("miniLesson")] public string? MiniLesson { get; set; }
        [JsonPropertyName("nextImprovementStep")] public string? NextImprovementStep { get; set; }
    }

    private sealed class ListeningQuestionFeedback
    {
        [JsonPropertyName("questionId")] public string QuestionId { get; set; } = string.Empty;
        [JsonPropertyName("question")] public string Question { get; set; } = string.Empty;
        [JsonPropertyName("studentAnswer")] public string StudentAnswer { get; set; } = string.Empty;
        [JsonPropertyName("expectedAnswerSummary")] public string ExpectedAnswerSummary { get; set; } = string.Empty;
        [JsonPropertyName("isCorrect")] public bool IsCorrect { get; set; }
        [JsonPropertyName("score")] public double Score { get; set; }
        [JsonPropertyName("feedback")] public string Feedback { get; set; } = string.Empty;
    }
}

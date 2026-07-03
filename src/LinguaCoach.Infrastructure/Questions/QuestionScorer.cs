using LinguaCoach.Application.Questions;
using LinguaCoach.Domain.Questions;

namespace LinguaCoach.Infrastructure.Questions;

/// <summary>Deterministic scoring for submitted answers, shared by onboarding (CEFR-scored
/// steps) and placement. Replaces PlacementScoringService and the CEFR-scoring branch of
/// OnboardingV2CompleteHandler with one implementation written once against the shared
/// QuestionContent schema.</summary>
public sealed class QuestionScorer : IQuestionScorer
{
    public QuestionScoreResult Score(QuestionContent content, QuestionAnswer answer)
    {
        var subScores = ScoreLeaves(content, answer).ToList();
        if (subScores.Count == 0)
            return new QuestionScoreResult(false, 0.0, subScores, "No scorable sub-questions found.");

        var overallScore = subScores.Average(s => s.Score);
        var overallCorrect = subScores.All(s => s.IsCorrect != false);
        var notes = subScores.Count == 1
            ? DescribeSingle(subScores[0])
            : $"{subScores.Count(s => s.IsCorrect == true)}/{subScores.Count} sub-questions correct.";

        return new QuestionScoreResult(overallCorrect, overallScore, subScores, notes);
    }

    private IEnumerable<QuestionSubScore> ScoreLeaves(QuestionContent content, QuestionAnswer answer)
    {
        switch (content)
        {
            case SingleChoiceQuestion q:
                yield return ScoreSingleChoice(q, answer);
                break;
            case MultipleChoiceQuestion q:
                yield return ScoreMultipleChoice(q, answer);
                break;
            case GapFillQuestion q:
                yield return ScoreGapFill(q, answer);
                break;
            case FreeTextQuestion:
                yield return new QuestionSubScore(content.Id, null, 1.0);
                break;
            case ListeningGroupQuestion group:
                foreach (var sub in group.Questions)
                    foreach (var score in ScoreLeaves(sub, answer))
                        yield return score;
                break;
            case ReadingGroupQuestion group:
                foreach (var sub in group.Questions)
                    foreach (var score in ScoreLeaves(sub, answer))
                        yield return score;
                break;
        }
    }

    private static QuestionSubScore ScoreSingleChoice(SingleChoiceQuestion q, QuestionAnswer answer)
    {
        if (string.IsNullOrWhiteSpace(q.CorrectAnswerKey))
            return new QuestionSubScore(q.Id, null, 1.0);

        var given = answer.Find(q.Id)?.Values.FirstOrDefault();
        var isCorrect = given is not null && string.Equals(given, q.CorrectAnswerKey, StringComparison.OrdinalIgnoreCase);
        return new QuestionSubScore(q.Id, isCorrect, isCorrect ? 1.0 : 0.0);
    }

    private static QuestionSubScore ScoreMultipleChoice(MultipleChoiceQuestion q, QuestionAnswer answer)
    {
        if (q.CorrectAnswerKeys is null || q.CorrectAnswerKeys.Count == 0)
            return new QuestionSubScore(q.Id, null, 1.0);

        var given = answer.Find(q.Id)?.Values ?? [];
        var expected = new HashSet<string>(q.CorrectAnswerKeys, StringComparer.OrdinalIgnoreCase);
        var givenSet = new HashSet<string>(given, StringComparer.OrdinalIgnoreCase);
        var isCorrect = expected.SetEquals(givenSet);
        return new QuestionSubScore(q.Id, isCorrect, isCorrect ? 1.0 : 0.0);
    }

    private static QuestionSubScore ScoreGapFill(GapFillQuestion q, QuestionAnswer answer)
    {
        if (string.IsNullOrWhiteSpace(q.CorrectAnswer))
            return new QuestionSubScore(q.Id, null, 1.0);

        var given = answer.Find(q.Id)?.Values.FirstOrDefault();
        var isCorrect = given is not null && string.Equals(given.Trim(), q.CorrectAnswer.Trim(), StringComparison.OrdinalIgnoreCase);
        return new QuestionSubScore(q.Id, isCorrect, isCorrect ? 1.0 : 0.0);
    }

    private static string DescribeSingle(QuestionSubScore score) => score.IsCorrect switch
    {
        true => "Correct.",
        false => "Incorrect.",
        null => "Answer recorded.",
    };
}

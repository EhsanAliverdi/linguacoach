using LinguaCoach.Domain.Questions;

namespace LinguaCoach.Application.Questions;

public sealed record QuestionValidationResult(bool IsValid, string? Error)
{
    public static QuestionValidationResult Ok() => new(true, null);
    public static QuestionValidationResult Fail(string error) => new(false, error);
}

/// <summary>Validates that a submitted <see cref="QuestionAnswer"/> has the right shape for a
/// given <see cref="QuestionContent"/> — right sub-questions answered, valid choice keys,
/// text within length limits. Does not judge correctness; see <see cref="IQuestionScorer"/>.</summary>
public interface IQuestionAnswerValidator
{
    QuestionValidationResult Validate(QuestionContent content, QuestionAnswer answer);
}

public sealed record QuestionSubScore(string QuestionId, bool? IsCorrect, double Score);

public sealed record QuestionScoreResult(bool IsCorrect, double Score, IReadOnlyList<QuestionSubScore> SubScores, string Notes);

/// <summary>Scores a submitted <see cref="QuestionAnswer"/> against a <see cref="QuestionContent"/>'s
/// correct-answer fields, where one exists. For group questions, aggregates per-sub-question scores
/// (mean score; correct only if every sub-question with a defined correct answer was answered correctly).
/// Questions with no correct answer defined (e.g. onboarding profile-capture) score as correct/1.0 —
/// there's nothing to grade, submitting a valid answer is success.</summary>
public interface IQuestionScorer
{
    QuestionScoreResult Score(QuestionContent content, QuestionAnswer answer);
}

namespace LinguaCoach.Domain.Questions;

/// <summary>
/// A submitted answer to one sub-question, addressed by <see cref="QuestionContent.Id"/>.
/// Values is a list so single-choice/gap-fill/free-text answers (one element) and
/// multiple-choice answers (N elements) share the same shape.
/// </summary>
public sealed record QuestionAnswerItem(string QuestionId, IReadOnlyList<string> Values);

/// <summary>
/// The full submitted answer for one question definition. A standalone leaf question submits
/// a single-element list (addressed by its default "q1" id); a group question submits one
/// element per sub-question.
/// </summary>
public sealed record QuestionAnswer(IReadOnlyList<QuestionAnswerItem> Answers)
{
    public QuestionAnswerItem? Find(string questionId) =>
        Answers.FirstOrDefault(a => a.QuestionId == questionId);
}

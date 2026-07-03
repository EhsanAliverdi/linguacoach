using LinguaCoach.Application.Questions;
using LinguaCoach.Domain.Questions;

namespace LinguaCoach.Infrastructure.Questions;

/// <summary>Shape validation for submitted answers, shared by onboarding and placement.
/// Replaces onboarding's per-step-type ValidateAnswer switch with one implementation
/// written once against the shared QuestionContent schema.</summary>
public sealed class QuestionAnswerValidator : IQuestionAnswerValidator
{
    public QuestionValidationResult Validate(QuestionContent content, QuestionAnswer answer)
    {
        return content switch
        {
            SingleChoiceQuestion q => ValidateSingleChoice(q, answer),
            MultipleChoiceQuestion q => ValidateMultipleChoice(q, answer),
            GapFillQuestion q => ValidateGapFill(q, answer),
            FreeTextQuestion q => ValidateFreeText(q, answer),
            ListeningGroupQuestion q => ValidateGroup(q.Questions, answer),
            ReadingGroupQuestion q => ValidateGroup(q.Questions, answer),
            _ => QuestionValidationResult.Fail($"Unknown question type '{content.GetType().Name}'."),
        };
    }

    private static QuestionValidationResult ValidateSingleChoice(SingleChoiceQuestion q, QuestionAnswer answer)
    {
        var item = answer.Find(q.Id);
        if (item is null || item.Values.Count != 1 || string.IsNullOrWhiteSpace(item.Values[0]))
            return QuestionValidationResult.Fail($"Question '{q.Id}' requires exactly one selected choice.");

        if (!q.Choices.Any(c => string.Equals(c.Key, item.Values[0], StringComparison.OrdinalIgnoreCase)))
            return QuestionValidationResult.Fail($"Question '{q.Id}': '{item.Values[0]}' is not a valid choice.");

        return QuestionValidationResult.Ok();
    }

    private static QuestionValidationResult ValidateMultipleChoice(MultipleChoiceQuestion q, QuestionAnswer answer)
    {
        var item = answer.Find(q.Id);
        if (item is null || item.Values.Count == 0)
            return QuestionValidationResult.Fail($"Question '{q.Id}' requires at least one selected choice.");

        var invalid = item.Values.FirstOrDefault(v => !q.Choices.Any(c => string.Equals(c.Key, v, StringComparison.OrdinalIgnoreCase)));
        if (invalid is not null)
            return QuestionValidationResult.Fail($"Question '{q.Id}': '{invalid}' is not a valid choice.");

        return QuestionValidationResult.Ok();
    }

    private static QuestionValidationResult ValidateGapFill(GapFillQuestion q, QuestionAnswer answer)
    {
        var item = answer.Find(q.Id);
        if (item is null || item.Values.Count != 1 || string.IsNullOrWhiteSpace(item.Values[0]))
            return QuestionValidationResult.Fail($"Question '{q.Id}' requires a single non-empty answer.");

        return QuestionValidationResult.Ok();
    }

    private static QuestionValidationResult ValidateFreeText(FreeTextQuestion q, QuestionAnswer answer)
    {
        var item = answer.Find(q.Id);
        if (item is null || item.Values.Count != 1 || string.IsNullOrWhiteSpace(item.Values[0]))
            return QuestionValidationResult.Fail($"Question '{q.Id}' requires a single non-empty answer.");

        if (q.MaxLength is int maxLength && item.Values[0].Length > maxLength)
            return QuestionValidationResult.Fail($"Question '{q.Id}': answer exceeds the {maxLength}-character limit.");

        return QuestionValidationResult.Ok();
    }

    private QuestionValidationResult ValidateGroup(IReadOnlyList<QuestionContent> subQuestions, QuestionAnswer answer)
    {
        if (subQuestions.Count == 0)
            return QuestionValidationResult.Fail("Group question has no sub-questions defined.");

        foreach (var sub in subQuestions)
        {
            var result = Validate(sub, answer);
            if (!result.IsValid) return result;
        }

        return QuestionValidationResult.Ok();
    }
}

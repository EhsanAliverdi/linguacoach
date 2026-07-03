using System.Text.Json.Serialization;

namespace LinguaCoach.Domain.Questions;

/// <summary>
/// Shared, polymorphic question schema used by both onboarding steps and placement items.
/// Serialized via System.Text.Json's built-in polymorphism, discriminated by "type".
/// Leaf types (SingleChoice/MultipleChoice/GapFill/FreeText) represent exactly one question
/// with one answer. Group types (ListeningGroup/ReadingGroup) wrap a stimulus (audio script /
/// reading passage) plus one-or-more leaf sub-questions — a single-question reading/listening
/// item is simply a group with one sub-question, so no special-casing is needed for "1 vs many".
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(SingleChoiceQuestion), "single_choice")]
[JsonDerivedType(typeof(MultipleChoiceQuestion), "multiple_choice")]
[JsonDerivedType(typeof(GapFillQuestion), "gap_fill")]
[JsonDerivedType(typeof(FreeTextQuestion), "free_text")]
[JsonDerivedType(typeof(ListeningGroupQuestion), "listening_group")]
[JsonDerivedType(typeof(ReadingGroupQuestion), "reading_group")]
public abstract class QuestionContent
{
    /// <summary>Stable identifier for this question within its containing definition, used to address
    /// answers in <see cref="QuestionAnswer"/>. Standalone (non-grouped) questions default to "q1".</summary>
    public string Id { get; init; } = "q1";
}

public sealed class ChoiceOption
{
    public required string Key { get; init; }
    public required string Label { get; init; }
}

/// <summary>Exactly one correct choice, or none (e.g. onboarding profile-capture questions with no scoring).</summary>
public sealed class SingleChoiceQuestion : QuestionContent
{
    public required string QuestionText { get; init; }
    public required IReadOnlyList<ChoiceOption> Choices { get; init; }
    public string? CorrectAnswerKey { get; init; }
}

/// <summary>Zero or more correct choices may be selected.</summary>
public sealed class MultipleChoiceQuestion : QuestionContent
{
    public required string QuestionText { get; init; }
    public required IReadOnlyList<ChoiceOption> Choices { get; init; }
    public IReadOnlyList<string>? CorrectAnswerKeys { get; init; }
}

public sealed class GapFillQuestion : QuestionContent
{
    public required string QuestionText { get; init; }
    public string? CorrectAnswer { get; init; }
}

public sealed class FreeTextQuestion : QuestionContent
{
    public required string QuestionText { get; init; }
    public string? Placeholder { get; init; }
    public int? MaxLength { get; init; }
}

/// <summary>An audio stimulus (script authored by an admin, converted to speech and stored via
/// object storage) followed by one-or-more leaf sub-questions.</summary>
public sealed class ListeningGroupQuestion : QuestionContent
{
    public string? Instructions { get; init; }
    public required string AudioScript { get; init; }
    public string? AudioStorageKey { get; init; }
    public string? AudioContentType { get; init; }
    public required IReadOnlyList<QuestionContent> Questions { get; init; }
}

/// <summary>A reading passage followed by one-or-more leaf sub-questions.</summary>
public sealed class ReadingGroupQuestion : QuestionContent
{
    public string? Instructions { get; init; }
    public required string Passage { get; init; }
    public required IReadOnlyList<QuestionContent> Questions { get; init; }
}

/// <summary>Strips correct-answer fields from a QuestionContent tree before it's sent to a
/// student — the client only ever needs to know what to ask/render, never what scores correctly.
/// Must be applied to every student-facing DTO that carries Content (e.g. PlacementNextItemDto);
/// admin-facing DTOs intentionally keep the correct answers.</summary>
public static class QuestionContentRedactor
{
    public static QuestionContent RedactCorrectAnswers(QuestionContent content) => content switch
    {
        SingleChoiceQuestion q => new SingleChoiceQuestion { Id = q.Id, QuestionText = q.QuestionText, Choices = q.Choices, CorrectAnswerKey = null },
        MultipleChoiceQuestion q => new MultipleChoiceQuestion { Id = q.Id, QuestionText = q.QuestionText, Choices = q.Choices, CorrectAnswerKeys = null },
        GapFillQuestion q => new GapFillQuestion { Id = q.Id, QuestionText = q.QuestionText, CorrectAnswer = null },
        FreeTextQuestion q => q,
        ListeningGroupQuestion q => new ListeningGroupQuestion
        {
            Id = q.Id, Instructions = q.Instructions, AudioScript = q.AudioScript,
            AudioStorageKey = q.AudioStorageKey, AudioContentType = q.AudioContentType,
            Questions = q.Questions.Select(RedactCorrectAnswers).ToList(),
        },
        ReadingGroupQuestion q => new ReadingGroupQuestion
        {
            Id = q.Id, Instructions = q.Instructions, Passage = q.Passage,
            Questions = q.Questions.Select(RedactCorrectAnswers).ToList(),
        },
        _ => content,
    };
}

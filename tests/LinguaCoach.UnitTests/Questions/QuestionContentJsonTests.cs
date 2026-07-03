using LinguaCoach.Domain.Questions;

namespace LinguaCoach.UnitTests.Questions;

/// <summary>
/// Regression tests for a live production incident (2026-07-04): Postgres jsonb columns do not
/// preserve object key insertion order (shorter keys are stored first), but System.Text.Json's
/// built-in polymorphic deserializer requires the "type" discriminator to be the first property
/// in the JSON object. Every ContentJson row read back from jsonb had "Id" before "type", which
/// threw NotSupportedException — uncaught by the existing JsonException-only guard — and 500'd
/// the entire onboarding/placement admin list endpoints. See
/// docs/reviews/2026-07-04-onboarding-categories-placement-permission-500-debugging.md.
/// </summary>
public sealed class QuestionContentJsonTests
{
    [Fact]
    public void TryDeserializeContent_DiscriminatorAfterOtherProperties_StillDeserializes()
    {
        // "Id" before "type" — exactly the shape Postgres jsonb returns for content written with
        // System.Text.Json's default polymorphic serialization (which emits "type" first, but
        // jsonb storage reorders keys regardless of write order).
        const string json = """
            {"Id":"q1","QuestionText":"Which is correct?","Choices":[{"Key":"A","Label":"am"}],"CorrectAnswerKey":"A","type":"single_choice"}
            """;

        var result = QuestionContentJson.TryDeserializeContent(json);

        var single = Assert.IsType<SingleChoiceQuestion>(result);
        Assert.Equal("q1", single.Id);
        Assert.Equal("Which is correct?", single.QuestionText);
        Assert.Equal("A", single.CorrectAnswerKey);
    }

    [Fact]
    public void TryDeserializeContent_GroupWithReorderedNestedSubQuestions_StillDeserializes()
    {
        // Nested sub-questions inside a group are reordered by jsonb too — the fix must recurse.
        const string json = """
            {"Id":"q1","AudioScript":"Turn left.","Instructions":null,"AudioStorageKey":null,"AudioContentType":null,"Questions":[{"Id":"q1","QuestionText":"Which way?","Choices":[{"Key":"A","Label":"left"}],"CorrectAnswerKey":"A","type":"single_choice"}],"type":"listening_group"}
            """;

        var result = QuestionContentJson.TryDeserializeContent(json);

        var group = Assert.IsType<ListeningGroupQuestion>(result);
        Assert.Equal("Turn left.", group.AudioScript);
        Assert.Single(group.Questions);
        var sub = Assert.IsType<SingleChoiceQuestion>(group.Questions[0]);
        Assert.Equal("A", sub.CorrectAnswerKey);
    }

    [Fact]
    public void TryDeserializeContent_MissingDiscriminator_ReturnsNullInsteadOfThrowing()
    {
        const string json = """{"Id":"q1","QuestionText":"No type here"}""";

        var result = QuestionContentJson.TryDeserializeContent(json);

        Assert.Null(result);
    }

    [Fact]
    public void TryDeserializeContent_MalformedJson_ReturnsNull()
    {
        var result = QuestionContentJson.TryDeserializeContent("{not json");

        Assert.Null(result);
    }

    [Fact]
    public void TryDeserializeContent_NullInput_ReturnsNull()
    {
        Assert.Null(QuestionContentJson.TryDeserializeContent(null));
    }
}

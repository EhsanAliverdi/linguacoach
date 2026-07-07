using LinguaCoach.Application.FormIo;

namespace LinguaCoach.UnitTests.FormIo;

/// <summary>Covers FormIoQuizAnnotationCodec.CollectComponentKeys — used by
/// AdminOnboardingTemplateService to check which OnboardingProfileFieldMapping keys a schema
/// actually contains before allowing a publish.</summary>
public sealed class FormIoQuizAnnotationCodecCollectComponentKeysTests
{
    [Fact]
    public void CollectComponentKeys_TopLevelComponents_ReturnsAllKeys()
    {
        var schema = """{"components":[{"type":"textfield","key":"preferred_name"},{"type":"radio","key":"difficulty_preference"}]}""";

        var keys = FormIoQuizAnnotationCodec.CollectComponentKeys(schema);

        Assert.Equal(new HashSet<string> { "preferred_name", "difficulty_preference" }, keys);
    }

    [Fact]
    public void CollectComponentKeys_NestedInsidePanel_FindsKeyAtAnyDepth()
    {
        var schema = """
        {
          "components": [
            { "type": "panel", "key": "page1", "components": [
              { "type": "textfield", "key": "preferred_name" }
            ] }
          ]
        }
        """;

        var keys = FormIoQuizAnnotationCodec.CollectComponentKeys(schema);

        Assert.Contains("preferred_name", keys);
        Assert.Contains("page1", keys);
    }

    [Fact]
    public void CollectComponentKeys_EmptySchema_ReturnsEmptySet()
    {
        var schema = """{"components":[]}""";

        var keys = FormIoQuizAnnotationCodec.CollectComponentKeys(schema);

        Assert.Empty(keys);
    }

    [Fact]
    public void CollectComponentKeys_ComponentWithoutKey_IsSkippedWithoutThrowing()
    {
        var schema = """{"components":[{"type":"content","html":"<p>hi</p>"}]}""";

        var keys = FormIoQuizAnnotationCodec.CollectComponentKeys(schema);

        Assert.Empty(keys);
    }
}

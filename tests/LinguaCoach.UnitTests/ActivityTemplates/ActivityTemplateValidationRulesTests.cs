using LinguaCoach.Infrastructure.ActivityTemplates;

namespace LinguaCoach.UnitTests.ActivityTemplates;

public sealed class ActivityTemplateValidationRulesTests
{
    private const string ValidSchema = """
        {"display":"form","components":[{"type":"textfield","key":"prompt_text","label":"Prompt"}]}
        """;

    [Fact]
    public void Parse_NullOrWhitespace_ReturnsEmptyRules()
    {
        var rules = ActivityTemplateValidationRules.Parse(null);
        Assert.Empty(rules.RequiredComponentKeys);
        Assert.Null(rules.MaxSchemaLength);
        Assert.Empty(rules.ForbiddenWords);
    }

    [Fact]
    public void Parse_MalformedJson_ReturnsEmptyRules()
    {
        var rules = ActivityTemplateValidationRules.Parse("{not json");
        Assert.Empty(rules.RequiredComponentKeys);
    }

    [Fact]
    public void Parse_ValidJson_ExtractsAllFields()
    {
        var json = """{"requiredComponentKeys":["prompt_text"],"maxSchemaLength":500,"forbiddenWords":["lorem"]}""";
        var rules = ActivityTemplateValidationRules.Parse(json);

        Assert.Equal(["prompt_text"], rules.RequiredComponentKeys);
        Assert.Equal(500, rules.MaxSchemaLength);
        Assert.Equal(["lorem"], rules.ForbiddenWords);
    }

    [Fact]
    public void Validate_NoRules_ReturnsNoErrors()
    {
        var rules = ActivityTemplateValidationRules.Parse(null);
        Assert.Empty(rules.Validate(ValidSchema));
    }

    [Fact]
    public void Validate_RequiredComponentKeyPresent_NoError()
    {
        var rules = ActivityTemplateValidationRules.Parse("""{"requiredComponentKeys":["prompt_text"]}""");
        Assert.Empty(rules.Validate(ValidSchema));
    }

    [Fact]
    public void Validate_RequiredComponentKeyMissing_ReturnsError()
    {
        var rules = ActivityTemplateValidationRules.Parse("""{"requiredComponentKeys":["missing_key"]}""");
        var errors = rules.Validate(ValidSchema);

        Assert.Single(errors);
        Assert.Contains("missing_key", errors[0]);
    }

    [Fact]
    public void Validate_RequiredComponentKeyInNestedPanel_NoError()
    {
        var nestedSchema = """
            {"display":"form","components":[{"type":"panel","key":"panel1","components":[{"type":"textfield","key":"nested_key"}]}]}
            """;
        var rules = ActivityTemplateValidationRules.Parse("""{"requiredComponentKeys":["nested_key"]}""");
        Assert.Empty(rules.Validate(nestedSchema));
    }

    [Fact]
    public void Validate_SchemaExceedsMaxLength_ReturnsError()
    {
        var rules = ActivityTemplateValidationRules.Parse("""{"maxSchemaLength":10}""");
        var errors = rules.Validate(ValidSchema);

        Assert.Contains(errors, e => e.Contains("exceeds maxSchemaLength"));
    }

    [Fact]
    public void Validate_ContainsForbiddenWord_ReturnsError()
    {
        var schemaWithForbiddenWord = """{"display":"form","components":[{"type":"content","key":"c1","html":"lorem ipsum"}]}""";
        var rules = ActivityTemplateValidationRules.Parse("""{"forbiddenWords":["lorem"]}""");
        var errors = rules.Validate(schemaWithForbiddenWord);

        Assert.Contains(errors, e => e.Contains("forbidden word 'lorem'"));
    }

    [Fact]
    public void Validate_MalformedCandidateJson_WithRequiredKeys_ReturnsError()
    {
        var rules = ActivityTemplateValidationRules.Parse("""{"requiredComponentKeys":["k"]}""");
        var errors = rules.Validate("not json");

        Assert.Contains(errors, e => e.Contains("not valid JSON"));
    }
}

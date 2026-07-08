using LinguaCoach.Infrastructure.Onboarding;

namespace LinguaCoach.UnitTests.Onboarding;

public sealed class FormIoSchemaValidationServiceTests
{
    private readonly FormIoSchemaValidationService _sut = new();

    [Fact]
    public void ValidSchema_WithTextfieldAndSubmitButton_IsValid()
    {
        // Form.io always auto-adds a "button"/submit component to every new form.
        var json = """
        {"components":[
            {"type":"textfield","key":"preferred_name","label":"Name"},
            {"type":"button","key":"submit","label":"Submit","action":"submit"}
        ]}
        """;

        var result = _sut.ValidateSchema(json);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Component_WithEmptyStringScriptProperties_IsValid()
    {
        // Form.io's builder stamps every component with customConditional/calculateValue/
        // customDefaultValue defaulted to "" ("not used") — these must not be flagged as
        // script/eval abuse just because the key is present.
        var json = """
        {"components":[
            {"type":"textfield","key":"preferred_name","label":"Name",
             "customConditional":"","calculateValue":"","customDefaultValue":"",
             "validate":{"custom":""}}
        ]}
        """;

        var result = _sut.ValidateSchema(json);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Component_WithNonEmptyCustomConditional_IsRejected()
    {
        var json = """
        {"components":[
            {"type":"textfield","key":"preferred_name","label":"Name",
             "customConditional":"show = true;"}
        ]}
        """;

        var result = _sut.ValidateSchema(json);

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Component_WithNonEmptyCalculateValue_IsRejected()
    {
        var json = """
        {"components":[
            {"type":"textfield","key":"preferred_name","calculateValue":"value = 1;"}
        ]}
        """;

        var result = _sut.ValidateSchema(json);

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Component_WithValidateCustomScript_IsRejected()
    {
        var json = """
        {"components":[
            {"type":"textfield","key":"preferred_name","validate":{"custom":"valid = true;"}}
        ]}
        """;

        var result = _sut.ValidateSchema(json);

        Assert.False(result.IsValid);
    }

    [Fact]
    public void DatagridWithReorder_AndValidNestedComponents_IsValid()
    {
        // Phase C3 — stock "datagrid" (reorder enabled) used for reorder_paragraphs, with a
        // "hidden" itemId field and a disabled "textarea" row field, both allow-listed.
        var json = """
        {"components":[
            {"type":"datagrid","key":"paragraphs","reorder":true,"disableAddingRemovingRows":true,
             "components":[
                {"type":"hidden","key":"itemId"},
                {"type":"textarea","key":"text","disabled":true}
             ]}
        ]}
        """;

        var result = _sut.ValidateSchema(json);

        Assert.True(result.IsValid, result.Error);
    }

    [Fact]
    public void DatagridWithDisallowedNestedComponent_IsRejected()
    {
        var json = """
        {"components":[
            {"type":"datagrid","key":"paragraphs","components":[
                {"type":"iframe","key":"nested_bad"}
            ]}
        ]}
        """;

        var result = _sut.ValidateSchema(json);

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Component_WithDisallowedType_IsRejected()
    {
        var json = """{"components":[{"type":"iframe","key":"x"}]}""";

        var result = _sut.ValidateSchema(json);

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Component_WithExternalDataSrc_IsRejected()
    {
        var json = """{"components":[{"type":"select","key":"x","dataSrc":"url"}]}""";

        var result = _sut.ValidateSchema(json);

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Component_WithCorrectAnswerKeyInProperties_IsRejected()
    {
        var json = """
        {"components":[
            {"type":"radio","key":"assessment_q1","properties":{"correctAnswerKey":"b"}}
        ]}
        """;

        var result = _sut.ValidateSchema(json);

        Assert.False(result.IsValid);
    }

    [Fact]
    public void NestedPanelComponents_AreValidatedRecursively()
    {
        var json = """
        {"components":[
            {"type":"panel","key":"p1","components":[
                {"type":"iframe","key":"nested_bad"}
            ]}
        ]}
        """;

        var result = _sut.ValidateSchema(json);

        Assert.False(result.IsValid);
    }

    [Fact]
    public void InvalidJson_IsRejected()
    {
        var result = _sut.ValidateSchema("not json");

        Assert.False(result.IsValid);
    }
}

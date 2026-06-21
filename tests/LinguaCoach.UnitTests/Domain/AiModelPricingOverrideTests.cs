using LinguaCoach.Domain.Entities;

namespace LinguaCoach.UnitTests.Domain;

public sealed class AiModelPricingOverrideTests
{
    private static AiModelPricingOverride Make(
        string provider = "openai",
        string model = "gpt-4o",
        decimal input = 0.002m,
        decimal output = 0.008m,
        string currency = "USD",
        DateTime? from = null,
        DateTime? to = null) =>
        new(provider, model, input, output, currency, from ?? DateTime.UtcNow.AddMinutes(-1), to, null, null);

    [Fact]
    public void Constructor_SetsAllFields()
    {
        var from = DateTime.UtcNow.AddDays(-1);
        var o = Make(from: from);
        Assert.Equal("openai", o.ProviderName);
        Assert.Equal("gpt-4o", o.ModelName);
        Assert.Equal(0.002m, o.InputPricePer1KTokens);
        Assert.Equal(0.008m, o.OutputPricePer1KTokens);
        Assert.Equal("USD", o.Currency);
        Assert.True(o.IsActive);
        Assert.Equal(from, o.EffectiveFromUtc);
        Assert.Null(o.EffectiveToUtc);
        Assert.NotEqual(Guid.Empty, o.Id);
    }

    [Fact]
    public void Constructor_NormalizesProviderToLower()
    {
        var o = Make(provider: "OpenAI");
        Assert.Equal("openai", o.ProviderName);
    }

    [Fact]
    public void Constructor_NormalizesCurrencyToUpper()
    {
        var o = Make(currency: "usd");
        Assert.Equal("USD", o.Currency);
    }

    [Fact]
    public void Constructor_WithBlankProvider_Throws()
    {
        Assert.Throws<ArgumentException>(() => Make(provider: ""));
    }

    [Fact]
    public void Constructor_WithBlankModel_Throws()
    {
        Assert.Throws<ArgumentException>(() => Make(model: ""));
    }

    [Fact]
    public void Constructor_WithNegativeInput_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Make(input: -0.001m));
    }

    [Fact]
    public void Constructor_WithNegativeOutput_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Make(output: -0.001m));
    }

    [Fact]
    public void Constructor_WithEffectiveToBeforeFrom_Throws()
    {
        var now = DateTime.UtcNow;
        Assert.Throws<ArgumentException>(() => Make(from: now, to: now.AddMinutes(-1)));
    }

    [Fact]
    public void Constructor_WithZeroPrices_IsValid()
    {
        var o = Make(input: 0m, output: 0m);
        Assert.Equal(0m, o.InputPricePer1KTokens);
        Assert.Equal(0m, o.OutputPricePer1KTokens);
    }

    [Fact]
    public void Update_ChangesPricesAndSetsUpdatedAt()
    {
        var o = Make();
        var before = o.UpdatedAtUtc;
        o.Update(0.005m, 0.020m, "USD", DateTime.UtcNow.AddMinutes(-1), null, "updated", Guid.NewGuid());
        Assert.Equal(0.005m, o.InputPricePer1KTokens);
        Assert.Equal(0.020m, o.OutputPricePer1KTokens);
        Assert.Equal("updated", o.Notes);
        Assert.NotNull(o.UpdatedAtUtc);
        Assert.NotEqual(before, o.UpdatedAtUtc);
    }

    [Fact]
    public void Update_WithNegativeInput_Throws()
    {
        var o = Make();
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            o.Update(-0.001m, 0.008m, "USD", DateTime.UtcNow.AddMinutes(-1), null, null, null));
    }

    [Fact]
    public void Deactivate_SetsIsActiveFalseAndUpdatedAt()
    {
        var o = Make();
        Assert.True(o.IsActive);
        o.Deactivate(Guid.NewGuid());
        Assert.False(o.IsActive);
        Assert.NotNull(o.UpdatedAtUtc);
    }
}

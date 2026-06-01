using FluentAssertions;
using LinguaCoach.Domain.Entities;

namespace LinguaCoach.UnitTests.Domain;

public sealed class AiPromptTokenBudgetTests
{
    [Fact]
    public void NewPrompt_WithoutBudget_HasNullTokenLimits()
    {
        var prompt = new AiPrompt("key", "content");
        prompt.MaxInputTokens.Should().BeNull();
        prompt.MaxOutputTokens.Should().BeNull();
    }

    [Fact]
    public void NewPrompt_WithBudget_StoresBothLimits()
    {
        var prompt = new AiPrompt("key", "content", maxInputTokens: 800, maxOutputTokens: 600);
        prompt.MaxInputTokens.Should().Be(800);
        prompt.MaxOutputTokens.Should().Be(600);
    }

    [Fact]
    public void NewPrompt_WithZeroMaxInputTokens_Throws()
    {
        var act = () => new AiPrompt("key", "content", maxInputTokens: 0);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void NewPrompt_WithNegativeMaxOutputTokens_Throws()
    {
        var act = () => new AiPrompt("key", "content", maxOutputTokens: -1);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void SetTokenBudget_UpdatesBothLimits()
    {
        var prompt = new AiPrompt("key", "content");
        prompt.SetTokenBudget(700, 500);
        prompt.MaxInputTokens.Should().Be(700);
        prompt.MaxOutputTokens.Should().Be(500);
    }

    [Fact]
    public void SetTokenBudget_WithZeroInput_Throws()
    {
        var prompt = new AiPrompt("key", "content");
        var act = () => prompt.SetTokenBudget(0, 500);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}

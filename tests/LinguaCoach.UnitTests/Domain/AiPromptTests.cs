using FluentAssertions;
using LinguaCoach.Domain.Entities;

namespace LinguaCoach.UnitTests.Domain;

public sealed class AiPromptTests
{
    [Fact]
    public void Constructor_SetsAllFields()
    {
        var prompt = new AiPrompt("email-rewrite", "Rewrite the following email...", 2);

        prompt.Key.Should().Be("email-rewrite");
        prompt.Content.Should().Be("Rewrite the following email...");
        prompt.Version.Should().Be(2);
        prompt.IsActive.Should().BeTrue();
    }

    [Fact]
    public void Constructor_DefaultsVersionToOne()
    {
        var prompt = new AiPrompt("key", "content");
        prompt.Version.Should().Be(1);
    }

    [Fact]
    public void Constructor_TrimsKey()
    {
        var prompt = new AiPrompt("  key  ", "content");
        prompt.Key.Should().Be("key");
    }

    [Fact]
    public void Constructor_TrimsContent()
    {
        var prompt = new AiPrompt("key", "  content  ");
        prompt.Content.Should().Be("content");
    }

    [Fact]
    public void Constructor_WithBlankKey_Throws()
    {
        var act = () => new AiPrompt("", "content");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_WithWhitespaceKey_Throws()
    {
        var act = () => new AiPrompt("   ", "content");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_WithBlankContent_Throws()
    {
        var act = () => new AiPrompt("key", "");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_WithWhitespaceContent_Throws()
    {
        var act = () => new AiPrompt("key", "   ");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Deactivate_SetsIsActiveFalse()
    {
        var prompt = new AiPrompt("key", "content");
        prompt.Deactivate();
        prompt.IsActive.Should().BeFalse();
    }

    [Fact]
    public void Activate_SetsIsActiveTrue()
    {
        var prompt = new AiPrompt("key", "content");
        prompt.Deactivate();
        prompt.Activate();
        prompt.IsActive.Should().BeTrue();
    }

    [Fact]
    public void Constructor_WithVersionZero_Throws()
    {
        var act = () => new AiPrompt("key", "content", 0);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Constructor_WithNegativeVersion_Throws()
    {
        var act = () => new AiPrompt("key", "content", -1);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}

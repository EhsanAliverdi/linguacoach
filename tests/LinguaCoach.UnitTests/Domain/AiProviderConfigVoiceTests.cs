using FluentAssertions;
using LinguaCoach.Domain.Entities;

namespace LinguaCoach.UnitTests.Domain;

/// <summary>
/// Unit tests for AiProviderConfig VoiceName support (T35, tts.* feature keys).
/// </summary>
public sealed class AiProviderConfigVoiceTests
{
    [Fact]
    public void Constructor_WithVoiceName_SetsVoice()
    {
        var config = new AiProviderConfig("tts.listening", "fake", "fake", "onyx");
        config.VoiceName.Should().Be("onyx");
    }

    [Fact]
    public void Constructor_WithoutVoiceName_VoiceIsNull()
    {
        var config = new AiProviderConfig("tts.listening", "fake", "fake");
        config.VoiceName.Should().BeNull();
    }

    [Fact]
    public void Constructor_WithWhitespaceVoiceName_VoiceIsNull()
    {
        var config = new AiProviderConfig("tts.placement", "fake", "fake", "   ");
        config.VoiceName.Should().BeNull();
    }

    [Fact]
    public void UpdateVoice_SetsVoiceName()
    {
        var config = new AiProviderConfig("tts.listening", "fake", "fake");
        config.UpdateVoice("nova");
        config.VoiceName.Should().Be("nova");
    }

    [Fact]
    public void UpdateVoice_WithNull_ClearsVoiceName()
    {
        var config = new AiProviderConfig("tts.listening", "fake", "fake", "onyx");
        config.UpdateVoice(null);
        config.VoiceName.Should().BeNull();
    }

    [Fact]
    public void UpdateVoice_WithWhitespace_ClearsVoiceName()
    {
        var config = new AiProviderConfig("tts.listening", "fake", "fake", "onyx");
        config.UpdateVoice("   ");
        config.VoiceName.Should().BeNull();
    }

    [Fact]
    public void FakeProvider_IsInAllowedModels()
    {
        AiProviderConfig.AllowedModels.Should().ContainKey("fake");
        AiProviderConfig.AllowedModels["fake"].Should().Contain("fake");
    }

    [Fact]
    public void OpenAiProvider_IncludesTtsModels()
    {
        var openAiModels = AiProviderConfig.AllowedModels["openai"];
        openAiModels.Should().Contain("tts-1");
        openAiModels.Should().Contain("tts-1-hd");
    }

    [Fact]
    public void Update_WithFakeProviderAndFakeModel_Succeeds()
    {
        var config = new AiProviderConfig("tts.listening", "openai", "tts-1");
        var act = () => config.Update("fake", "fake");
        act.Should().NotThrow();
        config.ProviderName.Should().Be("fake");
        config.ModelName.Should().Be("fake");
    }

    [Fact]
    public void Update_WithOpenAiTts1_Succeeds()
    {
        var config = new AiProviderConfig("tts.listening", "fake", "fake");
        var act = () => config.Update("openai", "tts-1");
        act.Should().NotThrow();
        config.ProviderName.Should().Be("openai");
        config.ModelName.Should().Be("tts-1");
    }
}

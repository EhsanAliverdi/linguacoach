using System.Text.Json;
using FluentAssertions;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Infrastructure.Activity;
using Microsoft.Extensions.Logging.Abstractions;

namespace LinguaCoach.UnitTests.Activity;

/// <summary>
/// Unit tests for ListeningAudioService.EnsureAudioAsync — Phase 8P.
/// Tests focus on guard behaviour and the AudioStatus field surfaced by MapToDto.
/// </summary>
public sealed class ListeningAudioServiceTests
{
    // TtsProviderResolver/DbContext are sealed/unavailable in a unit test — pass null when the
    // guard fires before either is reached.
    private static ListeningAudioService CreateSut() =>
        new(null!, null!, null!, null!, NullLogger<ListeningAudioService>.Instance);

    private static LearningActivity MakeActivity(
        ActivityType type,
        string contentJson,
        string? patternKey = null) =>
        new(
            activityType: type,
            source: ActivitySource.AiGenerated,
            title: "Test activity",
            difficulty: "B1",
            aiGeneratedContentJson: contentJson,
            exercisePatternKey: patternKey);

    // ── Guard: non-listening type ──────────────────────────────────────────────

    [Fact]
    public async Task EnsureAudioAsync_WritingScenario_IsNoop()
    {
        var sut = CreateSut();
        var activity = MakeActivity(ActivityType.WritingScenario, """{"situation":"test"}""");
        var originalJson = activity.AiGeneratedContentJson;

        await sut.EnsureAudioAsync(activity, "en", CancellationToken.None);

        activity.AiGeneratedContentJson.Should().Be(originalJson);
    }

    [Fact]
    public async Task EnsureAudioAsync_VocabularyPractice_IsNoop()
    {
        var sut = CreateSut();
        var activity = MakeActivity(ActivityType.VocabularyPractice, """{"items":[]}""");
        var originalJson = activity.AiGeneratedContentJson;

        await sut.EnsureAudioAsync(activity, "en", CancellationToken.None);

        activity.AiGeneratedContentJson.Should().Be(originalJson);
    }

    // ── ListeningComprehension with missing audioScript ────────────────────────

    [Fact]
    public async Task EnsureAudioAsync_ListeningComprehension_MissingScript_SetsUnavailable()
    {
        var sut = CreateSut();
        var contentJson = JsonSerializer.Serialize(new
        {
            scenario = "A colleague calls.",
            audioScript = (string?)null,
        });
        var activity = MakeActivity(ActivityType.ListeningComprehension, contentJson);

        await sut.EnsureAudioAsync(activity, "en", CancellationToken.None);

        var updated = JsonDocument.Parse(activity.AiGeneratedContentJson);
        updated.RootElement.TryGetProperty("audio", out var audioEl).Should().BeTrue();
        audioEl.GetProperty("audioAvailable").GetBoolean().Should().BeFalse();
        audioEl.GetProperty("unavailableMessage").GetString().Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task EnsureAudioAsync_ListeningComprehension_EmptyScript_SetsUnavailable()
    {
        var sut = CreateSut();
        var contentJson = JsonSerializer.Serialize(new { audioScript = "" });
        var activity = MakeActivity(ActivityType.ListeningComprehension, contentJson);

        await sut.EnsureAudioAsync(activity, "en", CancellationToken.None);

        var updated = JsonDocument.Parse(activity.AiGeneratedContentJson);
        updated.RootElement.GetProperty("audio").GetProperty("audioAvailable").GetBoolean()
            .Should().BeFalse();
    }

    // ── Already-ready audio is skipped ────────────────────────────────────────

    [Fact]
    public async Task EnsureAudioAsync_AlreadyAvailable_IsNoop()
    {
        var sut = CreateSut();
        var contentJson = JsonSerializer.Serialize(new
        {
            audioScript = "Hello world.",
            audio = new
            {
                audioAvailable = true,
                storageKey = "audio/abc.mp3",
                contentType = "audio/mpeg",
                durationMs = 3000,
            },
        });
        var activity = MakeActivity(ActivityType.ListeningComprehension, contentJson);
        var originalJson = activity.AiGeneratedContentJson;

        await sut.EnsureAudioAsync(activity, "en", CancellationToken.None);

        activity.AiGeneratedContentJson.Should().Be(originalJson);
    }

    // ── AudioStatus field exists on ActivityDto ───────────────────────────────

    [Fact]
    public void ActivityDto_HasAudioStatusField()
    {
        var prop = typeof(LinguaCoach.Application.Activity.ActivityDto)
            .GetProperty(nameof(LinguaCoach.Application.Activity.ActivityDto.AudioStatus));
        prop.Should().NotBeNull("AudioStatus must be exposed on ActivityDto (Phase 8P)");
        prop!.PropertyType.Should().Be(typeof(string));
    }

    [Theory]
    [InlineData("ready")]
    [InlineData("pending")]
    [InlineData("unavailable")]
    [InlineData(null)]
    public void ActivityDto_AudioStatus_AcceptsExpectedValues(string? status)
    {
        var dto = new LinguaCoach.Application.Activity.ActivityDto(
            ActivityId: Guid.NewGuid(),
            ActivityType: ActivityType.ListeningComprehension,
            Source: ActivitySource.AiGenerated,
            Title: "Test",
            Difficulty: "B1",
            Situation: null,
            LearningGoal: null,
            TargetPhrases: [],
            TargetVocabulary: [],
            ExampleText: null,
            CommonMistakeToAvoid: null,
            InstructionInSourceLanguage: null,
            AudioStatus: status);

        dto.AudioStatus.Should().Be(status);
    }

    // ── ListeningPatternKeys — all 10 patterns are ListeningComprehension ─────

    [Theory]
    [InlineData("listen_and_answer")]
    [InlineData("listen_and_gap_fill")]
    [InlineData("listening_multiple_choice_single")]
    [InlineData("listening_multiple_choice_multi")]
    [InlineData("listening_fill_in_blanks")]
    [InlineData("select_missing_word")]
    [InlineData("highlight_correct_summary")]
    [InlineData("highlight_incorrect_words")]
    [InlineData("write_from_dictation")]
    [InlineData("summarize_spoken_text")]
    public async Task EnsureAudioAsync_ListeningPattern_MissingScript_SetsUnavailable(string patternKey)
    {
        var sut = CreateSut();
        // All listening patterns use ActivityType.ListeningComprehension (confirmed in seeder).
        var contentJson = JsonSerializer.Serialize(new { audioScript = (string?)null });
        var activity = MakeActivity(ActivityType.ListeningComprehension, contentJson, patternKey);

        await sut.EnsureAudioAsync(activity, "en", CancellationToken.None);

        var updated = JsonDocument.Parse(activity.AiGeneratedContentJson);
        updated.RootElement.TryGetProperty("audio", out _).Should().BeTrue(
            $"pattern '{patternKey}' should cause audio metadata to be written");
    }
}

using FluentAssertions;
using LinguaCoach.Infrastructure.Lessons;

namespace LinguaCoach.UnitTests.Lessons;

/// <summary>Rich-text rebuild — HTML sanitization applied to Lesson Body/UsageNotes/Examples/
/// CommonMistakes on every create/update, before persistence.</summary>
public sealed class LessonHtmlSanitizerTests
{
    [Fact]
    public void Sanitize_keeps_toolbar_formatting_tags()
    {
        var html = "<p><strong>Bold</strong> and <em>italic</em>, plus <a href=\"https://example.com\">a link</a>.</p>";

        var result = LessonHtmlSanitizer.Sanitize(html);

        result.Should().Contain("<strong>Bold</strong>");
        result.Should().Contain("<em>italic</em>");
        result.Should().Contain("href=\"https://example.com\"");
    }

    [Fact]
    public void Sanitize_keeps_lesson_media_embeds()
    {
        var html = "<p>See below.</p><img src=\"/api/lesson-media/lesson-media/abc/1.png\" alt=\"diagram\">"
            + "<audio controls src=\"/api/lesson-media/lesson-media/abc/2.weba\"></audio>"
            + "<video controls src=\"/api/lesson-media/lesson-media/abc/3.mp4\"></video>";

        var result = LessonHtmlSanitizer.Sanitize(html);

        result.Should().Contain("<img");
        result.Should().Contain("<audio");
        result.Should().Contain("<video");
        result.Should().Contain("/api/lesson-media/lesson-media/abc/1.png");
    }

    [Fact]
    public void Sanitize_strips_script_tags_and_event_handlers()
    {
        var html = "<p onclick=\"alert(1)\">Click me</p><script>alert('xss')</script>";

        var result = LessonHtmlSanitizer.Sanitize(html);

        result.Should().NotContain("<script");
        result.Should().NotContain("onclick");
        result.Should().NotContain("alert");
    }

    [Fact]
    public void Sanitize_passes_through_plain_text_with_no_tags_unchanged_in_content()
    {
        var result = LessonHtmlSanitizer.Sanitize("Used for past actions with present relevance.");

        result.Should().Contain("Used for past actions with present relevance.");
    }

    [Fact]
    public void SanitizeAll_sanitizes_each_array_item_independently()
    {
        var items = new[] { "<p>Example one</p>", "<script>bad()</script><p>Example two</p>" };

        var result = LessonHtmlSanitizer.SanitizeAll(items);

        result.Should().HaveCount(2);
        result[0].Should().Contain("Example one");
        result[1].Should().Contain("Example two");
        result[1].Should().NotContain("<script");
    }

    [Fact]
    public void SanitizeAll_returns_empty_list_for_null_input()
    {
        LessonHtmlSanitizer.SanitizeAll(null).Should().BeEmpty();
    }
}

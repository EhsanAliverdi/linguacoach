using FluentAssertions;
using LinguaCoach.Infrastructure.Lessons;

namespace LinguaCoach.UnitTests.Lessons;

/// <summary>Rich-text rebuild — strips Lesson rich-text HTML down to plain text before it's fed
/// into an AI prompt variable (e.g. AiModuleGenerationService's "lessonBody").</summary>
public sealed class HtmlTextExtractorTests
{
    [Fact]
    public void ToPlainText_strips_tags_and_collapses_whitespace()
    {
        var result = HtmlTextExtractor.ToPlainText("<p><strong>Used</strong>  for   past actions.</p>");

        result.Should().Be("Used for past actions.");
    }

    [Fact]
    public void ToPlainText_decodes_html_entities()
    {
        var result = HtmlTextExtractor.ToPlainText("<p>Rock &amp; roll &mdash; simple.</p>");

        result.Should().Be("Rock & roll — simple.");
    }

    [Fact]
    public void ToPlainText_drops_media_embed_tags_entirely()
    {
        var result = HtmlTextExtractor.ToPlainText("<p>See:</p><img src=\"/api/lesson-media/x.png\"><audio src=\"/api/lesson-media/y.weba\"></audio>");

        result.Should().Be("See:");
    }

    [Fact]
    public void ToPlainText_returns_empty_string_for_null_or_whitespace()
    {
        HtmlTextExtractor.ToPlainText(null).Should().BeEmpty();
        HtmlTextExtractor.ToPlainText("   ").Should().BeEmpty();
    }

    [Fact]
    public void ToPlainText_passes_through_plain_text_with_no_tags_unchanged()
    {
        HtmlTextExtractor.ToPlainText("Used for past actions.").Should().Be("Used for past actions.");
    }
}

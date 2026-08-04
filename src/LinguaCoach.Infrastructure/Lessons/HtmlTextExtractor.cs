using System.Net;
using System.Text.RegularExpressions;

namespace LinguaCoach.Infrastructure.Lessons;

/// <summary>
/// Strips rich-text HTML markup (Lesson Body/UsageNotes/Examples/CommonMistakes, now authored via
/// the admin rich-text editor) down to plain text before it's fed into an AI prompt variable —
/// keeps prompts free of markup noise/token bloat regardless of how a Lesson's content is
/// formatted. Not a security control (that's <see cref="LessonHtmlSanitizer"/>'s job on save) —
/// purely a plain-text projection for AI-facing consumption.
/// </summary>
public static class HtmlTextExtractor
{
    private static readonly Regex TagPattern = new("<[^>]*>", RegexOptions.Compiled);
    private static readonly Regex WhitespacePattern = new(@"\s+", RegexOptions.Compiled);

    public static string ToPlainText(string? html)
    {
        if (string.IsNullOrWhiteSpace(html)) return string.Empty;

        var withoutTags = TagPattern.Replace(html, " ");
        var decoded = WebUtility.HtmlDecode(withoutTags);
        return WhitespacePattern.Replace(decoded, " ").Trim();
    }
}

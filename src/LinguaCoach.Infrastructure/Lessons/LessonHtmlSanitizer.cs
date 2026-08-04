using Ganss.Xss;

namespace LinguaCoach.Infrastructure.Lessons;

/// <summary>
/// Sanitizes rich-text HTML authored through the admin Lesson editor's TipTap toolbar (Body,
/// UsageNotes, and each Examples/CommonMistakes array item) before it's persisted — defense in
/// depth even though only admins author this content. Allows exactly the tag/attribute set the
/// editor's toolbar can produce: text formatting, lists, links, and the image/audio/video embeds
/// served through <c>ILessonMediaService</c>'s <c>/api/lesson-media/{key}</c> endpoint.
/// </summary>
public static class LessonHtmlSanitizer
{
    private static readonly HtmlSanitizer Sanitizer = BuildSanitizer();

    public static string Sanitize(string html) => Sanitizer.Sanitize(html);

    public static IReadOnlyList<string> SanitizeAll(IReadOnlyList<string>? items) =>
        items is null ? [] : items.Select(Sanitize).ToList();

    private static HtmlSanitizer BuildSanitizer()
    {
        var sanitizer = new HtmlSanitizer();
        sanitizer.AllowedTags.Clear();
        foreach (var tag in new[]
        {
            "p", "br", "strong", "b", "em", "i", "u", "s",
            "ul", "ol", "li", "a",
            "img", "audio", "video", "source",
        })
        {
            sanitizer.AllowedTags.Add(tag);
        }

        sanitizer.AllowedAttributes.Clear();
        foreach (var attr in new[] { "href", "target", "rel", "src", "controls", "alt", "type" })
            sanitizer.AllowedAttributes.Add(attr);

        sanitizer.AllowedSchemes.Clear();
        sanitizer.AllowedSchemes.Add("http");
        sanitizer.AllowedSchemes.Add("https");

        // Lesson media embeds always point at this app's own relative serving path
        // (`/api/lesson-media/{key}`, never a raw storage-provider URL) — allow relative URLs so
        // that path validates instead of being stripped as scheme-less.
        sanitizer.AllowedCssProperties.Clear();
        sanitizer.KeepChildNodes = false;

        return sanitizer;
    }
}

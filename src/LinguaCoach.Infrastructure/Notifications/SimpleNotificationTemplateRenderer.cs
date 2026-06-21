using System.Text.RegularExpressions;
using LinguaCoach.Application.Notifications;

namespace LinguaCoach.Infrastructure.Notifications;

public sealed class SimpleNotificationTemplateRenderer : INotificationTemplateRenderer
{
    private static readonly Regex PlaceholderPattern =
        new(@"\{\{(\w+)\}\}", RegexOptions.Compiled, TimeSpan.FromSeconds(1));

    public TemplateRenderResult Render(
        string? subject,
        string? title,
        string body,
        IReadOnlyDictionary<string, string> variables)
    {
        var missing = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var renderedBody = Replace(body, variables, missing);
        var renderedSubject = subject is not null ? Replace(subject, variables, missing) : null;
        var renderedTitle = title is not null ? Replace(title, variables, missing) : null;

        return new TemplateRenderResult(
            Succeeded: true,
            RenderedSubject: renderedSubject,
            RenderedTitle: renderedTitle,
            RenderedBody: renderedBody,
            MissingVariables: missing.ToList());
    }

    private static string Replace(
        string template,
        IReadOnlyDictionary<string, string> variables,
        HashSet<string> missing)
    {
        return PlaceholderPattern.Replace(template, match =>
        {
            var key = match.Groups[1].Value;
            if (variables.TryGetValue(key, out var value))
                return value;
            missing.Add(key);
            return match.Value; // leave placeholder visible
        });
    }
}

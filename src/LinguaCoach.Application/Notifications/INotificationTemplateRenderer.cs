namespace LinguaCoach.Application.Notifications;

public sealed record TemplateRenderResult(
    bool Succeeded,
    string? RenderedSubject,
    string? RenderedTitle,
    string RenderedBody,
    IReadOnlyList<string> MissingVariables);

public interface INotificationTemplateRenderer
{
    /// <summary>
    /// Replaces {{VarName}} placeholders with values from <paramref name="variables"/>.
    /// Missing variables are left as-is (visible placeholder) and reported in MissingVariables.
    /// </summary>
    TemplateRenderResult Render(
        string? subject,
        string? title,
        string body,
        IReadOnlyDictionary<string, string> variables);
}

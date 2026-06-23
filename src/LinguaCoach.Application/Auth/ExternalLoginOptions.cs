namespace LinguaCoach.Application.Auth;

/// <summary>
/// Configuration for a single external OAuth provider (e.g. Google).
/// Bound from Authentication:ExternalProviders:Google section.
/// ClientSecret is never exposed through APIs or logs.
/// </summary>
public sealed class GoogleExternalLoginOptions
{
    public const string SectionName = "Authentication:ExternalProviders:Google";

    /// <summary>Enable Google external login. Default false — safe by default.</summary>
    public bool Enabled { get; set; } = false;

    public string ClientId { get; set; } = string.Empty;

    /// <summary>Never exposed through APIs or logged.</summary>
    public string ClientSecret { get; set; } = string.Empty;

    /// <summary>
    /// If non-empty, only Google accounts with a hosted domain (hd) in this list are accepted.
    /// Leave empty to allow any Google account (subject to other rules).
    /// </summary>
    public List<string> AllowedDomains { get; set; } = [];

    /// <summary>
    /// When true, a verified Google email that matches an existing local account
    /// will be linked automatically. Default true.
    /// </summary>
    public bool AllowAutoLinkByEmail { get; set; } = true;

    /// <summary>
    /// When true, unknown Google accounts automatically get a Student account created.
    /// Default false — no public self-registration.
    /// </summary>
    public bool AllowStudentAutoProvisioning { get; set; } = false;
}

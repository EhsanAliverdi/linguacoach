namespace LinguaCoach.Application.PracticeGym;

/// <summary>
/// Resolves whether the Practice Gym Form.io template pilot is enabled — a single admin-toggled
/// boolean backed by a `RuntimeSettingOverride` row (key "PracticeGymFormIoPilot.Enabled"),
/// defaulting to false when no override exists. See
/// docs/reviews/2026-07-07-ai-bank-assessment-architecture-plan.md.
/// </summary>
public interface IPracticeGymFormIoTemplatePilotSettingsProvider
{
    Task<bool> IsEnabledAsync(CancellationToken ct = default);
}

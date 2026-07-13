using System.Text.Json;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;

namespace LinguaCoach.Infrastructure.ResourceImport;

/// <summary>
/// Shared logic for the client-facing "can this candidate even be attempted for publish" summary
/// (Passed/NeedsReview = attemptable, Failed/Pending = hard-blocked) — used by both
/// <see cref="ResourceImportMappers.ToDto(ResourceCandidate, Guid, Guid)"/> (so the admin UI can
/// hide Approve &amp; Publish for hard-blocked rows without duplicating gate rules) and
/// <see cref="ResourceCandidatePublishService"/> (whose live gate re-check is the actual source of
/// truth — this helper only mirrors that one specific gate for display purposes).
/// </summary>
internal static class ResourceCandidatePublishGateHelper
{
    public static bool CanAttemptPublish(ResourceCandidate candidate) =>
        candidate.ValidationStatus is ResourceCandidateValidationStatus.Passed or ResourceCandidateValidationStatus.NeedsReview;

    /// <summary>Non-null only when <see cref="CanAttemptPublish"/> is false and the candidate isn't
    /// already published.</summary>
    public static string? DescribeHardBlock(ResourceCandidate candidate)
    {
        if (candidate.IsPublished || CanAttemptPublish(candidate))
            return null;

        if (candidate.ValidationStatus == ResourceCandidateValidationStatus.Pending)
            return "This candidate has not been validated yet — run Analyze/Validate first.";

        if (string.IsNullOrWhiteSpace(candidate.RejectReason))
            return "Re-run validation to see the specific blocking error(s).";

        try
        {
            using var doc = JsonDocument.Parse(candidate.RejectReason);
            if (doc.RootElement.TryGetProperty("errors", out var errorsEl) && errorsEl.ValueKind == JsonValueKind.Array)
            {
                var errors = errorsEl.EnumerateArray()
                    .Where(e => e.ValueKind == JsonValueKind.String)
                    .Select(e => e.GetString()!)
                    .ToList();
                if (errors.Count > 0)
                    return $"Blocking error(s): {string.Join("; ", errors)}";
            }
        }
        catch (JsonException)
        {
            // Fall through to the generic message below.
        }

        return "Re-run validation to see the specific blocking error(s).";
    }
}

namespace LinguaCoach.Application.Admin;

public sealed record AdminStudentPracticeQuery(Guid StudentId);

public sealed record AdminStudentPracticeSuggestionItem(
    string Title,
    string? PrimarySkill,
    string CallToAction,
    string Explanation,
    string RoutingReason,
    string TargetCefrLevel,
    int? EstimatedDurationMinutes);

public sealed record AdminStudentPracticeResult(
    /// <summary>Ready | ReviewOnly | Preparing | NotAvailable</summary>
    string Status,
    int ReviewQueueCount,
    int ReservedCount,
    string? WeakestSkill,
    AdminStudentPracticeSuggestionItem? TopSuggestion,
    bool IsReplenishmentRecommended);

public interface IAdminStudentPracticeQuery
{
    Task<AdminStudentPracticeResult> HandleAsync(
        AdminStudentPracticeQuery query, CancellationToken ct = default);
}

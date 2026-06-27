using LinguaCoach.Application.Admin;
using LinguaCoach.Application.PracticeGym;

namespace LinguaCoach.Infrastructure.Admin;

public sealed class AdminStudentPracticeQueryHandler : IAdminStudentPracticeQuery
{
    private readonly IPracticeGymSuggestionService _suggestions;

    public AdminStudentPracticeQueryHandler(IPracticeGymSuggestionService suggestions)
    {
        _suggestions = suggestions;
    }

    public async Task<AdminStudentPracticeResult> HandleAsync(
        AdminStudentPracticeQuery query, CancellationToken ct = default)
    {
        try
        {
            var dto = await _suggestions.GetSuggestionsForStudentAsync(query.StudentId, ct);

            var active = dto.SuggestedItems.Concat(dto.ContinueItems).ToList();
            var status = active.Count > 0 ? "Ready"
                       : dto.ReviewItems.Count > 0 ? "ReviewOnly"
                       : "Preparing";

            AdminStudentPracticeSuggestionItem? top = null;
            if (active.Count > 0)
            {
                var first = active[0];
                top = new AdminStudentPracticeSuggestionItem(
                    first.Title, first.PrimarySkill, first.CallToAction,
                    first.Explanation, first.RoutingReason, first.TargetCefrLevel,
                    first.EstimatedDurationMinutes);
            }

            var weakest = dto.ReviewItems.FirstOrDefault()?.PrimarySkill
                       ?? dto.SuggestedItems.FirstOrDefault(i => i.IsLowerLevelContent)?.PrimarySkill;

            return new AdminStudentPracticeResult(
                Status: status,
                ReviewQueueCount: dto.ReviewItems.Count,
                ReservedCount: dto.ReservedCount,
                WeakestSkill: weakest,
                TopSuggestion: top,
                IsReplenishmentRecommended: dto.IsReplenishmentRecommended);
        }
        catch
        {
            return new AdminStudentPracticeResult(
                Status: "NotAvailable",
                ReviewQueueCount: 0,
                ReservedCount: 0,
                WeakestSkill: null,
                TopSuggestion: null,
                IsReplenishmentRecommended: false);
        }
    }
}

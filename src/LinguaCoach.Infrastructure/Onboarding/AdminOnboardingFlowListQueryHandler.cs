using LinguaCoach.Application.Onboarding;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LinguaCoach.Infrastructure.Onboarding;

public sealed class AdminOnboardingFlowListQueryHandler : IAdminOnboardingFlowListQuery
{
    private readonly LinguaCoachDbContext _db;

    public AdminOnboardingFlowListQueryHandler(LinguaCoachDbContext db) => _db = db;

    public async Task<IReadOnlyList<AdminOnboardingFlowSummaryDto>> HandleAsync(
        ListAdminOnboardingFlowsQuery query, CancellationToken ct = default)
    {
        var flows = await _db.OnboardingFlowDefinitions
            .Include(f => f.Steps)
            .OrderByDescending(f => f.Version)
            .ToListAsync(ct);

        return flows.Select(f => new AdminOnboardingFlowSummaryDto(
            FlowId: f.Id,
            Name: f.Name,
            Version: f.Version,
            IsActive: f.IsActive,
            TotalSteps: f.Steps.Count,
            RequiredSteps: f.Steps.Count(s => s.IsEnabled),
            CreatedAt: f.CreatedAt
        )).ToList();
    }
}

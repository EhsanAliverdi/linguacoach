using LinguaCoach.Application.Onboarding;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LinguaCoach.Infrastructure.Onboarding;

public sealed class AdminActivateOnboardingFlowHandler : IAdminActivateOnboardingFlowHandler
{
    private readonly LinguaCoachDbContext _db;

    public AdminActivateOnboardingFlowHandler(LinguaCoachDbContext db) => _db = db;

    public async Task HandleAsync(ActivateOnboardingFlowCommand command, CancellationToken ct = default)
    {
        var flow = await _db.OnboardingFlowDefinitions
            .FirstOrDefaultAsync(f => f.Id == command.FlowId, ct)
            ?? throw new OnboardingV2ValidationException($"Flow {command.FlowId} not found.");

        var enabledStepCount = await _db.Set<LinguaCoach.Domain.Entities.OnboardingStepDefinition>()
            .CountAsync(s => s.FlowDefinitionId == command.FlowId && s.IsEnabled, ct);

        if (enabledStepCount == 0)
            throw new OnboardingV2ValidationException("Cannot activate a flow with no enabled steps.");

        // Deactivate any currently active flows.
        await _db.OnboardingFlowDefinitions
            .Where(f => f.IsActive && f.Id != command.FlowId)
            .ExecuteUpdateAsync(s => s.SetProperty(f => f.IsActive, false), ct);

        flow.Activate();
        await _db.SaveChangesAsync(ct);
    }
}

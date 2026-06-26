using LinguaCoach.Application.Onboarding;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LinguaCoach.Infrastructure.Onboarding;

public sealed class AdminReorderOnboardingStepsHandler : IAdminReorderOnboardingStepsHandler
{
    private readonly LinguaCoachDbContext _db;

    public AdminReorderOnboardingStepsHandler(LinguaCoachDbContext db) => _db = db;

    public async Task HandleAsync(ReorderOnboardingStepsCommand command, CancellationToken ct = default)
    {
        var steps = await _db.Set<OnboardingStepDefinition>()
            .Where(s => s.FlowDefinitionId == command.FlowId)
            .ToListAsync(ct);

        for (var i = 0; i < command.StepKeyOrder.Count; i++)
        {
            var key = command.StepKeyOrder[i];
            var step = steps.FirstOrDefault(s => s.StepKey == key)
                ?? throw new OnboardingV2ValidationException($"Step '{key}' not found in flow {command.FlowId}.");
            step.SetOrder(i + 1);
        }

        await _db.SaveChangesAsync(ct);
    }
}

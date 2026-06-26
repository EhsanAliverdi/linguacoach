using LinguaCoach.Application.Onboarding;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LinguaCoach.Infrastructure.Onboarding;

public sealed class AdminRemoveOnboardingStepHandler : IAdminRemoveOnboardingStepHandler
{
    private readonly LinguaCoachDbContext _db;

    public AdminRemoveOnboardingStepHandler(LinguaCoachDbContext db) => _db = db;

    public async Task HandleAsync(RemoveOnboardingStepCommand command, CancellationToken ct = default)
    {
        var step = await _db.Set<OnboardingStepDefinition>()
            .FirstOrDefaultAsync(s => s.FlowDefinitionId == command.FlowId && s.StepKey == command.StepKey, ct)
            ?? throw new InvalidOperationException($"Step '{command.StepKey}' not found in flow {command.FlowId}.");

        _db.Set<OnboardingStepDefinition>().Remove(step);
        await _db.SaveChangesAsync(ct);
    }
}

using LinguaCoach.Application.Onboarding;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Persistence;

namespace LinguaCoach.Infrastructure.Onboarding;

public sealed class AdminCreateOnboardingFlowHandler : IAdminCreateOnboardingFlowHandler
{
    private readonly LinguaCoachDbContext _db;

    public AdminCreateOnboardingFlowHandler(LinguaCoachDbContext db) => _db = db;

    public async Task<AdminOnboardingFlowDto> HandleAsync(
        CreateOnboardingFlowCommand command, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(command.Name))
            throw new OnboardingV2ValidationException("Flow name is required.");
        if (command.Version <= 0)
            throw new OnboardingV2ValidationException("Version must be a positive integer.");

        var flow = new OnboardingFlowDefinition(command.Name.Trim(), command.Version);
        _db.OnboardingFlowDefinitions.Add(flow);
        await _db.SaveChangesAsync(ct);

        return new AdminOnboardingFlowDto(flow.Id, flow.Name, flow.Version, flow.IsActive, []);
    }
}

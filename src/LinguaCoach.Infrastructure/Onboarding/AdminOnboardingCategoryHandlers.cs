using LinguaCoach.Application.Onboarding;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LinguaCoach.Infrastructure.Onboarding;

public sealed class AdminAddOnboardingCategoryHandler : IAdminAddOnboardingCategoryHandler
{
    private readonly LinguaCoachDbContext _db;
    public AdminAddOnboardingCategoryHandler(LinguaCoachDbContext db) => _db = db;

    public async Task<AdminOnboardingCategoryDto> HandleAsync(AddOnboardingCategoryCommand command, CancellationToken ct = default)
    {
        var flowExists = await _db.OnboardingFlowDefinitions.AnyAsync(f => f.Id == command.FlowId, ct);
        if (!flowExists) throw new OnboardingV2ValidationException($"Flow {command.FlowId} not found.");

        OnboardingCategoryDefinition category;
        try
        {
            category = new OnboardingCategoryDefinition(command.FlowId, command.Name, command.CategoryOrder, command.IsEnabled, command.Description);
        }
        catch (ArgumentException ex)
        {
            throw new OnboardingV2ValidationException(ex.Message);
        }

        _db.OnboardingCategoryDefinitions.Add(category);
        await _db.SaveChangesAsync(ct);

        return new AdminOnboardingCategoryDto(category.Id, category.Name, category.Description, category.CategoryOrder, category.IsEnabled);
    }
}

public sealed class AdminUpdateOnboardingCategoryHandler : IAdminUpdateOnboardingCategoryHandler
{
    private readonly LinguaCoachDbContext _db;
    public AdminUpdateOnboardingCategoryHandler(LinguaCoachDbContext db) => _db = db;

    public async Task<AdminOnboardingCategoryDto> HandleAsync(UpdateOnboardingCategoryCommand command, CancellationToken ct = default)
    {
        var category = await _db.OnboardingCategoryDefinitions
            .FirstOrDefaultAsync(c => c.Id == command.CategoryId && c.FlowDefinitionId == command.FlowId, ct)
            ?? throw new OnboardingV2ValidationException($"Category {command.CategoryId} not found in flow {command.FlowId}.");

        try
        {
            category.Update(command.Name, command.Description, command.CategoryOrder, command.IsEnabled);
        }
        catch (ArgumentException ex)
        {
            throw new OnboardingV2ValidationException(ex.Message);
        }

        await _db.SaveChangesAsync(ct);

        return new AdminOnboardingCategoryDto(category.Id, category.Name, category.Description, category.CategoryOrder, category.IsEnabled);
    }
}

public sealed class AdminRemoveOnboardingCategoryHandler : IAdminRemoveOnboardingCategoryHandler
{
    private readonly LinguaCoachDbContext _db;
    public AdminRemoveOnboardingCategoryHandler(LinguaCoachDbContext db) => _db = db;

    public async Task HandleAsync(RemoveOnboardingCategoryCommand command, CancellationToken ct = default)
    {
        var category = await _db.OnboardingCategoryDefinitions
            .FirstOrDefaultAsync(c => c.Id == command.CategoryId && c.FlowDefinitionId == command.FlowId, ct)
            ?? throw new OnboardingV2ValidationException($"Category {command.CategoryId} not found in flow {command.FlowId}.");

        var hasSteps = await _db.OnboardingStepDefinitions.AnyAsync(s => s.CategoryId == command.CategoryId, ct);
        if (hasSteps)
            throw new OnboardingV2ValidationException("Cannot remove a category that still has steps assigned to it.");

        _db.OnboardingCategoryDefinitions.Remove(category);
        await _db.SaveChangesAsync(ct);
    }
}

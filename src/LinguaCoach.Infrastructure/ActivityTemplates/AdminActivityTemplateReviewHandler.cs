using LinguaCoach.Application.ActivityTemplates;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LinguaCoach.Infrastructure.ActivityTemplates;

public sealed class AdminActivityTemplateReviewHandler : IAdminActivityTemplateReviewHandler
{
    private readonly LinguaCoachDbContext _db;

    public AdminActivityTemplateReviewHandler(LinguaCoachDbContext db)
    {
        _db = db;
    }

    public async Task<AdminActivityTemplateDto> HandleAsync(SetActivityTemplateReviewStatusCommand command, CancellationToken ct = default)
    {
        var template = await _db.ActivityTemplates.FirstOrDefaultAsync(t => t.Id == command.TemplateId, ct)
            ?? throw new ActivityTemplateValidationException($"Activity template {command.TemplateId} not found.");

        try
        {
            switch (command.Action.Trim().ToLowerInvariant())
            {
                case "approve":
                    template.Approve(command.Reason);
                    break;
                case "reject":
                    if (string.IsNullOrWhiteSpace(command.Reason))
                        throw new ActivityTemplateValidationException("Reason is required to reject a template.");
                    template.Reject(command.Reason);
                    break;
                case "reset":
                    template.ResetToPendingReview();
                    break;
                default:
                    throw new ActivityTemplateValidationException($"Unknown review action '{command.Action}'. Expected approve, reject, or reset.");
            }
        }
        catch (ArgumentException ex)
        {
            throw new ActivityTemplateValidationException(ex.Message);
        }

        await _db.SaveChangesAsync(ct);

        return ActivityTemplateMapper.ToDto(template);
    }
}

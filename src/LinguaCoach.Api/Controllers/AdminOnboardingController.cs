using LinguaCoach.Application.Onboarding;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LinguaCoach.Api.Controllers;

[ApiController]
[Route("api/admin/onboarding")]
[Authorize(Roles = "Admin")]
public sealed class AdminOnboardingController : ControllerBase
{
    private readonly IAdminOnboardingFlowQuery _flowQuery;
    private readonly IAdminOnboardingFlowListQuery _flowListQuery;
    private readonly IAdminCreateOnboardingFlowHandler _createFlow;
    private readonly IAdminActivateOnboardingFlowHandler _activateFlow;
    private readonly IAdminAddOnboardingStepHandler _addStep;
    private readonly IAdminUpdateOnboardingStepHandler _updateStep;
    private readonly IAdminRemoveOnboardingStepHandler _removeStep;
    private readonly IAdminReorderOnboardingStepsHandler _reorderSteps;

    public AdminOnboardingController(
        IAdminOnboardingFlowQuery flowQuery,
        IAdminOnboardingFlowListQuery flowListQuery,
        IAdminCreateOnboardingFlowHandler createFlow,
        IAdminActivateOnboardingFlowHandler activateFlow,
        IAdminAddOnboardingStepHandler addStep,
        IAdminUpdateOnboardingStepHandler updateStep,
        IAdminRemoveOnboardingStepHandler removeStep,
        IAdminReorderOnboardingStepsHandler reorderSteps)
    {
        _flowQuery = flowQuery;
        _flowListQuery = flowListQuery;
        _createFlow = createFlow;
        _activateFlow = activateFlow;
        _addStep = addStep;
        _updateStep = updateStep;
        _removeStep = removeStep;
        _reorderSteps = reorderSteps;
    }

    // GET api/admin/onboarding/flows
    [HttpGet("flows")]
    public async Task<IActionResult> ListFlows(CancellationToken ct)
    {
        var result = await _flowListQuery.HandleAsync(new ListAdminOnboardingFlowsQuery(), ct);
        return Ok(result);
    }

    // GET api/admin/onboarding/flow  (active flow detail)
    [HttpGet("flow")]
    public async Task<IActionResult> GetFlow(CancellationToken ct)
    {
        var result = await _flowQuery.HandleAsync(new GetAdminOnboardingFlowQuery(), ct);
        if (result is null) return NotFound(new { error = "No active onboarding flow found." });
        return Ok(result);
    }

    // GET api/admin/onboarding/flows/{flowId}
    [HttpGet("flows/{flowId:guid}")]
    public async Task<IActionResult> GetFlowById(Guid flowId, CancellationToken ct)
    {
        var all = await _flowListQuery.HandleAsync(new ListAdminOnboardingFlowsQuery(), ct);
        var summary = all.FirstOrDefault(f => f.FlowId == flowId);
        if (summary is null) return NotFound(new { error = $"Flow {flowId} not found." });
        return Ok(summary);
    }

    // POST api/admin/onboarding/flows
    [HttpPost("flows")]
    public async Task<IActionResult> CreateFlow([FromBody] CreateFlowRequest request, CancellationToken ct)
    {
        try
        {
            var result = await _createFlow.HandleAsync(
                new CreateOnboardingFlowCommand(request.Name, request.Version), ct);
            return CreatedAtAction(nameof(GetFlowById), new { flowId = result.FlowId }, result);
        }
        catch (OnboardingV2ValidationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    // POST api/admin/onboarding/flows/{flowId}/activate
    [HttpPost("flows/{flowId:guid}/activate")]
    public async Task<IActionResult> ActivateFlow(Guid flowId, CancellationToken ct)
    {
        try
        {
            await _activateFlow.HandleAsync(new ActivateOnboardingFlowCommand(flowId), ct);
            return NoContent();
        }
        catch (OnboardingV2ValidationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    // POST api/admin/onboarding/flows/{flowId}/steps
    [HttpPost("flows/{flowId:guid}/steps")]
    public async Task<IActionResult> AddStep(Guid flowId, [FromBody] StepRequest request, CancellationToken ct)
    {
        try
        {
            var result = await _addStep.HandleAsync(new AddOnboardingStepCommand(
                flowId, request.StepKey, request.Title, request.Description,
                request.StepType, request.RequirementType, request.AnswerMapping,
                request.StepOrder, request.IsEnabled, request.Options), ct);
            return Ok(result);
        }
        catch (OnboardingV2ValidationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { error = ex.Message });
        }
    }

    // PUT api/admin/onboarding/flows/{flowId}/steps/{stepKey}
    [HttpPut("flows/{flowId:guid}/steps/{stepKey}")]
    public async Task<IActionResult> UpdateStep(Guid flowId, string stepKey, [FromBody] StepRequest request, CancellationToken ct)
    {
        try
        {
            var result = await _updateStep.HandleAsync(new UpdateOnboardingStepCommand(
                flowId, stepKey, request.Title, request.Description,
                request.StepType, request.RequirementType, request.AnswerMapping,
                request.StepOrder, request.IsEnabled, request.Options), ct);
            return Ok(result);
        }
        catch (OnboardingV2ValidationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    // DELETE api/admin/onboarding/flows/{flowId}/steps/{stepKey}
    [HttpDelete("flows/{flowId:guid}/steps/{stepKey}")]
    public async Task<IActionResult> RemoveStep(Guid flowId, string stepKey, CancellationToken ct)
    {
        try
        {
            await _removeStep.HandleAsync(new RemoveOnboardingStepCommand(flowId, stepKey), ct);
            return NoContent();
        }
        catch (OnboardingV2ValidationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    // PUT api/admin/onboarding/flows/{flowId}/steps/reorder
    [HttpPut("flows/{flowId:guid}/steps/reorder")]
    public async Task<IActionResult> ReorderSteps(Guid flowId, [FromBody] ReorderStepsRequest request, CancellationToken ct)
    {
        try
        {
            await _reorderSteps.HandleAsync(new ReorderOnboardingStepsCommand(flowId, request.StepKeyOrder), ct);
            return NoContent();
        }
        catch (OnboardingV2ValidationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    // ── Request models ────────────────────────────────────────────────────────

    public sealed record CreateFlowRequest(string Name, int Version);

    public sealed record StepRequest(
        string StepKey,
        string Title,
        string? Description,
        string StepType,
        string RequirementType,
        string AnswerMapping,
        int StepOrder,
        bool IsEnabled,
        IReadOnlyList<OnboardingOptionDto>? Options);

    public sealed record ReorderStepsRequest(IReadOnlyList<string> StepKeyOrder);
}

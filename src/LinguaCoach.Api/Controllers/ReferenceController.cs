using LinguaCoach.Application.Reference;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LinguaCoach.Api.Controllers;

[ApiController]
[Route("api/reference")]
[Authorize]
public sealed class ReferenceController : ControllerBase
{
    private readonly IReferenceQueryService _referenceService;

    public ReferenceController(IReferenceQueryService referenceService)
    {
        _referenceService = referenceService;
    }

    [HttpGet("language-pairs")]
    public async Task<IActionResult> GetLanguagePairs(CancellationToken ct)
    {
        var pairs = await _referenceService.GetActiveLanguagePairsAsync(ct);
        return Ok(pairs);
    }

    [HttpGet("tracks")]
    public async Task<IActionResult> GetTracks([FromQuery] Guid languagePairId, CancellationToken ct)
    {
        var tracks = await _referenceService.GetTracksByLanguagePairAsync(languagePairId, ct);
        return Ok(tracks);
    }

    [HttpGet("career-profiles")]
    public async Task<IActionResult> GetCareerProfiles([FromQuery] Guid languagePairId, CancellationToken ct)
    {
        var profiles = await _referenceService.GetCareerProfilesByLanguagePairAsync(languagePairId, ct);
        return Ok(profiles);
    }
}

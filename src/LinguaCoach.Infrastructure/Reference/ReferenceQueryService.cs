using LinguaCoach.Application.Reference;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LinguaCoach.Infrastructure.Reference;

public sealed class ReferenceQueryService : IReferenceQueryService
{
    private readonly LinguaCoachDbContext _db;

    public ReferenceQueryService(LinguaCoachDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<LanguagePairDto>> GetActiveLanguagePairsAsync(CancellationToken ct = default)
    {
        return await _db.LanguagePairs
            .Include(lp => lp.SourceLanguage)
            .Include(lp => lp.TargetLanguage)
            .Where(lp => lp.IsActive)
            .Select(lp => new LanguagePairDto(
                lp.Id,
                lp.SourceLanguage.Code,
                lp.SourceLanguage.Name,
                lp.TargetLanguage.Code,
                lp.TargetLanguage.Name))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<LearningTrackDto>> GetTracksByLanguagePairAsync(Guid languagePairId, CancellationToken ct = default)
    {
        return await _db.LearningTracks
            .Where(t => t.LanguagePairId == languagePairId)
            .Select(t => new LearningTrackDto(t.Id, t.Name, t.Description))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<CareerProfileDto>> GetCareerProfilesByLanguagePairAsync(Guid languagePairId, CancellationToken ct = default)
    {
        return await _db.CareerProfiles
            .Where(c => c.LanguagePairId == languagePairId)
            .Select(c => new CareerProfileDto(c.Id, c.Name, c.Description))
            .ToListAsync(ct);
    }
}

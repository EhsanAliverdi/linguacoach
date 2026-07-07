namespace LinguaCoach.Application.Reference;

public sealed record LanguagePairDto(Guid Id, string SourceCode, string SourceName, string TargetCode, string TargetName);
public sealed record CareerProfileDto(Guid Id, string Name, string Description);

public interface IReferenceQueryService
{
    Task<IReadOnlyList<LanguagePairDto>> GetActiveLanguagePairsAsync(CancellationToken ct = default);
    Task<IReadOnlyList<CareerProfileDto>> GetCareerProfilesByLanguagePairAsync(Guid languagePairId, CancellationToken ct = default);
}

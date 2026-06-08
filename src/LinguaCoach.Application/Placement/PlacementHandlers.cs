namespace LinguaCoach.Application.Placement;

// ── Start / resume ───────────────────────────────────────────────────────────

public sealed record StartPlacementCommand(Guid UserId);

public interface IStartPlacementHandler
{
    Task<PlacementStatusDto> HandleAsync(StartPlacementCommand command, CancellationToken ct = default);
}

// ── Save answers for a section ───────────────────────────────────────────────

public sealed record SavePlacementAnswersCommand(
    Guid UserId,
    string SectionKey,
    IReadOnlyList<PlacementAnswerDto> Answers);

public interface ISavePlacementAnswersHandler
{
    Task<PlacementStatusDto> HandleAsync(SavePlacementAnswersCommand command, CancellationToken ct = default);
}

// ── Complete (evaluate) ──────────────────────────────────────────────────────

public sealed record CompletePlacementCommand(Guid UserId);

public interface ICompletePlacementHandler
{
    Task<PlacementResultDto> HandleAsync(CompletePlacementCommand command, CancellationToken ct = default);
}

// ── Status ───────────────────────────────────────────────────────────────────

public sealed record GetPlacementStatusQuery(Guid UserId);

public interface IGetPlacementStatusHandler
{
    Task<PlacementStatusDto> HandleAsync(GetPlacementStatusQuery query, CancellationToken ct = default);
}

// ── Current section ──────────────────────────────────────────────────────────

public sealed record GetPlacementCurrentSectionQuery(Guid UserId);

public interface IGetPlacementCurrentSectionHandler
{
    Task<PlacementCurrentSectionDto> HandleAsync(GetPlacementCurrentSectionQuery query, CancellationToken ct = default);
}

// ── Result ───────────────────────────────────────────────────────────────────

public sealed record GetPlacementResultQuery(Guid UserId);

public interface IGetPlacementResultHandler
{
    Task<PlacementResultDto> HandleAsync(GetPlacementResultQuery query, CancellationToken ct = default);
}

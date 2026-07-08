namespace LinguaCoach.Application.ResourceImport;

// ── Phase E1 — English Resource Source Registry admin CRUD contracts ──────────────
// CefrResourceSource is the reused source registry (see LinguaCoach.Domain.Entities.
// CefrResourceSource). These contracts are admin-facing only.

public sealed record AdminResourceSourceDto(
    Guid SourceId,
    string Name,
    string LicenseType,
    string? SourceUrl,
    string? UsageRestrictionNotes,
    bool IsImportApproved,
    DateTimeOffset? ImportedAtUtc,
    string LanguageCode,
    bool AllowsStudentDisplay,
    bool AllowsCommercialUse,
    string? AttributionText,
    string? SourceVersion,
    string? DownloadUrl,
    DateTime CreatedAt,
    DateTime? UpdatedAtUtc
);

public sealed record ListAdminResourceSourcesQuery(
    int Page = 1,
    int PageSize = 20,
    bool? IsImportApproved = null,
    string? LanguageCode = null,
    string? Search = null
);

public sealed record AdminResourceSourceListResult(
    IReadOnlyList<AdminResourceSourceDto> Items,
    int TotalCount,
    int OverallTotalCount,
    int ApprovedCount
);

public interface IAdminResourceSourceListQuery
{
    Task<AdminResourceSourceListResult> HandleAsync(ListAdminResourceSourcesQuery query, CancellationToken ct = default);
}

public sealed record GetAdminResourceSourceQuery(Guid SourceId);

public interface IAdminResourceSourceGetQuery
{
    Task<AdminResourceSourceDto?> HandleAsync(GetAdminResourceSourceQuery query, CancellationToken ct = default);
}

public sealed record AddResourceSourceCommand(
    string Name,
    string LicenseType,
    string? SourceUrl,
    string? UsageRestrictionNotes,
    string LanguageCode,
    bool AllowsStudentDisplay,
    bool AllowsCommercialUse,
    string? AttributionText,
    string? SourceVersion,
    string? DownloadUrl
);

public interface IAdminAddResourceSourceHandler
{
    Task<AdminResourceSourceDto> HandleAsync(AddResourceSourceCommand command, CancellationToken ct = default);
}

public sealed record UpdateResourceSourceCommand(
    Guid SourceId,
    string Name,
    string LicenseType,
    string? SourceUrl,
    string? UsageRestrictionNotes,
    string LanguageCode,
    bool AllowsStudentDisplay,
    bool AllowsCommercialUse,
    string? AttributionText,
    string? SourceVersion,
    string? DownloadUrl
);

public interface IAdminUpdateResourceSourceHandler
{
    Task<AdminResourceSourceDto> HandleAsync(UpdateResourceSourceCommand command, CancellationToken ct = default);
}

public sealed record SetResourceSourceApprovalCommand(Guid SourceId, bool Approve, string? Reason = null);

public interface IAdminResourceSourceApprovalHandler
{
    Task<AdminResourceSourceDto> HandleAsync(SetResourceSourceApprovalCommand command, CancellationToken ct = default);
}

public sealed class ResourceSourceValidationException : Exception
{
    public ResourceSourceValidationException(string message) : base(message) { }
}

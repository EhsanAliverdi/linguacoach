using LinguaCoach.Application.ResourceImport;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LinguaCoach.Infrastructure.ResourceImport;

public sealed class AdminResourceSourceListQueryHandler : IAdminResourceSourceListQuery
{
    private readonly LinguaCoachDbContext _db;

    public AdminResourceSourceListQueryHandler(LinguaCoachDbContext db) => _db = db;

    public async Task<AdminResourceSourceListResult> HandleAsync(
        ListAdminResourceSourcesQuery query, CancellationToken ct = default)
    {
        var page = Math.Max(query.Page, 1);
        var pageSize = Math.Clamp(query.PageSize, 1, 200);

        var filtered = _db.CefrResourceSources.AsQueryable();

        if (query.IsImportApproved.HasValue)
            filtered = filtered.Where(s => s.IsImportApproved == query.IsImportApproved.Value);

        if (!string.IsNullOrWhiteSpace(query.LanguageCode))
            filtered = filtered.Where(s => s.LanguageCode == query.LanguageCode.Trim().ToLowerInvariant());

        if (!string.IsNullOrWhiteSpace(query.Search))
            filtered = filtered.Where(s => s.Name.Contains(query.Search));

        var totalCount = await filtered.CountAsync(ct);
        var overallTotalCount = await _db.CefrResourceSources.CountAsync(ct);
        var approvedCount = await _db.CefrResourceSources.CountAsync(s => s.IsImportApproved, ct);

        var items = await filtered
            .OrderBy(s => s.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new AdminResourceSourceListResult(
            items.Select(ResourceImportMappers.ToDto).ToList(), totalCount, overallTotalCount, approvedCount);
    }
}

public sealed class AdminResourceSourceGetQueryHandler : IAdminResourceSourceGetQuery
{
    private readonly LinguaCoachDbContext _db;

    public AdminResourceSourceGetQueryHandler(LinguaCoachDbContext db) => _db = db;

    public async Task<AdminResourceSourceDto?> HandleAsync(GetAdminResourceSourceQuery query, CancellationToken ct = default)
    {
        var source = await _db.CefrResourceSources.FirstOrDefaultAsync(s => s.Id == query.SourceId, ct);
        return source is null ? null : ResourceImportMappers.ToDto(source);
    }
}

public sealed class AdminAddResourceSourceHandler : IAdminAddResourceSourceHandler
{
    private readonly LinguaCoachDbContext _db;

    public AdminAddResourceSourceHandler(LinguaCoachDbContext db) => _db = db;

    public async Task<AdminResourceSourceDto> HandleAsync(AddResourceSourceCommand command, CancellationToken ct = default)
    {
        var duplicateName = await _db.CefrResourceSources.AnyAsync(s => s.Name == command.Name.Trim(), ct);
        if (duplicateName)
            throw new ResourceSourceValidationException($"A resource source named '{command.Name}' already exists.");

        CefrResourceSource source;
        try
        {
            source = new CefrResourceSource(
                command.Name, command.LicenseType, command.SourceUrl, command.UsageRestrictionNotes,
                command.LanguageCode, command.AllowsStudentDisplay, command.AllowsCommercialUse,
                command.AttributionText, command.SourceVersion, command.DownloadUrl);
        }
        catch (ArgumentException ex)
        {
            throw new ResourceSourceValidationException(ex.Message);
        }

        _db.CefrResourceSources.Add(source);
        await _db.SaveChangesAsync(ct);

        return ResourceImportMappers.ToDto(source);
    }
}

public sealed class AdminUpdateResourceSourceHandler : IAdminUpdateResourceSourceHandler
{
    private readonly LinguaCoachDbContext _db;

    public AdminUpdateResourceSourceHandler(LinguaCoachDbContext db) => _db = db;

    public async Task<AdminResourceSourceDto> HandleAsync(UpdateResourceSourceCommand command, CancellationToken ct = default)
    {
        var source = await _db.CefrResourceSources.FirstOrDefaultAsync(s => s.Id == command.SourceId, ct)
            ?? throw new ResourceSourceValidationException($"Resource source '{command.SourceId}' was not found.");

        try
        {
            source.Update(
                command.Name, command.LicenseType, command.SourceUrl, command.UsageRestrictionNotes,
                command.LanguageCode, command.AllowsStudentDisplay, command.AllowsCommercialUse,
                command.AttributionText, command.SourceVersion, command.DownloadUrl);
        }
        catch (ArgumentException ex)
        {
            throw new ResourceSourceValidationException(ex.Message);
        }

        await _db.SaveChangesAsync(ct);
        return ResourceImportMappers.ToDto(source);
    }
}

public sealed class AdminResourceSourceApprovalHandler : IAdminResourceSourceApprovalHandler
{
    private readonly LinguaCoachDbContext _db;

    public AdminResourceSourceApprovalHandler(LinguaCoachDbContext db) => _db = db;

    public async Task<AdminResourceSourceDto> HandleAsync(SetResourceSourceApprovalCommand command, CancellationToken ct = default)
    {
        var source = await _db.CefrResourceSources.FirstOrDefaultAsync(s => s.Id == command.SourceId, ct)
            ?? throw new ResourceSourceValidationException($"Resource source '{command.SourceId}' was not found.");

        if (command.Approve)
        {
            source.ApproveForImport(command.Reason);
        }
        else
        {
            if (string.IsNullOrWhiteSpace(command.Reason))
                throw new ResourceSourceValidationException("A reason is required to revoke import approval.");
            source.RevokeApproval(command.Reason);
        }

        await _db.SaveChangesAsync(ct);
        return ResourceImportMappers.ToDto(source);
    }
}

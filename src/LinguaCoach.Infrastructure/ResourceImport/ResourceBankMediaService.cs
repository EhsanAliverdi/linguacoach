using LinguaCoach.Application.ResourceImport;
using LinguaCoach.Application.Storage;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LinguaCoach.Infrastructure.ResourceImport;

/// <summary>
/// Phase 4.6 — serves the real audio file backing a published <see cref="Domain.Entities.ResourceBankItem"/>
/// of type Listening. Mirrors <see cref="ResourceCandidateAudioService"/>'s signed-url/stream-fallback
/// pattern exactly. Ownership/existence is enforced by construction: the storage key is always read
/// from the row's own <c>ContentJson</c> (never accepted from the caller), and the row is always
/// looked up by its own Id filtered to <see cref="PublishedResourceType.Listening"/> — there is no
/// code path here that can resolve an arbitrary or cross-resource storage key.
/// </summary>
public sealed class ResourceBankMediaService : IResourceBankMediaService
{
    private static readonly TimeSpan SignedUrlExpiry = TimeSpan.FromMinutes(15);

    private readonly LinguaCoachDbContext _db;
    private readonly IFileStorageService _storage;

    public ResourceBankMediaService(LinguaCoachDbContext db, IFileStorageService storage)
    {
        _db = db;
        _storage = storage;
    }

    public async Task<ResourceBankAudioUrlResult?> GetAudioUrlAsync(Guid resourceBankItemId, CancellationToken ct = default)
    {
        var content = await LoadListeningContentAsync(resourceBankItemId, ct);
        if (content is null || string.IsNullOrWhiteSpace(content.AudioStorageKey))
            return null;

        var signed = await _storage.GenerateSignedUrlAsync(content.AudioStorageKey, SignedUrlExpiry, ct);
        var url = signed.Url.StartsWith("local://", StringComparison.OrdinalIgnoreCase)
                  || signed.Url.StartsWith("fake://", StringComparison.OrdinalIgnoreCase)
            ? $"/api/admin/resource-bank/{resourceBankItemId}/audio"
            : signed.Url;

        return new ResourceBankAudioUrlResult(url, signed.ExpiresAt);
    }

    public async Task<ResourceBankAudioStreamResult?> GetAudioStreamAsync(Guid resourceBankItemId, CancellationToken ct = default)
    {
        var content = await LoadListeningContentAsync(resourceBankItemId, ct);
        if (content is null || string.IsNullOrWhiteSpace(content.AudioStorageKey))
            return null;

        if (!await _storage.ExistsAsync(content.AudioStorageKey, ct))
            return null;

        await using var stream = await _storage.ReadAsync(content.AudioStorageKey, ct);
        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms, ct);
        return new ResourceBankAudioStreamResult(ms.ToArray(), content.AudioContentType);
    }

    private async Task<ListeningPassageContent?> LoadListeningContentAsync(Guid resourceBankItemId, CancellationToken ct)
    {
        var entry = await _db.ResourceBankItems
            .Where(e => e.Id == resourceBankItemId && e.Type == PublishedResourceType.Listening)
            .FirstOrDefaultAsync(ct);
        if (entry is null) return null;

        return ResourceBankItemContent.Deserialize<ListeningPassageContent>(entry.ContentJson);
    }
}

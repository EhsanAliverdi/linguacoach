using LinguaCoach.Domain.Enums;

namespace LinguaCoach.Infrastructure.ResourceImport;

/// <summary>
/// Phase 4.2 — the file-extension → (MIME type, media type) table used by every place in the
/// import pipeline that needs to classify a file: the ZIP-derived asset extraction stage and the
/// inline (paste/multi-file) submission service. Extracted so both stay in sync instead of
/// maintaining two copies of the same switch.
/// </summary>
internal static class ImportAssetClassification
{
    public static (string MimeType, ImportAssetMediaType MediaType) Classify(string extension)
    {
        var ext = (extension ?? string.Empty).ToLowerInvariant();
        return ext switch
        {
            ".csv" => ("text/csv", ImportAssetMediaType.StructuredData),
            ".json" => ("application/json", ImportAssetMediaType.StructuredData),
            ".jsonl" => ("application/jsonl", ImportAssetMediaType.StructuredData),
            ".xml" => ("application/xml", ImportAssetMediaType.StructuredData),
            ".txt" or ".md" => ("text/plain", ImportAssetMediaType.Text),
            ".mp3" => ("audio/mpeg", ImportAssetMediaType.Audio),
            ".wav" => ("audio/wav", ImportAssetMediaType.Audio),
            ".m4a" => ("audio/mp4", ImportAssetMediaType.Audio),
            ".ogg" => ("audio/ogg", ImportAssetMediaType.Audio),
            ".jpg" or ".jpeg" => ("image/jpeg", ImportAssetMediaType.Image),
            ".png" => ("image/png", ImportAssetMediaType.Image),
            ".gif" => ("image/gif", ImportAssetMediaType.Image),
            ".webp" => ("image/webp", ImportAssetMediaType.Image),
            ".mp4" => ("video/mp4", ImportAssetMediaType.Video),
            _ => ("application/octet-stream", ImportAssetMediaType.Unknown),
        };
    }
}

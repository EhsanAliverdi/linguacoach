using LinguaCoach.Application.Vocabulary;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LinguaCoach.Infrastructure.Vocabulary;

public sealed class UpdateVocabularyStatusHandler : IUpdateVocabularyStatusHandler
{
    private static readonly HashSet<string> AllowedStatuses =
        new(Enum.GetNames<VocabularyItemStatus>(), StringComparer.OrdinalIgnoreCase);

    private readonly LinguaCoachDbContext _db;

    public UpdateVocabularyStatusHandler(LinguaCoachDbContext db) => _db = db;

    public async Task HandleAsync(UpdateVocabularyStatusCommand command, CancellationToken ct = default)
    {
        if (!AllowedStatuses.Contains(command.Status))
            throw new ArgumentException($"Invalid status '{command.Status}'.", nameof(command));

        var profile = await _db.StudentProfiles
            .FirstOrDefaultAsync(p => p.UserId == command.UserId, ct)
            ?? throw new InvalidOperationException("Student profile not found.");

        var item = await _db.StudentVocabularyItems
            .FirstOrDefaultAsync(v => v.Id == command.ItemId, ct)
            ?? throw new KeyNotFoundException($"Vocabulary item {command.ItemId} not found.");

        if (item.StudentProfileId != profile.Id)
            throw new UnauthorizedAccessException("Access denied.");

        var newStatus = Enum.Parse<VocabularyItemStatus>(command.Status, ignoreCase: true);
        item.UpdateStatus(newStatus);
        await _db.SaveChangesAsync(ct);
    }
}

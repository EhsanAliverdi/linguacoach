namespace LinguaCoach.Application.ResourceImport;

// ── Phase K3 — "one word in, one Lesson + one Exercise + one Module out" admin dev/testing
// shortcut. Bypasses the full import-staging/review workflow (ResourceCandidate → validate →
// approve → publish) entirely — it publishes a Vocabulary ResourceBankItem directly, then
// cascades through the existing deterministic Lesson/Exercise/Module generation pipeline,
// auto-approving the Lesson and Exercise along the way (Generate Module's own hard gate requires
// both already approved — see IGenerateModuleFromResourceHandler). The Module itself is left
// PendingReview so an admin can still review the final artifact. This is a convenience/testing
// tool, not a replacement for the real content-import review workflow. ──

public sealed record QuickWordPipelineRequest(
    string Word,
    string CefrLevel,
    string? PartOfSpeech = null,
    string? Definition = null,
    Guid? CreatedByUserId = null
);

public sealed record QuickWordPipelineResult(
    Guid ResourceBankItemId,
    Guid LessonId,
    Guid ExerciseId,
    Guid ModuleId
);

public interface IQuickWordPipelineService
{
    /// <summary>Throws <see cref="ResourceImportValidationException"/> with a specific message if
    /// any stage fails (invalid CEFR level, generation gate rejected, etc.) — never leaves a
    /// partially-cascaded state silently unreported.</summary>
    Task<QuickWordPipelineResult> RunAsync(QuickWordPipelineRequest request, CancellationToken ct = default);
}

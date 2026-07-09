namespace LinguaCoach.Application.ModuleDefinitions;

// ── Phase H5 — "Generate Module" from existing Learn Items and Activity Definitions. Deterministic
// composition only — no AI provider call, same reasoning as H3/H4. Module generation composes
// EXISTING approved Learn Items/Activity Definitions; it never cascade-generates new ones. Every
// "find compatible" entry point (from-resource/from-learn-item/from-activity) only considers
// Approved sources — a draft/pending Learn Item or Activity is never silently pulled into a
// generated Module. ──

public sealed record GenerateModuleFromItemsRequest(
    IReadOnlyList<ModuleLearnItemLinkInput> LearnItemLinks,
    IReadOnlyList<ModuleActivityLinkInput> ActivityLinks,
    string? Title = null,
    string? Notes = null,
    Guid? CreatedByUserId = null
);

public sealed record GenerateModuleFromResourceRequest(
    string ResourceType,
    Guid ResourceId,
    string? Title = null,
    string? Notes = null,
    Guid? CreatedByUserId = null
);

public sealed record GenerateModuleFromLearnItemRequest(
    Guid LearnItemId,
    string? Title = null,
    string? Notes = null,
    Guid? CreatedByUserId = null
);

public sealed record GenerateModuleFromActivityRequest(
    Guid ActivityDefinitionId,
    string? Title = null,
    string? Notes = null,
    Guid? CreatedByUserId = null
);

public sealed record GenerateModuleDefinitionResult(ModuleDefinitionDto Module, string ReviewRoute);

/// <summary>Composes a Module from explicitly-selected Learn Item(s) + Activity Definition(s).
/// Every selected item must already be <c>Approved</c> — rejected with a clear message naming the
/// first non-approved item, rather than silently allowing a draft into a generated Module.</summary>
public interface IGenerateModuleFromItemsHandler
{
    Task<GenerateModuleDefinitionResult> HandleAsync(GenerateModuleFromItemsRequest request, CancellationToken ct = default);
}

/// <summary>Finds an existing Approved Learn Item and an existing Approved Activity Definition
/// both linked to the given published Resource Bank row, and composes a Module from them. Never
/// creates a Learn Item or Activity Definition — rejected with a clear message when either is
/// missing.</summary>
public interface IGenerateModuleFromResourceHandler
{
    Task<GenerateModuleDefinitionResult> HandleAsync(GenerateModuleFromResourceRequest request, CancellationToken ct = default);
}

/// <summary>Starts from an Approved Learn Item and finds compatible (matching CEFR/skill where
/// set) Approved Activity Definitions to compose a Module. Rejected when the Learn Item itself
/// isn't approved yet, or when no compatible Approved Activity Definition exists.</summary>
public interface IGenerateModuleFromLearnItemHandler
{
    Task<GenerateModuleDefinitionResult> HandleAsync(GenerateModuleFromLearnItemRequest request, CancellationToken ct = default);
}

/// <summary>Starts from an Approved Activity Definition and finds compatible (matching CEFR/skill
/// where set) Approved Learn Items to compose a Module. Rejected when the Activity itself isn't
/// approved yet, or when no compatible Approved Learn Item exists.</summary>
public interface IGenerateModuleFromActivityHandler
{
    Task<GenerateModuleDefinitionResult> HandleAsync(GenerateModuleFromActivityRequest request, CancellationToken ct = default);
}

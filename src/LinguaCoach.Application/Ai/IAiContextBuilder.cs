namespace LinguaCoach.Application.Ai;

/// <summary>
/// Builds the AI request payload for a given feature context.
/// Fetches the correct prompt template from DB, substitutes variables,
/// and enforces the per-feature token budget before the call is made.
/// Not implemented in MVP skeleton.
/// </summary>
public interface IAiContextBuilder
{
    /// <summary>
    /// Renders a named prompt template with the provided variables and enforces
    /// the token budget defined on the template record.
    /// Throws <see cref="TokenBudgetExceededException"/> if the rendered prompt
    /// would exceed the template's MaxInputTokens limit.
    /// </summary>
    Task<AiRequest> BuildAsync(string promptKey, IReadOnlyDictionary<string, string> variables, CancellationToken ct = default);
}

/// <summary>
/// Thrown when a rendered prompt exceeds the configured token budget for a feature.
/// </summary>
public sealed class TokenBudgetExceededException : Exception
{
    public string PromptKey { get; }
    public int EstimatedTokens { get; }
    public int BudgetTokens { get; }

    public TokenBudgetExceededException(string promptKey, int estimatedTokens, int budgetTokens)
        : base($"Prompt '{promptKey}' estimated {estimatedTokens} tokens exceeds budget of {budgetTokens}.")
    {
        PromptKey = promptKey;
        EstimatedTokens = estimatedTokens;
        BudgetTokens = budgetTokens;
    }
}

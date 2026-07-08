namespace LinguaCoach.Infrastructure.ResourceImport;

/// <summary>
/// Shared conservative English/non-English script heuristic used by both
/// <see cref="ResourceImportService"/> (Phase E1, at original import time) and
/// <see cref="ResourceCandidateValidationService"/> (Phase E2, at re-validation time). Factored
/// out of ResourceImportService so both services check the exact same signal rather than
/// maintaining two independently-drifting heuristics.
///
/// This is NOT a language-identification library — it only reliably catches Persian/Arabic-script
/// text and a high proportion of other non-Latin characters. Documented limitation, carried over
/// unchanged from Phase E1: it does not detect non-English text written in Latin script (e.g.
/// French, Turkish). A real language-ID pass is future work if needed.
/// </summary>
internal static class ResourceLanguageHeuristic
{
    private static bool IsArabicScriptChar(char c) =>
        (c >= '؀' && c <= 'ۿ') ||
        (c >= 'ݐ' && c <= 'ݿ') ||
        (c >= 'ﭐ' && c <= '﷿') ||
        (c >= 'ﹰ' && c <= '﻿');

    /// <summary>True if <paramref name="text"/> looks like it is NOT English, by this
    /// conservative script heuristic (Persian/Arabic-script text, or predominantly non-Latin
    /// characters). False (i.e. "looks English enough") for empty/whitespace-only input.</summary>
    public static bool LooksNonEnglish(string? text, out string? reason)
    {
        reason = null;
        if (string.IsNullOrEmpty(text))
            return false;

        if (text.Any(IsArabicScriptChar))
        {
            reason = "Text contains Persian/Arabic-script characters.";
            return true;
        }

        var letters = text.Where(char.IsLetter).ToList();
        if (letters.Count > 0)
        {
            var nonBasicLatin = letters.Count(c => c > 'ɏ'); // beyond Latin Extended-B
            if ((double)nonBasicLatin / letters.Count > 0.15)
            {
                reason = "Text is predominantly non-Latin-script.";
                return true;
            }
        }

        return false;
    }
}

using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;

namespace LinguaCoach.Infrastructure.Ai;

/// <summary>
/// Resolves the language name used for AI-generated translation support text
/// (e.g. "sourceLanguageSupport", "feedbackInSourceLanguage" prompt fields).
///
/// Never defaults to a specific foreign language (e.g. "Persian") when the
/// student has no genuine support-language preference — that produced
/// unprompted foreign-language content for students who never chose one.
/// Falls back to the target language name instead, which yields no
/// translation content rather than a guessed one.
/// </summary>
public static class LanguageSupportResolver
{
    public static string ResolveSourceLanguageName(StudentProfile profile)
    {
        if (profile.TranslationHelpPreference == TranslationHelpPreference.Never)
            return ResolveTargetLanguageName(profile);

        return profile.SupportLanguageName
            ?? profile.LanguagePair?.SourceLanguage?.Name
            ?? ResolveTargetLanguageName(profile);
    }

    public static string ResolveTargetLanguageName(StudentProfile profile) =>
        profile.LanguagePair?.TargetLanguage?.Name ?? "English";
}

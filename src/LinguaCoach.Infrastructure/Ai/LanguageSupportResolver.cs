using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;

namespace LinguaCoach.Infrastructure.Ai;

/// <summary>
/// Resolves the language name used for AI-generated translation support text
/// (e.g. "sourceLanguageSupport", "feedbackInSourceLanguage" prompt fields).
///
/// Never defaults to a specific foreign language (e.g. "Persian") when the
/// student has no genuine, explicit support-language preference — that
/// produced unprompted foreign-language content for students who never
/// opted in. Translation content is only enabled when the student has
/// explicitly set TranslationHelpPreference to WhenDifficult or
/// AlwaysAvailable on /profile — "Not set" (null) must behave the same as
/// "no", since the student hasn't opted in yet. LanguagePair.SourceLanguage
/// (a v1-onboarding "native language" field, distinct from the newer
/// SupportLanguageCode preference) is only consulted as a fallback once the
/// student has opted in, never to infer opt-in itself.
/// </summary>
public static class LanguageSupportResolver
{
    public static string ResolveSourceLanguageName(StudentProfile profile)
    {
        var hasOptedIntoTranslationHelp =
            profile.TranslationHelpPreference == TranslationHelpPreference.WhenDifficult
            || profile.TranslationHelpPreference == TranslationHelpPreference.AlwaysAvailable;

        if (!hasOptedIntoTranslationHelp)
            return ResolveTargetLanguageName(profile);

        return profile.SupportLanguageName
            ?? profile.LanguagePair?.SourceLanguage?.Name
            ?? ResolveTargetLanguageName(profile);
    }

    public static string ResolveTargetLanguageName(StudentProfile profile) =>
        profile.LanguagePair?.TargetLanguage?.Name ?? "English";
}

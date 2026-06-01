using LinguaCoach.Domain.Enums;

namespace LinguaCoach.UnitTests.Domain;

/// <summary>
/// Pins the integer values of all enums stored as int columns in the database.
/// Enums stored as integers are vulnerable to silent data corruption if values
/// are reordered or if new values are inserted before existing ones.
/// These tests must fail loudly before any such change reaches production.
/// To add a new enum value: append it at the end with the next integer.
/// Never reorder, never insert in the middle.
/// </summary>
public sealed class EnumValuePinningTests
{
    [Fact] public void LanguageDirection_Ltr_IsZero()      => Assert.Equal(0, (int)LanguageDirection.Ltr);
    [Fact] public void LanguageDirection_Rtl_IsOne()       => Assert.Equal(1, (int)LanguageDirection.Rtl);
    [Fact] public void LanguageDirection_HasExactlyTwoValues() => Assert.Equal(2, Enum.GetValues<LanguageDirection>().Length);

    [Fact] public void OnboardingStatus_NotStarted_IsZero() => Assert.Equal(0, (int)OnboardingStatus.NotStarted);
    [Fact] public void OnboardingStatus_InProgress_IsOne()  => Assert.Equal(1, (int)OnboardingStatus.InProgress);
    [Fact] public void OnboardingStatus_Complete_IsTwo()    => Assert.Equal(2, (int)OnboardingStatus.Complete);
    [Fact] public void OnboardingStatus_HasExactlyThreeValues() => Assert.Equal(3, Enum.GetValues<OnboardingStatus>().Length);

    [Fact] public void OnboardingStep_None_IsZero()     => Assert.Equal(0, (int)OnboardingStep.None);
    [Fact] public void OnboardingStep_Language_IsOne()  => Assert.Equal(1, (int)OnboardingStep.Language);
    [Fact] public void OnboardingStep_Track_IsTwo()     => Assert.Equal(2, (int)OnboardingStep.Track);
    [Fact] public void OnboardingStep_Career_IsThree()  => Assert.Equal(3, (int)OnboardingStep.Career);
    [Fact] public void OnboardingStep_Skill_IsFour()    => Assert.Equal(4, (int)OnboardingStep.Skill);
    [Fact] public void OnboardingStep_HasExactlyFiveValues() => Assert.Equal(5, Enum.GetValues<OnboardingStep>().Length);

    [Fact] public void SkillFocus_Writing_IsZero()    => Assert.Equal(0, (int)SkillFocus.Writing);
    [Fact] public void SkillFocus_Speaking_IsOne()    => Assert.Equal(1, (int)SkillFocus.Speaking);
    [Fact] public void SkillFocus_Vocabulary_IsTwo()  => Assert.Equal(2, (int)SkillFocus.Vocabulary);
    [Fact] public void SkillFocus_HasExactlyThreeValues() => Assert.Equal(3, Enum.GetValues<SkillFocus>().Length);
}

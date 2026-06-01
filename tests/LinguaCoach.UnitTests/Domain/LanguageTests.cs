using FluentAssertions;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;

namespace LinguaCoach.UnitTests.Domain;

public sealed class LanguageTests
{
    [Fact]
    public void Language_NormalisesCodeToLowercase()
    {
        var lang = new Language("FA", "Persian", LanguageDirection.Rtl);
        lang.Code.Should().Be("fa");
    }

    [Fact]
    public void Language_WithEmptyCode_Throws()
    {
        var act = () => new Language("", "Persian", LanguageDirection.Rtl);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Language_WithEmptyName_Throws()
    {
        var act = () => new Language("fa", "", LanguageDirection.Rtl);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void LanguagePair_WithSameSourceAndTarget_Throws()
    {
        var lang = new Language("fa", "Persian", LanguageDirection.Rtl);
        var act = () => new LanguagePair(lang, lang);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void LanguagePair_WithSameCodeDistinctInstances_Throws()
    {
        // Guard must compare by Code, not by object identity.
        var fa1 = new Language("fa", "Persian", LanguageDirection.Rtl);
        var fa2 = new Language("fa", "Farsi", LanguageDirection.Rtl);
        var act = () => new LanguagePair(fa1, fa2);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void LanguagePair_CanBeDeactivated()
    {
        var fa = new Language("fa", "Persian", LanguageDirection.Rtl);
        var en = new Language("en", "English", LanguageDirection.Ltr);
        var pair = new LanguagePair(fa, en);

        pair.Deactivate();

        pair.IsActive.Should().BeFalse();
    }

    [Fact]
    public void LanguagePair_CanBeReactivated()
    {
        var fa = new Language("fa", "Persian", LanguageDirection.Rtl);
        var en = new Language("en", "English", LanguageDirection.Ltr);
        var pair = new LanguagePair(fa, en);

        pair.Deactivate();
        pair.Activate();

        pair.IsActive.Should().BeTrue();
    }

    [Fact]
    public void LanguagePair_WithNullSource_Throws()
    {
        var en = new Language("en", "English", LanguageDirection.Ltr);
        var act = () => new LanguagePair(null!, en);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void LanguagePair_WithNullTarget_Throws()
    {
        var fa = new Language("fa", "Persian", LanguageDirection.Rtl);
        var act = () => new LanguagePair(fa, null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Language_TrimsAndNormalisesCode()
    {
        var lang = new Language("  FA  ", "Persian", LanguageDirection.Rtl);
        lang.Code.Should().Be("fa");
    }

    [Fact]
    public void Language_WithCodeLongerThan2Chars_Throws()
    {
        var act = () => new Language("eng", "English", LanguageDirection.Ltr);
        act.Should().Throw<ArgumentException>().WithMessage("*ISO 639-1*");
    }

    [Fact]
    public void Language_WithCodeShorterThan2Chars_Throws()
    {
        var act = () => new Language("e", "English", LanguageDirection.Ltr);
        act.Should().Throw<ArgumentException>().WithMessage("*ISO 639-1*");
    }
}

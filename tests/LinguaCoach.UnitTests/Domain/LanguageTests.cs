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
    public void LanguagePair_CanBeDeactivated()
    {
        var fa = new Language("fa", "Persian", LanguageDirection.Rtl);
        var en = new Language("en", "English", LanguageDirection.Ltr);
        var pair = new LanguagePair(fa, en);

        pair.Deactivate();

        pair.IsActive.Should().BeFalse();
    }
}

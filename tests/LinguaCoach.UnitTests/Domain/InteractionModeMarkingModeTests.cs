using LinguaCoach.Domain.Enums;

namespace LinguaCoach.UnitTests.Domain;

/// <summary>
/// Pins the integer values of InteractionMode and MarkingMode stored as int columns.
/// Never reorder or insert values — append only.
/// </summary>
public sealed class InteractionModeMarkingModeTests
{
    // ── InteractionMode ───────────────────────────────────────────────────────
    [Fact] public void InteractionMode_ReadOnly_IsZero()         => Assert.Equal(0, (int)InteractionMode.ReadOnly);
    [Fact] public void InteractionMode_FreeTextEntry_IsOne()     => Assert.Equal(1, (int)InteractionMode.FreeTextEntry);
    [Fact] public void InteractionMode_GapFill_IsTwo()           => Assert.Equal(2, (int)InteractionMode.GapFill);
    [Fact] public void InteractionMode_MultipleChoice_IsThree()  => Assert.Equal(3, (int)InteractionMode.MultipleChoice);
    [Fact] public void InteractionMode_MatchingPairs_IsFour()    => Assert.Equal(4, (int)InteractionMode.MatchingPairs);
    [Fact] public void InteractionMode_SentenceBuilder_IsFive()  => Assert.Equal(5, (int)InteractionMode.SentenceBuilder);
    [Fact] public void InteractionMode_ErrorCorrection_IsSix()   => Assert.Equal(6, (int)InteractionMode.ErrorCorrection);
    [Fact] public void InteractionMode_ChatReply_IsSeven()       => Assert.Equal(7, (int)InteractionMode.ChatReply);
    [Fact] public void InteractionMode_AudioAndFreeText_IsEight()=> Assert.Equal(8, (int)InteractionMode.AudioAndFreeText);
    [Fact] public void InteractionMode_AudioAndGapFill_IsNine()  => Assert.Equal(9, (int)InteractionMode.AudioAndGapFill);
    [Fact] public void InteractionMode_EmailReply_IsTen()        => Assert.Equal(10, (int)InteractionMode.EmailReply);
    [Fact] public void InteractionMode_AudioResponse_IsEleven()        => Assert.Equal(11, (int)InteractionMode.AudioResponse);
    [Fact] public void InteractionMode_MultipleChoiceMulti_IsTwelve()  => Assert.Equal(12, (int)InteractionMode.MultipleChoiceMulti);
    [Fact] public void InteractionMode_ReadingFillInBlanks_IsThirteen() => Assert.Equal(13, (int)InteractionMode.ReadingFillInBlanks);
    [Fact] public void InteractionMode_ReorderParagraphs_IsFourteen()              => Assert.Equal(14, (int)InteractionMode.ReorderParagraphs);
    [Fact] public void InteractionMode_ReadingWritingFillInBlanks_IsFifteen()      => Assert.Equal(15, (int)InteractionMode.ReadingWritingFillInBlanks);
    [Fact] public void InteractionMode_ListeningFillInBlanks_IsSixteen()           => Assert.Equal(16, (int)InteractionMode.ListeningFillInBlanks);
    [Fact] public void InteractionMode_HighlightCorrectSummary_IsSeventeen()       => Assert.Equal(17, (int)InteractionMode.HighlightCorrectSummary);
    [Fact] public void InteractionMode_HighlightIncorrectWords_IsEighteen()        => Assert.Equal(18, (int)InteractionMode.HighlightIncorrectWords);
    [Fact] public void InteractionMode_WriteFromDictation_IsNineteen()             => Assert.Equal(19, (int)InteractionMode.WriteFromDictation);
    [Fact] public void InteractionMode_HasExactlyTwentyValues()                   => Assert.Equal(20, Enum.GetValues<InteractionMode>().Length);

    // ── MarkingMode ───────────────────────────────────────────────────────────
    [Fact] public void MarkingMode_AiOpenEnded_IsZero()    => Assert.Equal(0, (int)MarkingMode.AiOpenEnded);
    [Fact] public void MarkingMode_AiStructured_IsOne()    => Assert.Equal(1, (int)MarkingMode.AiStructured);
    [Fact] public void MarkingMode_ExactMatch_IsTwo()      => Assert.Equal(2, (int)MarkingMode.ExactMatch);
    [Fact] public void MarkingMode_KeyedSelection_IsThree()=> Assert.Equal(3, (int)MarkingMode.KeyedSelection);
    [Fact] public void MarkingMode_NoMarking_IsFour()      => Assert.Equal(4, (int)MarkingMode.NoMarking);
    [Fact] public void MarkingMode_HasExactlyFiveValues()  => Assert.Equal(5, Enum.GetValues<MarkingMode>().Length);
}

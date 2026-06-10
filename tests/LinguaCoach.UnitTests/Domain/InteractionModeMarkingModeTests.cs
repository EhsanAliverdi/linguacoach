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
    [Fact] public void InteractionMode_HasExactlyTenValues()     => Assert.Equal(10, Enum.GetValues<InteractionMode>().Length);

    // ── MarkingMode ───────────────────────────────────────────────────────────
    [Fact] public void MarkingMode_AiOpenEnded_IsZero()    => Assert.Equal(0, (int)MarkingMode.AiOpenEnded);
    [Fact] public void MarkingMode_AiStructured_IsOne()    => Assert.Equal(1, (int)MarkingMode.AiStructured);
    [Fact] public void MarkingMode_ExactMatch_IsTwo()      => Assert.Equal(2, (int)MarkingMode.ExactMatch);
    [Fact] public void MarkingMode_KeyedSelection_IsThree()=> Assert.Equal(3, (int)MarkingMode.KeyedSelection);
    [Fact] public void MarkingMode_NoMarking_IsFour()      => Assert.Equal(4, (int)MarkingMode.NoMarking);
    [Fact] public void MarkingMode_HasExactlyFiveValues()  => Assert.Equal(5, Enum.GetValues<MarkingMode>().Length);
}

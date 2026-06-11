namespace LinguaCoach.Domain.Enums;

/// <summary>The kind of audio asset stored through IFileStorageService.</summary>
public enum AssetType
{
    ListeningTts = 0,
    SpeakingRecording = 1,
    LiveAiTurn = 2
}

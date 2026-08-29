namespace ChatOutLoud.TtsProviders;

internal sealed class TtsAudioResult
{
    public TtsAudioResult(
        byte[] audioData,
        string contentType)
    {
        AudioData = audioData;
        ContentType = contentType;
    }

    public byte[] AudioData { get; }

    public string ContentType { get; }
}
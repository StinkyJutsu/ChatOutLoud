using System.Collections.Generic;

namespace ChatOutLoud.TtsProviders;

internal sealed class TtsVoice
{
    public TtsVoice(
        string providerId,
        string providerName,
        string voiceId,
        string displayName,
        IReadOnlyList<string>? languageCodes = null)
    {
        ProviderId = providerId;
        ProviderName = providerName;
        VoiceId = voiceId;
        DisplayName = displayName;

        LanguageCodes =
            languageCodes ??
            [];
    }

    public string ProviderId { get; }

    public string ProviderName { get; }

    public string VoiceId { get; }

    public string DisplayName { get; }

    public IReadOnlyList<string> LanguageCodes { get; }

    public override string ToString()
    {
        return DisplayName;
    }
}
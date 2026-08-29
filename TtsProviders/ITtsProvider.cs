using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ChatOutLoud.TtsProviders;

internal interface ITtsProvider
{
    string ProviderId { get; }

    string ProviderName { get; }

    TtsProviderStatus Status { get; }

    Task<bool> VerifyConnectionAsync(
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TtsVoice>> GetVoicesAsync(
        CancellationToken cancellationToken = default);

    Task<TtsAudioResult> SynthesizeSpeechAsync(
        string text,
        TtsVoice voice,
        CancellationToken cancellationToken = default);
}
namespace ChatOutLoud.TtsProviders;

internal enum TtsProviderStatus
{
    NotConfigured,
    Configured,
    Checking,
    Connected,
    NeedsAttention
}
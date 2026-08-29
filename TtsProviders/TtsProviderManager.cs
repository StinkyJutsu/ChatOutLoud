using ChatOutLoud.TtsProviders.Providers;
using System;
using System.Collections.Generic;

namespace ChatOutLoud.TtsProviders;

internal sealed class TtsProviderManager
{
    public static TtsProviderManager Shared { get; } =
        CreateSharedManager();

    private readonly Dictionary<string, ITtsProvider> _providers =
        new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyCollection<ITtsProvider> Providers =>
        _providers.Values;

    public void RegisterProvider(ITtsProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);

        _providers[provider.ProviderId] = provider;
    }

    public ITtsProvider? GetProvider(string providerId)
    {
        if (string.IsNullOrWhiteSpace(providerId))
        {
            return null;
        }

        return _providers.TryGetValue(
            providerId,
            out ITtsProvider? provider)
            ? provider
            : null;
    }

    private static TtsProviderManager CreateSharedManager()
    {
        TtsProviderManager manager =
            new();

        manager.RegisterProvider(
            new GoogleCloudTtsProvider());

        return manager;
    }
}
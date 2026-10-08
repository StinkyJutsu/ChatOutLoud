using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Security;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace ChatOutLoud.TtsProviders.Providers;

internal sealed class AzureSpeechTtsProvider : ITtsProvider
{
    private const string AzureProviderId =
        "microsoft-azure";

    private static readonly HttpClient Http =
        new();

    private static readonly JsonSerializerOptions JsonOptions =
        new()
        {
            PropertyNameCaseInsensitive = true
        };

    private string? _subscriptionKey;
    private string? _region;

    public AzureSpeechTtsProvider()
    {
        TryLoadSavedCredential();
    }

    public string ProviderId =>
        AzureProviderId;

    public string ProviderName =>
        "Microsoft Azure Speech";

    public TtsProviderStatus Status
    {
        get;
        private set;
    } = TtsProviderStatus.NotConfigured;

    public async Task<bool> ConfigureAsync(
        string subscriptionKey,
        string region,
        CancellationToken cancellationToken = default)
    {
        subscriptionKey =
            NormalizeSubscriptionKey(
                subscriptionKey);

        region =
            NormalizeRegion(
                region);

        Status =
            TtsProviderStatus.Checking;

        try
        {
            IReadOnlyList<TtsVoice> voices =
                await FetchUsableVoicesAsync(
                    subscriptionKey,
                    region,
                    cancellationToken);

            if (voices.Count == 0)
            {
                Status =
                    TtsProviderStatus.NeedsAttention;

                return false;
            }

            AzureCredential credential =
                new()
                {
                    SubscriptionKey =
                        subscriptionKey,

                    Region =
                        region
                };

            string credentialJson =
                JsonSerializer.Serialize(
                    credential);

            TtsCredentialStore.Save(
                ProviderId,
                credentialJson);

            TtsVoiceCatalogStore.Save(
                ProviderId,
                voices);

            _subscriptionKey =
                subscriptionKey;

            _region =
                region;

            Status =
                TtsProviderStatus.Connected;

            return true;
        }
        catch
        {
            Status =
                TtsProviderStatus.NeedsAttention;

            throw;
        }
    }

    public async Task<bool> VerifyConnectionAsync(
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(
                _subscriptionKey) ||
            string.IsNullOrWhiteSpace(
                _region))
        {
            Status =
                TtsProviderStatus.NotConfigured;

            return false;
        }

        Status =
            TtsProviderStatus.Checking;

        try
        {
            IReadOnlyList<TtsVoice> voices =
                await FetchUsableVoicesAsync(
                    _subscriptionKey,
                    _region,
                    cancellationToken);

            bool connected =
                voices.Count > 0;

            if (connected)
            {
                TtsVoiceCatalogStore.Save(
                    ProviderId,
                    voices);
            }

            Status =
                connected
                    ? TtsProviderStatus.Connected
                    : TtsProviderStatus.NeedsAttention;

            return connected;
        }
        catch
        {
            Status =
                TtsProviderStatus.NeedsAttention;

            return false;
        }
    }

    public async Task<IReadOnlyList<TtsVoice>> GetVoicesAsync(
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(
                _subscriptionKey) ||
            string.IsNullOrWhiteSpace(
                _region))
        {
            return Array.Empty<TtsVoice>();
        }

        IReadOnlyList<TtsVoice> voices =
            await FetchUsableVoicesAsync(
                _subscriptionKey,
                _region,
                cancellationToken);

        TtsVoiceCatalogStore.Save(
            ProviderId,
            voices);

        return voices;
    }

    public async Task<TtsAudioResult> SynthesizeSpeechAsync(
        string text,
        TtsVoice voice,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(
                _subscriptionKey) ||
            string.IsNullOrWhiteSpace(
                _region))
        {
            throw new InvalidOperationException(
                "Microsoft Azure Speech is not configured.");
        }

        if (string.IsNullOrWhiteSpace(
                text))
        {
            throw new ArgumentException(
                "Speech text cannot be empty.",
                nameof(text));
        }

        ArgumentNullException.ThrowIfNull(
            voice);

        if (!string.Equals(
                voice.ProviderId,
                ProviderId,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "The selected voice does not belong to Microsoft Azure Speech.");
        }

        if (string.IsNullOrWhiteSpace(
                voice.VoiceId))
        {
            throw new InvalidOperationException(
                "The selected Azure voice does not have a valid voice ID.");
        }

        if (voice.LanguageCodes.Count == 0 ||
            string.IsNullOrWhiteSpace(
                voice.LanguageCodes[0]))
        {
            throw new InvalidOperationException(
                "The selected Azure voice does not have a valid language code.");
        }

        string escapedText =
            SecurityElement.Escape(
                text) ?? "";

        string escapedVoiceName =
            SecurityElement.Escape(
                voice.VoiceId) ?? "";

        string escapedLanguageCode =
            SecurityElement.Escape(
                voice.LanguageCodes[0]) ?? "";

        string ssml =
            $"<speak version=\"1.0\" " +
            $"xmlns=\"http://www.w3.org/2001/10/synthesis\" " +
            $"xml:lang=\"{escapedLanguageCode}\">" +
            $"<voice name=\"{escapedVoiceName}\">" +
            $"{escapedText}" +
            "</voice>" +
            "</speak>";

        Uri requestUri =
            new(
                $"https://{_region}.tts.speech.microsoft.com/cognitiveservices/v1");

        using HttpRequestMessage request =
            new(
                HttpMethod.Post,
                requestUri);

        request.Headers.TryAddWithoutValidation(
            "Ocp-Apim-Subscription-Key",
            _subscriptionKey);

        request.Headers.TryAddWithoutValidation(
            "X-Microsoft-OutputFormat",
            "riff-24khz-16bit-mono-pcm");

        request.Headers.UserAgent.ParseAdd(
            "ChatOutLoud");

        request.Content =
            new StringContent(
                ssml,
                Encoding.UTF8,
                "application/ssml+xml");

        using HttpResponseMessage response =
            await Http.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            string errorBody =
                await response.Content.ReadAsStringAsync(
                    cancellationToken);

            throw new InvalidOperationException(
                $"Microsoft Azure Speech rejected the synthesis request " +
                $"({(int)response.StatusCode} {response.ReasonPhrase}): " +
                $"{errorBody}");
        }

        byte[] audioData =
            await response.Content.ReadAsByteArrayAsync(
                cancellationToken);

        if (audioData.Length == 0)
        {
            throw new InvalidOperationException(
                "Microsoft Azure Speech returned an empty audio response.");
        }

        return new TtsAudioResult(
            audioData,
            response.Content.Headers.ContentType?.MediaType ??
            "audio/wav");
    }

    public void RemoveConfiguration()
    {
        TtsCredentialStore.Delete(
            ProviderId);

        TtsVoiceCatalogStore.Delete(
            ProviderId);

        _subscriptionKey =
            null;

        _region =
            null;

        Status =
            TtsProviderStatus.NotConfigured;
    }

    private async Task<IReadOnlyList<TtsVoice>> FetchUsableVoicesAsync(
        string subscriptionKey,
        string region,
        CancellationToken cancellationToken)
    {
        Uri requestUri =
            new(
                $"https://{region}.tts.speech.microsoft.com/cognitiveservices/voices/list");

        using HttpRequestMessage request =
            new(
                HttpMethod.Get,
                requestUri);

        request.Headers.TryAddWithoutValidation(
            "Ocp-Apim-Subscription-Key",
            subscriptionKey);

        request.Headers.UserAgent.ParseAdd(
            "ChatOutLoud");

        using HttpResponseMessage response =
            await Http.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            string errorBody =
                await response.Content.ReadAsStringAsync(
                    cancellationToken);

            throw new InvalidOperationException(
                $"Microsoft Azure Speech voice retrieval failed " +
                $"({(int)response.StatusCode} {response.ReasonPhrase}): " +
                $"{errorBody}");
        }

        await using System.IO.Stream responseStream =
            await response.Content.ReadAsStreamAsync(
                cancellationToken);

        List<AzureVoiceInfo>? returnedVoices =
            await JsonSerializer.DeserializeAsync<
                List<AzureVoiceInfo>>(
                    responseStream,
                    JsonOptions,
                    cancellationToken);

        if (returnedVoices is null)
        {
            throw new InvalidOperationException(
                "Microsoft Azure Speech returned an invalid voice catalog.");
        }

        List<TtsVoice> usableVoices =
            [];

        foreach (AzureVoiceInfo sourceVoice in
                 returnedVoices)
        {
            if (!IsUsableVoice(
                    sourceVoice))
            {
                continue;
            }

            List<string> languageCodes =
                [];

            languageCodes.Add(
                sourceVoice.Locale!);

            if (sourceVoice.SecondaryLocaleList is not null)
            {
                languageCodes.AddRange(
                    sourceVoice.SecondaryLocaleList.Where(
                        locale =>
                            !string.IsNullOrWhiteSpace(
                                locale)));
            }

            languageCodes =
                languageCodes
                    .Distinct(
                        StringComparer.OrdinalIgnoreCase)
                    .ToList();

            string displayName =
                string.IsNullOrWhiteSpace(
                    sourceVoice.DisplayName)
                    ? sourceVoice.ShortName!
                    : $"{sourceVoice.DisplayName} ({sourceVoice.Locale})";

            usableVoices.Add(
                new TtsVoice(
                    ProviderId,
                    ProviderName,
                    sourceVoice.ShortName!,
                    displayName,
                    languageCodes));
        }

        return usableVoices
            .OrderBy(
                voice => voice.DisplayName,
                StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static bool IsUsableVoice(
        AzureVoiceInfo voice)
    {
        if (string.IsNullOrWhiteSpace(
                voice.ShortName) ||
            string.IsNullOrWhiteSpace(
                voice.Locale))
        {
            return false;
        }

        if (!string.Equals(
                voice.Status,
                "GA",
                StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!string.Equals(
                voice.VoiceType,
                "Neural",
                StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (voice.ShortName.Contains(
                "DragonHD",
                StringComparison.OrdinalIgnoreCase) ||
            voice.ShortName.EndsWith(
                "NeuralHD",
                StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return true;
    }

    private void TryLoadSavedCredential()
    {
        try
        {
            string? credentialJson =
                TtsCredentialStore.Load(
                    ProviderId);

            if (string.IsNullOrWhiteSpace(
                    credentialJson))
            {
                Status =
                    TtsProviderStatus.NotConfigured;

                return;
            }

            AzureCredential? credential =
                JsonSerializer.Deserialize<
                    AzureCredential>(
                    credentialJson,
                    JsonOptions);

            if (credential is null)
            {
                throw new InvalidOperationException(
                    "Saved Microsoft Azure Speech credentials are invalid.");
            }

            _subscriptionKey =
                NormalizeSubscriptionKey(
                    credential.SubscriptionKey);

            _region =
                NormalizeRegion(
                    credential.Region);

            Status =
                TtsProviderStatus.Configured;
        }
        catch
        {
            _subscriptionKey =
                null;

            _region =
                null;

            Status =
                TtsProviderStatus.NeedsAttention;
        }
    }

    private static string NormalizeSubscriptionKey(
        string subscriptionKey)
    {
        subscriptionKey =
            subscriptionKey?.Trim() ?? "";

        if (string.IsNullOrWhiteSpace(
                subscriptionKey))
        {
            throw new ArgumentException(
                "Microsoft Azure Speech key cannot be empty.",
                nameof(subscriptionKey));
        }

        return subscriptionKey;
    }

    private static string NormalizeRegion(
        string region)
    {
        region =
            region?.Trim().ToLowerInvariant() ?? "";

        if (string.IsNullOrWhiteSpace(
                region))
        {
            throw new ArgumentException(
                "Microsoft Azure Speech region cannot be empty.",
                nameof(region));
        }

        foreach (char character in region)
        {
            bool isAsciiLetter =
                character >= 'a' &&
                character <= 'z';

            bool isDigit =
                character >= '0' &&
                character <= '9';

            if (!isAsciiLetter &&
                !isDigit &&
                character != '-')
            {
                throw new ArgumentException(
                    "Microsoft Azure Speech region contains invalid characters.",
                    nameof(region));
            }
        }

        if (region.StartsWith(
                "-",
                StringComparison.Ordinal) ||
            region.EndsWith(
                "-",
                StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "Microsoft Azure Speech region is invalid.",
                nameof(region));
        }

        return region;
    }

    private sealed class AzureCredential
    {
        [JsonPropertyName(
            "subscriptionKey")]
        public string SubscriptionKey
        {
            get;
            set;
        } = "";

        [JsonPropertyName(
            "region")]
        public string Region
        {
            get;
            set;
        } = "";
    }

    private sealed class AzureVoiceInfo
    {
        [JsonPropertyName(
            "DisplayName")]
        public string? DisplayName
        {
            get;
            set;
        }

        [JsonPropertyName(
            "ShortName")]
        public string? ShortName
        {
            get;
            set;
        }

        [JsonPropertyName(
            "Locale")]
        public string? Locale
        {
            get;
            set;
        }

        [JsonPropertyName(
            "SecondaryLocaleList")]
        public List<string>? SecondaryLocaleList
        {
            get;
            set;
        }

        [JsonPropertyName(
            "VoiceType")]
        public string? VoiceType
        {
            get;
            set;
        }

        [JsonPropertyName(
            "Status")]
        public string? Status
        {
            get;
            set;
        }
    }
}
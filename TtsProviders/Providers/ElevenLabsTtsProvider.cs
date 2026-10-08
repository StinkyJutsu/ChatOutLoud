using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace ChatOutLoud.TtsProviders.Providers;

internal sealed class ElevenLabsTtsProvider : ITtsProvider
{
    private const string ElevenLabsProviderId =
        "elevenlabs";

    private const string ElevenLabsModelId =
        "eleven_multilingual_v2";

    private static readonly HttpClient Http =
        new()
        {
            BaseAddress =
                new Uri(
                    "https://api.elevenlabs.io/")
        };

    private string? _apiKey;

    public ElevenLabsTtsProvider()
    {
        TryLoadSavedCredential();
    }

    public string ProviderId =>
        ElevenLabsProviderId;

    public string ProviderName =>
        "ElevenLabs";

    public TtsProviderStatus Status
    {
        get;
        private set;
    } = TtsProviderStatus.NotConfigured;

    public async Task<bool> ConfigureAsync(
        string apiKey,
        CancellationToken cancellationToken = default)
    {
        apiKey =
            NormalizeApiKey(
                apiKey);

        Status =
            TtsProviderStatus.Checking;

        try
        {
            IReadOnlyList<TtsVoice> voices =
                await FetchUsableVoicesAsync(
                    apiKey,
                    cancellationToken);

            TtsCredentialStore.Save(
                ProviderId,
                apiKey);

            TtsVoiceCatalogStore.Save(
                ProviderId,
                voices);

            _apiKey =
                apiKey;

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
                _apiKey))
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
                    _apiKey,
                    cancellationToken);

            TtsVoiceCatalogStore.Save(
                ProviderId,
                voices);

            Status =
                TtsProviderStatus.Connected;

            return true;
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
                _apiKey))
        {
            return Array.Empty<TtsVoice>();
        }

        IReadOnlyList<TtsVoice> voices =
            await FetchUsableVoicesAsync(
                _apiKey,
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
                _apiKey))
        {
            throw new InvalidOperationException(
                "ElevenLabs is not configured.");
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
                "The selected voice does not belong to ElevenLabs.");
        }

        string requestPath =
            $"v1/text-to-speech/{Uri.EscapeDataString(voice.VoiceId)}" +
            "?output_format=mp3_44100_128";

        string requestJson =
            JsonSerializer.Serialize(
                new
                {
                    text,
                    model_id =
                        ElevenLabsModelId
                });

        using HttpRequestMessage request =
            CreateRequest(
                HttpMethod.Post,
                requestPath,
                _apiKey);

        request.Content =
            new StringContent(
                requestJson,
                Encoding.UTF8,
                "application/json");

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

            string? errorCode =
                null;

            string errorMessage =
                errorBody;

            try
            {
                using JsonDocument errorJson =
                    JsonDocument.Parse(
                        errorBody);

                if (errorJson.RootElement.TryGetProperty(
                        "detail",
                        out JsonElement detail) &&
                    detail.ValueKind ==
                        JsonValueKind.Object)
                {
                    if (detail.TryGetProperty(
                            "code",
                            out JsonElement codeElement))
                    {
                        errorCode =
                            codeElement.GetString();
                    }

                    if (detail.TryGetProperty(
                            "message",
                            out JsonElement messageElement))
                    {
                        errorMessage =
                            messageElement.GetString() ??
                            errorBody;
                    }
                }
            }
            catch (JsonException)
            {
            }

            string errorCodeText =
                string.IsNullOrWhiteSpace(
                    errorCode)
                    ? ""
                    : $" [{errorCode}]";

            throw new InvalidOperationException(
                $"ElevenLabs TTS rejected this request" +
                $"{errorCodeText}: {errorMessage}");
        }

        byte[] audioData =
            await response.Content.ReadAsByteArrayAsync(
                cancellationToken);

        if (audioData.Length == 0)
        {
            throw new InvalidOperationException(
                "ElevenLabs returned an empty audio response.");
        }

        return new TtsAudioResult(
            audioData,
            response.Content.Headers.ContentType?.MediaType ??
            "audio/mpeg");
    }

    public void RemoveConfiguration()
    {
        TtsCredentialStore.Delete(
            ProviderId);

        TtsVoiceCatalogStore.Delete(
            ProviderId);

        _apiKey =
            null;

        Status =
            TtsProviderStatus.NotConfigured;
    }

    private async Task<IReadOnlyList<TtsVoice>> FetchUsableVoicesAsync(
        string apiKey,
        CancellationToken cancellationToken)
    {
        string subscriptionTier =
            await FetchSubscriptionTierAsync(
                apiKey,
                cancellationToken);

        List<ElevenLabsVoice> returnedVoices =
            [];

        returnedVoices.AddRange(
            await FetchVoiceTypeAsync(
                apiKey,
                "default",
                cancellationToken));

        bool isFreeTier =
            string.Equals(
                subscriptionTier,
                "trial",
                StringComparison.OrdinalIgnoreCase) ||
            string.Equals(
                subscriptionTier,
                "free",
                StringComparison.OrdinalIgnoreCase);

        returnedVoices.AddRange(
            await FetchVoiceTypeAsync(
                apiKey,
                isFreeTier
                    ? "non-community"
                    : "non-default",
                cancellationToken));

        Dictionary<string, TtsVoice> usableVoices =
            new(
                StringComparer.Ordinal);

        foreach (ElevenLabsVoice sourceVoice in
                 returnedVoices)
        {
            if (!IsUsableVoice(
                    sourceVoice,
                    subscriptionTier))
            {
                continue;
            }

            List<string> languageCodes =
                sourceVoice.VerifiedLanguages
                    .Where(
                        language =>
                            !string.IsNullOrWhiteSpace(
                                language.Locale) ||
                            !string.IsNullOrWhiteSpace(
                                language.Language))
                    .Select(
                        language =>
                            !string.IsNullOrWhiteSpace(
                                language.Locale)
                                ? language.Locale!
                                : language.Language!)
                    .Distinct(
                        StringComparer.OrdinalIgnoreCase)
                    .ToList();

            usableVoices[sourceVoice.VoiceId!] =
                new TtsVoice(
                    ProviderId,
                    ProviderName,
                    sourceVoice.VoiceId!,
                    sourceVoice.Name!,
                    languageCodes);
        }

        return usableVoices.Values
            .OrderBy(
                voice => voice.DisplayName,
                StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private async Task<string> FetchSubscriptionTierAsync(
        string apiKey,
        CancellationToken cancellationToken)
    {
        using HttpRequestMessage request =
            CreateRequest(
                HttpMethod.Get,
                "v1/user/subscription",
                apiKey);

        using HttpResponseMessage response =
            await Http.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                "ElevenLabs could not read the account subscription. " +
                "Make sure the API key has User Access enabled.");
        }

        await using System.IO.Stream responseStream =
            await response.Content.ReadAsStreamAsync(
                cancellationToken);

        using JsonDocument subscription =
            await JsonDocument.ParseAsync(
                responseStream,
                cancellationToken:
                    cancellationToken);

        if (!subscription.RootElement.TryGetProperty(
                "tier",
                out JsonElement tierElement))
        {
            throw new InvalidOperationException(
                "ElevenLabs did not return the account subscription tier.");
        }

        string? tier =
            tierElement.GetString();

        if (string.IsNullOrWhiteSpace(
                tier))
        {
            throw new InvalidOperationException(
                "ElevenLabs returned an invalid subscription tier.");
        }

        return tier.Trim();
    }
    private async Task<List<ElevenLabsVoice>> FetchVoiceTypeAsync(
        string apiKey,
        string voiceType,
        CancellationToken cancellationToken)
    {
        List<ElevenLabsVoice> voices =
            [];

        string? nextPageToken =
            null;

        do
        {
            string requestPath =
                "v2/voices" +
                "?page_size=100" +
                "&include_total_count=false" +
                "&sort=name" +
                "&sort_direction=asc" +
                $"&voice_type={Uri.EscapeDataString(voiceType)}";

            if (!string.IsNullOrWhiteSpace(
                    nextPageToken))
            {
                requestPath +=
                    $"&next_page_token={Uri.EscapeDataString(nextPageToken)}";
            }

            using HttpRequestMessage request =
                CreateRequest(
                    HttpMethod.Get,
                    requestPath,
                    apiKey);

            using HttpResponseMessage response =
                await Http.SendAsync(
                    request,
                    HttpCompletionOption.ResponseHeadersRead,
                    cancellationToken);

            response.EnsureSuccessStatusCode();

            await using System.IO.Stream responseStream =
                await response.Content.ReadAsStreamAsync(
                    cancellationToken);

            ElevenLabsVoiceListResponse? result =
                await JsonSerializer.DeserializeAsync<
                    ElevenLabsVoiceListResponse>(
                    responseStream,
                    cancellationToken:
                        cancellationToken);

            if (result is null)
            {
                break;
            }

            voices.AddRange(
                result.Voices);

            nextPageToken =
                result.HasMore
                    ? result.NextPageToken
                    : null;
        }
        while (!string.IsNullOrWhiteSpace(
            nextPageToken));

        return voices;
    }

    private static bool IsUsableVoice(
        ElevenLabsVoice voice,
        string subscriptionTier)
    {
        if (string.IsNullOrWhiteSpace(
                voice.VoiceId) ||
            string.IsNullOrWhiteSpace(
                voice.Name))
        {
            return false;
        }

        if (voice.AvailableForTiers is
            {
                Count: > 0
            } availableForTiers &&
            !availableForTiers.Any(
                tier =>
                    string.Equals(
                        tier,
                        subscriptionTier,
                        StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        if (voice.VoiceVerification is
            {
                RequiresVerification: true
            } &&
            voice.VoiceVerification.IsVerified != true)
        {
            return false;
        }

        if (string.Equals(
                voice.Category,
                "professional",
                StringComparison.OrdinalIgnoreCase) &&
            voice.FineTuning?.State is
            {
                Count: > 0
            } states &&
            !states.Values.Any(
                state =>
                    string.Equals(
                        state,
                        "fine_tuned",
                        StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        return true;
    }

    private static HttpRequestMessage CreateRequest(
        HttpMethod method,
        string requestPath,
        string apiKey)
    {
        HttpRequestMessage request =
            new(
                method,
                requestPath);

        request.Headers.Add(
            "xi-api-key",
            apiKey);

        return request;
    }

    private void TryLoadSavedCredential()
    {
        try
        {
            string? apiKey =
                TtsCredentialStore.Load(
                    ProviderId);

            if (string.IsNullOrWhiteSpace(
                    apiKey))
            {
                Status =
                    TtsProviderStatus.NotConfigured;

                return;
            }

            _apiKey =
                NormalizeApiKey(
                    apiKey);

            Status =
                TtsProviderStatus.Configured;
        }
        catch
        {
            _apiKey =
                null;

            Status =
                TtsProviderStatus.NeedsAttention;
        }
    }

    private static string NormalizeApiKey(
        string apiKey)
    {
        if (string.IsNullOrWhiteSpace(
                apiKey))
        {
            throw new ArgumentException(
                "ElevenLabs API key cannot be empty.",
                nameof(apiKey));
        }

        return apiKey.Trim();
    }

    private sealed class ElevenLabsVoiceListResponse
    {
        [JsonPropertyName("voices")]
        public List<ElevenLabsVoice> Voices
        {
            get;
            set;
        } = [];

        [JsonPropertyName("has_more")]
        public bool HasMore
        {
            get;
            set;
        }

        [JsonPropertyName("next_page_token")]
        public string? NextPageToken
        {
            get;
            set;
        }
    }

    private sealed class ElevenLabsVoice
    {
        [JsonPropertyName("voice_id")]
        public string? VoiceId
        {
            get;
            set;
        }

        [JsonPropertyName("name")]
        public string? Name
        {
            get;
            set;
        }

        [JsonPropertyName("category")]
        public string? Category
        {
            get;
            set;
        }

        [JsonPropertyName("available_for_tiers")]
        public List<string>? AvailableForTiers
        {
            get;
            set;
        }

        [JsonPropertyName("fine_tuning")]
        public ElevenLabsFineTuning? FineTuning
        {
            get;
            set;
        }

        [JsonPropertyName("voice_verification")]
        public ElevenLabsVoiceVerification? VoiceVerification
        {
            get;
            set;
        }

        [JsonPropertyName("verified_languages")]
        public List<ElevenLabsVerifiedLanguage> VerifiedLanguages
        {
            get;
            set;
        } = [];
    }

    private sealed class ElevenLabsFineTuning
    {
        [JsonPropertyName("state")]
        public Dictionary<string, string> State
        {
            get;
            set;
        } = [];
    }

    private sealed class ElevenLabsVoiceVerification
    {
        [JsonPropertyName("requires_verification")]
        public bool RequiresVerification
        {
            get;
            set;
        }

        [JsonPropertyName("is_verified")]
        public bool? IsVerified
        {
            get;
            set;
        }
    }

    private sealed class ElevenLabsVerifiedLanguage
    {
        [JsonPropertyName("language")]
        public string? Language
        {
            get;
            set;
        }

        [JsonPropertyName("locale")]
        public string? Locale
        {
            get;
            set;
        }
    }
}
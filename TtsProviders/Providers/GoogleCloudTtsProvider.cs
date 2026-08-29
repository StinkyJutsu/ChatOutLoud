using Google.Cloud.TextToSpeech.V1;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ChatOutLoud.TtsProviders.Providers;

internal sealed class GoogleCloudTtsProvider : ITtsProvider
{
    private const string GoogleProviderId =
        "google-cloud";

    private TextToSpeechClient? _client;

    public GoogleCloudTtsProvider()
    {
        TryLoadSavedCredential();
    }

    public string ProviderId =>
        GoogleProviderId;

    public string ProviderName =>
        "Google Cloud TTS";

    public TtsProviderStatus Status
    {
        get;
        private set;
    } = TtsProviderStatus.NotConfigured;

    public async Task<bool> ConfigureAsync(
        string credentialJson,
        CancellationToken cancellationToken = default)
    {
        ValidateCredentialJson(
            credentialJson);

        Status =
            TtsProviderStatus.Checking;

        TextToSpeechClient candidateClient =
            CreateClient(
                credentialJson);

        try
        {
            ListVoicesResponse response =
                await candidateClient.ListVoicesAsync(
                    new ListVoicesRequest(),
                    cancellationToken);

            if (response.Voices.Count == 0)
            {
                Status =
                    TtsProviderStatus.NeedsAttention;

                return false;
            }

            List<TtsVoice> voices =
                ConvertVoices(
                    response);

            TtsCredentialStore.Save(
                ProviderId,
                credentialJson);

            TtsVoiceCatalogStore.Save(
                ProviderId,
                voices);

            _client =
                candidateClient;

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
        if (_client is null)
        {
            Status =
                TtsProviderStatus.NotConfigured;

            return false;
        }

        Status =
            TtsProviderStatus.Checking;

        try
        {
            ListVoicesResponse response =
                await _client.ListVoicesAsync(
                    new ListVoicesRequest(),
                    cancellationToken);

            bool connected =
                response.Voices.Count > 0;

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
        if (_client is null)
        {
            return Array.Empty<TtsVoice>();
        }

        ListVoicesResponse response =
            await _client.ListVoicesAsync(
                new ListVoicesRequest(),
                cancellationToken);

        List<TtsVoice> voices =
            ConvertVoices(
                response);

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
        if (_client is null)
        {
            throw new InvalidOperationException(
                "Google Cloud TTS is not configured.");
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            throw new ArgumentException(
                "Speech text cannot be empty.",
                nameof(text));
        }

        ArgumentNullException.ThrowIfNull(voice);

        if (!string.Equals(
                voice.ProviderId,
                ProviderId,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "The selected voice does not belong to Google Cloud TTS.");
        }

        if (string.IsNullOrWhiteSpace(
                voice.VoiceId))
        {
            throw new InvalidOperationException(
                "The selected Google voice does not have a valid voice ID.");
        }

        if (voice.LanguageCodes.Count == 0 ||
            string.IsNullOrWhiteSpace(
                voice.LanguageCodes[0]))
        {
            throw new InvalidOperationException(
                "The selected Google voice does not have a valid language code.");
        }

        SynthesizeSpeechRequest request =
            new()
            {
                Input =
                    new SynthesisInput
                    {
                        Text =
                            text
                    },

                Voice =
                    new VoiceSelectionParams
                    {
                        Name =
                            voice.VoiceId,

                        LanguageCode =
                            voice.LanguageCodes[0],

                        ModelName =
                            voice.VoiceId.Contains(
                                '-',
                                StringComparison.Ordinal)
                                ? ""
                                : "gemini-2.5-flash-tts"
                    },

                AudioConfig =
                    new AudioConfig
                    {
                        AudioEncoding =
                            AudioEncoding.Linear16
                    }
            };

        SynthesizeSpeechResponse response =
            await _client.SynthesizeSpeechAsync(
                request,
                cancellationToken);

        return new TtsAudioResult(
            response.AudioContent.ToByteArray(),
            "audio/wav");
    }

    public void RemoveConfiguration()
    {
        TtsCredentialStore.Delete(
            ProviderId);

        TtsVoiceCatalogStore.Delete(
            ProviderId);

        _client = null;

        Status =
            TtsProviderStatus.NotConfigured;
    }

    private List<TtsVoice> ConvertVoices(
        ListVoicesResponse response)
    {
        List<TtsVoice> voices =
            new(response.Voices.Count);

        foreach (Voice googleVoice in
                 response.Voices)
        {
            if (!googleVoice.Name.Contains(
                    '-',
                    StringComparison.Ordinal))
            {
                continue;
            }

            List<string> languageCodes =
                new(googleVoice.LanguageCodes);

            voices.Add(
                new TtsVoice(
                    ProviderId,
                    ProviderName,
                    googleVoice.Name,
                    googleVoice.Name,
                    languageCodes));
        }

        return voices;
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

            ValidateCredentialJson(
                credentialJson);

            _client =
                CreateClient(
                    credentialJson);

            Status =
                TtsProviderStatus.Configured;
        }
        catch
        {
            _client = null;

            Status =
                TtsProviderStatus.NeedsAttention;
        }
    }

    private static TextToSpeechClient CreateClient(
        string credentialJson)
    {
        Google.Apis.Auth.OAuth2.ServiceAccountCredential serviceAccountCredential =
            Google.Apis.Auth.OAuth2.CredentialFactory
                .FromJson<Google.Apis.Auth.OAuth2.ServiceAccountCredential>(
                    credentialJson);

        Google.Apis.Auth.OAuth2.GoogleCredential googleCredential =
            serviceAccountCredential.ToGoogleCredential();

        TextToSpeechClientBuilder builder =
            new()
            {
                GoogleCredential =
                    googleCredential
            };

        return builder.Build();
    }

    private static void ValidateCredentialJson(
        string credentialJson)
    {
        if (string.IsNullOrWhiteSpace(
                credentialJson))
        {
            throw new ArgumentException(
                "Google credential data is empty.",
                nameof(credentialJson));
        }

        using JsonDocument document =
            JsonDocument.Parse(
                credentialJson);

        JsonElement root =
            document.RootElement;

        if (root.ValueKind !=
            JsonValueKind.Object ||
            !HasStringProperty(
                root,
                "type",
                out string type) ||
            !string.Equals(
                type,
                "service_account",
                StringComparison.Ordinal) ||
            !HasStringProperty(
                root,
                "project_id",
                out _) ||
            !HasStringProperty(
                root,
                "client_email",
                out _) ||
            !HasStringProperty(
                root,
                "private_key",
                out _))
        {
            throw new InvalidOperationException(
                "The selected file is not a valid Google Cloud service-account credential.");
        }
    }

    private static bool HasStringProperty(
        JsonElement root,
        string propertyName,
        out string value)
    {
        value = "";

        if (!root.TryGetProperty(
                propertyName,
                out JsonElement property) ||
            property.ValueKind !=
                JsonValueKind.String)
        {
            return false;
        }

        value =
            property.GetString() ?? "";

        return !string.IsNullOrWhiteSpace(
            value);
    }
}
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace ChatOutLoud.TtsProviders;

internal static class TtsVoiceCatalogStore
{
    private static readonly string CatalogDirectory =
        Path.Combine(
            Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData),
            "ChatOutLoud",
            "TtsProviders");

    public static void Save(
        string providerId,
        IReadOnlyList<TtsVoice> voices)
    {
        ArgumentNullException.ThrowIfNull(voices);

        string path =
            GetCatalogPath(providerId);

        Directory.CreateDirectory(
            CatalogDirectory);

        List<StoredTtsVoice> storedVoices =
            new(voices.Count);

        foreach (TtsVoice voice in voices)
        {
            storedVoices.Add(
                new StoredTtsVoice
                {
                    ProviderId =
                        voice.ProviderId,

                    ProviderName =
                        voice.ProviderName,

                    VoiceId =
                        voice.VoiceId,

                    DisplayName =
                        voice.DisplayName,

                    LanguageCodes =
                        new List<string>(
                            voice.LanguageCodes)
                });
        }

        string json =
            JsonSerializer.Serialize(
                storedVoices,
                new JsonSerializerOptions
                {
                    WriteIndented = true
                });

        File.WriteAllText(
            path,
            json);
    }

    public static IReadOnlyList<TtsVoice> Load(
        string providerId)
    {
        try
        {
            string path =
                GetCatalogPath(providerId);

            if (!File.Exists(path))
            {
                return Array.Empty<TtsVoice>();
            }

            string json =
                File.ReadAllText(path);

            List<StoredTtsVoice>? storedVoices =
                JsonSerializer.Deserialize<
                    List<StoredTtsVoice>>(json);

            if (storedVoices is null)
            {
                return Array.Empty<TtsVoice>();
            }

            List<TtsVoice> voices =
                new(storedVoices.Count);

            foreach (StoredTtsVoice storedVoice
                     in storedVoices)
            {
                if (!string.Equals(
                        storedVoice.ProviderId,
                        providerId,
                        StringComparison.OrdinalIgnoreCase) ||
                    string.IsNullOrWhiteSpace(
                        storedVoice.VoiceId) ||
                    string.IsNullOrWhiteSpace(
                        storedVoice.DisplayName))
                {
                    continue;
                }

                voices.Add(
                    new TtsVoice(
                        storedVoice.ProviderId,
                        storedVoice.ProviderName,
                        storedVoice.VoiceId,
                        storedVoice.DisplayName,
                        storedVoice.LanguageCodes));
            }

            return voices;
        }
        catch
        {
            return Array.Empty<TtsVoice>();
        }
    }

    public static void Delete(
        string providerId)
    {
        string path =
            GetCatalogPath(providerId);

        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    private static string GetCatalogPath(
        string providerId)
    {
        if (string.IsNullOrWhiteSpace(providerId))
        {
            throw new ArgumentException(
                "Provider ID cannot be empty.",
                nameof(providerId));
        }

        foreach (char character in providerId)
        {
            if (!char.IsLetterOrDigit(character) &&
                character != '-' &&
                character != '_')
            {
                throw new ArgumentException(
                    "Provider ID contains invalid characters.",
                    nameof(providerId));
            }
        }

        return Path.Combine(
            CatalogDirectory,
            $"{providerId}-voices.json");
    }

    private sealed class StoredTtsVoice
    {
        public string ProviderId { get; set; } = "";

        public string ProviderName { get; set; } = "";

        public string VoiceId { get; set; } = "";

        public string DisplayName { get; set; } = "";

        public List<string> LanguageCodes { get; set; } =
            [];
    }
}
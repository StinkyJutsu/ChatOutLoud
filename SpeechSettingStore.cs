using System;
using System.IO;
using System.Text.Json;

namespace ChatOutLoud;

internal static class SpeechSettingsStore
{
    private static readonly string SettingsFolder =
        Path.Combine(
            Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData),
            "ChatOutLoud");

    private static readonly string SettingsFile =
        Path.Combine(
            SettingsFolder,
            "speech-settings.json");

    public static SpeechSettings Load()
    {
        try
        {
            if (!File.Exists(SettingsFile))
            {
                return new SpeechSettings();
            }

            string json =
                File.ReadAllText(SettingsFile);

            return JsonSerializer.Deserialize<SpeechSettings>(json)
                   ?? new SpeechSettings();
        }
        catch
        {
            return new SpeechSettings();
        }
    }

    public static void Save(
        SpeechSettings settings)
    {
        Directory.CreateDirectory(
            SettingsFolder);

        string json =
            JsonSerializer.Serialize(
                settings,
                new JsonSerializerOptions
                {
                    WriteIndented = true
                });

        File.WriteAllText(
            SettingsFile,
            json);
    }
}

internal sealed class SpeechSettings
{
    public bool SpeakUsernames { get; set; } = true;

    public string? VoiceName { get; set; }

    public double Volume { get; set; } = 100.0;

    public double SpeechSpeed { get; set; }

    public bool MuteLinks { get; set; }

    public bool MuteCommands { get; set; }

    public bool MuteNumbers { get; set; }

    public bool MuteSpecialSymbols { get; set; }

    public bool MuteEmotes { get; set; }

    public bool EnableJoinSound { get; set; }

    public bool EnableJoinUsername { get; set; }

    public double JoinSoundVolume { get; set; } = 100.0;
}
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace ChatOutLoud;

internal static class MutedPhraseStore
{
    private static readonly string SettingsFolder =
        Path.Combine(
            Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData),
            "ChatOutLoud");

    private static readonly string MutedPhrasesPath =
        Path.Combine(
            SettingsFolder,
            "muted-phrases.json");

    public static List<string> Load()
    {
        if (!File.Exists(MutedPhrasesPath))
        {
            return [];
        }

        try
        {
            string json =
                File.ReadAllText(MutedPhrasesPath);

            return JsonSerializer.Deserialize<List<string>>(json)
                ?? [];
        }
        catch
        {
            return [];
        }
    }

    public static void Save(
        IEnumerable<string> phrases)
    {
        Directory.CreateDirectory(
            SettingsFolder);

        string json =
            JsonSerializer.Serialize(
                phrases,
                new JsonSerializerOptions
                {
                    WriteIndented = true
                });

        File.WriteAllText(
            MutedPhrasesPath,
            json);
    }
}
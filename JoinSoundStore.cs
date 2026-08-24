using System;
using System.IO;

namespace ChatOutLoud;

internal static class JoinSoundStore
{
    private static readonly string SettingsFolder =
        Path.Combine(
            Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData),
            "ChatOutLoud");

    private static readonly string SettingsFile =
        Path.Combine(
            SettingsFolder,
            "join-sound.txt");

    public static string? Load()
    {
        try
        {
            if (!File.Exists(SettingsFile))
            {
                return null;
            }

            string path =
                File.ReadAllText(SettingsFile).Trim();

            return string.IsNullOrWhiteSpace(path)
                ? null
                : path;
        }
        catch
        {
            return null;
        }
    }

    public static void Save(
        string path)
    {
        Directory.CreateDirectory(
            SettingsFolder);

        File.WriteAllText(
            SettingsFile,
            path);
    }
}
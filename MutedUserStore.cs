using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace ChatOutLoud;

internal static class MutedUserStore
{
    private static readonly string SettingsFolder =
        Path.Combine(
            Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData),
            "ChatOutLoud");

    private static readonly string MutedUsersPath =
        Path.Combine(
            SettingsFolder,
            "muted-users.json");

    public static HashSet<string> Load()
    {
        if (!File.Exists(MutedUsersPath))
        {
            return new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);
        }

        try
        {
            string json =
                File.ReadAllText(MutedUsersPath);

            List<string>? users =
                JsonSerializer.Deserialize<List<string>>(json);

            return new HashSet<string>(
                users ?? [],
                StringComparer.OrdinalIgnoreCase);
        }
        catch
        {
            return new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);
        }
    }

    public static void Save(
        IEnumerable<string> users)
    {
        Directory.CreateDirectory(
            SettingsFolder);

        string json =
            JsonSerializer.Serialize(
                users,
                new JsonSerializerOptions
                {
                    WriteIndented = true
                });

        File.WriteAllText(
            MutedUsersPath,
            json);
    }
}
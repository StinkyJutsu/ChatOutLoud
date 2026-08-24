using System.IO;
using System.Text.Json;

namespace ChatOutLoud;

internal static class AudioOutputStore
{
    private static readonly string FolderPath =
        Path.Combine(
            Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData),
            "ChatOutLoud");

    private static readonly string FilePath =
        Path.Combine(
            FolderPath,
            "audio-output.json");

    public static void Save(
        string? deviceId)
    {
        Directory.CreateDirectory(
            FolderPath);

        string json =
            JsonSerializer.Serialize(
                deviceId);

        File.WriteAllText(
            FilePath,
            json);
    }

    public static string? Load()
    {
        if (!File.Exists(FilePath))
        {
            return null;
        }

        try
        {
            string json =
                File.ReadAllText(
                    FilePath);

            return JsonSerializer.Deserialize<string?>(
                json);
        }
        catch
        {
            return null;
        }
    }
}
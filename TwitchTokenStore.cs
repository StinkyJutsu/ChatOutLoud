using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ChatOutLoud;

public static class TwitchTokenStore
{
    private static readonly string StorageFolder =
        Path.Combine(
            Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData),
            "ChatOutLoud");

    private static readonly string StorageFile =
        Path.Combine(
            StorageFolder,
            "twitch-auth.dat");

    private static readonly byte[] Entropy =
        Encoding.UTF8.GetBytes(
            "ChatOutLoud-Twitch-Authentication");

    public static void Save(
        string accessToken,
        string refreshToken)
    {
        Directory.CreateDirectory(StorageFolder);

        StoredTwitchTokens tokens = new()
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken
        };

        string json =
            JsonSerializer.Serialize(tokens);

        byte[] plainBytes =
            Encoding.UTF8.GetBytes(json);

        byte[] encryptedBytes =
            ProtectedData.Protect(
                plainBytes,
                Entropy,
                DataProtectionScope.CurrentUser);

        File.WriteAllBytes(
            StorageFile,
            encryptedBytes);
    }

    public static StoredTwitchTokens? Load()
    {
        if (!File.Exists(StorageFile))
        {
            return null;
        }

        try
        {
            byte[] encryptedBytes =
                File.ReadAllBytes(StorageFile);

            byte[] plainBytes =
                ProtectedData.Unprotect(
                    encryptedBytes,
                    Entropy,
                    DataProtectionScope.CurrentUser);

            string json =
                Encoding.UTF8.GetString(plainBytes);

            return JsonSerializer.Deserialize<StoredTwitchTokens>(
                json);
        }
        catch
        {
            Delete();
            return null;
        }
    }

    public static void Delete()
    {
        if (File.Exists(StorageFile))
        {
            File.Delete(StorageFile);
        }
    }
}

public sealed class StoredTwitchTokens
{
    public string AccessToken { get; set; } = "";

    public string RefreshToken { get; set; } = "";
}
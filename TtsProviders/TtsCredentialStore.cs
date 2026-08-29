using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace ChatOutLoud.TtsProviders;

internal static class TtsCredentialStore
{
    private static readonly string CredentialDirectory =
        Path.Combine(
            Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData),
            "ChatOutLoud",
            "TtsProviders");

    public static void Save(
        string providerId,
        string credentialText)
    {
        if (string.IsNullOrWhiteSpace(credentialText))
        {
            throw new ArgumentException(
                "Credential data cannot be empty.",
                nameof(credentialText));
        }

        string path =
            GetCredentialPath(providerId);

        Directory.CreateDirectory(
            CredentialDirectory);

        byte[] plainBytes =
            Encoding.UTF8.GetBytes(
                credentialText);

        byte[] protectedBytes =
            ProtectedData.Protect(
                plainBytes,
                null,
                DataProtectionScope.CurrentUser);

        File.WriteAllBytes(
            path,
            protectedBytes);
    }

    public static string? Load(
        string providerId)
    {
        string path =
            GetCredentialPath(providerId);

        if (!File.Exists(path))
        {
            return null;
        }

        byte[] protectedBytes =
            File.ReadAllBytes(path);

        byte[] plainBytes =
            ProtectedData.Unprotect(
                protectedBytes,
                null,
                DataProtectionScope.CurrentUser);

        return Encoding.UTF8.GetString(
            plainBytes);
    }

    public static void Delete(
        string providerId)
    {
        string path =
            GetCredentialPath(providerId);

        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    private static string GetCredentialPath(
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
            CredentialDirectory,
            $"{providerId}.bin");
    }
}
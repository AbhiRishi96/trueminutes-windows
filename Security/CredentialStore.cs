using System.Security.Cryptography;
using System.Text;

namespace TrueMinutes.Windows.Security;

/// Secure credential storage using Windows Data Protection API (DPAPI).
/// Windows equivalent of macOS KeychainStore.swift.
///
/// DPAPI encrypts with the current user's credentials so only this user account
/// can decrypt — equivalent to kSecAttrAccessibleAfterFirstUnlockThisDeviceOnly.
/// Secrets are stored as encrypted files in %APPDATA%\TrueMinutes\.secrets\
/// (alternatively use Windows Credential Manager for cross-process sharing).
public static class CredentialStore
{
    public enum Key
    {
        OllamaBaseUrl,
        OpenAIApiKey,
        AnthropicApiKey,
        GroqApiKey,
        OpenRouterApiKey,
        GoogleCalendarTokens,
        OutlookCalendarTokens,
        NotionToken,
        SlackWebhookUrl,
    }

    private static readonly string SecretsDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "TrueMinutes", ".secrets");

    /// Read a stored secret. Returns null if not found.
    public static string? Read(Key key)
    {
        var path = FilePath(key);
        if (!File.Exists(path)) return null;
        try
        {
            var encrypted = File.ReadAllBytes(path);
            var plain = ProtectedData.Unprotect(encrypted, Entropy(key), DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(plain);
        }
        catch { return null; }
    }

    /// Persist a secret. Throws on file I/O errors.
    public static void Save(Key key, string value)
    {
        Directory.CreateDirectory(SecretsDir);
        var plain = Encoding.UTF8.GetBytes(value);
        var encrypted = ProtectedData.Protect(plain, Entropy(key), DataProtectionScope.CurrentUser);
        File.WriteAllBytes(FilePath(key), encrypted);
    }

    /// Remove a stored secret.
    public static void Delete(Key key)
    {
        var path = FilePath(key);
        if (File.Exists(path)) File.Delete(path);
    }

    // Use a per-key entropy salt so DPAPI blobs are key-scoped.
    private static byte[] Entropy(Key key) =>
        Encoding.UTF8.GetBytes($"TrueMinutes.{key}");

    private static string FilePath(Key key) =>
        Path.Combine(SecretsDir, $"{key}.dat");
}

using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace LuaToolsGui.Services;

/// <summary>
/// Versioned settings backup. Never exports authentication material (Hubcap API keys or session tokens).
/// Restoring preserves the device's existing key, with a checksum and schema gate before any mutation.
/// </summary>
internal sealed class SettingsBackupService
{
    private const int SchemaVersion = 1;
    private const int MaxBackupBytes = 256 * 1024;
    private readonly SettingsService _settings;
    private readonly DiagnosticsService _diagnostics;

    internal SettingsBackupService(SettingsService settings, DiagnosticsService? diagnostics = null)
    {
        _settings = settings;
        _diagnostics = diagnostics ?? DiagnosticsService.Default;
    }

    private sealed class Envelope
    {
        public int Version { get; set; }
        public DateTimeOffset CreatedUtc { get; set; }
        public AppSettings? Settings { get; set; }
        public string? Sha256 { get; set; }
    }

    /// <summary>Write a portable backup without the locally stored API key.</summary>
    internal void Export(string backupPath)
    {
        try
        {
            GuardSettingsFile(backupPath);
            AppSettings snapshot = _settings.Snapshot();
            snapshot.HubcapApiKey = null;

            var payload = JsonSerializer.Serialize(snapshot);
            var envelope = new Envelope
            {
                Version = SchemaVersion,
                CreatedUtc = DateTimeOffset.UtcNow,
                Settings = snapshot,
                Sha256 = Hash(payload)
            };

            string output = JsonSerializer.Serialize(envelope,
                new JsonSerializerOptions { WriteIndented = true });
            if (Encoding.UTF8.GetByteCount(output) > MaxBackupBytes)
                throw new InvalidDataException("Settings backup is too large.");

            DiagnosticsService.AtomicWrite(backupPath, output);
            _diagnostics.Record(DiagnosticEvent.SettingsExported);
        }
        catch
        {
            _diagnostics.Record(DiagnosticEvent.SettingsExportFailed);
            throw;
        }
    }

    /// <summary>
    /// Validate a backup before touching the active settings. The original local API key is preserved.
    /// Invalid and future-version backups are rejected without modifying the current configuration.
    /// </summary>
    internal void Restore(string backupPath)
    {
        try
        {
            string json;
            using (var stream = new FileStream(backupPath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                if (stream.Length > MaxBackupBytes)
                    throw new InvalidDataException("Settings backup exceeds the size limit.");

                using var reader = new StreamReader(stream, Encoding.UTF8, true);
                json = reader.ReadToEnd();
            }

            Envelope? envelope;
            try { envelope = JsonSerializer.Deserialize<Envelope>(json); }
            catch (JsonException ex)
            {
                throw new InvalidDataException("Invalid settings backup format.", ex);
            }

            if (envelope?.Version != SchemaVersion || envelope.Settings is null)
                throw new InvalidDataException("Unsupported settings backup schema.");

            // A backup is never allowed to import a credential, even if manually edited.
            if (!string.IsNullOrEmpty(envelope.Settings.HubcapApiKey))
                throw new InvalidDataException("Settings backup unexpectedly contains a secret.");

            string payload = JsonSerializer.Serialize(envelope.Settings);
            string expected = Hash(payload);
            if (envelope.Sha256 is null ||
                !string.Equals(envelope.Sha256, expected, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Settings backup checksum mismatch.");

            _settings.ApplyRestoredSettings(envelope.Settings);
            _diagnostics.Record(DiagnosticEvent.SettingsRestored);
        }
        catch
        {
            _diagnostics.Record(DiagnosticEvent.SettingsRestoreFailed);
            throw;
        }
    }

    private void GuardSettingsFile(string destination)
    {
        string full = Path.GetFullPath(destination);
        foreach (var reserved in new[]
        {
            _settings.SettingsFilePath,
            _settings.SettingsFilePath + ".bak",
            _settings.SettingsFilePath + ".tmp"
        })
        {
            if (string.Equals(full, Path.GetFullPath(reserved),
                    StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Choose a separate backup destination.");
        }
    }

    private static string Hash(string serializedSettings) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(serializedSettings)));
}

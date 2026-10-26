using System.IO;
using System.Reflection;
using System.Text.Json;

namespace LuaToolsGui.Services;

/// <summary>Fixed event identifiers only: never pass tokens, user input, paths, URLs or exception messages.</summary>
internal enum DiagnosticEvent
{
    SettingsPrimaryCorrupt,
    SettingsRecoveredFromBackup,
    SettingsRecoveryUnavailable,
    SettingsExported,
    SettingsExportFailed,
    SettingsRestored,
    SettingsRestoreFailed,
    DiagnosticReportCreated
}

/// <summary>
/// Local bounded audit trail and explicit, allowlisted diagnostics export. No secrets, raw logs or
/// user-supplied strings are written. The report aggregates event counts, never raw log lines.
/// </summary>
internal sealed class DiagnosticsService
{
    private const long MaxLogBytes = 512 * 1024;
    private static readonly object Gate = new();
    internal static readonly DiagnosticsService Default = new(Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "LuaToolsGui", "diagnostics.log"));

    private readonly string _logPath;
    internal DiagnosticsService(string logPath) => _logPath = logPath;

    internal void Record(DiagnosticEvent diagnosticEvent)
    {
        try
        {
            lock (Gate)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_logPath)!);
                if (File.Exists(_logPath) && new FileInfo(_logPath).Length >= MaxLogBytes)
                    File.Move(_logPath, _logPath + ".previous", overwrite: true);

                File.AppendAllText(_logPath,
                    $"{DateTimeOffset.UtcNow:O}|{diagnosticEvent}{Environment.NewLine}");
            }
        }
        catch (IOException) { /* diagnostics cannot interfere with normal operation */ }
        catch (UnauthorizedAccessException) { /* non-fatal */ }
    }

    /// <summary>Create a privacy-safe report without user settings, absolute paths or exception details.</summary>
    internal void ExportReport(string targetPath, string settingsPath)
    {
        GuardReportDestination(targetPath, settingsPath);
        var counts = Enum.GetNames<DiagnosticEvent>()
            .ToDictionary(name => name, _ => 0, StringComparer.Ordinal);

        lock (Gate)
        {
            foreach (var path in new[] { _logPath + ".previous", _logPath })
            {
                if (!File.Exists(path)) continue;
                // Only read the bounded log files created by this service. Skip externally
                // modified oversized logs to prevent memory exhaustion during export.
                if (new FileInfo(path).Length > MaxLogBytes + 4096) continue;
                foreach (string line in File.ReadLines(path))
                {
                    string eventName = line[(line.LastIndexOf('|') + 1)..];
                    if (counts.ContainsKey(eventName))
                        counts[eventName]++;
                }
            }
        }

        var report = new
        {
            SchemaVersion = 1,
            GeneratedUtc = DateTimeOffset.UtcNow,
            AppVersion = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "unknown",
            RuntimeVersion = Environment.Version.ToString(),
            SettingsFilePresent = File.Exists(settingsPath),
            RecoveryBackupPresent = File.Exists(settingsPath + ".bak"),
            EventCounts = counts
        };

        string json = JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true });
        AtomicWrite(targetPath, json);
        Record(DiagnosticEvent.DiagnosticReportCreated);
    }

    internal static bool IsProtectedReportDestination(string targetPath, string settingsPath,
        string diagnosticsPath)
    {
        string target = Path.GetFullPath(targetPath);
        string primary = Path.GetFullPath(settingsPath);
        string diagnostics = Path.GetFullPath(diagnosticsPath);
        string pluginLog = Path.Combine(Path.GetDirectoryName(primary)!, "plugin-backend.log");
        foreach (var path in new[] { primary, primary + ".bak", primary + ".tmp",
                     diagnostics, diagnostics + ".previous", pluginLog })
        {
            if (string.Equals(target, Path.GetFullPath(path), StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    private void GuardReportDestination(string targetPath, string settingsPath)
    {
        if (IsProtectedReportDestination(targetPath, settingsPath, _logPath))
            throw new InvalidOperationException("Cannot overwrite LuaTools configuration or logs.");
    }

    internal static void AtomicWrite(string targetPath, string content)
    {
        string fullPath = Path.GetFullPath(targetPath);
        string folder = Path.GetDirectoryName(fullPath)!;
        Directory.CreateDirectory(folder);
        string tempPath = Path.Combine(folder, "." + Path.GetFileName(fullPath) +
            "." + Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            File.WriteAllText(tempPath, content);
            File.Move(tempPath, fullPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
    }
}

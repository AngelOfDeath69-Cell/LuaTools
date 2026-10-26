using System.IO;

namespace LuaToolsGui.Services;

/// <summary>
/// Compatibility logger for existing plugin callers. Store ONLY fixed diagnostic categories,
/// never caller-provided request bodies, usernames, URLs, API keys or exception details.
/// </summary>
public static class PluginLog
{
    private static readonly object Gate = new();
    private const long MaxLogBytes = 256 * 1024;

    // Use a new file so old unredacted logs are never mixed into new diagnostics.
    public static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "LuaToolsGui", "plugin-events.log");

    internal static string Category(string? message)
    {
        if (message is null) return "Plugin.Other";
        if (message.StartsWith("HTTP ", StringComparison.Ordinal)) return "HTTP.Request";
        if (message.StartsWith("/add-source/", StringComparison.Ordinal)) return "HTTP.AddSource";
        if (message.StartsWith("PluginAdd.Start", StringComparison.Ordinal)) return "PluginAdd.Start";
        if (message.StartsWith("PluginAdd.Pick", StringComparison.Ordinal)) return "PluginAdd.Pick";
        if (message.StartsWith("PluginAdd.Check", StringComparison.Ordinal)) return "PluginAdd.Check";
        if (message.StartsWith("PluginAdd.Download", StringComparison.Ordinal)) return "PluginAdd.Download";
        return "Plugin.Other";
    }

    public static void Log(string msg)
    {
        string category = Category(msg);
        try
        {
            lock (Gate)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
                if (File.Exists(FilePath) && new FileInfo(FilePath).Length >= MaxLogBytes)
                    File.Move(FilePath, FilePath + ".previous", overwrite: true);
                File.AppendAllText(FilePath, $"[{DateTimeOffset.UtcNow:O}] {category}{Environment.NewLine}");
            }
        }
        catch (IOException) { /* logging must never break the application */ }
        catch (UnauthorizedAccessException) { /* logging must never break the application */ }
    }
}

using System.IO;
using System.Net.Http;
using System.Text;

namespace LuaToolsGui.Services.Downloads;

/// <summary>
/// Shared streaming download-to-file implementation. Stages downloads outside the install target
/// and removes partial files after failures or cancellation.
/// </summary>
internal static class HttpFileDownloader
{
    public static readonly string StagingFolder =
        Path.Combine(Path.GetTempPath(), "LuaToolsGui", "downloads");

    /// <summary>Prevent server-supplied filenames from escaping the staging directory.</summary>
    internal static string SafeFileName(string? serverName, string fallbackName)
    {
        string cleaned = CleanName(serverName);
        if (cleaned.Length == 0) cleaned = CleanName(fallbackName);
        return cleaned.Length == 0 ? "download.bin" : cleaned;
    }

    private static string CleanName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "";

        string name = value.Trim().Trim('"').Replace('\\', '/');
        name = name[(name.LastIndexOf('/') + 1)..];
        if (name is "" or "." or "..") return "";

        char[] invalid = Path.GetInvalidFileNameChars();
        var safe = new StringBuilder(name.Length);
        foreach (char c in name)
            safe.Append(char.IsControl(c) || "<>:\"/\\|?*".Contains(c) ||
                Array.IndexOf(invalid, c) >= 0 ? '_' : c);

        name = safe.ToString().Trim().TrimEnd('.', ' ');
        if (name is "" or "." or "..") return "";

        // Windows forbids these device names even when a file extension is present.
        string stem = name.Split('.')[0];
        if (stem.Equals("CON", StringComparison.OrdinalIgnoreCase) ||
            stem.Equals("PRN", StringComparison.OrdinalIgnoreCase) ||
            stem.Equals("AUX", StringComparison.OrdinalIgnoreCase) ||
            stem.Equals("NUL", StringComparison.OrdinalIgnoreCase) ||
            (stem.Length == 4 &&
             (stem.StartsWith("COM", StringComparison.OrdinalIgnoreCase) ||
              stem.StartsWith("LPT", StringComparison.OrdinalIgnoreCase)) &&
             stem[3] is >= '1' and <= '9'))
            name = "_" + name;

        const int maxLength = 160;
        if (name.Length > maxLength)
        {
            string extension = Path.GetExtension(name);
            name = extension.Length is > 0 and <= 20
                ? name[..(maxLength - extension.Length)] + extension
                : name[..maxLength];
        }
        return name;
    }

    /// <summary>
    /// Save a response under a unique temporary name. Never expose incomplete bytes as a
    /// successful download, and never overwrite another job's staging file.
    /// </summary>
    public static async Task<DownloadedFile> SaveResponseAsync(
        HttpResponseMessage res, string fallbackName,
        IProgress<DownloadProgress>? progress, CancellationToken ct)
    {
        var disposition = res.Content.Headers.ContentDisposition;
        string fileName = SafeFileName(disposition?.FileNameStar ?? disposition?.FileName, fallbackName);

        Directory.CreateDirectory(StagingFolder);
        string extension = Path.GetExtension(fileName);
        string stem = Path.GetFileNameWithoutExtension(fileName);
        string filePath = Path.Combine(StagingFolder, $"{stem}-{Guid.NewGuid():N}{extension}");

        try
        {
            long? total = res.Content.Headers.ContentLength;
            await using var src = await res.Content.ReadAsStreamAsync(ct);
            await using var dst = new FileStream(filePath, FileMode.CreateNew, FileAccess.Write,
                FileShare.None, 81920, FileOptions.Asynchronous);

            var buffer = new byte[81920];
            long written = 0;
            int read;
            while ((read = await src.ReadAsync(buffer, ct)) > 0)
            {
                await dst.WriteAsync(buffer.AsMemory(0, read), ct);
                written += read;
                if (total.HasValue && written > total.Value)
                    throw new InvalidDataException(
                        $"Download exceeded declared Content-Length ({total.Value} bytes).");
                progress?.Report(new DownloadProgress(written, total));
            }

            if (total.HasValue && written != total.Value)
                throw new EndOfStreamException(
                    $"Download incomplete: expected {total.Value} bytes, received {written} bytes.");

            progress?.Report(new DownloadProgress(written, total ?? written));
            return new DownloadedFile(filePath, fileName);
        }
        catch
        {
            try { File.Delete(filePath); }
            catch (IOException) { /* best effort; the next startup will sweep the file */ }
            catch (UnauthorizedAccessException) { /* best effort */ }
            throw;
        }
    }

    /// <summary>Remove abandoned staged files older than one day on startup.</summary>
    public static void SweepStale()
    {
        try
        {
            if (!Directory.Exists(StagingFolder)) return;
            var cutoff = DateTime.UtcNow.AddDays(-1);
            foreach (string path in Directory.EnumerateFiles(StagingFolder))
            {
                try
                {
                    if (File.GetLastWriteTimeUtc(path) < cutoff) File.Delete(path);
                }
                catch { /* best effort; retry next startup */ }
            }
        }
        catch { /* best effort */ }
    }
}

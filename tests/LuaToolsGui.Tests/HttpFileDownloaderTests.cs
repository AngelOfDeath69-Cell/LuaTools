using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using LuaToolsGui.Services.Downloads;
using Xunit;

namespace LuaToolsGui.Tests;

public class HttpFileDownloaderTests
{
    [Theory]
    [InlineData(@"..\..\other.zip", "fallback.zip", "other.zip")]
    [InlineData("../../other.zip", "fallback.zip", "other.zip")]
    [InlineData("../", "fallback.zip", "fallback.zip")]
    [InlineData("CON.zip", "fallback.zip", "_CON.zip")]
    [InlineData("LPT1.lua", "fallback.zip", "_LPT1.lua")]
    [InlineData("bad<name>.zip", "fallback.zip", "bad_name_.zip")]
    [InlineData("   ", "fallback.zip", "fallback.zip")]
    [InlineData("", "", "download.bin")]
    public void SafeFileName_RemovesUnsafeNames(string server, string fallback, string expected) =>
        Assert.Equal(expected, HttpFileDownloader.SafeFileName(server, fallback));

    [Fact]
    public void SafeFileName_PreservesExtensionWithinWindowsLengthLimit()
    {
        string name = HttpFileDownloader.SafeFileName(new string('x', 400) + ".zip", "fallback.zip");
        Assert.True(name.Length <= 160);
        Assert.EndsWith(".zip", name, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SaveResponseAsync_GivesRepeatedNamesUniquePaths()
    {
        using var a = Response("first");
        using var b = Response("second");
        DownloadedFile? first = null;
        DownloadedFile? second = null;
        try
        {
            first = await HttpFileDownloader.SaveResponseAsync(a, "same.zip", null, default);
            second = await HttpFileDownloader.SaveResponseAsync(b, "same.zip", null, default);
            Assert.NotEqual(first.FilePath, second.FilePath);
            Assert.Equal("same.zip", first.FileName);
            Assert.EndsWith(".zip", first.FilePath, StringComparison.OrdinalIgnoreCase);
            Assert.Equal("first", await File.ReadAllTextAsync(first.FilePath));
            Assert.Equal("second", await File.ReadAllTextAsync(second.FilePath));
        }
        finally
        {
            if (first is not null) File.Delete(first.FilePath);
            if (second is not null) File.Delete(second.FilePath);
        }
    }

    [Fact]
    public async Task SaveResponseAsync_UsesSafeContentDispositionName()
    {
        using var response = Response("content");
        response.Content.Headers.ContentDisposition = new ContentDispositionHeaderValue("attachment")
        {
            FileName = @"..\..\payload.zip"
        };
        DownloadedFile? downloaded = null;
        try
        {
            downloaded = await HttpFileDownloader.SaveResponseAsync(response, "fallback.zip", null, default);
            Assert.Equal("payload.zip", downloaded.FileName);
            Assert.Equal(Path.GetFullPath(HttpFileDownloader.StagingFolder),
                Path.GetDirectoryName(Path.GetFullPath(downloaded.FilePath)));
        }
        finally
        {
            if (downloaded is not null) File.Delete(downloaded.FilePath);
        }
    }

    [Fact]
    public async Task SaveResponseAsync_RemovesFileWhenProgressCallbackThrows()
    {
        string prefix = "progress-" + Guid.NewGuid().ToString("N");
        using var response = Response("data");
        var progress = new ProgressRelay<DownloadProgress>(_ => throw new InvalidOperationException("test error"));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            HttpFileDownloader.SaveResponseAsync(response, prefix + ".zip", progress, default));
        Assert.Empty(Directory.GetFiles(HttpFileDownloader.StagingFolder, prefix + "-*.zip"));
    }

    [Fact]
    public async Task SaveResponseAsync_RejectsTruncatedDownload_AndDeletesPartialFile()
    {
        string prefix = "truncated-" + Guid.NewGuid().ToString("N");
        using var response = Response("short");
        response.Content.Headers.ContentLength = 100;

        await Assert.ThrowsAsync<EndOfStreamException>(() =>
            HttpFileDownloader.SaveResponseAsync(response, prefix + ".zip", null, default));
        Assert.Empty(Directory.GetFiles(HttpFileDownloader.StagingFolder, prefix + "-*.zip"));
    }

    [Fact]
    public async Task SaveResponseAsync_RejectsOversizedDownload_AndDeletesPartialFile()
    {
        string prefix = "oversized-" + Guid.NewGuid().ToString("N");
        using var response = Response("long content");
        response.Content.Headers.ContentLength = 2;

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            HttpFileDownloader.SaveResponseAsync(response, prefix + ".zip", null, default));
        Assert.Empty(Directory.GetFiles(HttpFileDownloader.StagingFolder, prefix + "-*.zip"));
    }

    private static HttpResponseMessage Response(string value) => new(HttpStatusCode.OK)
    {
        Content = new ByteArrayContent(Encoding.UTF8.GetBytes(value))
    };
}

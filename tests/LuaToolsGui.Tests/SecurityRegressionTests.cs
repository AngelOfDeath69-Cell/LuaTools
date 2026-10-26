using System.IO;
using LuaToolsGui.Services;
using Xunit;

namespace LuaToolsGui.Tests;

public sealed class SecurityRegressionTests
{
    [Theory]
    [InlineData("https://store.steampowered.com", true)]
    [InlineData("https://steamcommunity.com", true)]
    [InlineData("https://store.steampowered.com.evil.example", false)]
    [InlineData("https://evil.example", false)]
    [InlineData("null", false)]
    [InlineData("http://store.steampowered.com", false)]
    [InlineData("https://steamcommunity.com:8443", false)]
    public void LocalApi_RejectsUntrustedBrowserOrigins(string origin, bool allowed) =>
        Assert.Equal(allowed, LocalApiRequestPolicy.IsAllowed(origin, "cross-site"));

    [Fact]
    public void LocalApi_NativeCallWithoutOriginAllowed_BrowserCrossSiteWithoutOriginDenied()
    {
        Assert.True(LocalApiRequestPolicy.IsAllowed(null, null));
        Assert.False(LocalApiRequestPolicy.IsAllowed(null, "cross-site"));
    }

    [Fact]
    public void PluginLog_NeverPersistsUntrustedFreeformContents()
    {
        string secret = "smm_very_private_api_key";
        Assert.Equal("HTTP.AddSource", PluginLog.Category(
            "/add-source/42 body='{\"token\":\"" + secret + "\"}'"));
        Assert.Equal("PluginAdd.Download", PluginLog.Category(
            "PluginAdd.Download FAILURE exception: " + secret));
        Assert.Equal("Plugin.Other", PluginLog.Category(secret));
        Assert.DoesNotContain(secret, PluginLog.Category(secret));
    }

    [Theory]
    [InlineData("settings.json")]
    [InlineData("settings.json.bak")]
    [InlineData("settings.json.tmp")]
    [InlineData("diagnostics.log")]
    [InlineData("diagnostics.log.previous")]
    [InlineData("plugin-backend.log")]
    [InlineData("plugin-events.log")]
    [InlineData("plugin-events.log.previous")]
    public void DiagnosticReport_CannotOverwriteApplicationData(string protectedName)
    {
        string root = Path.Combine(Path.GetTempPath(), "LuaTools-Security-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            string settings = Path.Combine(root, "settings.json");
            string log = Path.Combine(root, "diagnostics.log");
            string target = Path.Combine(root, protectedName);
            var diagnostics = new DiagnosticsService(log);
            Assert.True(DiagnosticsService.IsProtectedReportDestination(target, settings, log));
            Assert.Throws<InvalidOperationException>(() => diagnostics.ExportReport(target, settings));
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public void Backup_WhitelistsFieldsAndNeverExportsCredentials()
    {
        var current = new AppSettings { HubcapApiKey = "smm_private", DnsMode = "Never" };
        var portable = SettingsBackupService.PortableSnapshot(current);
        Assert.Null(portable.HubcapApiKey);
        Assert.Equal("Never", portable.DnsMode);
    }

    [Fact]
    public void RestoringEmptySettings_RetainsRollbackBackup()
    {
        string root = Path.Combine(Path.GetTempPath(), "LuaTools-Rollback-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            string target = Path.Combine(root, "settings.json");
            var settings = new SettingsService(target) { DnsMode = "Never" };
            settings.ApplyRestoredSettings(new AppSettings());
            Assert.True(File.Exists(target));
            Assert.True(File.Exists(target + ".bak"));
            Assert.Equal("Never", new SettingsService(target + ".bak").DnsMode);
            Assert.Equal("Auto", new SettingsService(target).DnsMode);
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public void UnknownOnlySettingsJson_IsQuarantinedInsteadOfSilentlyDiscardingSettings()
    {
        string root = Path.Combine(Path.GetTempPath(), "LuaTools-Unknown-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            string path = Path.Combine(root, "settings.json");
            File.WriteAllText(path, "{\"InjectedUnknownSetting\":\"not a valid settings payload\"}");
            File.WriteAllText(path + ".bak", "{\"DnsMode\":\"Always\"}");
            var loaded = new SettingsService(path);
            Assert.Equal("Always", loaded.DnsMode);
            Assert.Single(Directory.GetFiles(root, "*.corrupt"));
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public void DownloadTemporaryUniqueName_DoesNotCorruptOriginalLuaBuildIdentity()
    {
        string staged = Path.Combine(Path.GetTempPath(), "1234_5678-" + Guid.NewGuid().ToString("N") + ".lua");
        Assert.Null(LuaInstaller.BuildIdFromFileName(staged));
        Assert.Equal("5678", LuaInstaller.BuildIdFromStagedDownload(staged, "1234_5678.lua"));
        Assert.Null(LuaInstaller.BuildIdFromStagedDownload(staged, "1234.lua"));
    }
}

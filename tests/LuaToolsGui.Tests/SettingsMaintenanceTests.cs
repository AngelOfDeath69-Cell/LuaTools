using System.IO;
using System.Text.Json;
using LuaToolsGui.Services;
using Xunit;

namespace LuaToolsGui.Tests;

public sealed class SettingsMaintenanceTests
{
    [Fact]
    public void Export_RemovesApiKey_RestorePreservesCurrentLocalKey()
    {
        using var t = new TempSettings();
        t.Settings.DnsMode = "Always";
        t.Settings.Language = "id";
        t.Settings.HubcapApiKey = "smm_original_secret";
        var backups = new SettingsBackupService(t.Settings, t.Diagnostics);
        string backup = t.Path("export.json");

        backups.Export(backup);
        string saved = File.ReadAllText(backup);
        Assert.DoesNotContain("smm_original_secret", saved);

        t.Settings.DnsMode = "Never";
        t.Settings.Language = "en";
        t.Settings.HubcapApiKey = "smm_new_secret";

        backups.Restore(backup);
        Assert.Equal("Always", t.Settings.DnsMode);
        Assert.Equal("id", t.Settings.Language);
        Assert.Equal("smm_new_secret", t.Settings.HubcapApiKey);

        var relaunched = new SettingsService(t.Settings.SettingsFilePath);
        Assert.Equal("Always", relaunched.DnsMode);
        Assert.Equal("smm_new_secret", relaunched.HubcapApiKey);
    }

    [Fact]
    public void ModifiedBackupChecksum_IsRejectedWithoutChangingSettings()
    {
        using var t = new TempSettings();
        t.Settings.DnsMode = "Always";
        var backups = new SettingsBackupService(t.Settings, t.Diagnostics);
        string backup = t.Path("backup.json");
        backups.Export(backup);

        File.WriteAllText(backup, File.ReadAllText(backup).Replace(
            "\"Always\"", "\"Never\"", StringComparison.Ordinal));

        t.Settings.DnsMode = "Never";
        Assert.Throws<InvalidDataException>(() => backups.Restore(backup));
        Assert.Equal("Never", t.Settings.DnsMode);
    }

    [Fact]
    public void UnsupportedVersion_IsRejectedWithoutChangingSettings()
    {
        using var t = new TempSettings();
        t.Settings.DnsMode = "Always";
        var backups = new SettingsBackupService(t.Settings, t.Diagnostics);
        string backup = t.Path("version.json");
        backups.Export(backup);

        File.WriteAllText(backup, File.ReadAllText(backup).Replace(
            "\"Version\": 1", "\"Version\": 99", StringComparison.Ordinal));

        t.Settings.DnsMode = "Never";
        Assert.Throws<InvalidDataException>(() => backups.Restore(backup));
        Assert.Equal("Never", t.Settings.DnsMode);
    }

    [Fact]
    public void OversizedBackup_IsRejected()
    {
        using var t = new TempSettings();
        string backup = t.Path("big.json");
        File.WriteAllText(backup, new string('A', 300_000));
        Assert.Throws<InvalidDataException>(
            () => new SettingsBackupService(t.Settings, t.Diagnostics).Restore(backup));
    }

    [Fact]
    public void BackupCannotOverwriteLiveSettingsOrRecoveryFiles()
    {
        using var t = new TempSettings();
        var backup = new SettingsBackupService(t.Settings, t.Diagnostics);
        foreach (string path in new[] { t.Settings.SettingsFilePath,
                     t.Settings.SettingsFilePath + ".bak",
                     t.Settings.SettingsFilePath + ".tmp" })
            Assert.Throws<InvalidOperationException>(() => backup.Export(path));
    }

    [Fact]
    public void CorruptPrimary_IsQuarantined_AndGoodBakRecovered()
    {
        using var t = new TempSettings();
        string path = t.Settings.SettingsFilePath;
        File.WriteAllText(path, "{broken");
        File.WriteAllText(path + ".bak", JsonSerializer.Serialize(
            new AppSettings { DnsMode = "Never", Language = "id" }));

        var restored = new SettingsService(path);
        Assert.Equal("Never", restored.DnsMode);
        Assert.Equal("id", restored.Language);
        Assert.Single(Directory.GetFiles(t.DirectoryPath, "*.corrupt"));
        Assert.Equal("{broken", File.ReadAllText(Directory.GetFiles(t.DirectoryPath, "*.corrupt")[0]));
    }

    [Fact]
    public void MultipleCorruptFiles_AreNotOverwrittenByFutureRecovery()
    {
        using var t = new TempSettings();
        string path = t.Settings.SettingsFilePath;
        File.WriteAllText(path, "bad-1");
        _ = new SettingsService(path);
        File.WriteAllText(path, "bad-2");
        _ = new SettingsService(path);

        string[] corrupt = Directory.GetFiles(t.DirectoryPath, "*.corrupt");
        Assert.Equal(2, corrupt.Length);
        Assert.Contains(corrupt, file => File.ReadAllText(file) == "bad-1");
        Assert.Contains(corrupt, file => File.ReadAllText(file) == "bad-2");
    }

    [Fact]
    public void DiagnosticReport_ContainsOnlyAllowlistedCountsAndNoSecrets()
    {
        using var t = new TempSettings();
        t.Settings.HubcapApiKey = "smm_dont_leak";
        t.Settings.SteamPathOverride = @"C:\Users\secret-name\Games";
        t.Diagnostics.Record(DiagnosticEvent.SettingsExported);
        t.Diagnostics.Record(DiagnosticEvent.SettingsRestored);

        // Even if a user altered the local diagnostic log, export only accepts known event identifiers.
        File.AppendAllText(t.DiagnosticPath, "2026-01-01|smm_injected_secret\n");
        string report = t.Path("report.json");
        t.Diagnostics.ExportReport(report, t.Settings.SettingsFilePath);

        string json = File.ReadAllText(report);
        Assert.DoesNotContain("smm_", json);
        Assert.DoesNotContain("secret-name", json);
        Assert.Contains("\"SettingsExported\": 1", json);
        Assert.Contains("\"SettingsRestored\": 1", json);
        Assert.Contains("\"SettingsFilePresent\": true", json);
    }

    private sealed class TempSettings : IDisposable
    {
        public string DirectoryPath { get; } = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(), "LuaTools-M2-Test-" + Guid.NewGuid().ToString("N"));
        public string DiagnosticPath => Path("events.log");
        public SettingsService Settings { get; }
        public DiagnosticsService Diagnostics { get; }
        public TempSettings()
        {
            System.IO.Directory.CreateDirectory(DirectoryPath);
            Settings = new SettingsService(Path("settings.json"));
            Diagnostics = new DiagnosticsService(DiagnosticPath);
        }
        public string Path(string file) => System.IO.Path.Combine(DirectoryPath, file);
        public void Dispose() => System.IO.Directory.Delete(DirectoryPath, recursive: true);
    }
}

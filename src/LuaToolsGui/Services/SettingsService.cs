using System.IO;
using System.Text.Json;

namespace LuaToolsGui.Services;

public class AppSettings
{
    public string? SteamPathOverride { get; set; }

    // ── Unlocker mode (Mode page). User's chosen backend ────────────
    // "Ost" | "Bst" | "Custom". Older builds wrote "SteamTools" | "OpenSteamTools" |
    // "OpenSteamToolsNightly" | "CloudRedirect"; see ModeMigration, which rewrites those on startup.
    public string? SelectedMode { get; set; }

    // ── Install behavior ─────────────────────────────────────────────
    // When true (default), installs comment out setManifestid() lines and skip copying .manifest
    // files, so games aren't pinned to a version and Steam keeps them updated. Nullable so we can
    // tell "never set" (→ default ON) from an explicit user choice.
    public bool? AutoUpdateApps { get; set; }

    // When true (default), donate spare Steam decryption keys to the community pool. Nullable so
    // "never set" (→ default ON) is distinguishable from an explicit user choice.
    public bool? DonateKeys { get; set; }

    // Manage-page results-per-page. 0 = "All" (single infinite scroll). Nullable so "never set"
    // (→ default 24) is distinguishable from an explicit choice.
    public int? ManagePageSize { get; set; }

    // Fixes-page results-per-page. 0 = "All" (single infinite scroll). Nullable so "never set"
    // (→ default 24) is distinguishable from an explicit choice.
    public int? FixesPageSize { get; set; }

    // Builds-page game-list results-per-page. 0 = "All". Kept separate from ManagePageSize. The Builds
    // list is a narrow sidebar, so a size that suits the Manage grid rarely suits both.
    public int? BuildsPageSize { get; set; }

    // UI language as a BCP-47 tag ("en", "zh-Hans"). Null = follow the Windows display language.
    public string? Language { get; set; }

    // The user's own Hubcap (hubcapmanifest.com) API key ("smm_…"). Null = not configured; key-gated
    // sources stay locked until set. Stored locally. The app calls Hubcap directly with it.
    public string? HubcapApiKey { get; set; }

    // When true, register the app to launch on Windows sign-in (HKCU …\Run). Nullable so "never set"
    // (→ default OFF) is distinguishable from an explicit choice.
    public bool? StartWithWindows { get; set; }

    // When true, minimizing hides the window to the system tray instead of the taskbar. Nullable so
    // "never set" (→ default OFF) is distinguishable from an explicit choice.
    public bool? MinimizeToTray { get; set; }

    // When true, FastFetch auto-picks the first available source and downloads immediately.
    // Nullable so "never set" (→ default OFF) is distinguishable from an explicit choice.
    public bool? FastFetch { get; set; }

    // How host names are resolved: "Auto" (default), "Always" or "Never". Stored in English because
    // it is matched in code; the Settings page localizes the display only. Null = never set → "Auto".
    // See AppHttp for what each mode does.
    public string? DnsMode { get; set; }
}

public class SettingsService
{
    private readonly string _dir;
    private readonly string _filePath;
    private readonly string _tmpPath;
    private readonly string _bakPath;

    private AppSettings _settings = new();

    public SettingsService() : this(Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "LuaToolsGui", "settings.json")) { }

    /// <summary>Override the settings path for isolated test and recovery scenarios.</summary>
    internal SettingsService(string filePath)
    {
        _filePath = Path.GetFullPath(filePath);
        _dir = Path.GetDirectoryName(_filePath)!;
        _tmpPath = _filePath + ".tmp";
        _bakPath = _filePath + ".bak";
        Load();
    }

    internal string SettingsFilePath => _filePath;

    internal AppSettings Snapshot() =>
        JsonSerializer.Deserialize<AppSettings>(JsonSerializer.Serialize(_settings))!;

    /// <summary>Apply settings as one persisted operation, keeping the local API key unchanged.</summary>
    internal void ApplyRestoredSettings(AppSettings restored)
    {
        ArgumentNullException.ThrowIfNull(restored);
        var previous = _settings;
        restored.HubcapApiKey = previous.HubcapApiKey;
        _settings = restored;
        try { Save(preserveEmptyFile: true); }
        catch
        {
            _settings = previous;
            throw;
        }
    }

    /// <summary>User-chosen Steam folder. Null = auto-detect from registry. Persisted only when set.</summary>
    public string? SteamPathOverride
    {
        get => _settings.SteamPathOverride;
        set
        {
            _settings.SteamPathOverride = string.IsNullOrWhiteSpace(value) ? null : value;
            Save();
        }
    }

    /// <summary>Selected unlocker backend ("SteamTools" | "OpenSteamTools"), or null if never chosen.</summary>
    public string? SelectedMode
    {
        get => _settings.SelectedMode;
        set { _settings.SelectedMode = string.IsNullOrWhiteSpace(value) ? null : value; Save(); }
    }

    /// <summary>When true (default), installs don't lock manifests so apps keep auto-updating.</summary>
    public bool AutoUpdateApps
    {
        get => _settings.AutoUpdateApps ?? true; // default ON
        set { _settings.AutoUpdateApps = value; Save(); }
    }

    /// <summary>When true (default), donate spare Steam decryption keys to the community pool.</summary>
    public bool DonateKeys
    {
        get => _settings.DonateKeys ?? true; // default ON
        set { _settings.DonateKeys = value; Save(); }
    }

    /// <summary>Manage-page results-per-page (default 24). 0 = "All" (single infinite scroll).</summary>
    public int ManagePageSize
    {
        get => _settings.ManagePageSize ?? 24; // default 24
        set { _settings.ManagePageSize = value; Save(); }
    }

    /// <summary>Fixes-page results-per-page (default 24). 0 = "All" (single infinite scroll).</summary>
    public int FixesPageSize
    {
        get => _settings.FixesPageSize ?? 24; // default 24
        set { _settings.FixesPageSize = value; Save(); }
    }

    /// <summary>Builds-page game-list results-per-page (default 10). 0 = "All". Smaller than the other
    /// pages' 24. This list is a narrow sidebar next to the build detail, not a full-width grid.</summary>
    public int BuildsPageSize
    {
        get => _settings.BuildsPageSize ?? 10; // default 10
        set { _settings.BuildsPageSize = value; Save(); }
    }

    /// <summary>UI language tag ("en" | "zh-Hans"), or null to follow the Windows display language.</summary>
    public string? Language
    {
        get => _settings.Language;
        set { _settings.Language = string.IsNullOrWhiteSpace(value) ? null : value; Save(); }
    }

    /// <summary>The user's Hubcap API key ("smm_…"), or null if not configured.</summary>
    public string? HubcapApiKey
    {
        get => _settings.HubcapApiKey;
        set { _settings.HubcapApiKey = string.IsNullOrWhiteSpace(value) ? null : value; Save(); }
    }

    /// <summary>When true, the app is registered to launch on Windows sign-in (default OFF).</summary>
    public bool StartWithWindows
    {
        get => _settings.StartWithWindows ?? false; // default OFF
        set { _settings.StartWithWindows = value; Save(); }
    }

    /// <summary>When true, minimizing hides the window to the system tray (default OFF).</summary>
    public bool MinimizeToTray
    {
        get => _settings.MinimizeToTray ?? false; // default OFF
        set { _settings.MinimizeToTray = value; Save(); }
    }

    /// <summary>When true, FastFetch auto-picks the first available source and downloads immediately (default OFF).</summary>
    public bool FastFetch
    {
        get => _settings.FastFetch ?? false; // default OFF
        set { _settings.FastFetch = value; Save(); }
    }

    /// <summary>
    /// How host names are resolved: "Auto" (default), "Always" or "Never". See <see cref="AppHttp"/>.
    /// </summary>
    /// <remarks>
    /// Defaults to Auto rather than off on purpose. A user whose ISP DNS-blocks lua.tools cannot reach
    /// anything in the app to discover that a setting would fix it, so an opt-in toggle would be found
    /// by everyone except the people who need it. Auto costs unaffected users nothing: the system
    /// resolver is still tried first and DoH only engages once it has actually failed.
    /// </remarks>
    public string DnsMode
    {
        get => _settings.DnsMode is "Always" or "Never" ? _settings.DnsMode : "Auto";
        set { _settings.DnsMode = value; Save(); }
    }

    private void Load()
    {
        if (TryLoad(_filePath)) return;

        bool corruptPrimary = File.Exists(_filePath);
        if (corruptPrimary)
        {
            PreserveCorrupt(_filePath);
            DiagnosticsService.Default.Record(DiagnosticEvent.SettingsPrimaryCorrupt);
        }

        if (TryLoad(_bakPath))
        {
            DiagnosticsService.Default.Record(DiagnosticEvent.SettingsRecoveredFromBackup);
            return;
        }

        if (corruptPrimary || File.Exists(_bakPath))
            DiagnosticsService.Default.Record(DiagnosticEvent.SettingsRecoveryUnavailable);

        _settings = new AppSettings();
    }

    private bool TryLoad(string path)
    {
        try
        {
            if (!File.Exists(path)) return false;
            string json = File.ReadAllText(path);
            // Unknown-only objects must not silently replace existing settings with defaults.
            // Still accept {} (an intentional default-only restore) and known fields with
            // unknown future fields for backward-compatible migrations.
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object) return false;
            bool hasProperties = false;
            bool hasKnownProperty = false;
            foreach (var property in document.RootElement.EnumerateObject())
            {
                hasProperties = true;
                if (typeof(AppSettings).GetProperty(property.Name) is not null)
                    hasKnownProperty = true;
            }
            if (hasProperties && !hasKnownProperty) return false;

            if (JsonSerializer.Deserialize<AppSettings>(json) is { } loaded)
            {
                _settings = loaded;
                return true;
            }
        }
        catch { /* missing / truncated / invalid JSON → caller falls through */ }
        return false;
    }

    /// <summary>A present-but-unparseable settings file is moved aside (not deleted) so its contents survive
    /// for manual recovery and a subsequent Save can't overwrite it.</summary>
    private static void PreserveCorrupt(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                // Preserve *every* corrupt copy; never overwrite an earlier recovery file.
                File.Move(path, path + "." + DateTime.UtcNow.ToString("yyyyMMddHHmmss") +
                    "." + Guid.NewGuid().ToString("N") + ".corrupt");
            }
        }
        catch (IOException) { /* best effort: never delete the original */ }
        catch (UnauthorizedAccessException) { /* preserve the original */ }
    }


    /// <summary>True when at least one user-selected setting must survive an app restart.</summary>
    internal static bool HasPersistedValues(AppSettings settings) =>
        settings.SteamPathOverride is not null
        || settings.SelectedMode is not null
        || settings.AutoUpdateApps is not null
        || settings.DonateKeys is not null
        || settings.ManagePageSize is not null
        || settings.FixesPageSize is not null
        || settings.BuildsPageSize is not null
        || settings.Language is not null
        || settings.HubcapApiKey is not null
        || settings.StartWithWindows is not null
        || settings.MinimizeToTray is not null
        || settings.FastFetch is not null
        || settings.DnsMode is not null;

    private void Save(bool preserveEmptyFile = false)
    {
        // Nothing worth persisting → don't leave a settings file behind.
        // Every persisted field must be listed here. A field left out is treated as "nothing worth
        // keeping", so a user whose ONLY change was that setting gets the file deleted and the setting
        // silently lost on the next save. (FixesPageSize was missing.)
        if (!preserveEmptyFile && !HasPersistedValues(_settings))
        {
            foreach (var p in new[] { _filePath, _bakPath, _tmpPath })
                try { if (File.Exists(p)) File.Delete(p); } catch { /* best effort */ }
            return;
        }

        Directory.CreateDirectory(_dir);
        string json = JsonSerializer.Serialize(_settings, new JsonSerializerOptions { WriteIndented = true });

        // Atomic write: fill a temp file, then rename it over the target. A crash/kill mid-write can only
        // ever truncate the .tmp. The live settings.json is replaced by an atomic move (same-volume rename)
        // and is therefore never left half-written. (This class of loss is exactly what a forced kill during
        // a plain WriteAllText caused.) A .bak of the last good file is kept as a second recovery source.
        File.WriteAllText(_tmpPath, json);
        try { if (File.Exists(_filePath)) File.Copy(_filePath, _bakPath, overwrite: true); } catch { /* best effort */ }
        File.Move(_tmpPath, _filePath, overwrite: true);
    }
}

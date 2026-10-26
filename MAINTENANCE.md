# Milestone 2 — Backup, Recovery and Diagnostics

M2 adds in-app settings backup/restore plus privacy-conscious local diagnostics. This feature is implemented on a stacked branch based on the completed M1 branch, not main.

## Use the feature

Open **Settings > Backup & Diagnostics** (in Bahasa Indonesia: **Pengaturan > Cadangan & Diagnostik**).

- **Export settings backup**: choose a location to save a portable JSON backup. Export is versioned, limited to 256 KiB and protected by a SHA-256 checksum to detect accidental modifications.
- **Restore settings backup**: choose the previously exported JSON, confirm the replacement, and restart LuaTools to apply the restored preferences.
- **Export diagnostic report**: save a privacy-filtered JSON report with only application/runtime versions, whether settings and recovery files exist, and counts of named diagnostic events.

### Information excluded from backup and diagnostics

- The Hubcap API key is **never exported**. When restoring a settings backup, the API key already on that PC is preserved.
- Diagnostics do not include email, user names, authentication/session tokens, download URLs, file paths, Steam paths, or raw exception messages.
- Reports contain event counts rather than raw local logs.

### Privacy warning

The **settings backup does include preferences**, potentially including **SteamPathOverride**, which may contain a Windows profile name or other private folder information. Keep backup files in trusted private storage. Only the **diagnostic report** is intentionally free of personal paths.

The SHA-256 digest checks file integrity, **not authenticity**. It does not protect against someone maliciously editing the contents and recalculating the digest. Restore backups only from trusted sources.

## Automatic recovery

SettingsService continues to save settings.json atomically and retains settings.json.bak as a last-good recovery source. If settings.json is unreadable at startup:

1. Move the damaged file aside to a **unique** timestamp+GUID .corrupt file without overwriting any prior copy.
2. Attempt to load settings.json.bak.
3. Record a fixed-code diagnostic event indicating recovery status. No file contents are logged.
4. If no recovery copy works, fall back to defaults and preserve the corrupt files for manual investigation.

Data location: the LuaToolsGui folder under the current Windows user's Application Data (Roaming) directory.

### Manual recovery

Quit LuaTools before working with the settings folder. Make a copy of the complete folder first. Do not overwrite the .corrupt files. A known-good .bak copy can be restored to settings.json, or use **Restore settings backup** from within the application if the GUI loads.

## Quality gates

- All xUnit tests and translation key checks must pass in GitHub Actions.
- The win-x64 self-contained portable EXE must publish as an Actions artifact.
- Perform a manual smoke test of the WPF commands, valid/invalid JSON, startup fallback, restart and UI language.
- Test backup/restore with a configured API key and confirm it is not exported nor replaced after restore.
- No production API credentials should appear in test fixtures or reports.

## Limitations

- Backup only covers **AppSettings** in settings.json; it does not back up downloaded files, cache, plugin state or authenticated accounts.
- Credentials are intentionally not portable.
- UI may not immediately reflect restored values until restart.
- Other languages currently use English fallback text for these newly added controls (the Indonesian strings are translated); translations may be improved separately.
- This is an unsigned personal build. Do not equate successful CI with a full desktop smoke test.

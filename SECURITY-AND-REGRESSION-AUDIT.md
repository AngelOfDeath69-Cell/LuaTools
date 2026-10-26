# Security & Regression Audit — LuaTools Enhanced M2
Date: 2026-10-26
Scope: M1/M2 branches plus original main; Windows WPF .NET 8, local Steam-plugin bridge, persistence, diagnostics, plugin update, and Lua-file staging. This is a code-level audit and CI validation, NOT a full dynamic assessment of Steam or third-party DLL behavior.

## Reproducible findings and fixes (PR #3)

| Severity | Problem | Mitigation |
| --- | --- | --- |
| P0 | Startup recursively deleted the entire user-controlled Downloads/LuaTools folder | Remove the destructive cleanup; sweep only LuaTools-owned temporary staging |
| P0 | Diagnostic report export could overwrite settings.json, .bak, .tmp, and sensitive logs | Deny exports to protected application paths, with tests |
| P0 | Localhost HTTP service served wildcard CORS and dispatched requests from untrusted website origins | Allowlist Steam website origins and reject cross-site browser requests before dispatch; never return arbitrary exception text |
| P1 | Plugin logger persisted raw HTTP bodies and exception strings | Log fixed event categories in a separate bounded plugin-events.log; old plugin-backend.log stays untouched and should be manually examined/deleted |
| P1 | Restore of all-null settings deleted current JSON and rollback .bak | Preserve an explicit settings JSON file and rollback after a restore |
| P1 | Backups serialized all AppSettings fields, including any future secrets by default | Explicit allowlist for portable export; Hubcap API key always omitted |
| P1 | Staging added a unique GUID to Lua filenames, breaking filename-derived build metadata | Pass the original sanitized download name separately to the installer |
| P1 | Manifest-only/empty archives could be reported as base-game Add success | Only report success when app-specific Lua file exists after install; local save success does not guarantee Steam library visibility |
| P2 | Unknown-only JSON settings were silently accepted as defaults | Preserve as corrupt and fall back to .bak |

## What is and is not confirmed

- M1 changes to downloader staging are a confirmed source-level regression for Lua filenames carrying build IDs.
- Screenshot alone does NOT prove why the game failed to appear in Steam Library. The screenshot shows the Steam plugin uninstalled **after** the user's test.
- A file written under Steam's configuration is not proof of a Steam license, ownership or Library enrollment. No DRM/license-bypass behavior is implemented in these hotfixes.
- Portable single-file publishing differs from Velopack installation; startup registration, plugin lifecycle and installed-location expectations need manual verification on Windows.
- GitHub CI builds and xUnit tests do not exercise the actual Steam UI or loader DLL.

## Remaining security work (not claimed fixed)

1. The loopback bridge still trusts native clients without Origin headers. Any malicious process on the same Windows account can forge request headers. Full protection requires a mutually authenticated local protocol shared with a trusted plugin frontend and backwards-compatible migration tests.
2. Third-party loader DLLs, injected frontend JS, and any downloaded executable beyond the LTSP plugin have not been fully reverse-engineered or dynamically sandboxed. Treat plugin/loader installation as privileged code deployment.
3. The LTSP GitHub release currently publishes SHA-256 digests for plugin.zip and winmm.dll; PR #3 refuses plugin installation if expected digests are absent or mismatched. Future/legacy releases without digests will fail closed.
4. Existing users may retain legacy plugin-backend.log containing raw request/error details. This hotfix does not delete user log files automatically.
5. Signed builds, release provenance/attestations, vulnerability scans and all dependency upgrades require separate release-hardening work.
6. Origin restrictions must be compatibility-tested inside the actual Steam embedded browser, not just a normal browser.

## Acceptance checklist for Windows 10/11 x64 (must be executed manually)

1. Make a backup of AppData/Roaming/LuaToolsGui and Steam files used by the application before testing. Never test destructive operations on irreplaceable data.
2. Run the unsigned ZIP in a disposable Windows profile; confirm startup does not touch Downloads/LuaTools.
3. Export diagnostics using a normal file path. Verify the report contains no path, token, raw request or exception text. Attempt to export to protected settings/log paths; operation must be rejected and originals unchanged.
4. Export settings, check Hubcap key is absent. Restore, restart and verify settings and local key remain correct. Test {} restore and confirm .bak survives.
5. Exercise valid Steam-plugin requests and check HTTP 403 on unrelated browser Origin values. Check that expected Steam store/Steam Community origins remain functional.
6. Install the plugin from a release with valid GitHub asset digests; ensure no DLL/JS installs when digest is missing or altered.
7. For legally owned/test Lua files, confirm the original basename/build metadata is preserved through random staging, no false success on empty/manifest-only archives, and that the application reports the installed path accurately.
8. Check the Steam client library and plugin UI **separately**. A saved Lua file alone is not an entitlement/Library guarantee.
9. Collect error codes and expected vs actual results (without API keys or identifying file paths) for any failed tests.
10. Only merge after CI succeeds and runtime tests pass; merge stacked PRs in order #1, #2, then #3.

## Logs and privacy

The new local plugin-events.log writes only names such as HTTP.Request and PluginAdd.Download; it does not preserve app IDs, user-controlled request bodies or exceptions. Old plugin-backend.log from previous releases may still contain sensitive text. Remove it manually after securely retaining any needed diagnostic information.

## Non-goals

No change to Steam licensing, account ownership, entitlements or authorization checks. This work targets program correctness, truthful status reporting, file safety, privacy and local-API hardening.

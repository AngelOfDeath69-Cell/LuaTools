# LuaTools Enhanced — Development Roadmap

This is an incremental engineering plan for the Windows WPF/.NET 8 application. Planned items are **not yet implemented**.

## Rules for each milestone

- Develop in reviewable branches and merge only after CI passes.
- Preserve settings and user data; test migrations and rollback.
- Test every confirmed regression; Windows CI verifies xUnit and EXE compilation.
- WPF usability and Windows integrations still require manual testing.
- Focus on legitimate user-owned files, reliability, accessibility, and maintenance; bypassing access controls is not a roadmap objective.

## Milestones

| Milestone | State | Planned deliverables | Definition of done |
|---|---|---|---|
| M1 — Foundation | In progress | Portable win-x64 build, staged-download safety, unique temporary paths, partial-file cleanup, content-length verification, DNS settings regression, tests | Windows CI green, EXE ZIP and SHA-256 generated |
| M2 — Data safety | Planned | Settings export/restore, JSON recovery, confidential-data-aware diagnostic logs, error capture | Backup/restore tests; no API keys or auth tokens in logs |
| M3 — WPF UI/UX | Planned | Consistent navigation, keyboard shortcuts, accessibility, loading/empty/error states, localization | Windows 100%/150% display tests; keyboard and i18n checks |
| M4 — Lua file workflows | Planned | Search, preview, syntax diagnostics, diff, reversible backup/undo for user-authored Lua | UTF-8/BOM/CRLF tests, atomic saves, rollback |
| M5 — Performance & release | Planned | Cancellation, UI responsiveness, profiling, versioned release notes | Measured improvements, green CI, manual Windows checks |

## M1 implementation notes

- Strip untrusted path components and invalid Windows filename characters; handle reserved device names.
- Stage each response under a randomized unique filename; retain a sanitized human-facing filename separately.
- Delete partial files after exceptions/cancellation.
- Check declared Content-Length to reject truncated/oversized transfers; chunked responses remain supported.
- Save settings even when DNS mode is the only changed preference.
- Add regression tests for filename handling, collisions, malformed length and each settings property.
- Build .NET 8 self-contained win-x64 without trimming. No signing or installer is claimed.

## Manual release checklist

- [ ] GitHub Actions build and tests successful.
- [ ] Extract artifact ZIP and nested portable ZIP; LuaTools.exe exists.
- [ ] Check SHA-256 of the executable against SHA256SUMS.txt.
- [ ] Run on Windows 10/11 x64 with both new and existing user settings.
- [ ] Set DNS mode as the only preference, restart, confirm persistence.
- [ ] Test parallel same-name downloads and cancellation; verify cleanup.
- [ ] Test a deliberately truncated response in a controlled test.
- [ ] Check localization, keyboard use, screen scaling, startup and exit.
- [ ] Record known limitations before marking any version stable.

## Out of scope for M1

No major UI redesign, authentication changes, platform protection changes or signed installer. CI does not prove optional runtime integrations work.

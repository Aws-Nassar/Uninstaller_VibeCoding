# ROADMAP — Free & Open-Source Windows Uninstaller

Goal: build a Windows uninstaller that is measurably better than Revo Uninstaller Pro
and IObit Uninstaller, is fully free (MIT), and adds things none of them offer
(CLI automation, full undo of an uninstall, provable leftover-removal accuracy).

## 1. Competitive teardown (what the paid tools actually do)

### Revo Uninstaller Pro
- Leftover scan after uninstall in 3 depth modes: Safe / Moderate / Advanced.
- Forced Uninstall: remove apps that are not listed or already broken/partially removed.
- Real-time installation monitoring with a trace-log database (Pro only).
- Multi-level backup + uninstall history; log database of known apps.
- Hunter Mode (crosshair over a window/icon to identify and uninstall).
- Batch uninstall, browser-extension removal, Windows Apps removal, 8 extra cleaning tools.
- Weaknesses: paid (Forced Uninstall and monitoring are paywalled), no CLI/scripting,
  no true "undo", scans are known to produce huge noisy leftover lists
  (users report tens of thousands of registry items), no unattended/silent batch mode.

### IObit Uninstaller
- One-click leftover scan, batch uninstall, Install Monitor, Software Health,
  browser extensions, Windows Apps, bloatware remover, software updater (16.x),
  "stubborn program" removal, file shredder, software relocator.
- Weaknesses: free version nags/upsells, closed source, aggressive "health" fear-marketing,
  scanning is shallow compared to Uninstalr/Revo Advanced, no CLI, no undo.

### Uninstalr (free, closest to what we want to beat)
- Extremely thorough detection: portable apps, previously removed software leftovers,
  preview of exactly what will be removed before it starts, unattended batch mode,
  dark mode, portable build.
- Weaknesses: single-dev closed source, no install monitoring automation, no undo vault,
  no CLI, no multi-user/admin workflows.

### Bulk Crap Uninstaller (open source, our baseline for truth)
- Best-in-class detection coverage (Store, Steam, NSIS/Inno/WiX, orphaned apps),
  powerful automation, but: old WinForms UI, leftover scanning only for its own
  uninstalls, no dark mode, no installation monitoring, no undo.

## 2. Where we beat all of them (the differentiators)

| # | Differentiator | Why it wins |
|---|----------------|-------------|
| D1 | Everything free and MIT, no tiers, no ads, no nags | Revo/IObit paywall their best features |
| D2 | Before/after system snapshots + fingerprint scoring engine | Like Revo Advanced, but with explainable, tunable risk tiers and far less noise |
| D3 | Install Watch: a background monitor that auto-records every install | Free and automatic (Revo Pro paywalls it, IObit's is manual) |
| D4 | Quarantine vault + one-click Undo of the last uninstall | Nobody offers true undo of the whole operation |
| D5 | Dry-run preview: exact files/keys and total size before anything is touched | Uninstalr has preview, Revo/IObit do not |
| D6 | First-class CLI + JSON output + exit codes for scripts/IT admins | None of the four have a real CLI (BCU is GUI-first) |
| D7 | Ghost scan: leftovers of apps uninstalled before our tool ever existed | Only Uninstalr/Revo Forced do this, ours is free and previewable |
| D8 | Speed via NTFS MFT/USN indexing instead of recursive directory walks | Seconds instead of minutes on large disks |
| D9 | Arabic (RTL) + full localization | None ship proper RTL/Arabic |
| D10| Published accuracy benchmark: leftover removal measured on a corpus of test apps | Lets us prove "better" instead of claiming it |

## 3. Feature scope by milestone

### M0 — Foundations (repo + CI) — done
- .NET solution skeleton: App (WPF), Core, Windows (Win32 interop), Cli, Tests.
- GitHub Actions CI on `windows-latest`: build + unit tests + packaging artifact.
- Logging, settings store, design docs (see `docs/ARCHITECTURE.md`).
- Acceptance: `dotnet build` and `dotnet test` green in CI; empty shell app launches.

Status: solution builds with zero warnings, 345 unit tests green, CI workflow in
`.github/workflows/ci.yml` publishes the GUI and the CLI and runs `vibe version` as a smoke
test. `.github/workflows/release.yml` turns a `v*` tag into a GitHub release with an Inno
Setup installer, portable single-file executables and the self-contained win-x64 GUI/CLI
(the first one was `v0.1.0`; `v0.2.0` added the installer). The settings store is now a
tiny JSON file (`%AppData%\VibeUninstaller\settings.json`) that remembers the color theme
and whether registry keys are backed up before deletion; structured logging is still
deferred to M6.

### M1 — MVP: list, uninstall, clean (matches Revo Free) — done
- Discover installed apps from all Uninstall registry keys (HKLM/HKCU, 32/64-bit views)
  + Microsoft Store/AppX packages.
- Rich list UI: search, sort by size/date/name/publisher, details pane.
- Run the native uninstaller (prefer `QuietUninstallString`), elevation handling,
  process-tree waiting (handles Inno/NSIS temp-exe relaunch), exit-code interpretation.
- Post-uninstall rescan + review screen for leftover registry keys and files.
- Delete with a Recycle-Bin-first policy and automatic registry backup (.reg exports).
- Acceptance: uninstall Firefox/7-Zip/LibreOffice with zero leftovers; nothing outside
  the app's own footprint is ever listed.

Status: all of the above is implemented, plus the fingerprint based static scan (`vibe scan`),
a fingerprint-only leftover preview inside the GUI, and the CLI with JSON output (`docs/CLI.md`).
Added after the first release: four switchable color themes (Dark, Light, Midnight, Ocean) with
the choice persisted between runs, batch uninstall (Ctrl/Shift multi-select, each uninstaller in
turn, one combined leftover review), real application icons resolved from the installer's
`DisplayIcon`, then the app's own executable in its install folder, then the Store package logo,
with the fallback reason on the letter tiles that remain, a per-theme selected-row treatment that
cannot be confused with hover, a branded application icon for the window, taskbar, shortcuts and
executable, and an opt-out for the registry `.reg` backups (on by default, in the review screen
and as `--no-registry-backup`).
The acceptance run on real applications (Firefox, 7-Zip, LibreOffice) is still open and is
tracked as the first item of M2.

### M2 — Precision engine (beats Revo Free)
- Install fingerprinting: name tokens, publisher, install dirs, executables, GUIDs, install date.
- Fuzzy + weighted matching (token overlap, trigram, path containment) with a score per candidate.
- Risk tiers with explanations: Safe / Moderate / Advanced, plus "shared component" warnings
  (VC++ runtimes, .NET, DirectX) and a system-path deny-list that can never be deleted.
- Ghost scan: find leftovers of apps removed long ago (orphan uninstall keys, stray
  AppData/ProgramData/registry nodes that match no installed app).
- Forced Uninstall for broken/unlisted apps (choose a folder and/or name, scan, remove).
- Optimization: NTFS MFT/USN fast file index, cached snapshots, incremental rescans.
- Acceptance: benchmark vs Revo Advanced on the test corpus (docs/BENCHMARK.md).

### M3 — Automation (beats all four)
- Uninstall queue: batch select, run serially or with configurable parallelism.
- Silent-switch engine per installer family: MSI, NSIS, Inno, InstallShield, WiX Burn,
  Squirrel/Electron, Wise, MSIX — with per-app overrides.
- CLI: `uninstaller list --json`, `uninstaller remove <id> --silent --scan advanced --yes`,
  machine-readable reports, stable exit codes, `--dry-run`.
- Uninstall history log (JSON) with everything that was removed and where backups live.
- Acceptance: a PowerShell script can silently uninstall 10 apps and parse JSON results.

Status: partially delivered early in the GUI — a serial batch queue with multi-select, silent
switching when the installer family is known, honest per-app status and one combined review of
every leftover. Parallelism, per-app overrides and the CLI batch story are still open.

### M4 — Safety & reversibility (beats Revo Pro)
- Quarantine vault: files are moved, not deleted; registry changes saved as restore scripts.
- Undo: restore the last N uninstall sessions in one click (files + keys + services + tasks).
- Restore point creation before uninstall (WMI), plus scheduled-task/service snapshot-diff.
- Protected-app heuristics: warning before touching security software, drivers, runtimes.
- Acceptance: uninstall → undo → app is byte-identical (verified by hash comparison).

### M5 — Monitoring & system tools (unique territory)
- Install Watch: elevated helper records installs automatically (snapshot + live watchers:
  `ReadDirectoryChangesW` on Program Files/ProgramData/AppData + `RegNotifyChangeKeyValue`),
  producing a trace log usable for a perfect uninstall.
- System tools: startup manager, services, scheduled tasks, shell extensions,
  file associations, context-menu entries (Autoruns-lite, safe subset).
- Windows Apps / bloatware manager, browser-extension scan (Chrome/Edge/Firefox profiles),
  Steam and WinGet detection, portable-app discovery.
- Software "reinstall with winget" for accidental removals.
- Acceptance: install a test app while Watch is on, then uninstall with the trace log and
  get zero leftovers without any scanning.

### M6 — Release & polish
- Modern Fluent UI (dark/light), full Arabic RTL + English, accessibility.
- MSI/MSIX installer (an Inno Setup installer plus portable single-file executables already
  ship since `v0.2.0`), winget manifest, optional auto-update.
- Code signing, security review, performance pass (targets below), docs + website README.
- Acceptance: signed release artifacts, winget submission, published benchmark numbers.

Status: four color themes and the setup installer shipped early with `v0.2.0`. MSI/MSIX,
winget, code signing, Arabic RTL, accessibility and the performance pass remain open.

## 4. Performance targets

| Operation | Target |
|-----------|--------|
| Enumerate installed apps | < 2 s for 500+ apps |
| Full file index (NVMe, 1M files) | < 5 s (MFT), < 60 s fallback walk |
| Registry snapshot (HKLM+HKCU+HKCR) | < 4 s |
| Diff + classify leftovers | < 1 s |
| App cold start to usable UI | < 1.5 s |
| Idle memory | < 150 MB |

## 5. Testing & proof strategy

- Unit tests: command-line parsing, fingerprint scoring, diff engine, planner, silent-switch DB.
- Test-app farm: tiny NSIS / Inno Setup / WiX MSI installers built in CI, installed into a
  disposable Windows Sandbox / VM, uninstalled by the engine, then filesystem+registry
  asserted to be identical to the pre-install snapshot.
- Golden corpus: ~20 popular real apps (7-Zip, Firefox, VLC, Notepad++, LibreOffice, VS Code...)
  with expected leftover sets, run in CI VM on a schedule.
- Fuzzing: synthetic directory/registry trees fed to the deletion planner to prove the
  deny-list and scoring never propose system files.
- Benchmark document (`docs/BENCHMARK.md`): our leftover counts/recall vs Revo Advanced
  and BCU on the same corpus — the artefact that proves "better".

## 6. Risks and mitigations

| Risk | Mitigation |
|------|-----------|
| Deleting something shared (runtime, driver) breaks other apps | Deny-list, shared-component detection, quarantine, undo, dry-run |
| Uninstaller requires interaction / hangs | Process-tree watch, timeout, "finish manually" flow, then continue |
| Antivirus false positives | Open source, signing, no injection, behave like a normal elevated app |
| MFT/USN requires admin + NTFS | Fallback directory-walk index; prompt for elevation only when needed |
| 32/64-bit registry view confusion | Explicit `Registry32`/`Registry64` access layer, tested |
| Store apps behave differently | Dedicated AppX provider via `PackageManager` + `Remove-AppxPackage` fallback |
| Scope creep (updaters, cleaners...) | Not our job; the roadmap above is the contract |

## 7. Definition of done for v1.0

- M0–M4 and M6 complete; M5 partial (Install Watch + startup/Store/Steam tools).
- Zero known data-loss bugs; every destructive action is previewable and undoable.
- CLI documented; benchmark published; signed installer downloadable;
  README states honestly what the tool does and does not do.

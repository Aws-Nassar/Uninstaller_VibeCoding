# ARCHITECTURE

## Stack decision (recommended)

- Language/runtime: C# on .NET 10 (LTS), `net10.0-windows`.
  Rationale: best first-party access to registry, WMI, P/Invoke, MSI, AppX and
  scheduler APIs; BCUninstaller proves C# is sufficient for this exact product class;
  single-file self-contained publish means users need no runtime.
- UI: WPF + WPF-UI (Fluent design) + CommunityToolkit.Mvvm.
  Rationale: mature, no Windows App SDK packaging pain, fast to iterate, dark mode
  supported out of the box. All logic lives in UI-agnostic projects, so Avalonia or
  WinUI can replace WPF later without touching Core.
- Tests: xUnit; integration tests run in Windows Sandbox / disposable VM.
- CI: GitHub Actions `windows-latest` (build, test, package, winget manifest draft).

Alternatives considered: Avalonia (nice, but no advantage for a Windows-only tool),
WinUI 3 (packaging/self-contained complexity), Electron/Node (poor fit for elevated
system-level work; registry access needs native addons anyway), Rust/Tauri
(viable but slower to build this feature set; no mature MSI/AppX story).

## Solution layout

```
Uninstaller.sln
src/
  Uninstaller.App            WPF shell: views, view models, DI wiring
  Uninstaller.Core           Domain only, no OS calls: models, engine, planner,
                             fingerprinting, scoring, rules, result types
  Uninstaller.Windows        Win32/WMI/Registry/MSI/AppX/Task/Service providers
  Uninstaller.Snapshot       Snapshot capture, storage format, diffing
  Uninstaller.Cli            dotnet tool / console, JSON + human output
  Uninstaller.Helper         Optional elevated helper process (named pipes) - M4+
tests/
  Uninstaller.Core.Tests
  Uninstaller.Windows.IntegrationTests
  testapps/                  Tiny NSIS/Inno/WiX installers used as fixtures
docs/
  ARCHITECTURE.md, BENCHMARK.md, SILENT_SWITCHES.md, PRIVACY.md
```

## Core pipeline

```
Discover → Identify → Plan → Execute → Rescan → Classify → Review → Remove → Record
```

1. Discover — providers produce `InstalledApp` records:
   - RegistryProvider: `HKLM|HKCU\...\Uninstall` both 32/64-bit views; reads DisplayName,
     DisplayVersion, Publisher, InstallLocation, UninstallString, QuietUninstallString,
     EstimatedSize, InstallDate, SystemComponent, ParentKeyName.
   - MsiProvider: `MsiEnumProductsEx` for real product codes and patches.
   - AppxProvider: `PackageManager.FindPackagesForUser` for Store apps.
   - SteamProvider: `libraryfolders.vdf` + `appmanifest_*.acf`; WinGetProvider: `winget list`.
   - OrphanProvider: uninstall entries whose command/install path no longer exists.
2. Identify — build an `AppFingerprint`: normalized name tokens (lowercase, strip
   version/edition/bitness noise), publisher, install dirs, executables (with file
   version info), registry product GUIDs, install date, uninstaller path.
3. Plan — `UninstallPlan`: elevation needed?, silent-capable?, installer family,
   command line chosen, pre-steps (restore point), expected process tree pattern.
4. Execute — run uninstaller through `ProcessRunner`:
   - Argument-safe command splitting (quoted paths, `MsiExec.exe /X{GUID}`).
   - Prefer `QuietUninstallString`; inject silent switches per installer family only
     when the user asked for silent (see `docs/SILENT_SWITCHES.md`).
   - Track the whole process tree (poll `Win32_Process` parent PIDs every 500 ms) so
     temp-relaunch uninstallers (Inno `unins000.exe`, NSIS `~nsu.tmp\Au_.exe`) are awaited.
   - Interpret exit codes: 0/1641/3010 success (reboot flags), 1605 not installed,
     1614 unknown product, plus timeout + retry policy.
5. Rescan — capture an after-snapshot and diff against the before-snapshot:
   - Registry: enumerate HKLM\SOFTWARE, HKCU\SOFTWARE, HKCR, HKU with values (hashed).
   - Files: fast MFT index via `FSCTL_ENUM_USN_DATA` (`IFileIndexSource`), fallback to
     directory walk; store paths as parent-indexed arrays in a compact binary snapshot.
   - Diff produces candidates: present-before ∩ present-after, plus items created during
     uninstall (installer logs, temp upgrades), minus items that vanished.
6. Classify — `LeftoverClassifier` scores every candidate against the fingerprint:
   token overlap + trigram similarity + publisher match + path containment + GUID match,
   with weights and thresholds per risk tier. Output: `LeftoverItem { Path, Kind, Score,
   Reason, Tier, Size }`. Deny-list filter removes anything system-owned
   (WinSxS, DriverStore, Installer cache, MuiCache, `Windows\System32`, fonts...).
   Shared-component detector flags runtimes/drivers with a "used by other apps" warning.
7. Review — UI shows grouped, explained, size-totaled items; user edits selection;
   dry-run is the default mode (nothing is deleted until confirmed).
8. Remove — `RemovalEngine` executes the checkbox plan:
   - Files → moved into the quarantine vault on the same volume (instant, reversible).
   - Registry → the exact keys/values are exported to `.reg` first, then deleted.
   - Services/tasks/shortcuts → snapshotted (definition saved), then removed.
   - Everything recorded in a `UninstallSession` JSON in the vault.
9. Record — history + undo entry; vault has TTL and size cap; `Undo` replays the session.
   Installing apps can be reinstalled via winget as an added convenience.

## Fingerprint scoring sketch

```
score = 0.35 * nameTokenSimilarity      (Jaccard over normalized tokens)
      + 0.20 * publisherMatch           (exact / prefix / none)
      + 0.25 * pathAffinity             (containment in install dirs, exe names,
                                         uninstaller path, service image paths)
      + 0.10 * stringContainment        (registry values referencing app paths/name)
      + 0.10 * temporalAffinity         (created in the same window as install date)
Tier: >=0.75 Safe, 0.5-0.75 Moderate, 0.35-0.5 Advanced (shown only in Advanced mode)
Deny-list and shared-component rules are hard filters applied before scoring.
```

## Safety model

- Three independent safety nets: dry-run preview, quarantine vault, registry backups.
- Undo restores the last N sessions; each session stores exact hashes/ACLs metadata.
- Deny-list is code (not configuration) so it cannot be weakened by a bad rule pack.
- Elevation: M1–M3 manifest `requireAdministrator`; M4+ splits an unelevated GUI plus
  an elevated helper over named pipes so browsing the app list never needs UAC.
- No driver, no injection, no network calls except optional update check and winget.

## Rules pack (community-extensible)

- JSON rule files can add app-specific cleanup hints:
  `{ "match": { "name": "SomeApp", "publisher": "Vendor" }, "regions": [...],
  "files": ["%APPDATA%\\Vendor\\*"], "services": [...], "silent": "/S" }`.
- Used as a hint layer only; scoring still applies. Shipped rules are versioned and
  testable, so regressions on the golden corpus are caught in CI.

## Performance tactics

- One shared in-memory index reused by discovery and scanning; snapshots are cached.
- MFT enumeration (`FSCTL_ENUM_USN_DATA`) instead of recursive walks; USN journal
  read for incremental rescans.
- Registry read via direct P/Invoke (`RegEnumKeyExW`/`RegEnumValueW`) with large buffers,
  hashing names and values at read time instead of storing strings.
- Parallel providers and diff via `Parallel.For`; UI updates throttled and streamed.

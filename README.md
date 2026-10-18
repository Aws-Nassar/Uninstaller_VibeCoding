# VibeUninstaller

[![CI](https://github.com/Aws-Nassar/Uninstaller_VibeCoding/actions/workflows/ci.yml/badge.svg)](https://github.com/Aws-Nassar/Uninstaller_VibeCoding/actions/workflows/ci.yml)

A free, open-source (MIT) Windows uninstaller that removes applications **and** everything
they left behind, with a preview before anything is touched.

VibeUninstaller is an attempt to beat the paid tools (Revo Uninstaller Pro, IObit Uninstaller)
and the free ones (Uninstalr, Bulk Crap Uninstaller) on the things they do badly:
noise, missing automation, no undo, and paywalls.

## Status

Early development. The scope of the current milestone (M0 + M1) is:

| Area | State |
|------|-------|
| Discover installed apps (registry HKLM/HKCU, 32/64-bit views, Store/MSIX, Steam) | Done |
| WPF GUI: library views (Programs / Store / Updates / Everything), search, quick filters, sortable list, details pane with actions | Done |
| Fingerprint-only leftover preview from the GUI (changes nothing) | Done |
| Run the app's own uninstaller (process-tree waiting, exit-code interpretation, UAC) | Done |
| Before/after snapshots of files and registry + explainable leftover scoring | Done |
| Review screen with risk tiers (safe / moderate / advanced) and reasons | Done |
| Removal: Recycle Bin first, `.reg` backup before every key deletion | Done |
| Static leftover scan without running the uninstaller (`vibe scan`) | Done |
| CLI with JSON output and stable exit codes | Done |
| Portable self-contained releases (GUI + CLI ZIPs on version tags) | Done |
| Install Watch, quarantine vault + undo, MFT/USN index, localization | Planned (M2-M6) |

See [ROADMAP.md](ROADMAP.md) for the full plan and milestone status.
[docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) holds the design intent; the code and the
ROADMAP statuses are the source of truth for what is actually implemented today.

## Download

Portable, self-contained win-x64 builds are attached to every
[release](https://github.com/Aws-Nassar/Uninstaller_VibeCoding/releases):

| Archive | Contents |
|---------|----------|
| `VibeUninstaller-<version>-win-x64.zip` | The GUI. Unzip, run `VibeUninstaller.exe`, accept the UAC prompt. |
| `vibe-<version>-win-x64.zip` | The `vibe` CLI for scripts and terminal use. |

No .NET runtime installation is needed: the runtime is bundled into the executables.

> The executables are not code-signed yet, so the first launch can show SmartScreen's
> "Windows protected your PC" dialog. Choose *More info* and then *Run anyway* if you
> trust the source; the binaries are built by this repository's release workflow.

## Safety model

Deleting leftovers is the dangerous part of an uninstaller, so the rules are strict:

1. **Preview first.** The scan shows every item, its size, its risk tier and *why* it was flagged.
2. **Deny list in code.** Windows, Program Files, the package cache, `AppData\Temp` and
   Microsoft's own registry branches can never be proposed, no matter what the scan finds.
3. **Recycle Bin first.** Files are recycled, not destroyed.
4. **Registry backups.** Every key is exported to `.reg` before it is deleted.
5. **Nothing happens silently.** The CLI refuses to remove anything without `--yes`, and the GUI
   asks for confirmation.

## Requirements

- Windows 10 2004 (build 19041) or newer
- To build from source: [.NET 10 SDK](https://dotnet.microsoft.com/download)
- The released ZIPs are self-contained and do not need a .NET installation.

## Build and run

```powershell
dotnet build VibeUninstaller.slnx
dotnet test  VibeUninstaller.slnx

# GUI (asks for administrator rights)
dotnet run --project src/VibeUninstaller.App

# CLI
dotnet run --project src/VibeUninstaller.Cli -- list
```

## CLI

The command line tool is called `vibe`. It is meant for scripts and IT admins:
every command has stable exit codes and `--json` output on stdout.

```powershell
vibe list --json > apps.json
vibe info 7-Zip
vibe scan "Brave" --min-tier safe
vibe uninstall "Notepad++" --silent --clean --min-tier safe --yes
vibe uninstall "VLC" --dry-run
```

Full reference: [docs/CLI.md](docs/CLI.md).

## Repository layout

```
src/VibeUninstaller.Core        # UI-agnostic logic: fingerprints, scoring, command lines
src/VibeUninstaller.Snapshot    # registry and file system snapshots + diff
src/VibeUninstaller.Windows     # Win32 specifics: providers, uninstall runner, removal
src/VibeUninstaller.App         # WPF user interface
src/VibeUninstaller.Cli         # `vibe` command line tool
tests/VibeUninstaller.Tests     # xUnit tests, including a XAML load smoke test
AGENTS.md                       # instructions for coding agents working on this repo
```

## License

MIT. See [LICENSE](LICENSE).

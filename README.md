# VibeUninstaller

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
| WPF GUI: search, filters, sorting, details pane | Done |
| Run the app's own uninstaller (process-tree waiting, exit-code interpretation, UAC) | Done |
| Before/after snapshots of files and registry + explainable leftover scoring | Done |
| Review screen with risk tiers (safe / moderate / advanced) and reasons | Done |
| Removal: Recycle Bin first, `.reg` backup before every key deletion | Done |
| Fingerprint-based scan (works for apps that are already gone) | Done |
| CLI with JSON output and stable exit codes | Done |
| Install Watch, quarantine vault + undo, MFT/USN index, localization | Planned (M2-M6) |

See [ROADMAP.md](ROADMAP.md) for the full plan and [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md)
for the design.

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
- [.NET 10 SDK](https://dotnet.microsoft.com/download) to build; the released binaries will
  carry the runtime with them.

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
```

## License

MIT. See [LICENSE](LICENSE).

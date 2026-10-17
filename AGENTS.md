# AGENTS.md

VibeUninstaller: free (MIT) Windows uninstaller, .NET 10 — a WPF GUI plus an
automation-first CLI. Pre-1.0; only milestones M0/M1 in `ROADMAP.md` are implemented.

## Commands

`dotnet` is not on PATH in this environment; prefix every shell session with:

    $env:PATH = "C:\Program Files\dotnet;" + $env:PATH

- Build: `dotnet build VibeUninstaller.slnx -c Release` — the solution is SLNX;
  `VibeUninstaller.sln` does not exist and fails with MSB1009.
- Test: `dotnet test VibeUninstaller.slnx -c Release --no-build` (after a build).
- One test class: `dotnet test VibeUninstaller.slnx -c Release --filter "FullyQualifiedName~LeftoverClassifierTests"`.
- Run the CLI: `dotnet run --project src/VibeUninstaller.Cli -- list`.
- Keep the build at zero warnings; nullable and latest LangVersion are on, but no
  setting turns warnings into errors for you.
- The GUI manifest requires administrator, so launching it normally triggers UAC.
  To smoke-test it unelevated: publish with `-p:ApplicationManifest=` into a temp
  directory, start the exe, check the window title, then stop the process.

## Project map

- `VibeUninstaller.Core` (net10.0): pure, OS-free logic — classifier, rules,
  fingerprinting, reporting DTOs, name matching. New testable logic goes here.
- `VibeUninstaller.Windows`: registry/AppX/Steam providers, uninstall runner,
  file and registry removal.
- `VibeUninstaller.Snapshot`: before/after snapshots and diffing.
- `VibeUninstaller.App`: WPF GUI, assembly name `VibeUninstaller`,
  CommunityToolkit.Mvvm source generators.
- `VibeUninstaller.Cli`: assembly name `vibe`.
- `tests/VibeUninstaller.Tests` uses UseWPF and references App and CLI, not only
  Core (there is an XAML load test and CLI option-parser tests).

## Gotchas

- WPF projects have trimmed implicit usings — App code gets only `System`,
  `Collections.Generic`, `Linq`, `Threading`, `Threading.Tasks`. There is no
  `System.IO` and no `System.Windows.*`: add explicit usings (the ViewModels
  already had to).
- Unit tests must stay hermetic: they never touch the real filesystem or registry;
  `TestData.cs` fabricates a synthetic fingerprint environment.
- Missing XAML resources fail only at runtime, so `WpfSmokeTests` loads `App.xaml`
  and both windows on one STA thread. Extend that test instead of creating a
  second `Application` in another test class.
- The CLI is a contract: JSON only on stdout, diagnostics on stderr, exit codes
  0–6 documented in `docs/CLI.md`; non-interactive removal requires `--yes`.
- File-scoped namespaces are a warning-level rule in `.editorconfig`.

## Safety invariants (do not weaken)

- Deny/allow lists live in `LeftoverRules` as code, deliberately not configuration.
- Files are removed via `SHFileOperation` with FOF_ALLOWUNDO (Recycle Bin), never
  hard-deleted.
- `RegistryManipulator.ExportKey`: a registry key is never deleted when its `.reg`
  backup could not be written.
- The default flow is preview/dry-run; nothing is removed before confirmation.

## Docs

- `docs/ARCHITECTURE.md` predates the implementation and is partly aspirational:
  it still uses `Uninstaller.*` names and `.sln`, and describes things that do not
  exist yet (Helper process, MSI/WinGet/Orphan providers, WPF-UI, file quarantine
  vault, MFT/USN indexing, a different scoring sketch). Verify against code; do
  not propagate its names or numbers.
- `ROADMAP.md` status lines record what is actually done; `README.md` makes
  deliberate scope/limits claims. Update both when behavior changes.

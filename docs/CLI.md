# `vibe` command line reference

`vibe` is the VibeUninstaller CLI. It is designed to be scriptable: machine readable
output goes to **stdout**, warnings and progress go to **stderr**, and every command
returns a documented exit code.

```
vibe <command> [arguments] [options]
```

## Commands

| Command | Purpose |
|---------|---------|
| `list` | List installed applications |
| `info <name>` | Details and leftover fingerprint of one application |
| `scan <name>` | Preview leftovers without changing anything |
| `uninstall <name>` | Run the application's own uninstaller, then scan for leftovers |
| `version` | Version information |
| `help` | Usage |

`<name>` is matched against the application id first, then the exact display name, then a
substring, and finally word by word, so `vibe uninstall "code studio visual"` finds
"Microsoft Visual Studio Code". An ambiguous query lists the candidates and exits with
code `3`; nothing is ever guessed.

### `vibe list`

| Option | Meaning |
|--------|---------|
| `--json` | JSON array with metadata instead of a table |
| `--search <text>` | Filter by name or publisher |
| `--source <registry\|store\|steam\|all>` | Restrict the source |
| `--scope <machine\|user\|all>` | Restrict the installation scope |
| `--sort <name\|size\|date\|publisher>` | Sort order (default `name`) |
| `--limit <n>` | Show at most n applications |
| `--min-size <megabytes>` | Only applications at least this large |
| `--include-system` | Include system components and updates |
| `--no-store`, `--no-steam` | Skip those sources |

### `vibe info <name>`

Prints the installation details, the detected installer family, the silent command that
would be used and the fingerprint (name tokens, vendor tokens, install roots, candidate
folders). With `--json` the same data is returned as an object.

### `vibe scan <name>`

Runs the fingerprint scan: install roots, vendor/app folders under `ProgramData`,
`AppData` and `Program Files`, Start Menu and desktop shortcuts, and matching registry
branches. **Nothing is changed.** This is the dry run that answers "what is left of this
app?" and it also works when the application was uninstalled long ago.

| Option | Meaning |
|--------|---------|
| `--json` | JSON report |
| `--min-tier <safe\|moderate\|advanced>` | Lowest confidence to include (default `advanced`, i.e. everything) |

### `vibe uninstall <name>`

1. Resolves the application and prints the command it will use.
2. Optionally captures the system state (files + registry).
3. Runs the application's own uninstaller, waits for the whole process tree, and
   interprets the exit code (0, 1641, 3010, 1605, 1614, 1602).
4. Rescans and classifies what is left, with a reason and a risk tier per item.
5. With `--clean`, removes the items, recycles files and exports every registry key to a
   `.reg` backup before deleting it (unless `--no-registry-backup` is passed).

| Option | Meaning |
|--------|---------|
| `--silent` | Use unattended switches for known installer families |
| `--no-scan` | Skip the leftover scan |
| `--clean` | Remove the leftovers that were found |
| `--min-tier <safe\|moderate\|advanced>` | How aggressive `--clean` is (default `safe`) |
| `--yes` | Do not ask for confirmation; required when stdin is redirected |
| `--dry-run` | Show the command and every leftover (all tiers), change nothing |
| `--timeout <minutes>` | Give up on the uninstaller after n minutes (default 30, minimum 1) |
| `--no-registry-backup` | Delete registry keys without writing `.reg` backups. Files still go to the Recycle Bin. |
| `--json` | JSON report on stdout |

## Exit codes

| Code | Meaning |
|------|---------|
| 0 | Success |
| 1 | Bad usage or unexpected failure |
| 2 | No application matched the query |
| 3 | The query matched several applications |
| 4 | Administrator rights are required |
| 5 | The application's own uninstaller failed or timed out |
| 6 | Items were removed but some of them failed |

## JSON contract

The shapes below are stable; the schema is part of the public interface.
Removal (`--clean`) only happens when `--yes` is passed in non-interactive mode.

`vibe list --json`

```json
{
  "count": 1,
  "apps": [
    {
      "id": "reg|HKLM|7-Zip",
      "name": "7-Zip 24.09 (x64)",
      "version": "24.09",
      "publisher": "Igor Pavlov",
      "source": "registry",
      "scope": "machine",
      "installLocation": "C:\\Program Files\\7-Zip",
      "estimatedSizeBytes": 5919744,
      "uninstallString": "\"C:\\Program Files\\7-Zip\\Uninstall.exe\"",
      "registryKeyPath": "HKLM\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Uninstall\\7-Zip"
    }
  ],
  "warnings": []
}
```

`vibe scan --json` and `vibe uninstall --json`

```json
{
  "app": { "id": "reg|HKLM|7-Zip", "name": "7-Zip 24.09 (x64)", "source": "registry", "scope": "machine" },
  "run": {
    "status": "succeeded",
    "exitCode": 0,
    "rebootRequired": false,
    "durationMs": 4210,
    "processCount": 2,
    "commandLine": "\"C:\\Program Files\\7-Zip\\Uninstall.exe\" /S"
  },
  "scan": {
    "mode": "post-uninstall",
    "total": 3,
    "safe": 2,
    "moderate": 1,
    "advanced": 0,
    "sizeBytes": 5919744,
    "durationMs": 118,
    "items": [
      {
        "path": "HKLM\\SOFTWARE\\7-Zip",
        "kind": "registry",
        "tier": "safe",
        "score": 0.83,
        "reason": "Folder name matches the app",
        "createdDuringUninstall": false
      }
    ]
  },
  "removal": {
    "filesRemoved": 1,
    "filesFailed": 0,
    "registryRemoved": 2,
    "registryFailed": 0,
    "bytesRemoved": 5919744,
    "vaultPath": "C:\\ProgramData\\VibeUninstaller\\Vault\\7-Zip-2026-10-17-1200",
    "registryBackupSkipped": false,
    "errors": []
  },
  "dryRun": false,
  "outcome": "leftovers-found",
  "messages": []
}
```

`outcome` is one of `clean`, `leftovers-found`, `uninstalled`, `uninstall-failed`,
`partial`, `error`, `dry-run`.

`kind` is one of `registry`, `directory`, `file`. `tier` is one of `safe`, `moderate`,
`advanced`. `score` is a 0..1 confidence value; the tier thresholds are documented in
`docs/ARCHITECTURE.md`.

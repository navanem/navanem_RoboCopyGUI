# RoboSync

**A friendly Robocopy front-end for Windows.** RoboSync gives non-technical users a clean,
modern desktop interface for fast, reliable folder copy, mirror, move, and sync operations —
powered under the hood by Windows' battle-tested `Robocopy.exe`.

> Status: **v1.0 MVP** — complete and runnable. Core copy/mirror/move/sync workflows, filtering,
> retries, live progress, logging, saved jobs, and a live command preview are all implemented.

---

## Overview

Robocopy is the most robust file copy tool on Windows, but its command line is intimidating and
error-prone for everyday users. RoboSync wraps it in a polished GUI that:

- lets you pick folders, choose an operation mode, and tune common options with checkboxes;
- shows a **live, copy/paste-ready command preview** of exactly what will run;
- streams **live progress** (current file, files, bytes, elapsed, estimated time remaining);
- writes a **timestamped log** to disk and mirrors it in the UI;
- **saves and reloads job configurations** as readable JSON;
- validates inputs and confirms destructive actions before they happen;
- shows the product version in the header and an **About** dialog (with a link to www.navanem.com).

RoboSync does not try to expose every Robocopy switch. It focuses on the workflows that matter
most — copy, mirror, move, sync, filtering, retries, logs, saved jobs, and command preview — and
does them reliably and clearly.

---

## Tech stack and why

| Choice | Reason |
| --- | --- |
| **C# / .NET 8** | First-class on Windows, modern language features, long-term support. |
| **WPF (MVVM)** | The most direct path to a *real* native Windows desktop GUI with rich data binding, custom theming, and easy packaging — no browser runtime or web stack required. |
| **Native `Robocopy.exe` engine** | Robocopy already solves the hard problems: long paths (`\\?\`), locked-file retries, multithreaded copying, NTFS ACLs, and restartable transfers. Re-implementing those for an MVP would be slower *and* less reliable. Wrapping it also makes the on-screen command preview **authentic** — the preview is literally the command that runs. |
| **Hand-rolled MVVM (no MVVM framework)** | Keeps the application dependency-free so it builds and runs with a plain `dotnet run`. |
| **System.Text.Json** | Built into the framework; produces human-readable, diffable saved jobs. |
| **xUnit** | Standard, fast .NET test framework. |

**Why not a custom managed copy engine?** A hand-written copier would give finer-grained progress,
but at the cost of re-deriving long-path handling, retry/backoff, ACL copying, and multithreading —
exactly the things Robocopy is trusted for. For a production-minded MVP, wrapping Robocopy is the
responsible choice. The engine sits behind an `ICopyEngine` interface, so a managed engine could be
added later without touching the UI.

---

## Features

Mapped to the product requirements:

1. **Select source and destination folders** — text fields plus native folder pickers.
2. **Operation modes** — Copy, Mirror, Move, Sync (see mapping below).
3. **Advanced options** — include subfolders, retry count, retry wait, multithreaded copy + thread
   count, preserve timestamps, preserve permissions, exclude file patterns, exclude folder patterns,
   skip newer files in destination, dry-run/preview.
4. **Run controls** — Start, **Pause/Resume** (by suspending the Robocopy process), Cancel (kills the
   whole process tree), and rerun (just press Start again).
5. **Live progress** — current file, files processed (with total from a pre-scan), bytes copied
   (with total), elapsed time, and estimated time remaining.
6. **Logs** — color-coded live log panel **and** a timestamped log file per run under
   `%APPDATA%\RoboSync\logs`.
7. **Saved jobs** — save the current configuration, reload it, delete it; stored as
   `*.robosync.json` under `%APPDATA%\RoboSync\jobs`.
8. **Validation** — clear, specific error messages; destructive modes require confirmation.
9. **Command preview** — always-current preview of the equivalent Robocopy command, with a Copy button.

### How operation modes map to Robocopy

| Mode | Behavior | Key switches |
| --- | --- | --- |
| **Copy** | Add and update files; never deletes destination-only files. | `/E` (when including subfolders) |
| **Mirror** | Make the destination an exact replica, **including deletions**. | `/MIR` |
| **Move** | Copy, then delete the copied items from the source. | `/E /MOVE` |
| **Sync** | One-way incremental: copy new and newer files only; keep destination extras. | `/E /XO` |

Common switches always applied for safety and clean parsing: `/R:n /W:n` (explicit retry/wait so the
job never hangs on Robocopy's default million retries), `/BYTES /FP /NP` (machine-readable, line-oriented
output), plus `/MT:n`, `/DCOPY:DAT`, `/COPY:DATS`, `/XN`, `/XF`, `/XD`, and `/L` as selected.

---

## Requirements

- **Windows 10 or 11** (Robocopy ships with Windows).
- **.NET 8 SDK** to build/run from source — <https://dotnet.microsoft.com/download>.
  (The packaged single-file build needs **no** .NET installation on the target machine.)

---

## Setup

```powershell
git clone https://github.com/navanem/navanem_RoboCopyGUI.git
cd navanem_RoboCopyGUI
dotnet restore RoboSync.sln
```

## Run (development)

```powershell
# Option A: helper script
pwsh ./scripts/run.ps1

# Option B: dotnet directly
dotnet run --project src/RoboSync.App/RoboSync.App.csproj
```

## Build

```powershell
pwsh ./scripts/build.ps1            # Release build of the whole solution
# or: dotnet build RoboSync.sln -c Release
```

## Test

```powershell
pwsh ./scripts/test.ps1             # unit + Robocopy integration tests
# or: dotnet test
```

## Package for Windows

Produces a standalone, self-contained `RoboSync.App.exe` under `./publish` that runs on any 64-bit
Windows 10/11 machine **without** installing .NET:

```powershell
pwsh ./scripts/package.ps1
```

This runs `dotnet publish -c Release -r win-x64 --self-contained -p:PublishSingleFile=true`. For a much
smaller, framework-dependent build (requires the .NET 8 Desktop Runtime on the target), publish without
`--self-contained`.

---

## Project structure

```
navanem_RoboCopyGUI/
├── RoboSync.sln
├── README.md
├── LICENSE
├── .gitignore
├── samples/                         # Reference saved-job files (*.robosync.json)
│   ├── documents-backup.robosync.json
│   ├── photo-archive-mirror.robosync.json
│   └── project-sync.robosync.json
├── scripts/                         # Local dev / build / package helpers (PowerShell 7)
│   ├── _dotnet.ps1                  # Resolves the dotnet executable
│   ├── run.ps1
│   ├── build.ps1
│   ├── test.ps1
│   ├── package.ps1
│   └── generate-icon.ps1            # Renders the multi-resolution app icon from code
├── src/
│   ├── RoboSync.Core/               # Engine, models, logging, persistence, validation (no UI)
│   │   ├── Models/                  # JobConfiguration, CopyOptions, OperationMode, JobProgress, JobResult
│   │   ├── Engine/                  # RobocopyCommandBuilder, RobocopyOutputParser, RobocopyCopyEngine,
│   │   │                            #   RobocopyExitCodes, ProcessSuspender, ICopyEngine
│   │   ├── Logging/                 # IJobLogger, FileJobLogger
│   │   ├── Persistence/             # IJobStore, JsonJobStore
│   │   ├── Validation/              # JobValidator
│   │   └── Util/                    # ByteFormatter
│   └── RoboSync.App/                # WPF application (UI layer)
│       ├── App.xaml(.cs)            # Composition root
│       ├── MainWindow.xaml(.cs)     # Main view + log auto-scroll
│       ├── AboutWindow.xaml(.cs)    # About dialog (name, version, website)
│       ├── AppInfo.cs               # Product metadata (name, version, vendor, website)
│       ├── ViewModels/MainViewModel.cs
│       ├── Services/                # IUiService + WpfUiService (dialogs, clipboard, shell)
│       ├── Mvvm/                    # ObservableObject, RelayCommand, AsyncRelayCommand
│       ├── Converters/              # LogLevelToBrushConverter
│       ├── Themes/Theme.xaml        # Dark design system + control styles
│       ├── Assets/RoboSync.ico      # Application icon
│       └── app.manifest             # Per-monitor DPI + long-path awareness
└── tests/
    └── RoboSync.Core.Tests/         # xUnit: command builder, parser, validator, store, live engine
```

### Architecture

Clean separation of concerns, with the UI depending only on interfaces:

- **UI** (`RoboSync.App`) — WPF views + a single `MainViewModel`. No business logic in code-behind.
- **Job configuration** (`Core/Models`) — plain serializable models.
- **Copy engine** (`Core/Engine`) — `ICopyEngine` → `RobocopyCopyEngine` builds arguments, runs the
  process, parses output, and reports progress.
- **Logging** (`Core/Logging`) — `IJobLogger` → `FileJobLogger` writes to disk and raises events.
- **Persistence** (`Core/Persistence`) — `IJobStore` → `JsonJobStore` reads/writes saved jobs.
- **Validation** (`Core/Validation`) — pure `JobValidator` with errors and advisory warnings.

### Where your data lives

- Saved jobs: `%APPDATA%\RoboSync\jobs\*.robosync.json`
- Run logs: `%APPDATA%\RoboSync\logs\<timestamp>_<job>.log`

The files in `samples/` are reference configurations. To try one, copy it into
`%APPDATA%\RoboSync\jobs` and it will appear in the **Saved jobs** list, or recreate it in the UI and
press **Save**.

---

## Limitations and future improvements

### Known limitations (MVP scope)

- **Sync is one-way.** True two-way synchronization (with conflict resolution) is out of scope for v1;
  Sync copies new/newer files one direction only and never deletes destination extras.
- **Pause** works by suspending the Robocopy process. It stops progress immediately but does not
  release file handles while paused.
- **Progress percentage and ETA** rely on a quick list-only pre-scan. For very large trees the scan
  adds a short delay before copying starts; if the scan is skipped or fails, the bar runs in
  indeterminate mode and still reports live file/byte counts.
- **Non-ASCII file names** in the live log/progress depend on console output encoding and may render
  imperfectly. The copy itself is unaffected — Robocopy handles them correctly.
- **One job at a time** — the window runs a single job; concurrent jobs are not supported.

### Next 5 best improvements (v2)

1. **Job scheduling** — run saved jobs on a schedule via Windows Task Scheduler integration.
2. **Job queue / batch runner** — line up several saved jobs and run them sequentially with a summary.
3. **Per-file progress and richer parsing** — parse Robocopy's percentage stream for byte-accurate
   per-file progress bars and a transfer-rate graph.
4. **Two-way sync engine** — add an optional managed engine behind `ICopyEngine` for true bidirectional
   sync with conflict handling.
5. **Results summary & history** — a post-run report (copied/skipped/failed counts, failures list) and a
   browsable history of past runs with one-click rerun.

---

## License

MIT — see [LICENSE](LICENSE).

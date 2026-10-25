# Build Simple Tasks

## Requirements

- Windows 10 or 11 with .NET Framework 4.8.
- Windows PowerShell.
- The .NET Framework C# compiler in the Windows directory. The build script checks both the Framework64 and Framework locations.

The project uses built-in .NET Framework libraries and Windows Forms. No .NET SDK, Visual Studio project, NuGet restore, or network connection is needed for this build.

## Build

Download or clone the source, open PowerShell in the repository's root folder, and run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1
```

The script creates:

- `dist\Simple Tasks.exe` and `dist\Simple Tasks.exe.config`.
- A copy of the README, MIT license, and documentation in `dist`.
- `Simple-Tasks-Windows.zip`, containing the portable Windows download.

The script does not install anything or change the system execution policy. Close any app instance running from this source checkout's `dist` folder before rebuilding, because Windows may lock that executable.

## Check a build

The executable includes check modes that use a separate output directory. Run one at a time and check the exit code and report:

```powershell
$appPath = Join-Path (Get-Location) 'dist\Simple Tasks.exe'
$check = Start-Process -FilePath $appPath -ArgumentList '--self-test test-output' -Wait -PassThru
Get-Content -LiteralPath '.\test-output\test-results.txt'
if ($check.ExitCode -ne 0) { throw 'Checks failed.' }
```

`--self-test` exercises persistence, timers, repeat schedules, local-time behavior, calendar parsing, daily tasks, lifetime totals, priority mapping, and color settings. The prepared release passed 147 checks.

Additional interface checks use the same command pattern with these arguments:

| Arguments | Report |
| --- | --- |
| `--preview test-output` | `test-output\ui-results.txt` |
| `--check-interactions test-output` | `test-output\interaction-results.txt` |
| `--check-completed test-output` | `test-output\completed-results.txt` |
| `--check-colors test-output` | `test-output\color-results.txt` |

These modes use test files rather than the normal saved task file. Some checks open short-lived interface windows and save preview images. Generated files are excluded by `.gitignore`.

The `--check-refresh` diagnostic is different: it reads the current user's saved tasks to check rendering against a populated list. It does not save changes, but its preview images may contain private task titles. Use it only when needed and keep its output private.

The calendar command-line import mode writes to the current user's real task file. Use the normal import interface unless you intentionally need that mode. Calendar exports and real task files should stay out of the public repository.

## Share a build

Keep the source at the repository root and attach `Simple-Tasks-Windows.zip` to a GitHub Release. Include the MIT license in each distributed copy. The source build script already places it in the Windows ZIP.

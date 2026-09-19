# Agent check tools

These scripts are shared by all agents and developers. Run from any checkout; no Codex,
Claude or machine-local notes are required. They do not install permission rules.

## Unity checks (Windows and WSL)

Requires Windows PowerShell 5.1 or PowerShell on Windows, plus the Unity version in
`ProjectSettings/ProjectVersion.txt`. The default executable is under
`%ProgramFiles%\Unity\Hub\Editor\<version>\Editor\Unity.exe`.
For a nonstandard installation, pass `-UnityEditor 'D:\Unity\<version>\Editor\Unity.exe'`.
The supplied editor must match the project version. Native macOS/Linux launching is not
implemented; use the raw Unity commands in `CLAUDE.md` on those platforms.

From the checkout on Windows:

```powershell
powershell.exe -NoProfile -ExecutionPolicy RemoteSigned -File .\Tools\shipsim-check.ps1 -Action Status
powershell.exe -NoProfile -ExecutionPolicy RemoteSigned -File .\Tools\shipsim-check.ps1 -Action EditMode
powershell.exe -NoProfile -ExecutionPolicy RemoteSigned -File .\Tools\shipsim-check.ps1 -Action FastCrafts -DryRun
```

From WSL, pass the Windows path to the script:

```sh
powershell.exe -NoProfile -ExecutionPolicy RemoteSigned -File "$(wslpath -w "$PWD/Tools/shipsim-check.ps1")" -Action Status
```

`RemoteSigned` applies only to that PowerShell process; organizational policy still takes
precedence. The launcher refuses another Unity process for this checkout and never stops
processes. `Status` prints process IDs without exposing licensing command-line details.
`DryRun` checks prerequisites and prints arguments without creating output or starting Unity.
Project and output paths containing spaces are supported.

| Action | Operation |
|---|---|
| Status | Inspect Unity processes for this checkout |
| Compile | Headless import and compile |
| EditMode / PlayMode | Test suites with timestamped XML results |
| Shakedown / FastCrafts | Full fleet / Meteor and Luch handling captures |
| BuildCatalogue | Regenerate vessel catalogue assets |
| PhaseOne | Lighting and temporal rendering checks |
| WaterWeather | Fog, rain, bank waves and running lights |
| Wake | Wake runtime capture |
| Buoys | Buoy day/night captures |
| Menu | Start/save/load smoke check |

Logs and test results go to ignored `Logs/` and `TestResults/` directories with timestamped
names. The launcher preserves Unity failure exit codes and rejects missing logs, compiler
errors and missing or unsuccessful test XML. Runtime checks own their pass/fail exit codes;
see `CLAUDE.md` for their PASS markers and screenshot paths. Graphics checks need a working
GPU desktop session. `BuildCatalogue` changes assets; capture checks may replace their own
screenshot folders. Review the resulting Git diff. No automatic timeout or process killing
is performed; inspect a stalled run before deciding to terminate its specific process.

## Test results (Python 3, any OS)

```sh
python3 -I Tools/read-test-results.py
```

Prints one JSON summary per XML file in this checkout's `TestResults/`, skipping symlinks.
Use `python` instead of `python3` where appropriate. Malformed files produce error summaries.
This is a historical report, not a test runner or a pass/fail gate: compare timestamps and
use the Unity launcher's exit status for the current run.

## Personal approvals

The old `.codex/tools/` paths are compatibility wrappers for this workstation's existing
approvals. New users should use `Tools/` directly and configure their own narrowly scoped
permissions. Do not copy workstation paths or allow arbitrary Python/PowerShell commands.
Shared launchers execute project code; review changes to them just like other source code.

## Launcher regression checks

Run `powershell.exe -NoProfile -ExecutionPolicy RemoteSigned -File
.\Tools\test-shipsim-tools.ps1`. It uses temporary fixtures and mocked Unity/process APIs,
including a checkout path with spaces. No Unity editor starts. It checks compile/test
success, compiler errors, exit codes, missing outputs and refusal of a busy project.

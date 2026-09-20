# Windows releases

Use `release.ps1` and `release-smoke.ps1` in native Windows PowerShell 5.1 or PowerShell 7,
or the Bash equivalents in WSL, Git Bash or Linux. The PowerShell scripts use the same
arguments, including double-hyphen options; no WSL or Git Bash is needed. Release creation
requires Git on PATH and a configured Git author identity. Both scripts follow the
CrateVista version bump, release commit, annotated tag and optional push flow.
`PlayerSettings.bundleVersion` in `ProjectSettings/ProjectSettings.asset` is the single
version source. Stable `X.Y.Z` versions only. The menu displays `v{Application.version}`
in its lower-right corner, so editor and player builds use that Unity setting.

## One-time GitHub setup

Enable Actions and add repository secrets under Settings > Secrets and variables > Actions:

| Secret | Value |
| --- | --- |
| `UNITY_EMAIL` | Unity account email |
| `UNITY_PASSWORD` | Unity account password |
| `UNITY_LICENSE` | Entire activated `.ulf` license contents for a Personal license |
| `UNITY_SERIAL` | Professional license serial, used instead of `UNITY_LICENSE` |

Choose the license method appropriate for your account. Follow the current
[GameCI activation instructions](https://game.ci/docs/github/activation/) to obtain
the license. Never commit license files or credentials. No personal GitHub token is
needed: the workflow uses the automatic `GITHUB_TOKEN` with `contents: write`.

## Create a release

Review and commit the release implementation and intended game changes yourself.
The release must include `.github/workflows/release.yml`. Start from a clean
working tree on a branch with full history, and fetch remote tags before choosing a version.
From the repository root in Windows PowerShell:

```powershell
git fetch origin --tags
.\scripts\release.ps1 patch --dry-run --push
.\scripts\release.ps1 patch --push
```

If Windows blocks local scripts under its default execution policy, invoke the script
with a process-local policy, as with the repository's other Windows tooling:

```powershell
powershell.exe -NoProfile -ExecutionPolicy RemoteSigned -File .\scripts\release.ps1 patch --dry-run --push
```

From Bash:

```bash
git fetch origin --tags
bash scripts/release.sh patch --dry-run --push
bash scripts/release.sh patch --push
```

The last command asks for confirmation, updates `bundleVersion`, stages only
`ProjectSettings/ProjectSettings.asset`, and creates a `Release vX.Y.Z` commit limited
to that file. It verifies the committed version, creates an annotated `vX.Y.Z` tag
on that commit, then pushes the current branch to `origin` followed by the release
tag. A failed commit prevents tagging; a failed branch push prevents the tag push.
For example, a patch bump from the current `0.1.0` produces `0.1.1` and `v0.1.1`.

For later releases, use `patch` (the default), `minor`, `major`, or an explicit
version greater than the current bundle version. Existing target tags are rejected.
Use `--yes` for no prompt and `--message TEXT` for a custom tag annotation; the commit
message remains `Release vX.Y.Z`. Without `--push`, the script creates the local
commit and tag and prints both push commands. If pushing fails, retry the failed push
directly instead of rerunning a version bump. If the branch push failed, push the
branch successfully before pushing the existing release tag. Dry runs never change files, refs or
remotes and allow a dirty tree with a warning; real releases reject it.

If a write, staging, commit or tag step fails, the script stops without rolling back
your files or Git history. Inspect `git status` and `git log -1` before continuing.
A commit failure can leave the updated settings staged; a tag failure can leave the
release commit without a tag. Complete the intended release manually after resolving
the error, rather than rerunning a bump and incrementing the version again.

PowerShell examples for subsequent release previews:

```powershell
.\scripts\release.ps1 patch --dry-run
.\scripts\release.ps1 minor -n
.\scripts\release.ps1 major --dry-run
.\scripts\release.ps1 1.2.3 --dry-run --push -m "Release notes"
```

Both implementations also support `-y` for `--yes`, `-m` for `--message`, and
`-h` / `--help`. The release script resolves the repository from its own location.

## Build and verification

A pushed `v*` tag starts `.github/workflows/release.yml`. It rejects tags outside
`vX.Y.Z`, requires the tag version to equal the committed `bundleVersion`, checks
`ProjectVersion.txt` against `6000.6.0f1`, and builds with that exact
editor using [GameCI unity-builder](https://game.ci/docs/github/builder/).
GameCI uses `versioning: None` to preserve the committed Unity version rather than
derive or overwrite it from a tag.

The Ubuntu runner cross-compiles `StandaloneWindows64` with the project's current
Mono backend. All enabled Build Settings scenes are included, with Gorodets first.
If changing to IL2CPP, update the runner/toolchain and smoke checks together.
An editor upgrade also requires updating the workflow's version and guard. CI needs
the matching GameCI editor image and a working Unity license; the first remote run
must establish that these are available for this project.

`release-smoke.sh` and `release-smoke.ps1` reject missing or empty executable, UnityPlayer, Mono runtime,
player data and project runtime assembly files. The workflow ZIPs the entire build
directory, including DLLs, data, streaming assets and other generated companion files,
extracts the ZIP, then repeats the check before publishing the GitHub Release.
The asset is `ShipSim159-vX.Y.Z-windows-x64.zip`. Extract the whole ZIP and run
`ShipSim159.exe`, keeping all adjacent files and directories together.

To check an existing Windows build locally:

```powershell
.\scripts\release-smoke.ps1 --pkg-dir .\build\StandaloneWindows64
.\scripts\release-smoke.ps1 --pkg-dir 'C:\Builds\ShipSim159 release'
```

Or from Bash:

```bash
bash scripts/release-smoke.sh --pkg-dir build/StandaloneWindows64
```

Both smoke scripts default to `build/StandaloneWindows64` relative to the current
directory, accept `-h` / `--help`, and return 0 on success, 1 for missing or empty
build files, and 2 for invalid arguments. The PowerShell smoke script needs no Git
or Unity installation and supports the same `-ExecutionPolicy RemoteSigned` launcher
shown above if local execution policy requires it.

This is a file-layout check, not a gameplay or graphics test. Playability still
needs a Windows launch check; a successful layout check does not establish it.

## Release regression checks

The focused Python checks copy the release script and fixture settings into a temporary
directory and substitute a fake Git executable. They never commit, tag or push a real
repository. They cover SemVer bumps, dirty and invalid preflight states, confirmation,
dry-run immutability, a version-only commit before tagging, push ordering, failure
propagation and preservation of LF/CRLF line endings. On Linux they also exercise the
workflow's tag/bundle-version guard. Python 3 is only a test dependency.

```bash
python3 scripts/tests/test_release.py
```

On native Windows, test the PowerShell implementation with:

```powershell
python .\scripts\tests\test_release.py --powershell
```

`VoyageMenuTests.Menu_PausesStartsAndResumesPreviousSimulationSpeed` also checks that
the visible menu label matches `Application.version` and does not intercept input.

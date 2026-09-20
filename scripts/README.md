# Windows releases

Run these Bash scripts from WSL, Git Bash or Linux. `release.sh` follows FlightTrace's
plan, confirmation, annotated tag and optional push flow. Versions come from tags;
it does not edit Unity settings or create commits. Stable `X.Y.Z` versions only.

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
The tagged commit must contain `.github/workflows/release.yml`. Start from a clean
working tree on a branch with full history, and fetch remote tags before choosing a version:

```bash
git fetch origin --tags
bash scripts/release.sh 0.1.0 --dry-run --push
bash scripts/release.sh 0.1.0 --push
```

The last command asks for confirmation, creates `v0.1.0` on HEAD, then pushes the
current branch to `origin` followed by the release tag. If the branch push fails,
the tag is not pushed.
There are currently no release tags. With no local stable tags, the bump baseline
is `0.0.0`; the explicit first release above matches the project's initial version.

For later releases, use `patch` (the default), `minor`, `major`, or an explicit
version greater than the highest local stable tag. Use `--yes` for no prompt and
`--message TEXT` for a custom annotation. Without `--push`, the script creates the
local tag and prints its push command. If pushing fails, retry the failed push
directly instead of rerunning a version bump. If the branch push failed, push the
branch successfully before pushing the existing release tag. Dry runs never change files, refs or
remotes and allow a dirty tree with a warning; real releases reject it.

## Build and verification

A pushed `v*` tag starts `.github/workflows/release.yml`. It rejects tags outside
`vX.Y.Z`, checks `ProjectVersion.txt` against `6000.6.0f1`, and builds with that exact
editor using [GameCI unity-builder](https://game.ci/docs/github/builder/).
The tag supplies the player's version through `versioning: Tag`.

The Ubuntu runner cross-compiles `StandaloneWindows64` with the project's current
Mono backend. All enabled Build Settings scenes are included, with Gorodets first.
If changing to IL2CPP, update the runner/toolchain and smoke checks together.
An editor upgrade also requires updating the workflow's version and guard. CI needs
the matching GameCI editor image and a working Unity license; the first remote run
must establish that these are available for this project.

`release-smoke.sh` rejects missing or empty executable, UnityPlayer, Mono runtime,
player data and project runtime assembly files. The workflow ZIPs the entire build
directory, including DLLs, data, streaming assets and other generated companion files,
extracts the ZIP, then repeats the check before publishing the GitHub Release.
The asset is `ShipSim159-vX.Y.Z-windows-x64.zip`. Extract the whole ZIP and run
`ShipSim159.exe`, keeping all adjacent files and directories together.

To check an existing Windows build locally:

```bash
bash scripts/release-smoke.sh --pkg-dir build/StandaloneWindows64
```

This is a file-layout check, not a gameplay or graphics test. Playability still
needs a Windows launch check; a successful layout check does not establish it.

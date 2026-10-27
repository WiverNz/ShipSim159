# Release diagnostics correction, 2026-10-27

## What the v0.1.3 run actually reported

[Actions run 40547214885](https://github.com/WiverNz/ShipSim159/actions/runs/40547214885)
built commit `08eea542ed0220dcd16bf8706eafad9070c5788f`, exited successfully and
published a Windows package. Unity nevertheless reported **3 errors and 46
warnings** in its build summary. Error annotations repeated the same asset-import
failure: `Packages/com.gamelovers.mcp-unity/Editor/Tests/McpUnityServerBatchModeTests.cs`
had no `.meta` inside an immutable UPM package. This was not a gameplay exception
or a C# compilation failure. The standard action accepted Unity's successful build
result despite the error count.

Other actionable project warnings identified deprecated `FindObjectsSortMode`
overloads and 15 meshes without their required collision pre-bake flags: 13 convex
507B hull sections and the two static triangular river-bank meshes.

The complete run log was downloaded through the GitHub logs archive API. The
`gh run view --log` command returned an empty result in this environment; the
archive contained the full `0_windows.txt`. Diagnostic statements above are based
on that archive, not merely the run's green status.

## Corrections

- Pin MCP to upstream commit
  [`c69c3c61a2de6492f4d247f94bb3452f3ad82d73`](https://github.com/CoderGamester/mcp-unity/commit/c69c3c61a2de6492f4d247f94bb3452f3ad82d73).
  Its parent is the previously locked `382a43a30f4dab4c7f02d770b4ec7d084813bbe4`;
  the only change adds the missing metadata. Both manifest and lockfile identify
  the fixed revision. The MCP integration and package version remain available.
  No package-cache file is edited and no import error is suppressed.
- Use the Unity 6.6 unsorted `FindObjectsByType<T>()` overload in
  `BathymetryContactSurface`, `PlayerPerformanceCheck` and `VolgoDonContactBuilder`.
  This retains the existing unsorted search behavior.
- Enable convex collision pre-baking on the 13 generated hull-section assets and
  triangle collision pre-baking on the two river-bank assets. Set the same flags
  through `PhysicsEditorMeshExtensions` in their existing builders, so regeneration
  retains the fix. Geometry, convex/static collider choices and force models are
  unchanged.
- Route the release workflow through `LocalWindowsBuild.RunRelease`, retaining
  non-development Mono Windows output, enabled scenes and the committed version.
  The shared builder requires both `BuildResult.Succeeded` and zero reported errors.
  Its regression test explicitly rejects `Succeeded` with three errors, matching
  the observed run. `manualExit` propagates the builder's failure exit status.
  The frame-timing preference is restored before batch exit.
- Upload `Logs/Release/build-report.txt` as `windows-build-report` even if the build
  fails. Packaging and release publication remain subsequent success-only steps.
- Replace ProjectStatus's September overview, five-vessel tables and superseded
  screenshots with the current fleet, equipment, UI, localization, camera,
  verification scope and outstanding work. Preserve detailed historical reviews
  instead of presenting old trial numbers as current measurements.

## Verification

EditMode **325/325** passed in
`TestResults/check-EditMode-20261027-193842-846.xml`. The new fixture verifies the pinned package metadata, release
failure policy and all 15 collision pre-bake settings. PlayMode **187/187** passed in
`TestResults/check-PlayMode-20261027-193914-112.xml`, including existing contact
checks using the same geometry and unsorted component discovery paths.

The Bash release-script regression suite passed **10/10**. It uses a fake Git
executable in a temporary fixture; it does not commit, tag or push this repository.

## Boundaries

The original run also contains infrastructure messages: an initial Library cache
miss, a Node `punycode` deprecation, Linux container preference-directory messages,
Unity licensing token messages and unsupported editor-preview shader variants.
The license/build ultimately succeeded. These are separate from the missing
project dependency metadata and do not justify changing credentials, disabling
render features or hiding errors. The next remote run must confirm the Linux
container result; a local Windows build is not a replay of that environment.

No Actions rerun, commit, push, tag or new release is performed by this correction.
The already published v0.1.3 artifact is unchanged. Changes are prepared locally
for review and the next human-triggered release. The prototype's physical-device
handling, target-GPU performance and integrated usability Part 9 acceptance remain
separate from compilation and package checks.

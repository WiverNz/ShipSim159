# Screenshot inventory

The `status-20261027-*.jpg` images are current Unity runtime captures for
[ProjectStatus](../../Assets/ShipSimulator/Documentation/ProjectStatus.md), refreshed
on 2026-10-27 after the release-diagnostics correction. They show actual rendered
models and UI. They were resized proportionally to at most 1600 pixels wide and
encoded as JPEG at quality 90; no content was synthesized or retouched.
The existing capture harness renders the game HUD with the scene.
The full-resolution source PNGs are generated locally and ignored by Git.

| Image suffix | Source PNG under `Logs/` | Capture path |
|---|---|---|
| `volgodon-507b` | `Shakedown/volgodon-507b-0-chase.png` | Fleet shakedown |
| `volgobalt-295ar` | `Shakedown/volgobalt-295ar-0-chase.png` | Fleet shakedown |
| `volgoneft-1577` | `Shakedown/volgoneft-1577-0-chase.png` | Fleet shakedown |
| `meteor-342u` | `Shakedown/meteor-342u-0-chase.png` | Fleet shakedown |
| `luch-14352` | `Shakedown/luch-14352-0-chase.png` | Fleet shakedown |
| `serpukhov-r96` | `Shakedown/serpukhov-r96-0-chase.png` | Fleet shakedown |
| `serpukhov-mn52` | `Shakedown/serpukhov-mn52-0-chase.png` | Fleet shakedown |
| `vessels` | `MenuScreenshots/menu-seven-commands.png` | PlayMode `VoyageMenuTests` |
| `language` | `Localization/ru/100/language-1920x1080.png` | PlayMode layout matrix |
| `settings` | `Localization/en/100/settings-1920x1080.png` | PlayMode layout matrix |

Run `Tools/shipsim-check.ps1` with `-Action PlayMode`, `-Action Shakedown` and
`-Action Menu` sequentially to regenerate these source captures using the pinned
Unity editor. See the [diagnostics review](../../Assets/ShipSimulator/Documentation/ReleaseDiagnosticsReview.md)
for the exact verification runs. Refresh the dated names and this inventory when
publishing a later set; do not silently relabel old pictures as new captures.

Older unprefixed images in this directory are retained because other documents
reference them. They depict earlier prototypes and are not used as current evidence
in ProjectStatus. A screenshot establishes appearance at its captured state; it
does not establish temporal stability, manual handling comfort or target-GPU performance.

## Configurable HUD, 2026-10-28

- `hud-panels-20261028-settings.png`: unmodified capture from
  `Logs/Localization/ru/100/panels-1920x1080.png`, final focused UI layout suite.
- `hud-panels-20261028-voyage.png`: unmodified capture from
  `Logs/Shakedown/volgodon-507b-0-chase.png`, final seven-selection fleet shakedown.

These captures show the configurable compact HUD added after the dated October 27 gallery.

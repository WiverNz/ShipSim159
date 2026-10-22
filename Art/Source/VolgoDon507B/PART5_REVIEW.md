# Part 5: playable integration and visual controls

The existing Volgo-Don 507B prefab now uses the Part 4 geometry and materials.
Its GUID, catalogue entry, physics controller, Rigidbody and compound collision
hull are retained. Vessel JSON and hydrodynamic coefficients are unchanged.

## Controls

The Volgo-Don HUD has a DECK MACHINERY panel above the weather controls:

- CLOSE: return the paired covers to their home stacks, lift, slide and lower.
- HALF OPEN: open both holds halfway.
- FORE OPEN / AFT OPEN: fully expose the selected hold, parking covers over the
  other hold. Changing between these states returns through the half-open stacks.
- UNSEAT / STOW ANCHORS: move the three anchors a short distance out of their
  stowed positions, or return them. This does not deploy chain or hold the vessel.

Commands go through `ShipPhysicsController` to `VesselMechanisms`. The machinery
panel is absent on vessels without this rig. Existing engine telegraphs and
steering controls are unchanged. Propellers use each shaft's signed actual RPM;
both nozzles follow the actual steering angle rather than the helm command.
Opposite propeller rotation follows the mirrored blade pitch in the authored
geometry. Historical shaft handedness remains an estimate.

Machinery timing is configurable on the prefab component: 40 seconds for the
common opening sequence, 45 seconds to move a pair to expose one hold and five
seconds for anchor unseating. These are visual estimates, not archive timings.
Simulation pause/speed applies to machinery. Resetting the vessel stows it.
Opening samples review frames 90-210, omitting the idle/propulsion demonstration
at the start of the FBX so the covers start lifting immediately after a command.

## Animation and persistence

The integrator extracts `Hatches.anim` and `Anchors.anim` from the validated FBX.
Their transform curves are restricted to the relevant mechanism names. Neither
clip can write the ship root, structural meshes, propellers or nozzles. Imported
Animator/Animation components are removed so they cannot compete with controls.
The runtime samples hatch and anchor curves separately; shaft/steering poses are
computed from the existing physics state.

`VoyageSave.mechanisms` is optional. It stores opening progress, which hold is
exposed, requested destination, anchor progress/command and shaft visual angles.
Mid-transition saves continue toward the same destination. Old saves without this
field restore closed hatches and stowed anchors. Validation rejects non-finite,
out-of-range and mechanically inconsistent states. Existing save version remains 1.

## Lights, camera and audio

The existing five-light `NavigationLightRig` and day/night control are reused.
Port/starboard, forward/aft masthead and stern lights retain their sector cookies
and steady lens rendering. `VesselLayout` positions follow the authored model;
these are estimated fixture positions, not a surveyed navigation-light plan.
Local spill is reduced for this vessel to avoid illuminating whole decks like
floodlights. Other vessels retain their existing intensity/range defaults.
The navigation-lens shader also corrects orthographic pixel sizing: distance to
the camera previously inflated a distant lens into a large translucent disc.
An actual render test covers both orthographic and perspective projections.
The forward/aft masthead elevations are 9.3/12.4 m in the model frame.

The navigator eye is (-1.8, 9.1, -43.3) m in ship-local Unity coordinates, inside
the front bridge windows measured through Blender MCP. A runtime navigator view
confirms the view through the bridge glazing. The inherited bridge interior is
still approximate.

`DetailedVisual/AudioPoints` contains eight silent attachment points: port and
starboard engines, bow water, both propellers, fore/aft hatch machinery and the
windlass. No sound assets or unsupported sound licenses were introduced.

## Integration and fallback

`Ship Simulator > Integrate Detailed Vessel Model` installs the improved visual.
The catalogue builder uses the same path. Repeated integration creates exactly one
`DetailedVisual` and one `VesselMechanisms`. Existing colliders are retained.
`Ship Simulator > Restore Legacy Volgo-Don Visual` restores the original model and
removes the mechanism component. A later normal integration/catalogue build selects
the improved model again. The original FBX and texture resources remain available.
An additional pre-integration prefab checkpoint is in ignored
`Logs/VolgoDon507B/before-part5.prefab`.

The Blender source and FBX geometry were not changed in Part 5:

- `Art/Source/VolgoDon507B/VolgoDon507B.blend`
- `Assets/ShipSimulator/Models/VolgoDon507B_Improved/VolgoDon507B.fbx`
- Installed prefab: `Assets/ShipSimulator/Prefabs/Vessels/VolgoDon507B.prefab`

## Verification and captures

The integration check installs twice and checks the physics component, Rigidbody
serialization, original collider identities/data and single-rig hierarchy. Unit
tests cover independent signed shaft rotation, steering, safe hold transitions,
root invariance, mid-transition restoration, old-save defaults, invalid state,
filtered animation bindings and bounded navigation-light spill.

The menu smoke check exercises HUD callbacks in the actual river scene, opens
each hold, verifies the root stays fixed during machinery sampling, toggles five
sector lights, then saves partway through opening. It changes scenario and vessel
to Volgoneft and loads back into Volgo-Don, checking restored machinery state.
All-vessel shakedown checks sailing, wake, camera and HUD integration.

Verification on 2026-10-22:

- Repeated integration PASS, exit 0: `Logs/VolgoDon507B/part5-integration-final.log`.
- Final scene/HUD/save smoke PASS, exit 0, no compiler errors:
  `Logs/VolgoDon507B/part5-runtime-final.log`. Both VOLGODON_RUNTIME and MENU_SMOKE
  PASS markers are present. The corrected night capture was visually reviewed.
- Final EditMode 209/209 PASS, exit 0:
  `TestResults/check-EditMode-20261022-194058-156.xml`, including lens renders in
  both projections and immediate hatch-command response.
- PlayMode 40/40 PASS, exit 0:
  `TestResults/check-PlayMode-20261022-185803-807.xml`.
- All five vessels passed shakedown, exit 0:
  `Logs/VolgoDon507B/part5-shakedown.log`. This preceded the final local-light
  intensity adjustment, which is covered by the final light tests and scene review.
- All 46 original model/material/parameter resources still match their Part 1
  baseline hashes. The 47th resource, the playable prefab, changes intentionally.
  New Unity assets have `.meta` files. No scene or catalogue assets were changed.

- [Forward hold open in scene](Previews/part5-runtime-ForwardOpen.png)
- [After hold open in scene](Previews/part5-runtime-AftOpen.png)
- [Closed in scene](Previews/part5-runtime-Closed.png)
- [Night lighting](Previews/part5-runtime-night.png)
- Sailing and navigator captures: `Logs/Shakedown/volgodon-507b-*.png`.

The mechanism diagnostic captures pause simulation while sampling states, so some
telemetry fields are blank. Shakedown captures show the populated HUD during sailing.

## Remaining scope

Part 6 reviews nozzle thrust deflection and propulsion calibration. The present
visual steering does not replace the existing generic rudder force model or
claim new manoeuvring accuracy. Part 7 handles cargo/ballast mass and draft.
Full chain payout, seabed anchoring forces, missing jack locations, hydraulic
lines and accurate machinery speeds remain unimplemented. Part 8 addresses LODs
and measured performance. This remains a prototype, not validated maritime training.

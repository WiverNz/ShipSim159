# Part 7: cargo, ballast and loading-dependent behavior

Date: 2026-10-23. Implementation and evidence for the existing playable 507B.
Part 8 has not started. No Blender mesh, FBX, prefab or collider replacement.

## Mass accounting and configuration

The optional `loading.enabled` section in `VolgoDon507B.json` describes five
equivalent mass groups. It is deliberately absent/disabled on the other vessels.
Legacy JSON without this section keeps the previous loading calculation.

| Group | Capacity / default mass | Transfer rate | Estimated local centre (x,y,z), m |
|---|---|---|---|
| Lightship | 1750 / 1750 t | fixed | (0,0,2) |
| Forward cargo | 2400 / 2400 t | 125 kg/s | (0,-1.6,26) |
| Aft cargo | 2400 / 2400 t | 125 kg/s | (0,-1.6,-22) |
| Forward ballast | 1100 / 0 t | 50 kg/s | (0,-2.5,26) |
| Aft ballast | 1100 / 0 t | 50 kg/s | (0,-2.5,-22) |
| Fuel and stores budget | 200 / 200 t | 10 kg/s | (0,-2.1,2) |

All capacity splits, centres, equivalent box dimensions and transfer rates are
authoring estimates. They are not a transcription of the ship's tank table.
Coordinates use Unity x starboard, y up, z forward; the keel is y=-3.53 m.
The default variable mass is **4800 t cargo + 200 t stores = 5000 t deadweight**.
Ballast consumes the same 5000 t budget, not an additional allowance. The initial
condition therefore retains 6750 t total displacement, 3.53 m mean draft and
the original loaded centre (0,-1.2,2). The budget limits modeled displacement;
it is not a safe-loading or stability certificate.

The [archive](https://russrivership.ru/public/files/doc257.pdf) supports the
covered-hold arrangement, ballast operation and a 2200 t ballasted speed condition.
It does not establish this legacy mesh's individual vessel or loading booklet.
Page 3 distinguishes 1565/507B values of 6750/6678 t and 3.53/3.5 m. Existing
6750 t / 3.53 m baseline is retained explicitly, rather than claiming these are
the archive's precise 507B condition. The 1750 t lightship value was derived from
that baseline; the 200 t consumables reservation is an estimate, not published
fuel capacity. The longitudinal groups approximate the two-hold layout; port and
starboard tank asymmetry is unsupported and rejected by configuration validation.

## Physics and continuity

Total mass equals lightship plus the actual sum of cargo, ballast and stores.
CG is their mass-weighted centre. Inertias sum uniform equivalent-box inertias
and parallel-axis terms about the combined CG. Existing gyration radii are used
as estimated lightship radii; the loaded yaw/roll/pitch inertias now differ from
the old constant-radius approximation. Products of inertia are omitted.

The existing wall-sided station-prism hydrostatics remain the common model:
mean draft = loaded draft * current mass / reference loaded mass. Buoyancy
stations retain the same hull and waterplane. Changed weight and CG produce
heave and trim through forces on the Rigidbody, without a position/Transform
offset. The manoeuvring model receives updated mass, CG, added mass, inertia,
wetted area and draft-dependent derivatives. Grounding still samples physical
keel contacts and under-keel clearance follows the actual pose.

Propeller thrust and load torque are scaled by estimated immersed disc height:
clamp((draft - axis height + radius) / diameter, 0, 1). The configured axis is
1 m above keel. Passive nozzle force uses the same factor. This is a mean-draft
ventilation surrogate, not an exact submerged circular area, local trim-aware
inflow, or cavitation calculation. Bow-thruster immersion is refreshed from the
new loading condition using the existing conservative ramp.

Each physical loading step rebuilds the dimensional force/hydrostatic models
and updates Rigidbody mass, CG and inertia. Shaft governor and reversal objects,
actual helm angle, thruster ramp, engine commands, position and velocity survive
the update. Repeated model construction during transfer is a remaining profiling
item for Part 8; there is no rebuild while inventory is unchanged.

## Controls and saves

The lower-left loading panel sits above the deck-machinery controls:

1. Stop engine commands and reduce ground speed below the configured 0.3 m/s.
2. For cargo, use **HALF OPEN** and wait until both hatch groups stop moving.
   Other hatch positions conservatively pause cargo transfer in this prototype.
3. Use **SELECT COMPARTMENT**, then **FILL** or **EMPTY**. Commands persist for
   each group, so more than one group can transfer. **STOP ALL** cancels them.
4. Read compartment tonnes/capacity, total displacement, mean draft and status.
   The main HUD now calls the shared percentage **deadweight**, not cargo.

Rates are per simulation second and respect pause/time scale. Transfer is an
external exchange with shore or surrounding water, not internal mass creation.
Withdrawals happen before fills, and fills are clipped to compartment capacity
and remaining shared budget. Filling ballast on the fully loaded default ship
does nothing until mass is removed. Transfers pause under way or with an engine
command; they do not cancel the saved command. No port/crane infrastructure,
pump power demand or automatic engine fuel consumption is modeled in this part.

Optional `VoyageSave.loading` stores explicit presence, compartment identifiers,
mass arrays and transfer commands. Capture copies arrays. Read/restore checks
finite values, lengths, identities, capacities and the total mass budget. Older
saves without the section restore the vessel's default inventory. Other vessels
retain their existing save behavior. Unity can instantiate absent nested JSON
objects, so explicit `enabled` / `present` flags are required instead of null-only
checks. Reset stops transfers without changing the current inventory.

## Numerical and runtime evidence

Repeatable calm/deep-water results: `part7-loading.json`, generated by
`ShipSimulator.Editor.VolgoDonLoadingReview.Run`. Uniform water, no wind/current,
existing pure simulator and standard stopping procedure. These are model outputs,
not measured full-scale performance.

| Condition | Total t | Cargo / ballast / stores t | Mean draft m | Full speed km/h | Stop m / s |
|---|---:|---|---:|---:|---:|
| Dry lightship bound | 1750 | 0 / 0 / 0 | 0.915 | 17.38 | 259 / 107 |
| Half initial inventory | 4250 | 2400 / 0 / 100 | 2.223 | 20.86 | 520 / 192 |
| Default full | 6750 | 4800 / 0 / 200 | 3.530 | 19.80 | 719 / 286 |
| Ballast plus stores | 4150 | 0 / 2200 / 200 | 2.170 | 20.90 | 511 / 188 |
| Forward-biased half load | 4250 | 2400 / 0 / 100 | 2.223 | 20.86 | 520 / 192 |

The biased condition moves 500 t from aft cargo to forward cargo: total mass is
unchanged, but longitudinal CG moves from 2 to 7.647 m forward. Pure planar
straight-line trials do not resolve trim resistance, which explains identical
straight-line speeds; separate Rigidbody tests verify actual bow-down trim.
The full condition retains Part 6 straight-line speed/stopping behavior. Its
stopping and turning accuracy limitations remain, not resolved by loading work.

The dry-lightship calculation is a boundary test, not a proposed service condition:
only about 45% propeller effectiveness remains and the bow thruster is dry.
Wall-sided prisms give exaggerated low-draft metacentric heights (18.5 m dry,
about 5.0 m full). Free-surface effects, real sectional hydrostatics, downflooding,
cargo-density-dependent KG and certified stability limits are not implemented.
No loading panel value should be interpreted as a stability approval.

Verification completed:

- EditMode **228/228**, `TestResults/check-EditMode-20261023-011059-805.xml`.
  Covers inventory conservation, shared capacity, finite-state validation,
  compartment identity, save round trip, cargo access, rates, immersion,
  parallel-axis response, shaft/reversal continuity and existing vessel trials.
- PlayMode **47/47**, `TestResults/check-PlayMode-20261023-011324-017.xml`.
  Includes empty/half/full physical flotation, forward-load trim, mass updates
  without teleporting, blocked transfers under way, old/new save behavior, and
  clearance recovery after unloading a grounded vessel.
- Loading review PASS, exit 0: `Logs/VolgoDon507B/part7-loading.log`.
  All stored handling metrics for the other four playable vessels and KVLCC2
  exactly match Part 6. Full-load 507B straight-line results remain unchanged.
- Menu smoke PASS, exit 0: `Logs/check-Menu-20261023-011005-663.log`.
  Tests actual loading HUD buttons, hatch interlock, ballast rate and persistence
  through save, vessel/scenario change and reload. Uses a test save in Logs.
- Five-vessel runtime shakedown PASS, exit 0:
  `Logs/check-Shakedown-20261023-011412-502.log`. Existing controller, water and
  grounding integration remain in use. Visual inspection confirmed the new
  loading panel fits above machinery controls without overlap at 1600x900.
- HUD capture: [part7-loading-hud.png](Previews/part7-loading-hud.png).
  This is the retained Part 7 review image; routine smoke captures go to Logs.
- `git diff --check` passed. No commits or push were made.

The initial check caught Unity's absent-nested-object deserialization behavior;
explicit flags fixed optional loading and legacy-save compatibility. The final
suites include all vessels. Part 8 remains pending the user's next instruction.

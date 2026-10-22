# Part 6: propulsion, steerable nozzles and handling

Date: 2026-10-22. Scope: the existing playable 507B, classic covered-hold
configuration. Part 7 loading work is not included. No model, source Blender
file, FBX, material, collider or prefab replacement was needed in this part.

## Implemented behavior

- Independent engine commands retain torque-based shaft dynamics, the 15 s
  setpoint ramp, 25% reversal threshold, 8 s restart delay and braking torque.
- Optional nozzle data replaces generic rudder force for 507B. Signed thrust
  deflects with actual helm angle, while passive nozzle/stabilizer cross-flow
  dissipates energy. Sway force and both lever-arm moments reach the existing
  rigid-body solver. Generic rudder slipstream is not counted again.
- Two 1.8 m screws replace the old 2.1 m physics diameter. Shaft/nozzle positions
  align with the authored geometry at 64.65 m aft and 3.6 m either side.
- Archived bow-waterjet power and tube diameter replace 160 kW / 1 m estimates.
  Existing ramp, immersion and speed-dependent force logic is retained, with
  effectiveness approaching zero at high speed instead of a 30% floor.
- Existing controls, actual shaft/nozzle animation, Rigidbody integration and
  save contracts remain in use. Forces are not implemented with Transform motion.
- Trial results now expose completion flags so a timed-out zigzag cannot be
  mistaken for zero overshoot. The review runner checks completion of turns,
  stopping and both 10/10 and 20/20 zigzags.

## Sources and parameter decisions

Primary source: [archive doc257.pdf](https://russrivership.ru/public/files/doc257.pdf).
The selected version remains approximate: no individual vessel/date has been
identified from the legacy mesh. Do not mix later engine upgrades into this setup.

| Parameter | Active value | Evidence and confidence |
|---|---|---|
| Main engines | 2 x 662 kW, reversible 6ChRN 36/45 | Archive p5: G60, 900 metric hp each |
| Engine rating | 375 rpm | Archive p5; the 1200 hp G70 entry is a different installation |
| Shaft setpoint | 375 rpm | Assumed 1:1 main drive, not established by archive; auxiliary shaft-generator reduction is not main-shaft evidence |
| Screws | 2 x 1.8 m, four blades | Archive p5; left/right pitch 1.52/1.47 m and area ratio 0.62 remain visual reference; hydrodynamic curves do not resolve handedness/pitch asymmetry |
| Ahead KT / KQ | [0.33,-0.25,-0.12] / [0.0336,-0.0176,-0.0096] | Estimated equivalent propulsion curves; KQ scaled to 80% of old fit to reconcile diameter, torque and speed |
| Astern KT / KQ | [0.23,-0.20,-0.10] / [0.0288,-0.0144,-0.008] | Estimated, not measured four-quadrant data |
| Wake / deduction | 0.20 / 0.20 | Retained estimates; no separate duct thrust boost |
| Nozzle dimensions | diameter 1.84 m, length 1.66 m | Archive p7; passive projected area 3.0544 m2 derived from these dimensions |
| Nozzle coefficients | deflection factor 1.0; passive normal slope 3.0 | Tunable approximations including stabilizers; not published 507B coefficients |
| Steering | +/-35 deg, 2.5 deg/s, common helm | Archive p7: 70 deg in at most 30 s; current actuator takes 28 s. Separate nozzle control is documented but not implemented |
| Nozzle location | x=-64.65 m, y=+/-3.6 m | Authored geometry, not measured hull offsets |
| Bow waterjet | 72 kW, 710 mm tube | Archive p7; represented by existing actuator-disc surrogate, not an actual pump/nozzle solution |
| Bow estimated inputs | x=60 m, axis=1.1 m above keel, merit=.62, ramp=5 s, speed scale=1 m/s, floor=0 | Unconfirmed geometry/efficiency/effectiveness. Predicted static thrust about 10 kN |
| Reference loading | 6750 t, draft 3.53 m, fresh water 1000 kg/m3 | Retained prototype condition. Archive p3 pairs 1565/507B as 6750/6678 t and 3.53/3.5 m; loading reconciliation belongs to Part 7 |
| Residual resistance C_R | 0.0001005 | Estimated service-speed calibration; friction/form factor unchanged |
| Added mass and inertia | unchanged | Existing estimates; not retuned to force a match to the archive circulation figures |

[Wartsila steering-nozzle description](https://www.wartsila.com/encyclopedia/term/steering-nozzle)
supports the deflected-jet mechanism, not the numerical coefficients used here.
All numerical coefficients without specific archive support remain estimates.

## Repeatable trial comparison

Raw evidence: `part6-before.json`, `part6-after.json`, and the preserved
`part6-baseline-vessel.json`. Run editor method
`ShipSimulator.Editor.VolgoDonHandlingReview.After` in Unity batch mode.
`Before` refuses to overwrite the original baseline. The runner also checks
unchanged results for the other four playable ships and KVLCC2 against that baseline.

Conditions: full prototype loading above, calm water, zero wind/current, no banks
or external forces, deep water or 4.6 m uniform depth. Standard manoeuvres use
0.05 s steps after a 2400 s approach at 0.2 s steps; acceleration uses 0.1 s.
Differential thrust runs port +1 / starboard -1 from rest for 180 s; low-speed
turn runs both shafts at .32 and full starboard helm from rest for 180 s.
Bow rate is measured after 180 s from rest with engines stopped.

| Deep-water result | Before | After | Archive p3 comparison |
|---|---:|---:|---|
| Full ahead | 18.17 km/h | 19.80 km/h | Loaded 20 km/h; -1.0% |
| Full astern | 14.33 km/h | 16.74 km/h | No matching reference |
| Advance | 434.6 m | 518.1 m | No unambiguous reference |
| Tactical diameter | 500.0 m | 618.2 m | Archive circulation 320/160 m lacks speed, condition and diameter definition |
| Steady turning diameter | 411.3 m | 499.8 m | Significant disagreement remains; not validated |
| Full-ahead crash stop distance | 604.8 m | 718.5 m | 510 m; current prediction +40.9%, worse agreement than baseline |
| Crash stop time | 262.6 s | 286.0 s | 210 s; +36.2%, worse agreement than baseline |
| 10/10 first / second overshoot | 3.60 / 4.16 deg | 3.31 / 3.96 deg | No matching reference |
| Differential heading after 180 s | 17.51 deg | 19.88 deg | No matching reference |
| Low-speed heading after 180 s | 25.24 deg | 30.27 deg | No matching reference |
| Bow-only rate at 180 s | 19.29 deg/min | 12.79 deg/min | Lower archived power, no measured rate |
| Steady port shaft / delivered-power load | 254.4 rpm / 98.8% | 341.9 rpm / 91.2% | Power-limited torque balance, no forced-speed animation |
| Time to 90% full speed | 443.0 s | 482.4 s | Multi-minute acceleration; no matching reference |

At 4.6 m depth the new prediction is 13.80 km/h, 1251 m tactical diameter,
359 m stopping distance / 203 s. These are restricted-water estimates, not
evidence of safe operation at that depth. The shallow-water correction model
and initial conditions explain why stopping distances differ from deep water.

## Verification and remaining limits

- EditMode: 217/217 passed, including eight new nozzle cases, existing shaft
  reversal, bollard pull, open-water efficiency, all-vessel trials and KVLCC2.
  Evidence: `TestResults/check-EditMode-20261022-195651-185.xml`.
- PlayMode: 40/40 passed, including existing physics, vessel swaps and save flow.
  Evidence: `TestResults/check-PlayMode-20261022-195740-130.xml`.
- Review runner: exit 0 and `VOLGODON_HANDLING|PASS|after` in
  `Logs/VolgoDon507B/part6-after.log`. Both zigzags, turning and stopping finish;
  every stored metric for the other four playable vessels and KVLCC2 exactly
  matches the pre-change report.
- An initial candidate failed the existing efficiency/bollard limits. It was
  discarded; the final calibration passes those unchanged physical bounds.
- Runtime shakedown: all five vessels passed, exit 0 and
  `VESSEL_SHAKEDOWN|PASS: 5 vessels` in
  `Logs/check-Shakedown-20261022-195829-086.log`. Screenshots and report are in
  `Logs/Shakedown/`. This short accelerating river run is not a steady-speed trial.
- `git diff --check` passed. Source Blender, both FBX variants, prefab, original
  materials and the other vessel JSON files were not modified.

The archive does not supply trial loading, depth, environmental conditions,
turn definition or measured propeller/pump curves. Stopping and turning values
therefore remain comparison targets, not proof of accuracy. The forward-speed
fit does not validate braking or reverse-quadrant forces. Locked-propeller drag,
duct cavitation, separate nozzle commands and exact air-start timing are not
resolved. Immersion/loading refinement remains Part 7, with full LOD/performance
review in Part 8. No certified stability or maritime-training claim is made.

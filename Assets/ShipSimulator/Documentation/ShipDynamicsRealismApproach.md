# Ship Dynamics Realism: Research and Implementation Approach

Status: research written 2026-09-13; phases 1 to 6 of the roadmap (section 17) implemented the
same day. Phase 7 (ship-ship interaction, locks, mooring, anchors) is not implemented. See
"Implementation status" at the end of section 17 for deviations from this proposal, and
`ShipSimulator_Physics.md` for the model as built.

It explains how a real ship's motion depends on its mass and loading, the water, the wind,
the current and restricted depth, collects the standard formulas used in manoeuvring
simulators, audits the current ShipSim159 physics against them, and proposes an
implementation path for Unity.

Important limits:

- The formulas are from published literature (sources in section 18). Several were checked
  against open reference implementations during this research (the MMG equations and
  KVLCC2 coefficients via the pymaneuvering package, Blendermann and Clarke formulas via
  Fossen's MSS toolbox). Anything marked "verify" must be checked against the original
  paper before implementation.
- All Project 507B numbers in this document that are not in `VolgoDon507B_Sources.md` are
  **illustrative estimates**. They show orders of magnitude, not validated data.
- A formula-based model becomes a *real* simulator only after calibration and validation
  against measured trials of the vessel (section 16). Until then ShipSim159 stays a
  prototype, not validated for maritime training.

## 1. How the forces act on a ship

A displacement ship moves in the horizontal plane under a balance of large forces that
almost cancel. Understanding which force dominates in which situation is the basis for a
believable simulator.

| Physical effect | What it changes | How it shows up in handling |
|---|---|---|
| Weight and buoyancy | Draft, trim, heel; the underwater shape | Loaded ships are slower to accelerate and stop, carry way longer, heel less in wind, and turn more sluggishly |
| Rigid-body inertia | Resistance to acceleration and rotation | A 6 750 t ship takes minutes to change speed and tens of seconds to start or stop a turn |
| Added mass | Water that must be accelerated with the hull | Effective mass sideways is often 1.5 to 2 times the ship mass, and more in shallow water |
| Hull resistance | Surge speed for a given thrust | Terminal speed, coasting distance |
| Hull lift from drift and yaw | Sway force and yaw moment when the hull moves at an angle | The hull, not the rudder, produces most turning force once a turn is established; ships are often directionally unstable |
| Propellers | Thrust from RPM and inflow speed | Thrust is highest at bollard (zero speed) and drops with speed; astern thrust is weaker |
| Rudders | Lift from the flow over the blade | Steering depends on propeller slipstream, so a kick ahead gives steering even at very low speed |
| Wind | Force and moment on the part above water | Leeway (sideways drift), weather helm, heel, harder berthing; large when light |
| Current | Moves the water mass the ship floats in | Ground track differs from heading; a non-uniform current rotates the ship |
| Shallow water | Flow squeezed under the keel | More resistance and added mass, weaker turning, bigger turning circle, squat (sinkage) |
| Banks and other ships | Asymmetric pressure field | Stern sucked toward the bank and bow pushed away; interaction when passing |

## 2. Audit of the current implementation

Reviewed files: `ShipPhysicsController.cs`, `PropulsionController.cs`,
`RudderController.cs`, `HydrodynamicResistance.cs`, `BuoyancyPoint.cs`,
`CurrentFieldProvider.cs`, `GroundingController.cs`, `ScenarioBathymetry.cs`,
`VolgoDon507B.json`.

Structure that is good and should be kept:

- The vessel is a Unity `Rigidbody` moved only by forces in `FixedUpdate`.
- Water-relative velocity is already computed from the effective current.
- Data-driven parameters with `VesselDataValidator`, save/restore, grounding and
  bathymetry sampling.

Findings, each derived from the code and the JSON:

1. **Propeller thrust is independent of speed and inconsistent with engine power.** Thrust is
   `throttle × maxAheadThrustN × count`, i.e. up to 2 × 310 kN = 620 kN at any speed. From
   actuator-disc momentum theory, the ideal bollard thrust of one propeller absorbing power P
   is `T = (2 ρ A P²)^(1/3)`. With 662 kW per engine, about 0.92 transmission efficiency and
   the estimated 2.1 m diameter, this gives about 136 kN ideal per propeller, and realistic
   open propellers reach perhaps 60 to 70 % of the ideal, so roughly **170 to 190 kN total**
   (estimate). The configured thrust is about three times that.
2. **No physical speed limit.** `maxLoadedSpeedMps` is read only by the HUD. Solving the
   deep-water resistance in the JSON, `5000 v + 5000 v² = 620 000 N`, gives about 10.6 m/s
   (about 20 kn) of terminal speed, against the published 10 ± 0.5 kn loaded speed. Shallow
   areas mask this through the ad hoc 2.4 resistance multiplier.
3. **No added mass.** Sway and yaw accelerate with rigid-body inertia only, so the ship starts
   and stops sideways motion and rotation too easily.
4. **No coupling between sway and yaw.** Resistance is separate in surge, sway and yaw. A real
   hull moving with a drift angle produces a yaw moment (the Munk moment and hull lift), which
   is what makes ships directionally unstable and gives turns their characteristic shape.
5. **Rudder sees only the ship's own water speed.** Lift uses the hull's water velocity, so at
   zero speed with engines full ahead there is no steering. Real twin-screw ships with rudders
   in the slipstream steer well with a short kick ahead. Lift is also linear up to 35° with no
   stall, and there is no hull-rudder interaction (the extra hull force induced by the rudder).
6. **Twin screws are merged.** One aggregate thrust is applied at the centreline, so engines
   cannot be used differentially to turn, a basic twin-screw manoeuvre.
7. **Wind force ignores the ship's shape.** `F = 800 · |V_rel| · V_rel` is isotropic, applied at
   the centre of mass, with no yaw moment and no dependence on wind angle or windage areas. At
   15 m/s beam wind it gives 180 kN; a Blendermann estimate with illustrative 507B windage gives
   about 76 kN (section 10), and it would also produce a yaw moment.
8. **Current shear is an arbitrary torque.** `-Δv_lateral · mass · 18`, clamped, rather than an
   integration of hull forces over the length.
9. **Shallow-water effects are ad hoc.** Squat `0.018 · V² · severity`, resistance up to × 2.4
   and rudder effectiveness down to 0.55 are not tied to published relations, and hydrodynamic
   derivatives and added mass do not change with depth.
10. **Buoyancy is not hydrostatics.** 15 point forces with a 1.5 reserve factor produce a stable
    float but the waterplane stiffness, metacentric height, roll period and trim response are
    not derived from the hull.
11. **Grounding drag is linear in velocity** with large constants, not a friction and contact
    model.

## 3. Coordinates and notation

The standard literature (MMG, Fossen) uses a right-handed body frame with x forward, y to
starboard, z down. Unity uses a left-handed frame with z forward, x to the right (starboard),
y up. The mapping for horizontal manoeuvring is direct:

| Quantity | MMG / Fossen | Unity body frame |
|---|---|---|
| Surge velocity u, force X | +x forward | +z local |
| Sway velocity v, force Y | +y starboard | +x local |
| Yaw rate r, moment N | positive turning bow to starboard | positive rotation about +y (seen from above, clockwise) |
| Heave | +z down | -y |

In the MMG standard (Yasukawa and Yoshimura 2015) a positive rudder angle δ produces a
positive yaw moment, i.e. a turn to starboard. Any implementation must have a unit test that a
positive helm command gives a positive yaw rate, because sign errors here are common.

Symbols used below: ρ water density (1000 kg/m³ fresh), ρ_a air density (about 1.225 kg/m³),
L = L_pp, B beam, d or T draft, C_B block coefficient, ∇ displacement volume, m mass,
U = √(u² + v²) speed through water, β = atan(-v/u) drift angle, h water depth, g = 9.81 m/s².

Non-dimensional ("prime") quantities in MMG:

```
v' = v / U            r' = r L / U
X' = X / (½ ρ L d U²)  Y' = Y / (½ ρ L d U²)  N' = N / (½ ρ L² d U²)
m' = m / (½ ρ L² d)    I' = I / (½ ρ L⁴ d)
```

## 4. Equations of motion

### 4.1 Three degrees of freedom (surge, sway, yaw), MMG form

With the origin at midship and the centre of gravity at x_G, the MMG standard method writes:

```
(m + m_x) u̇ − (m + m_y) v r − x_G m r²          = X_H + X_R + X_P + X_W + X_E
(m + m_y) v̇ + (m + m_x) u r + x_G m ṙ           = Y_H + Y_R + Y_W + Y_E
(I_zG + x_G² m + J_z) ṙ + x_G m (v̇ + u r)        = N_H + N_R + N_W + N_E
```

m_x, m_y: added masses in surge and sway; J_z: added moment of inertia in yaw. Subscripts: H
hull, R rudder, P propeller, W wind, E external (banks, ship interaction, mooring, contact).

This is equivalent to Fossen's vector form

```
M ν̇ + C(ν) ν + D(ν) ν = τ,   ν = [u v r]ᵀ,  M = M_RB + M_A
```

which is convenient in code because M is a constant 3 × 3 matrix to invert once per
loading condition.

### 4.2 Including current

For a uniform current, all hydrodynamic forces depend on the **relative velocity**
ν_r = ν − ν_c. Fossen's relative-velocity form is

```
M_RB ν̇ + C_RB(ν) ν + M_A ν̇_r + C_A(ν_r) ν_r + D(ν_r) ν_r = τ
```

The added-mass Coriolis term C_A(ν_r) ν_r contains the **Munk moment** (m_y − m_x) u_r v_r,
the destabilising yaw moment of a slender body moving at an angle to the flow. For a
non-uniform river current the relative velocity varies along the hull (section 11).

### 4.3 Vertical motions

Heave, roll and pitch matter for squat, heel in turns and wind, and bank or bottom contact,
but not for the horizontal track at river speeds. Two workable options:

1. Keep them in Unity's 6-DOF `Rigidbody`, driven by hydrostatic forces computed from the hull
   (section 5.2) plus damping, and solve only surge, sway and yaw with the manoeuvring model.
2. Solve a 4-DOF model (surge, sway, roll, yaw) as in Fossen chapter 7 if roll coupling in
   tight turns becomes important. For a river cargo ship with a large metacentric height this
   is usually not needed.

## 5. Mass, loading and hydrostatics

### 5.1 Loading condition

For each loading condition the model needs:

- Displacement `Δ = ρ ∇ = lightship + deadweight carried`, and from hydrostatic tables the
  mean draft T and trim for that displacement and longitudinal centre of gravity.
  `Tonnes per centimetre immersion TPC = ρ A_WP / 100` relates small mass changes to draft.
- Centre of gravity (x_G, KG) from lightship data plus cargo, fuel and ballast positions.
- Radii of gyration. Typical estimates: yaw k_zz ≈ 0.25 L, roll k_xx ≈ 0.35 to 0.40 B,
  pitch k_yy ≈ 0.25 L. Then `I = m k²`. The current JSON implies k_zz ≈ 40 m (0.30 L) and
  k_xx ≈ 4.7 m (0.29 B), both plausible but estimated.
- Block coefficient at the actual draft (C_B falls as a full-bodied ship lightens).

Loading changes manoeuvring through several channels at once: mass and inertia scale with
displacement; the hull force scale `½ ρ L d U²` scales with draft; lateral windage area grows
as freeboard increases; propeller and rudder immersion falls in ballast; and under-keel
clearance in a given channel shrinks as draft grows, strengthening shallow-water effects.

### 5.2 Hydrostatics from the hull geometry

Instead of reserve-factor point forces, compute hydrostatics from the actual hull:

1. Slice the hull mesh (or a dedicated simplified hydrostatic mesh) into 20 to 40 stations.
2. For each station and instantaneous waterline (including squat and heel) compute the
   immersed section area and its centroid.
3. Buoyancy `F_B = ρ g ∇` acts at the centre of buoyancy; apply it per station at the section
   centroid, which gives correct trim and heel moments automatically.
4. Precompute a table of section area versus local draft per station for speed.

Useful checks: metacentric height `GM = KB + BM − KG` with `BM = I_T / ∇` (I_T the transverse
second moment of waterplane area), and natural roll period `T_φ ≈ 2π k_xx / √(g GM)`.

Illustrative 507B estimate: a box-shaped waterplane gives `I_T = L B³ / 12 ≈ 50 500 m⁴` and
BM ≈ 7.5 m; a real waterplane is somewhat smaller, so BM ≈ 6 to 7 m. With KB ≈ 1.9 m and a
loaded KG of perhaps 4.5 to 5 m, GM is about 3 m and the roll period about 7 s. River cargo
ships typically have large GM like this. These numbers are estimates for plausibility checks
only.

Heel in a steady turn (small-angle approximation used in intact stability practice):

```
tan φ ≈ V² (KG − T/2) / (g R GM)
```

### 5.3 Added mass

Added mass is the most important missing inertia. Empirical estimates from principal
dimensions:

**Clarke, Gedling and Hine (1983)**, as implemented in Fossen's MSS `clarke83.m`, with
`S = π (T/L)²`:

```
Y'_v̇ = −S (1 + 0.16 C_B B/T − 5.1 (B/L)²)
Y'_ṙ = −S (0.67 B/L − 0.0033 (B/T)²)
N'_v̇ = −S (1.1 B/L − 0.041 B/T)
N'_ṙ = −S (1/12 + 0.017 C_B B/T − 0.33 B/L)
```

Dimensional values: `Y_v̇ = Y'_v̇ · ½ ρ L³`, `Y_ṙ, N_v̇ = ′ · ½ ρ L⁴`, `N_ṙ = N'_ṙ · ½ ρ L⁵`,
and m_y = −Y_v̇, J_z = −N_ṙ.

**Surge added mass** by Söding's approximation (MSS `addedMassSurge`):
`m_x ≈ 2.7 ρ ∇^(5/3) / L²`.

Illustrative 507B deep-water estimate (L = 135 m, B = 16.5 m, T = 3.53 m, C_B = 0.851,
∇ ≈ 6 690 m³, m ≈ 6.75 × 10⁶ kg):

| Quantity | Estimate | Ratio |
|---|---|---|
| m_x | ≈ 0.35 × 10⁶ kg | ≈ 5 % of m |
| m_y | ≈ 4.1 × 10⁶ kg | ≈ 61 % of m |
| J_z | ≈ 5.3 × 10⁹ kg m² | ≈ 49 % of I_z in the JSON |

In shallow water m_y and J_z increase strongly as h/T approaches 1 (section 12.1).

## 6. Hull forces

### 6.1 MMG hull force model

```
X_H = ½ ρ L d U² ( −R'_0 + X'_vv v'² + X'_vr v' r' + X'_rr r'² + X'_vvvv v'⁴ )
Y_H = ½ ρ L d U² ( Y'_v v' + Y'_R r' + Y'_vvv v'³ + Y'_vvr v'² r' + Y'_vrr v' r'² + Y'_rrr r'³ )
N_H = ½ ρ L² d U² ( N'_v v' + N'_R r' + N'_vvv v'³ + N'_vvr v'² r' + N'_vrr v' r'² + N'_rrr r'³ )
```

R'_0 is the straight-ahead resistance coefficient (section 6.3). The linear derivatives
dominate small manoeuvres; the cubic terms shape large drift angles and tight turns.

Reference benchmark: the MMG standard paper gives a complete coefficient set for the KVLCC2
tanker, reproduced in pymaneuvering. It is **not** a 507B dataset, but reproducing the
paper's KVLCC2 turning and zig-zag simulations is the right first test of an implementation.

| Coefficient | KVLCC2 value | Coefficient | KVLCC2 value |
|---|---|---|---|
| m'_x | 0.022 | m'_y | 0.223 |
| J'_z | 0.011 | R'_0 | 0.022 |
| X'_vv | −0.040 | X'_vr | 0.002 |
| X'_rr | 0.011 | X'_vvvv | 0.771 |
| Y'_v | −0.315 | Y'_R | 0.083 |
| Y'_vvv | −1.607 | Y'_vvr | 0.379 |
| Y'_vrr | −0.391 | Y'_rrr | 0.008 |
| N'_v | −0.137 | N'_R | −0.049 |
| N'_vvv | −0.030 | N'_vvr | −0.294 |
| N'_vrr | 0.055 | N'_rrr | −0.013 |
| t_P | 0.220 | w_P0 | 0.35 full scale (0.40 model) |
| k_0, k_1, k_2 | 0.2931, −0.2753, −0.1385 | C_1, C_2 | 2.0; 1.6 (β_P > 0), 1.1 (β_P < 0) |
| t_R | 0.387 | a_H | 0.312 |
| x'_H | −0.464 | ε | 1.09 |
| κ | 0.50 | l'_R | −0.710 |
| γ_R | 0.640 (β_R > 0), 0.395 (β_R < 0) | f_α | 2.747 |

### 6.2 Estimating 507B hull derivatives

Without captive model tests, derivatives must be estimated, then calibrated:

1. **Clarke et al. (1983) linear damping derivatives** (MSS `clarke83.m`):

   ```
   Y'_v = −S (1 + 0.4 C_B B/T)
   Y'_r = −S (−1/2 + 2.2 B/L − 0.08 B/T)
   N'_v = −S (1/2 + 2.4 T/L)
   N'_r = −S (1/4 + 0.039 B/T − 0.56 B/L)
   ```

   with the same S and the MSS non-dimensionalisation (½ ρ L² U for Y_v; ½ ρ L³ U for Y_r and
   N_v; ½ ρ L⁴ U for N_r). Note this differs from the MMG prime system in section 3, which uses
   L d instead of L²; convert carefully.
2. **Kijima et al. (1990)** regression formulas give linear and nonlinear MMG-type derivatives
   from L, B, d, C_B and trim, including loading effects. Verify the formulas in the original
   paper before use.
3. **CFD** (virtual captive tests: oblique towing, rotating arm, planar motion) on the 507B hull
   geometry, following the MMG test procedure.
4. **System identification** from full-scale manoeuvres or AIS tracks (section 16).

The 507B is long and slender for a cargo ship (L/B ≈ 8.2, B/T ≈ 4.7) and operates at very low
h/T, which is outside the range of many ocean-ship regressions. Treat estimates as starting
points only.

### 6.3 Straight-ahead resistance

Decomposition used by ITTC and Holtrop and Mennen (1982, 1984):

```
R_T = R_F (1 + k) + R_APP + R_W + R_A ...
R_F = ½ ρ V² S C_F,     C_F = 0.075 / (log₁₀ Re − 2)²   (ITTC 1957),   Re = V L / ν
R_A = ½ ρ V² S C_A     (model-ship correlation allowance)
```

Holtrop and Mennen give regressions for the form factor (1 + k), wave resistance R_W, wetted
surface S, and also for wake fraction w, thrust deduction t and relative rotative efficiency.
Their twin-screw propulsion factor regressions should be taken from the 1984 paper directly.

Illustrative 507B estimate at 10 kn (5.14 m/s), deep calm water: Mumford's wetted surface
`S ≈ 1.7 L T + ∇/T ≈ 2 700 m²`; Re ≈ 6.1 × 10⁸ (ν = 1.14 × 10⁻⁶ m²/s); C_F ≈ 0.00163;
R_F ≈ 58 kN. With a form factor and correlation allowance typical of full forms, deep-water
resistance is of order 80 to 100 kN; wave resistance is small at the length Froude number
Fn = V/√(gL) ≈ 0.14. Realistic effective power is then consistent with 1 324 kW installed
once shallow-water resistance increase and propulsive efficiency are included. These are
estimates.

`R'_0` for the MMG model follows from `R_T = ½ ρ L d U² R'_0` at the operating speed, or better
as a speed-dependent table.

### 6.4 Low speed and large drift: cross-flow drag

The MMG polynomial is normalised by U², so it degrades when U approaches zero (berthing,
drifting in current, wind off a berth, crabbing). The standard complement is a **cross-flow
drag** strip integral (used in the HSVA model by Oltmann and Sharma, and described by
Faltinsen), with local transverse relative velocity along the hull:

```
v_r(x) = v − v_c(x) + x r
Y_CF = −½ ρ ∫ T(x) C_D(x) |v_r(x)| v_r(x) dx
N_CF = −½ ρ ∫ x T(x) C_D(x) |v_r(x)| v_r(x) dx
```

C_D is the 2D cross-flow drag coefficient of the section, roughly 0.6 to 1.0 for barge-like
sections in deep water and increasing strongly in shallow water. A common approach blends the
MMG model at service speed with cross-flow drag at low speed. The strip integral also handles
non-uniform current naturally (section 11).

## 7. Propulsion

### 7.1 Engine and shaft

Replace the throttle lag with a physical shaft:

```
2π I_shaft ṅ = Q_engine(n, command) η_S − Q_prop(n, J)
Q_prop = ρ n² D⁵ K_Q(J)
```

A marine diesel on a governor produces torque up to a limit that depends on RPM and
turbocharger response. Telegraph positions map to RPM set points. Reversing a direct-drive
engine requires stopping and restarting astern, which takes time and air starts; the model
should reproduce that delay.

### 7.2 Propeller thrust

```
J = u_P / (n D),  u_P = u (1 − w_P)
T = ρ n² D⁴ K_T(J)
X_P = (1 − t_P) Σ T_i
```

K_T(J) and K_Q(J) come from open-water curves. Without manufacturer data, the Wageningen
B-series polynomials (Oosterveld and van Oossanen 1975; Kuiper 1992; MSS `wageningen.m`) for the
estimated blade count, pitch ratio and area ratio are the standard substitute. MMG uses a
quadratic fit `K_T = k_0 + k_1 J + k_2 J²`.

In manoeuvring the effective wake at the propeller changes with the local drift angle. MMG
2015 models it with `β_P = β − x'_P r'` and

```
(1 − w_P) = (1 − w_P0) [1 + (1 − exp(−C_1 |β_P|)) (C_2 − 1)]    (verify against the paper)
```

For **astern and crash-stop** manoeuvres, first-quadrant curves are wrong. Use four-quadrant
propeller data (thrust and torque coefficients as a function of the hydrodynamic advance angle
`β* = atan(V_A / (0.7 π n D))`), for example from published MARIN B-series four-quadrant data.

### 7.3 Twin screws

Keep port and starboard propellers separate. Each gives thrust at its own lateral position y_P,
so

```
N_P = −Σ y_P,i T_i   (sign per the frame convention in section 3)
```

This reproduces turning on the spot with one engine ahead and one astern, and the small
transverse "paddle wheel" effect of each propeller can be added as an optional side force.

### 7.4 Plausibility check for 507B thrust

Actuator-disc momentum theory gives an upper bound for bollard thrust:
`T_ideal = (2 ρ A P_D²)^(1/3)`. With P_D ≈ 610 kW per shaft and D = 2.1 m (estimated diameter),
T_ideal ≈ 136 kN per propeller. Real open propellers reach a fraction of the ideal, so total
bollard pull of roughly 170 to 190 kN is a reasonable prior estimate, to be replaced by
propeller data. At service speed the net thrust must equal the total resistance divided by
(1 − t).

## 8. Rudders

MMG 2015 rudder model, per rudder, with the rudder normal force F_N:

```
F_N = ½ ρ A_R U_R² f_α sin α_R
f_α = 6.13 Λ / (Λ + 2.25)                        (Fujii; Λ = rudder aspect ratio)
U_R = √(u_R² + v_R²),   α_R = δ − atan(v_R / u_R)
v_R = U γ_R β_R,        β_R = β − l'_R r'
u_R = ε u (1 − w_P) √( η [1 + κ (√(1 + 8 K_T / (π J²)) − 1)]² + (1 − η) )
η = D / H_R  (propeller diameter over rudder span)

X_R = −(1 − t_R) F_N sin δ
Y_R = −(1 + a_H) F_N cos δ
N_R = −(x_R + a_H x_H) F_N cos δ
```

What this adds compared with the current code:

- u_R includes the propeller slipstream (the κ and K_T term), so steering exists with the
  engine running even when the ship is nearly stopped.
- γ_R and l'_R model flow straightening by the hull and the effect of yaw rate on rudder inflow.
- a_H and x_H add the lateral force the rudder induces on the hull aft body, which is a
  significant part of the total turning moment.
- t_R is the added resistance of a deflected rudder, which slows the ship in turns.

Add stall: above an effective angle of attack of roughly 25 to 35° (depending on profile and
aspect ratio) lift falls and drag rises. Replace `sin α_R` with a lift curve that saturates.

Check against the formula: KVLCC2's f_α = 2.747 corresponds to Λ ≈ 1.83 with Fujii's expression.

Illustrative 507B rudder force: two rudders, total A_R = 11 m² (estimated), Λ ≈ 1.4 gives
f_α ≈ 2.35. With U_R ≈ 4.5 m/s in the slipstream at service speed and α_R = 20°,
F_N ≈ ½ · 1000 · 11 · 2.35 · 4.5² · sin 20° ≈ 90 kN, and the yaw moment including hull
interaction is of order 8 MN m. Estimates only.

## 9. Resistance and manoeuvring in waves

River wind waves are small, so added resistance in waves and seakeeping can be ignored for the
Gorodets scenario. Lake crossings (Onega, Ladoga) at the class limit of 2 m waves would need
a seakeeping extension (Fossen chapter 10, strip theory or response amplitude operators).
Ship-generated waves in `RiverWater.shader` remain visual only.

## 10. Wind

### 10.1 Force model

Relative wind at the ship: `V_rw = V_wind − V_ship`, relative angle γ_rw measured from the bow.
Blendermann (1994), as implemented in Fossen's MSS `blendermann94.m`:

```
CD_l = CD_lAF(γ) · A_Fw / A_Lw
den  = 1 − ½ δ (1 − CD_l / CD_t) sin²(2γ)
C_X  = −CD_lAF(γ) cos γ / den
C_Y  =  CD_t sin γ / den
C_K  =  κ (s_H / H_m) C_Y,          H_m = A_Lw / L_oa
C_N  = (s_L / L_oa − 0.18 (γ − π/2)) C_Y

X_W = ½ ρ_a V_rw² A_Fw C_X
Y_W = ½ ρ_a V_rw² A_Lw C_Y
K_W = ½ ρ_a V_rw² A_Lw H_m C_K       (heel moment)
N_W = ½ ρ_a V_rw² A_Lw L_oa C_N
```

A_Fw frontal and A_Lw lateral projected area above water, s_L the longitudinal position of the
lateral area centroid from midship, s_H its height above the waterline. CD_lAF uses the bow
value for |γ| ≤ 90° and the stern value otherwise.

Coefficients from the MSS table for "cargo vessel, loaded": CD_t = 0.85,
CD_lAF(0) = 0.65, CD_lAF(π) = 0.55, δ = 0.40, κ = 1.7. Isherwood (1972) is an alternative
regression for merchant ships (MSS `isherwood72.m`).

### 10.2 Wind speed at height and gusts

Reported wind speed refers to 10 m height. For windage centroids at other heights use a power
law `V(z) = V₁₀ (z/10)^α` with α ≈ 1/7 over open water, or a logarithmic profile. Add gusts from
a wind spectrum (Davenport or Harris, see Fossen chapter 10) or a filtered noise process with a
gust factor, and slowly varying direction.

### 10.3 Illustrative 507B numbers

Estimated loaded windage: lateral area of hull freeboard (≈ 2 m × 138 m), hatch coamings and
aft superstructure gives A_Lw ≈ 650 m²; frontal A_Fw ≈ 180 to 200 m². In ballast the freeboard
grows by more than 2 m and A_Lw increases substantially. Estimates only.

At 15 m/s beam wind (γ = 90°, den = 1, C_Y = 0.85):
`Y_W ≈ ½ · 1.225 · 15² · 650 · 0.85 ≈ 76 kN`.

Order-of-magnitude drift of the stopped ship: balancing with cross-flow drag
`½ ρ L T C_D v²` (C_D ≈ 0.8 deep water) gives about 0.6 m/s leeway. In shallow water C_D is much
larger and leeway smaller.

Steady wind heel: `φ ≈ K_W / (Δ g GM)`, small for a loaded 507B with large GM.

## 11. Currents

### 11.1 Uniform current

A uniform current carries the ship and the water together. The ship's motion **relative to the
water** is exactly the same as in still water; only the track over the ground changes:

```
ṗ_ground = R(ψ) ν_r + V_current
```

Consequences a simulator must show: the heading and ground track differ (crab angle when
crossing), stopping distance over the ground is longer going downstream, and turning circles
drift downstream.

### 11.2 Non-uniform river current

River currents vary across the channel (faster in the thalweg, slower near banks), along it
(bends, narrowings, confluences) and near structures. When bow and stern are in different
current, the relative velocity varies along the hull and produces a real turning moment. The
strip integral of section 6.4 with `v_c(x)` sampled at several stations handles this without
ad hoc torques. For the surge component, sample u_c at the propeller and rudder positions for
their inflow.

A simple cross-channel profile for scenario design: `u_c(y) = u_max (1 − |y / b|^n)` with b the
half width and n ≈ 4 to 8 for a trapezoidal channel. A depth-averaged 2D hydraulic model (for
example from a river engineering tool) is the realistic source for a real reach.

In current, time derivatives of the current seen by a moving ship also matter: a ship entering
a cross-current zone feels the change over its own length, which the strip model captures.

## 12. Restricted water

The Gorodets reach is shallow and narrow relative to the 507B, so these effects are central.

### 12.1 Shallow-water hydrodynamics

As h/T decreases below about 3, and dramatically below 1.5:

- Resistance increases (flow speeds up under the hull; wave resistance changes with the depth
  Froude number `Fn_h = V / √(g h)`). Lackenby (1963) and Schlichting (1934) give classical speed
  loss corrections; Russian inland practice (Voitkunsky; Pavlenko) has methods for river ships.
- Added mass and damping derivatives grow; ships become more directionally stable but turn much
  less, so turning diameter can double.
- Propeller wake fraction and rudder effectiveness change.

Practical source for depth corrections of MMG-type derivatives and added masses: Taimuri et al.
(2020), whose correction functions are implemented in pymaneuvering (`_shallow_water_hdm`).
Kijima et al. (1990) and Ankudinov et al. give alternatives. All are regressions with validity
ranges that must be checked for the 507B's very low h/T.

### 12.2 Squat

Squat is sinkage and trim from the pressure drop around a moving hull in shallow water.

**ICORELS** (bow squat, PIANC concept design):

```
S_b = C_S · (∇ / L_pp²) · Fn_h² / √(1 − Fn_h²)
C_S = 1.7 (C_B < 0.7), 2.0 (0.7 ≤ C_B < 0.8), 2.4 (C_B ≥ 0.8)
```

**Barrass** (maximum squat, at the bow for C_B > 0.7):

```
S_max = K C_B V_k² / 100        V_k in knots
K = 5.74 S^0.76,  with K = 1 for S < 0.10,  S = A_S / A_C (blockage),  A_S ≈ 0.98 B T
validity: 1.10 ≤ h/T ≤ 1.4,  0.10 ≤ S ≤ 0.25
```

**Römisch** critical speed in unrestricted shallow water, with typical practical operation at up
to about 80 % of it:

```
V_cr = 0.58 ((h/T)(L/B))^0.125 √(g h)
```

Illustrative 507B case (T = 3.53 m, h = 4.6 m so h/T ≈ 1.3, 10 kn): Fn_h ≈ 0.77; ICORELS gives
about 0.8 m; Barrass with K = 1 gives 0.85 m, and with canal-like blockage (K = 2) 1.7 m, which is
more than the 1.07 m static under-keel clearance. Römisch's V_cr ≈ 5.2 m/s, so practical speed
would be limited to about 4.2 m/s (8 kn). Note that the 507B's L/T ≈ 38 is outside the ICORELS
calibration range (16.1 to 20.2) and Fn_h is above its recommended 0.7, so only the qualitative
conclusion is safe: a loaded 507B must slow down substantially in such a reach, and squat must
feed into grounding checks. The squat formula comparison by Serban and Panaitescu (2016) lists
the validity constraints of each formula.

### 12.3 Bank effects

Near a bank the flow between hull and bank accelerates, lowering pressure on the bank side:

- a **sway force toward the bank** (bank suction),
- a **yaw moment turning the bow away from the bank** (bow cushion),
- extra sinkage on the bank side.

Effects grow roughly with U², with smaller ship-bank distance, lower under-keel clearance and
higher propeller loading. The state-of-the-art empirical models are by Vantorre et al. and Lataire
et al. (Ghent University and Flanders Hydraulics), based on extensive captive model tests, with
an "equivalent distance to bank" for irregular bank geometry. Their published formulas should be
implemented from the papers; they are designed for ship-handling simulators. A simulator needs a
bank distance query along the hull, which the existing `FairwayRoute` and bathymetry can provide.

### 12.4 Ship-ship interaction

Passing and overtaking in narrow fairways produces sway forces and yaw moments that change sign
during the encounter (Vantorre, Verzhbitskaya and Laforce 2002, and later work). Relevant once
traffic is added; the Volgo-Don canal and locks make it a realistic training topic.

### 12.5 Locks and blockage

In locks and narrow canals the return flow around the hull increases resistance and squat sharply
(Schijf's limiting speed theory, 1949). Entering a lock also produces a piston effect on the water
ahead of the ship. These matter for a future lock scenario.

## 13. Contact, grounding, mooring and anchors

- **Grounding**: contact points on the keel against the bathymetry, a normal force from
  penetration stiffness (soft for silt, stiff for rock) and Coulomb friction `F_f ≤ μ F_n` with
  bottom-dependent μ. Friction rather than linear drag gives realistic "stuck" behaviour and
  recovery with engines.
- **Berthing**: fenders as nonlinear springs with damping; the existing Unity colliders can supply
  contact points.
- **Mooring lines**: elastic lines with tension-dependent stiffness and breaking load.
- **Anchors**: holding force by anchor type and bottom, chain catenary; important for river
  emergency manoeuvres.

## 14. Implementation design for Unity (proposed)

### 14.1 Architecture

Keep `ShipPhysicsController` as the hub and the `Rigidbody` as the integrator and collision
authority, but move the hydrodynamics into testable plain C# classes:

| Class (proposed) | Responsibility |
|---|---|
| `ManoeuvringModel` | Assembles M, evaluates hull, rudder, propeller, wind and external forces, returns ν̇ |
| `HullForceModel` | MMG polynomial plus cross-flow drag blend; depth corrections |
| `PropellerModel` | Shaft RPM dynamics, K_T/K_Q (four-quadrant), wake and thrust deduction, per shaft |
| `RudderModel` | MMG rudder with slipstream and stall, per rudder |
| `WindLoadModel` | Blendermann coefficients with gusts |
| `HydrostaticsModel` | Station-based buoyancy, draft, trim, GM |
| `RestrictedWaterModel` | Squat, bank and interaction forces from bathymetry and route queries |

Each class takes plain structs and returns forces, so EditMode tests can check them against
published numbers without scenes or Play Mode.

### 14.2 Integrating added mass with a Rigidbody

A Rigidbody has one scalar mass, while added mass differs per axis and couples sway and yaw.
Recommended approach:

1. Each `FixedUpdate`, read u, v, r in the horizontal body frame from the `Rigidbody` velocities
   (the Rigidbody stays the source of truth, including after collisions).
2. Compute all hydrodynamic and environmental forces τ and solve
   `ν̇ = M⁻¹ (τ − C(ν_r) ν_r − D(ν_r) ν_r)` with the full 3 × 3 M.
3. Apply the result as accelerations: `AddForce(worldAcceleration, ForceMode.Acceleration)` for
   surge and sway and `AddTorque(Vector3.up * ṙ, ForceMode.Acceleration)` for yaw.
4. Leave heave, roll and pitch to hydrostatic and damping forces applied as ordinary forces.

This keeps the project invariant that the vessel moves only through Rigidbody forces and
torques, avoids the instability of feeding back added-mass forces from the previous step (which
fails when added mass approaches ship mass, as in shallow water), and keeps Unity collisions
working. Collision and grounding forces should be converted into the manoeuvring model's τ, or
their effective inertia understood, so contacts are not unrealistically soft in sway.

### 14.3 Time step and numerics

Manoeuvring time constants are tens of seconds, so Unity's 50 Hz fixed step is adequate for the
horizontal model. Shaft dynamics and stiff contact springs may need substeps. Simulation speed-up
(`SimulationTimeController`) keeps the same fixed step and runs more steps per frame; it must not
enlarge the step. Guard U → 0 in all U-normalised formulas.

### 14.4 Data schema

Extend `VesselData`, with matching `VesselDataValidator` rules, roughly:

- `hydrostatics`: station table or hull reference, KG, radii of gyration, per loading condition;
- `addedMass`: m_x, m_y, J_z or their prime values, plus depth correction selection;
- `hullDerivatives`: MMG coefficient set, R'_0 table by speed, cross-flow drag coefficients;
- `propellers`: per shaft position, diameter, pitch ratio, area ratio, blade count, K_T/K_Q source,
  w_P0, t_P, C_1, C_2, shaft inertia, engine torque curve;
- `rudders`: per rudder position, area, span, aspect ratio, stall angle, rate, t_R, a_H, x'_H, ε,
  κ, γ_R, l'_R;
- `windage`: A_Fw, A_Lw, s_L, s_H per loading condition, Blendermann vessel type;
- `validation`: references to trial data and tolerances.

Each value keeps an `estimated` flag and a source reference, continuing the practice in
`VolgoDon507B_Sources.md`.

## 15. Worked estimate summary for Project 507B

All values illustrative estimates for plausibility, not validated data.

| Quantity | Estimate | Section |
|---|---|---|
| Displacement volume ∇ | ≈ 6 690 m³ (C_B L B T) | 5 |
| Surge added mass m_x | ≈ 0.35 × 10⁶ kg (5 %) | 5.3 |
| Sway added mass m_y, deep water | ≈ 4.1 × 10⁶ kg (61 %) | 5.3 |
| Yaw added inertia J_z, deep water | ≈ 5.3 × 10⁹ kg m² | 5.3 |
| Frictional resistance at 10 kn | ≈ 58 kN | 6.3 |
| Total deep-water resistance at 10 kn | order 80 to 100 kN | 6.3 |
| Total bollard pull, two propellers | ≈ 170 to 190 kN (current JSON: 620 kN) | 7.4 |
| Rudder normal force, 20° at service speed | ≈ 90 kN | 8 |
| Beam wind force at 15 m/s, loaded | ≈ 76 kN (current code: 180 kN) | 10.3 |
| Squat at 10 kn, h/T ≈ 1.3 | ≈ 0.8 to 1.7 m depending on blockage | 12.2 |
| Römisch critical speed at h = 4.6 m | ≈ 5.2 m/s | 12.2 |
| GM, roll period, loaded | ≈ 3 m, ≈ 7 s | 5.2 |

## 16. Calibration and validation

### 16.1 Data to obtain for Project 507B

1. The ship's manoeuvring information: IMO Resolution A.601(15) defines the pilot card, wheelhouse
   poster and manoeuvring booklet with turning, stopping and speed data; river vessels often carry
   equivalent documentation required by the Russian River Register.
2. Sea or river trial reports: speed-power, turning circles, zig-zag, stopping.
3. Lines plan or hull model for hydrostatics and CFD; propeller and rudder drawings.
4. AIS tracks of 507B-class ships on the Volga: speed through bends, typical speeds by reach and
   water level, turning behaviour. AIS gives position, speed over ground and heading at intervals;
   combined with current estimates it supports system identification of Nomoto or MMG parameters.
5. Interviews and recorded ship-handling runs with experienced masters of this class.

### 16.2 Standard manoeuvres and acceptance

Implement automated simulated trials that output the same quantities as full-scale trials:

| Manoeuvre | Outputs |
|---|---|
| Turning circle, 35° rudder, both sides | Advance, transfer, tactical diameter, steady turning diameter, speed loss |
| Zig-zag 10°/10° and 20°/20° | First and second overshoot angles, initial turning time |
| Spiral or reverse spiral | Directional stability loop width |
| Crash stop, full ahead to full astern | Track reach, head reach, lateral deviation, time |
| Acceleration and deceleration | Speed-time curve, coasting distance |
| Pull-out | Residual yaw rate |

IMO MSC.137(76) criteria for sea-going ships, useful as a reference envelope:

- Turning: advance ≤ 4.5 L, tactical diameter ≤ 5 L.
- Initial turning: with 10° rudder, heading changes 10° within 2.5 L travelled.
- Yaw checking, 10°/10° first overshoot: ≤ 10° if L/V < 10 s; ≤ (5 + ½ L/V)° if 10 s ≤ L/V < 30 s;
  ≤ 20° if L/V ≥ 30 s. 20°/20° first overshoot ≤ 25°.
- Stopping: track reach in full astern ≤ 15 L.

Those criteria are specified for deep, unrestricted water at full load, so they do not apply
directly to a river vessel in shallow water, but a model that violates them badly in deep water is
likely wrong. Acceptance tolerances against real 507B trials should be agreed with maritime
specialists.

### 16.3 Nomoto model as a calibration bridge

The first-order Nomoto model `T ṙ + r = K δ` (Nomoto et al. 1957) summarises steering response with
two constants. K and T can be fitted from zig-zag data or AIS tracks and compared with the same
constants extracted from the full model, giving a quick check of whether the derivatives are in the
right range.

### 16.4 Test strategy in this repository

- EditMode tests for each force model against published values: KVLCC2 MMG turning and zig-zag
  results from the MMG paper (tolerances from the paper's figures), Blendermann coefficients from the
  MSS implementation, squat formulas against the Serban and Panaitescu example ship.
- Sign tests: positive helm turns to starboard; beam wind from port pushes to starboard; bank on
  starboard pulls the stern to starboard and turns the bow to port.
- Invariance tests: in a uniform current the water-relative trajectory equals the still-water one.
- PlayMode tests for the Rigidbody integration: added mass slows sway response; collisions still
  stop the ship.
- A batch "virtual sea trials" runner, similar to `ShipWakeRuntimeCheck`, that writes trial
  results to a log for comparison.

## 17. Proposed roadmap

| Phase | Work | Outcome |
|---|---|---|
| 1 | Coordinate convention and sign tests; separate twin propellers and rudders; power-consistent thrust with speed dependence; remove the unphysical speed headroom | Correct direction of every effect, speed near 10 kn from power |
| 2 | 3-DOF MMG model with added mass (Clarke, Söding), Clarke linear plus estimated nonlinear derivatives, MMG rudder with slipstream and stall, acceleration-based Rigidbody integration | Realistic turning, stopping and low-speed steering in deep water |
| 3 | Blendermann wind with windage by loading, gusts; strip-theory cross-flow drag for low speed and non-uniform current | Realistic leeway, berthing and current behaviour |
| 4 | Shallow-water corrections (Taimuri et al.), squat (ICORELS or Barrass by channel type) feeding grounding, bank effect models, resistance increase in shallow water | Correct behaviour in the Gorodets reach |
| 5 | Station-based hydrostatics, loading conditions, heel in turns and wind; shaft and engine dynamics with astern reversal; four-quadrant propeller | Loading-dependent behaviour and realistic crash stops |
| 6 | Virtual sea trials runner; calibration against 507B data; documented tolerances | Model quality measurable against reality |
| 7 | Ship-ship interaction, locks, mooring and anchors | Advanced training scenarios |

### Implementation status (2026-09-13)

Phases 1 to 6 are implemented as the classes listed in `ShipSimulator_Physics.md`, with EditMode
tests for each force model, PlayMode tests for the Rigidbody integration and the
`VirtualSeaTrials` report. Deviations from the proposal above:

- Propeller curves are quadratic open-water K_T and K_Q fits, separate for ahead and astern, not
  four-quadrant data (none is available for 507B).
- The bank effect is an estimated Bernoulli suction model from the side flow areas. The Lataire
  papers were not accessible, so their formulation is not used.
- Kijima and Taimuri depth factors use the B/T <= 4 branch for every hull, because the other branch
  gives unphysical factors for the very beamy 507B hull. Factors are clamped to at least 1.
- Lackenby shallow-water speed loss is capped at 45 %.
- Hydrostatics use wall-sided station prisms rather than a lines plan; the centre of buoyancy is
  placed by a linear area weighting.
- Collision contacts from Unity colliders still act directly on the Rigidbody; only grounding
  friction goes through the added-mass solve.
- Resistance C_R and rated propeller speed are calibrated to the published 10 kn at full power.
  Everything else that is not in the published particulars is estimated.
- The previous vessel JSON had roll and pitch inertia swapped relative to Unity's body axes; the
  new controller maps pitch to x, yaw to y and roll to z.
- Spiral and pull-out manoeuvres, calibration against 507B trial data and documented tolerances
  (the remaining parts of phase 6) wait for real data.

## 18. References

Books and standards

- T. I. Fossen, *Handbook of Marine Craft Hydrodynamics and Motion Control*, 2nd ed., Wiley, 2021.
  MSS toolbox (MATLAB/Octave): https://github.com/cybergalactic/MSS
- A. F. Molland, S. R. Turnock, D. A. Hudson, *Ship Resistance and Propulsion*, 2nd ed., Cambridge University Press, 2017.
- O. M. Faltinsen, *Sea Loads on Ships and Offshore Structures*, Cambridge University Press, 1990.
- M. A. Abkowitz, "Lectures on Ship Hydrodynamics: Steering and Manoeuvrability", Hydro- og Aerodynamisk Laboratorium Report Hy-5, 1964.
- A. N. Voitkunsky (ed.), *Handbook on Ship Theory* (Справочник по теории корабля), 3 vols., Sudostroenie, 1985 (Russian).
- C. B. Barrass, *Ship Squat*, 2004, and later editions on ship squat and interaction.
- PIANC, *Harbour Approach Channels: Design Guidelines*, Report 121 (Working Group 49), 2014; earlier WG 30 report, 1997.
- IMO Resolution MSC.137(76), *Standards for Ship Manoeuvrability*, 2002: https://wwwcdn.imo.org/localresources/en/KnowledgeCentre/IndexofIMOResolutions/MSCResolutions/MSC.137(76).pdf
- IMO Resolution A.601(15), *Provision and Display of Manoeuvring Information on Board Ships*, 1987.
- ITTC Recommended Procedures and Guidelines: full scale manoeuvring trials, captive model tests, and the ITTC 1957 friction line.

Manoeuvring models and hydrodynamic coefficients

- H. Yasukawa, Y. Yoshimura, "Introduction of MMG standard method for ship maneuvering predictions", Journal of Marine Science and Technology 20, 37-52, 2015: https://link.springer.com/article/10.1007/s00773-014-0293-y
- A. Ogawa, H. Kasai, "On the mathematical model of manoeuvring motion of ships", International Shipbuilding Progress 25, 1978.
- D. Clarke, P. Gedling, G. Hine, "The application of manoeuvring criteria in hull design using linear theory", Transactions RINA 125, 45-68, 1983.
- K. Kijima, T. Katsuno, Y. Nakiri, Y. Furukawa, "On the manoeuvring performance of a ship with the parameter of loading condition", Journal of the Society of Naval Architects of Japan 168, 1990.
- G. Taimuri, J. Matusiak, T. Mikkola, P. Kujala, S. Hirdaris, "A 6-DoF maneuvering model for the rapid estimation of hydrodynamic actions in deep and shallow waters", Ocean Engineering 218, 108103, 2020.
- P. Oltmann, S. D. Sharma, "Simulation of combined engine and rudder manoeuvres using an improved model of hull-propeller-rudder interactions", 15th Symposium on Naval Hydrodynamics, 1984.
- K. Nomoto, T. Taguchi, K. Honda, S. Hirano, "On the steering qualities of ships", International Shipbuilding Progress 4, 1957.
- SIMMAN workshops on verification and validation of ship manoeuvring simulation methods (KVLCC2, KCS benchmark data).
- pymaneuvering (MMG and Abkowitz models with shallow-water corrections, KVLCC2 parameters): https://github.com/nikpau/pymaneuvering

Resistance and propulsion

- J. Holtrop, G. G. J. Mennen, "An approximate power prediction method", International Shipbuilding Progress 29, 1982.
- J. Holtrop, "A statistical re-analysis of resistance and propulsion data", International Shipbuilding Progress 31, 272-276, 1984.
- M. W. C. Oosterveld, P. van Oossanen, "Further computer-analyzed data of the Wageningen B-screw series", International Shipbuilding Progress 22, 1975.
- G. Kuiper, *The Wageningen Propeller Series*, MARIN, 1992.
- H. Lackenby, "The effect of shallow water on ship speed", The Shipbuilder and Marine Engine Builder 70, 1963.

Wind

- W. Blendermann, "Parameter identification of wind loads on ships", Journal of Wind Engineering and Industrial Aerodynamics 51, 339-351, 1994.
- R. M. Isherwood, "Wind resistance of merchant ships", Transactions RINA 115, 1972.

Restricted water

- P. S. Serban, V. N. Panaitescu, "Comparison between formulas of maximum ship squat", Mircea cel Batran Naval Academy Scientific Bulletin XIX(1), 2016, DOI 10.21279/1454-864X-16-I1-018: https://www.anmb.ro/buletinstiintific/buletine/2016_Issue1/NMS/105-111.pdf
- M. J. Briggs, M. Vantorre, K. Uliczka, "Prediction of squat for underkeel clearance", Handbook of Coastal and Ocean Engineering, 2010.
- E. Lataire et al., experiment-based ship-bank interaction models for longitudinal force (2015) and sway force and yaw moment (2018), Ghent University and Flanders Hydraulics; overview in "Bank interaction effects on ships in 6 DOF": https://www.vliz.be/imisdocs/publications/47/407147.pdf
- M. Vantorre, E. Verzhbitskaya, E. Laforce, "Model test based formulations of ship-ship interaction forces", Ship Technology Research 49, 2002.
- J. B. Schijf, limiting speed theory for ships in canals, XVII International Navigation Congress (PIANC), Lisbon, 1949 (verify the exact paper title).

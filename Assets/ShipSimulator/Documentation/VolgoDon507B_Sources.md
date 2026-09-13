# Volgo-Don Project 507B: Sources and Data Status

This file distinguishes published vessel particulars from prototype calibration
assumptions. Values vary between individual vessels and later conversions. The
prototype represents an unshortened, river-service Project 507B baseline.

## Published particulars

| Parameter | Value | Unit | Source | Confidence | Notes |
|---|---:|---|---|---|---|
| Overall length | 138.3 | m | [FleetPhoto Project 507B](https://fleetphoto.ru/projects/1697), [Korabel project record](https://www.korabel.ru/fleet/info/7075.html) | high | MEB gives 138.74 m for a modernized variant. |
| Length between perpendiculars / design length | 135.0 | m | [MEB project page](https://mebspb.com/dry/507B.html), Korabel | high | Used by the hydrodynamic reference paper. |
| Overall beam | 16.7 | m | FleetPhoto, MEB, Korabel | high | |
| Moulded beam | 16.5 | m | MEB, Korabel | high | |
| Moulded depth | 5.5 | m | FleetPhoto, MEB, Korabel | high | |
| River loaded draught | 3.5-3.6 | m | MEB, FleetPhoto, Korabel | high | Prototype uses 3.53 m baseline. Loading and conversions change this. |
| Loaded displacement | 6750 | t | FleetPhoto, Korabel | medium | Public database value; vessel-specific loading varies. |
| Deadweight | 5000-5290 | t | FleetPhoto, MEB | high | MEB gives 5290 t in river condition; original/project records commonly state 5000 t. |
| Approximate lightship mass | 1750 | t | derived from 6750 t displacement - 5000 t deadweight | low | Derived, not a published lightship value. Not used as the default loaded simulation mass. |
| Cargo hold capacity | 6270 | m3 | FleetPhoto; VSUWT diploma project | medium | Hold arrangement changed across the series. MEB lists 9360 m3 for modernized vessels. |
| Main engines | 2 x 6ChRN 36/45 | count/type | FleetPhoto; VSUWT diploma project | high | Russian notation: 6ЧРН 36/45. |
| Total main-engine power | 1324 | kW | FleetPhoto, MEB | high | 2 x 662 kW, approximately 1800 metric hp total. |
| Propulsion arrangement | twin screw | count/type | [VSUWT diploma project](https://vsuwt-perm.ru/wp-content/uploads/vypusk/2018/isaev_a.s..pdf), p. 5 | high | Propeller geometry was not found publicly. |
| Service/maximum published speed | 10 +/- 0.5 | kn | MEB | high | FleetPhoto lists 21 km/h (11.34 kn), likely light condition. Prototype limit uses 10 kn loaded. |
| Hull type | single-deck dry cargo motor vessel; double sides and double bottom; aft machinery and accommodation | description | VSUWT diploma project, p. 5 | high | Two/four-hold arrangements exist. |
| River/limited open-water operation | River Register class "M"; Lake Onega/Ladoga, wind <= Beaufort 5, wave <= 2 m for the cited vessel | description | VSUWT diploma project, p. 5 | medium | Class and limits can differ after conversion. |
| Block coefficient | 0.851 | dimensionless | [Science-Education hydrodynamics paper](https://s.science-education.ru/pdf/2013/5/242.pdf), table 2 | medium | Paper uses L=135 m, B=16.5 m, T=3.2 m. |
| Endurance | 15 | days | MEB | medium | Modernized sea-river specification. |

## Unresolved real-world particulars

The following were not found in reliable public sources and must not be treated
as measured Project 507B facts:

- propeller diameter, pitch, blade count, RPM, open-water curves and efficiency
- shaft losses and bollard pull
- rudder count, geometry, area, balance and maximum mechanical rate
- centre of gravity, radii of gyration and inertia tensor
- longitudinal/lateral/yaw resistance derivatives
- turning diameter, advance, transfer, stopping distance and crash-stop time
- windage areas and coefficients
- loaded and ballast mass distributions for a specific vessel

Values for these fields in the JSON are marked `estimated: true` and exist only
to make the prototype executable. They require calibration against trials,
drawings, manoeuvring booklets, or class documentation before training use.

## Estimated manoeuvring parameters

All values below are estimates for the manoeuvring model described in
`ShipSimulator_Physics.md`. None is measured on a Project 507B vessel.

| Parameter | Value | Basis |
|---|---:|---|
| Centre of gravity, local (x, y, z) | (0, -1.2, 2.0) m | estimated, loaded cargo |
| Radii of gyration roll / pitch / yaw | 5.9 / 33.75 / 33.75 m | typical fractions 0.36 B and 0.25 L |
| Midship / waterplane coefficient | 0.98 / 0.90 | typical for a full river hull |
| Longitudinal centre of buoyancy | 2.0 m forward of midship | estimated, matches the centre of gravity |
| Heave / roll damping ratio | 0.30 / 0.08 | typical critical-damping fractions |
| Form factor, correlation allowance | 1.30, 0.0004 | Holtrop-range estimates |
| Residual resistance coefficient C_R | 0.000516 | calibrated to 10 kn at full power in deep water |
| Surge added mass m'_x | 0.011104 | Söding formula |
| Sway / yaw added mass m'_y, J'_z | 0.12817 / 0.009087 | Clarke et al. (1983) at the loaded draft |
| Y'_v, Y'_r, N'_v, N'_r | -0.21285, 0.04970, -0.04623, -0.02989 | Clarke et al. (1983) |
| Nonlinear hull derivatives | KVLCC2 set | Yasukawa and Yoshimura (2015), assumed to transfer |
| Gear / shaft efficiency | 0.92 / 0.98 | typical reduction gear and shaft line |
| Rated propeller speed | 257.4 rpm | calibrated with C_R to the published speed |
| Shaft inertia, governor band, ramp | 2500 kg m², 1 %, 15 s | estimated |
| Reversal speed / delay / brake | 25 % rated, 8 s, 50 % torque | estimated for an air-start reversible diesel |
| Propeller diameter, positions | 2.1 m at x = ±4 m, 55 m aft of midship | estimated from draft and stern shape |
| K_T, K_Q ahead | [0.33, -0.25, -0.12], [0.042, -0.022, -0.012] | quadratic fits in the Wageningen B-series range |
| K_T, K_Q astern | [0.23, -0.20, -0.10], [0.036, -0.018, -0.010] | estimated |
| Wake fraction / thrust deduction | 0.20 / 0.20 | typical twin-screw full hull |
| Rudders | 2 x 5.5 m², span 2.6 m, behind each propeller | estimated; total about 2.3 % of L T, typical for river vessels |
| Rudder interaction t_R, a_H, x'_H, ε, κ, γ_R, l'_R | 0.39, 0.31, -0.46, 1.05, 0.5, 0.5, -0.8 | KVLCC2 values, estimated |
| Rudder stall / post-stall normal coefficient | 32 deg / 1.1 | estimated |
| Bow thruster | fitted, 160 kW, 1.0 m tunnel, 60 m forward of midship, axis 1.1 m above keel | fitting reported by the project owner; power, size and position estimated, not found in the public sources checked |
| Bow thruster figure of merit, ramp | 0.62, 5 s to full | estimated; gives about 21 kN bollard thrust (13.5 kgf/kW) |
| Bow thruster speed loss u_ref, f_min | 1.0 m/s, 0.3 | estimated trend for tunnel thrusters under way |
| Windage frontal / lateral area (loaded) | 190 / 650 m² | estimated from superstructure and freeboard |
| Wind centroid (x, height) | -15 m, 3.0 m | estimated, aft superstructure |
| Blendermann coefficients C_Dt, C_Dl bow, C_Dl stern, δ | 0.85, 0.65, 0.55, 0.40 | general cargo ship range from Blendermann (1994) |
| Bank suction coefficient, moment lever | 0.5, -0.25 L | estimated; no published model for this hull was accessible |

The KVLCC2 benchmark file (`KVLCC2_MMG_Benchmark.json`) holds the published MMG coefficient set
used to check the implementation, not Project 507B data. Its resistance C_R, propeller K_Q fit and
engine power are chosen so the benchmark reaches its 15.5 kn design speed.


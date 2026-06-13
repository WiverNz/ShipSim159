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


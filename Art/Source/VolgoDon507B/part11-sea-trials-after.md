# Virtual Sea Trials

Generated 2026-10-23 20:42. Pure three-degree-of-freedom simulation of the
manoeuvring model. Vessel coefficients are estimates, not trial data; this is not
validated for maritime training. Distances are in ship lengths (Lpp).

| Vessel and condition | Depth m | Slow / half / full kn | Advance | Transfer | Tactical D | Steady D | Turn speed loss | 10/10 1st / 2nd deg | 20/20 1st deg | Initial turn | Crash stop track | Stop time s | Full-speed squat bow / stern m |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 507B loaded, deep water | deep | 3.6 / 7.5 / 10.7 | 3.66 | 1.82 | 4.29 | 3.42 | 55 % | 3.7 / 4.6 | 8.4 | 1.87 | 5.30 | 285 | 0.00 / 0.00 |
| 507B loaded, river familiarisation channel | 8.2 | 3.3 / 6.9 / 9.7 | 3.56 | 1.97 | 4.30 | 3.51 | 49 % | 3.1 / 3.7 | 7.5 | 1.99 | 4.44 | 261 | 0.33 / 0.03 |
| 507B loaded, Gorodets reach | 4.6 | 2.6 / 5.5 / 7.5 | 5.44 | 4.27 | 8.56 | 8.21 | 24 % | 0.8 / 0.8 | 2.2 | 3.40 | 2.67 | 203 | 0.35 / 0.03 |
| 507B lightship, deep water | deep | 3.9 / 8.2 / 12.0 | 2.08 | 0.84 | 2.13 | 1.95 | 47 % | 4.7 / 4.7 | 12.2 | 1.10 | 2.09 | 91 | 0.00 / 0.00 |
| Volgo-Balt 2-95A/R loaded, deep water | deep | 3.1 / 6.4 / 9.9 | 3.13 | 1.45 | 3.52 | 2.97 | 57 % | 4.1 / 4.9 | 10.0 | 1.60 | 4.54 | 213 | 0.00 / 0.00 |
| Volgo-Balt 2-95A/R loaded, Gorodets reach | 4.6 | 2.5 / 5.1 / 7.4 | 5.62 | 4.33 | 8.75 | 8.38 | 27 % | 0.8 / 0.8 | 2.3 | 3.32 | 2.60 | 161 | 0.37 / 0.00 |
| Volgoneft 1577 loaded, deep water | deep | 3.3 / 6.9 / 10.7 | 3.31 | 1.53 | 3.76 | 3.04 | 58 % | 3.9 / 4.5 | 9.0 | 1.64 | 4.99 | 254 | 0.00 / 0.00 |
| Volgoneft 1577 loaded, Gorodets reach | 4.6 | 2.5 / 5.2 / 7.5 | 4.45 | 3.35 | 6.72 | 6.31 | 26 % | 1.1 / 1.1 | 3.0 | 2.62 | 2.52 | 181 | 0.38 / 0.00 |
| Meteor 342U hydrofoil, deep water | deep | 8.9 / 17.6 / 34.4 | 3.08 | 1.19 | 2.93 | 2.92 | 61 % | 14.3 / 14.2 | 35.1 | 1.33 | 6.14 | 20 | 0.00 / 0.00 |
| Meteor 342U hydrofoil, Gorodets reach | 4.6 | 8.8 / 16.4 / 34.3 | 3.11 | 1.21 | 2.97 | 2.95 | 63 % | 13.8 / 13.7 | 34.3 | 1.33 | 6.16 | 20 | 0.00 / 0.04 |
| Luch 14352 air cushion, deep water | deep | 5.4 / 13.8 / 21.1 | 3.13 | 1.13 | 2.62 | 2.09 | 67 % | 10.7 / 10.4 | 28.6 | 1.32 | 5.76 | 21 | 0.00 / 0.00 |
| Luch 14352 air cushion, Gorodets reach | 4.6 | 5.4 / 13.2 / 21.1 | 3.14 | 1.13 | 2.61 | 2.10 | 67 % | 10.5 / 10.4 | 28.2 | 1.32 | 5.53 | 21 | 0.00 / 0.15 |
| KVLCC2 MMG benchmark, deep water | deep | 4.6 / 9.9 / 15.4 | 3.00 | 1.30 | 3.09 | 2.27 | 64 % | 5.7 / 10.3 | 11.7 | 1.67 | 5.58 | 488 | 0.00 / 0.00 |

Bow thruster alone, full to starboard from rest with engines stopped, yaw rate after 180 s:
507B loaded deep water 13.0 deg/min, Gorodets reach 6.3 deg/min, lightship 0.0 deg/min.

IMO MSC.137(76) reference envelope (deep, unrestricted water, full load): advance <= 4.5 L,
tactical diameter <= 5 L, initial turning <= 2.5 L, 10/10 and 20/20 overshoot limits by L/V,
crash stop track reach <= 15 L. The criteria do not apply to a river vessel in shallow water,
nor to the hydrofoil and air cushion craft, which are reported without an envelope check.

Result: deep-water loaded conditions are inside the IMO envelope.

# Serpukhov Zaton Scenario: Sources and Data Status

This file separates published navigation data from prototype estimates for the Serpukhov zaton
scenario (`SerpukhovZatonScene`, geometry in `Assets/ShipSimulator/Data/Scenarios/SerpukhovZaton.json`).
**Nothing here is validated for maritime training.** Depths, currents, shoals and speed limits below
are scenario design, not survey.

The passage runs from the Oka, in through the mouth of the Nara, up the 2 km of navigable river and
into the Serpukhov lay-up and repair basin: an artificial basin off the Nara near its confluence with
the Oka, in Serpukhov, Moscow region.

## Published data

| Parameter | Value | Source | Confidence |
|---|---:|---|---|
| Nara waterway, g. Serpukhov to the mouth | 2 km | Rosmorrechflot order of 29.12.2022 ZD-496-r, appendix 2, row 1420 | high |
| Nara inland waterway category | 3 (guaranteed dimensions, **unlit** navigation marks) | same order, general provisions clause 3 | high |
| Nara guaranteed depth | 1.00 m at the Kashira projected level (101.62) | same order, row 1420 | high |
| Nara guaranteed channel width | 20 m | same order, row 1420 | high |
| Nara bend radius | 100 m | same order, row 1420 | high |
| Nara navigation-aid season | 20 April to 31 October, 195 days | same order, row 1420 | high |
| Oka, Aleksin to Ozyory (Serpukhov lies inside it) | category 3, depth 1.00 m, width 30 m, radius 150 m | same order, row 1381 | high |
| Maximum convoy dimensions, Nara | 100 x 20 m | Moscow Canal daily information bulletin, 24.07.2025 | high |
| Maintained by | Serpukhov District of Waterways, Moscow Canal (Oka Kaluga to Shchurovo plus the Nara, 253 km) | kim-online.ru | high |
| Shoreline outlines | OpenStreetMap water polygons, extracted 2026-09-19 (ODbL) | relations 2174292 "Serpukhovskiy zaton (port)" and 2174294, way 543276172 | high |
| Basin size | about 11.9 ha, longest chord about 625 m | measured from the OpenStreetMap outline | medium |
| Navigable Nara reach, mouth to basin entrance | about 1.45 km along the channel | measured from the OpenStreetMap outline | medium |
| Basin entrance width | about 55 m | measured from the OpenStreetMap outline | medium |
| Berth, yacht club and port office positions | OpenStreetMap nodes/ways in the basin | OSM | medium |
| Mean Nara discharge | 5.5 m3/s, 80 km above the mouth | Russian Wikipedia, Nara (river) | medium |

## Estimated parameters

| Parameter | Value | Basis |
|---|---:|---|
| Fairway depth, Oka approach | 2.8 m centre / 1.5 m edge | ESTIMATED navigation-season depth, not the 1.00 m guarantee |
| Fairway depth, Nara reach | 2.4 m centre / 1.2 m edge | ESTIMATED navigation-season depth |
| Fairway depth, basin | 3.2 m centre / 1.6 m edge | ESTIMATED; a lay-up basin is dredged deeper than the river |
| Depth beyond the marked channel | 0.35 m | ESTIMATED; chosen so leaving 20 m of marked water grounds the vessel |
| Speed limits | 4.0 m/s Oka, 3.0 to 2.4 m/s Nara, 1.8 to 1.2 m/s basin | ESTIMATED |
| Oka current at the mouth | 0.55 m/s down-river | ESTIMATED |
| Nara ambient current | 0.15 m/s down-river | ESTIMATED, scaled from the published mean discharge over the measured section |
| Basin current | nil | ESTIMATED |
| Shoals: mouth bar, two bend silt patches, entrance sill | 0.5 to 0.9 m of depth reduction | ESTIMATED. No public survey of the basin or the reach exists |
| Bank and bed relief | procedural | ESTIMATED; visual only, the depth the vessel feels comes from `ScenarioBathymetry` |
| Quays, laid-up craft, port office, monastery massing | placeholder boxes | Positions from OpenStreetMap; shapes are massing, not surveyed buildings |

## Navigation marks

The reach is a third-category waterway, so **nothing in this scenario is lit at night**. The scene
therefore carries no night beacons: the marks are painted day marks only, and the published
navigation-aid season ends on 31 October.

- Edge buoys follow the Russian inland convention, which is read going **downstream**: red marks the
  right edge, white the left. The scenario route runs up-river, so the red buoys stand on its left.
- Two axial marks stand on the axis of the entry from the Oka and at the basin entrance. The axial
  system is used for the starting point and the axis of a fairway, which is what both are.
- One unlit leading line (front and rear boards) stands on the east bank in line with the entry leg
  out of the Oka.

No public scheme of the actual buoy layout for this reach exists. The Moscow Canal daily bulletins do
not list the Nara, and the positions of real marks live in the district's own records and in the
pilot chart of the Oka (Kaluga to Kolomna, 2022 edition). **The mark positions in this scene are
generated from the fairway, not copied from a chart.**

## Which vessels the passage admits

`VoyagePassage` limits the scenario to 40 m length, 10 m beam and 1.6 m draft, which admits the
Meteor 342U and the Luch 14352 and excludes the three cargo ships. The reasons:

- the marked channel is 20 m wide with a 100 m bend radius;
- the basin entrance is about 55 m across and the turn into it is about 70 degrees;
- the Volgo-Don 507B, Volgo-Balt 2-95A/R and Volgoneft 1577 are 114 to 138 m long and draw over
  3.5 m, so neither the channel nor the turn will take them.

Note the honest gap: at the **published guaranteed** depth of 1.00 m the Meteor 342U (1.15 m loaded
draft) would have no under-keel clearance either. The scenario models an ordinary navigation-season
level instead, and the draft limit follows from that, not from the guarantee.

## Local frame

Local metres. The origin is the mouth of the Nara at the Oka, 54.88440 N, 37.41060 E. +z runs up the
Nara towards the basin on a bearing of about 316.3 degrees; +x is starboard of that. The frame
parameters are recorded in the `frame` block of the geometry file.

## Rebuilding

`Ship Simulator > Build Serpukhov Zaton Scenario` regenerates the scene from the geometry file.
`Ship Simulator > Render Serpukhov Zaton Preview` writes stills to `Logs/Serpukhov/`. Scene content
is owned by `SerpukhovZatonBuilder`; edit the builder or the geometry file, not the scene.

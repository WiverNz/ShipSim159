# Serpukhov Zaton Scenario: Sources and Data Status

This file separates published navigation data from prototype estimates for the Serpukhov zaton
scenario (`SerpukhovZatonScene`, geometry in `Assets/ShipSimulator/Data/Scenarios/SerpukhovZaton.json`).
**Nothing here is validated for maritime training.** Depths, currents, shoals and speed limits below
are scenario design, not survey.

The passage starts at the head of the Serpukhov lay-up and repair basin, among the laid-up fleet,
runs the length of the harbour past the barges, the passenger berth and the yacht marina, out
through the basin's 55 m gate, down the 2 km of navigable Nara and into the Oka. The basin is an
artificial one off the Nara near its confluence with the Oka, in Serpukhov, Moscow region.

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
| Terrain relief | AWS terrarium terrain tiles, zoom 14, sampled 2026-09-19 | about 30 m source resolution | medium |
| River surface elevation | 107.1 m above sea level, median of the terrain tiles over the water | Russian Wikipedia gives 107 m for the Nara mouth | medium |
| Land cover | OpenStreetMap landuse/natural polygons (ODbL), 46 polygons | wood, farmland, built, allotments, sand | medium |
| Building footprints | OpenStreetMap (ODbL), 158 within 650 m of the fairway | reduced to their minimum-area box | medium |
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
| Quays | placeholder boxes | Positions from OpenStreetMap; shapes are massing |
| Building heights | 3 to 19 m | ESTIMATED from `building:levels` where tagged, otherwise by building type. Shapes are massing boxes, not surveyed buildings |
| Bank profile within 70 m of the water | procedural | ESTIMATED. A 30 m elevation source smears the waterline, so the first 70 m of bank is carried by the shoreline and blended into the terrain behind it |
| Moored craft | 21 hulls, 11 to 109 m | **APPROXIMATE**, see below |

## Moored craft

The laid-up vessels, barges, the floating dock and the port's piers were **traced by eye from Esri
World Imagery** in September 2026: each one is a line drawn bow to stern on the picture, plus a beam
read across it, converted at 0.343 m per pixel. That gives 21 hulls: two cargo barges of about 70 m
alongside the north bank, a group of laid-up vessels of 30 to 45 m on the north-east shore, a
floating dock of about 62 by 19 m, small craft, two mooring piers and a hulk on the west bank.

The yacht harbour on the west shore is handled differently: only the span of bank it occupies is
traced, and its five pontoon fingers are then laid out along the outward normal of the shoreline
itself, with eight boats in the slips on each. That keeps them square to the water however the
outline is re-extracted, and it avoids pretending to know where sixty individual boats lie. Forty
five of the sixty six floating objects in the scene are those marina berths and fingers.

What this is not: the positions are approximate, the sizes are read off a picture rather than from a
register, **no vessel is identified and no type is established**, and laid-up craft move between
seasons, so the arrangement is a snapshot of one image and not the state of the basin today. The
imagery was used as a reference only; none of it is redistributed with the project.

## Navigation marks

The reach is a third-category waterway, so **nothing in this scenario is lit at night**. The scene
therefore carries no night beacons: the marks are painted day marks only, and the published
navigation-aid season ends on 31 October.

- Edge buoys follow the Russian inland convention, which is read going **downstream**: red marks the
  right edge, white the left. The passage runs downstream, so the red buoys stand to starboard of it.
- Two axial marks stand on the axis of the entry from the Oka and at the basin entrance. The axial
  system is used for the starting point and the axis of a fairway, which is what both are.
- One unlit leading line (front and rear boards) stands on the east bank in line with the lower Nara
  straight. Outbound it is a stern transit; inbound it leads a vessel in from the Oka.

No public scheme of the actual buoy layout for this reach exists. The Moscow Canal daily bulletins do
not list the Nara, and the positions of real marks live in the district's own records and in the
pilot chart of the Oka (Kaluga to Kolomna, 2022 edition). **The mark positions in this scene are
generated from the fairway, not copied from a chart.**

## Which vessels the passage admits

`VoyagePassage` limits the scenario to 40 m length, 10 m beam and 1.6 m draft. **Only the Luch 14352
gets in.** The reasons:

- the marked channel is 20 m wide with a 100 m bend radius, and the basin entrance is about 55 m
  across with a turn of some 70 degrees into it, so nothing of cargo-ship size can be turned there;
- the draft that matters is the deepest point, not the hull. A Meteor 342U draws 1.15 m on her hull
  but 2.35 m with her foils down, and the foils touch first. On a waterway guaranteed to 1.00 m she
  has no business being there, and in this scene she grounded on her foils in the channel itself.
  `VoyagePassage.DeepestDraftM` adds the support appendage for exactly this reason;
- the Luch is a skeg air-cushion craft drawing 0.66 m on her hull and 1.11 m to the bottom of her
  skegs, which the modelled channel takes.

Note the honest gap: at the **published guaranteed** depth of 1.00 m even the Luch would be down to
her skegs. The scenario models an ordinary navigation-season level instead, and the draft limit
follows from that, not from the guarantee.

## Running aground

Depth beyond the marked channel is 0.6 m. That is a scenario choice, and the numbers behind it were
measured rather than guessed:

| Luch on the bottom | Holding force | Astern thrust | Outcome |
|---|---:|---:|---|
| 0.60 m (a light touch, 6 cm) | 12 kN | 13 kN | works herself off astern |
| 0.50 m | 28 kN | 13 kN | held |
| 0.35 m | 50 kN | 13 kN | held fast |

So leaving the marked water still puts you on the bottom, but the edge of the channel is recoverable
while running properly up on a shoal is not, which is how it should be. The HUD shows the holding
force next to the grounding warning so the figure is on the bridge, not only in the physics.

## Local frame

Local metres. The origin is the mouth of the Nara at the Oka, 54.88440 N, 37.41060 E. +z runs up the
Nara towards the basin on a bearing of about 316.3 degrees; +x is starboard of that. The frame
parameters are recorded in the `frame` block of the geometry file.

## Rebuilding

`Tools/serpukhov_zaton_geometry.py` regenerates the geometry file from its sources: it needs network
access and Pillow, caches every download under `Tools/.cache/`, and refuses to write a file whose
fairway leaves the charted water. The traced moorings live in its `TRACED_MOORINGS` table, and the
fairway, currents and shoals, which are scenario design rather than survey, are written into the
script by hand.

`Ship Simulator > Build Serpukhov Zaton Scenario` regenerates the scene from the geometry file.
`Ship Simulator > Render Serpukhov Zaton Preview` writes stills to `Logs/Serpukhov/`. Scene content
is owned by `SerpukhovZatonBuilder`; edit the builder or the geometry file, not the scene.

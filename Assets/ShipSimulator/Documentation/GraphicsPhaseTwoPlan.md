# Graphics Phase 2: Work Plan

Status: Steps 0 and 1 implemented on 2026-09-13. Target-hardware performance acceptance remains
open. Steps 2 and 3 are not implemented. The sections below retain the original plan and criteria.

Minimum target agreed with the user: **GeForce RTX 3060, 1920 x 1080**.

## Step 0 and Step 1 results

- `GraphicsPhaseTwoCheck.RunBefore` and `RunAfter` preserve separate, non-overwriting sets in
  `Logs/GraphicsPhaseTwo/{before,after}/`: 25 screenshots (five poses in five conditions), 300
  timing samples per pose after 120 warmup frames, and 16 temporal crops per long-view condition.
  A fifth pose isolates the bank bed. `frame-times.csv` records CPU, GPU and editor intervals
  separately; unavailable measurements are `NA`. Simulation uses a fixed 1/60 s step.
- Scene colour refraction rejects foreground depth at the offset and its neighbours. RGB
  extinction uses estimated visibility depth 1.2 m, with rain reducing it by up to 25 percent.
  The shader composes transmission and scattering, retaining alpha only for the contact fade.
- `RiverPlanarReflection` filters successive mips with a separable Gaussian of increasing width;
  this approximates GGX roughness, not an exact GGX convolution. Devices without compute retain
  generated mips. Night reflection sampling is elongated vertically by ripple variance.
- Screen edges fade to the live HDR sky. Direct inspection found Unity's per-renderer probe
  lookup still returned black for water even with reflection variants enabled. `RiverLighting`
  now also binds its existing captured cubemap explicitly. `RunSkyProbe` renders this fallback
  directly with planar reflection disabled; it shows clouds and rejects a black result.
- Normal derivatives increase reflection roughness and reduce the glint exponent. The clear
  toward-sun crop's temporal variance decreased 16%, with the sun path retained. Eight of ten
  crops improved; dawn toward sun was effectively flat (+0.7%), and rain toward sun increased
  10.2%. The temporal comparison includes all visible motion and exposure, not only aliasing.
- The CPU median delta across the 25 cases was -0.116 ms, with largest increase +0.091 ms, on
  RTX 4090 / Direct3D12. GPU timings were unavailable. **This does not pass the RTX 3060 +1 ms
  GPU gate.** Additional vessel-catalogue work landed in the shared checkout between runs, so
  these timings describe the tested whole scenes rather than an isolated shader benchmark.

Optics settings are applied by `RiverWaterAndSkyBuilder.ConfigureOptics` and `ApplyOpticsBoth`,
which only updates the water material. The baseline captures precede all optics changes;
initial diagnostic attempts are retained in separately named directories. See the project
context for final regression results. Future baseline runs must preserve or rename existing
capture directories deliberately; the check refuses to overwrite them.

Phase 2 of the roadmap in `GraphicsRealismApproach.md` (section 10) covers three things:

1. water refraction and depth absorption, with better reflection filtering and specular
   antialiasing;
2. exponential height fog with sun in-scattering, replacing the uniform fog;
3. a flow map baked from the simulated current, so ripples, streaks and foam move with the river.

This document turns those rows into concrete steps for the current code, in the order to do them,
with acceptance criteria and checks. All visual parameters (Secchi depth, fog heights, absorption
ratios) are estimated look-development values, not measured data for any river.

## Where to start

Start with **Step 0** (half a day: baseline captures and a frame-time log) and then **Step 1,
water optics**. Water fills most of every view, the work stays inside one shader and one
component, and it needs no render pipeline change. Step 2 (fog) touches every shader and adds a
renderer feature, so it goes second. Step 3 (flow map) depends on how currents will be authored
for future passages, so it goes last.

| Step | Work | Main files | Rough effort |
|---|---|---|---|
| 0 | Baseline captures and frame-time log | new `GraphicsPhaseTwoCheck.cs` | 0.5 day |
| 1 | Water optics: refraction, Beer-Lambert absorption, reflection fallback and filtering, specular antialiasing | `RiverWaterSurface.hlsl`, `RiverWater.shader`, `RiverPlanarReflection.cs`, `RiverWaterAndSkyBuilder.cs` | 4 to 6 days |
| 2 | Height fog with sun in-scattering | new `RiverFog.hlsl`, new `RiverFogFeature.cs`, all `River*` shaders, `WeatherController.cs` | 3 to 5 days |
| 3 | Flow map from the simulated current | new `RiverFlowMap.cs`, `RiverWaterSurface.hlsl`, `CurrentFieldProvider.cs`, scene builders | 3 to 5 days |

Efforts are rough single-developer estimates.

## Baseline after phase 1

| Topic | Today | Where |
|---|---|---|
| Fog | Unity exponential squared fog, uniform in height; density and colour from `WeatherController.ApplyFog`; every custom shader calls `MixFog`; the sky blends `unity_FogColor` at the horizon | `WeatherController.cs`, `River*.shader` |
| Water colour | Scene depth is sampled; one scalar `exp(-depth k)` blends deep and shallow colours; the surface is alpha blended over the bed | `RiverWaterSurface.hlsl` lines 562 to 638 |
| Refraction | None. The PC URP asset already provides the opaque texture (2x downsampled) and the depth texture | `Settings/PC_RPAsset.asset` |
| Reflection | 768 px planar texture, blur by plain mip level from roughness; clamped at the screen edge because the probe rendered a black fringe | `RiverPlanarReflection.cs`, shader line 604 |
| Sky probe | Phase 1 added a live sky reflection probe in `RiverLighting`, but the water does not use it as a fallback yet | `RiverLighting.cs` |
| Specular aliasing | Glint exponent widens with distance only; ripple normals have no motion vectors and can shimmer or soften under TAA | shader line 617 |
| Current on water | One global `_FlowDirection` vector for the whole river | `RiverWater.shader`, shader line 125 |
| Frame time | Not measured by any check | none |

## Step 0: Baseline captures and frame-time log

Every later step is judged against this baseline, so build it before touching a shader.

- Create `Scripts/Editor/GraphicsPhaseTwoCheck.cs` on the pattern of `GraphicsPhaseOneCheck`:
  dedicated batch editor, staged captures, `GRAPHICS_PHASE_TWO|PASS` or `FAIL`, runtime errors count
  as failure, captures in `Logs/GraphicsPhaseTwo/`.
- Fixed camera poses in Gorodets: bank-side shallows with the hull at the waterline, a long view
  down the reach toward the sun, the same view away from the sun, and an overhead of a current
  region boundary. Conditions: clear noon, low sun (dawn), fog 65 %, rain, night.
- Frame-time log: after warm-up, record 300 frames per pose with `FrameTimingManager` (CPU and GPU
  time where the platform reports it) and write the median and 95th percentile to
  `Logs/GraphicsPhaseTwo/frame-times.csv`. Note the GPU in the file; timings from one development
  machine are a comparison, not a budget.
- Run it once on the current build and keep the captures and CSV as the "before" set.

**Decision needed before Step 1:** the minimum target GPU and resolution. The research document's
example budget (1080p at 60 Hz) cannot be checked without it.

## Step 1: Water optics

### 1a. Refraction

- Sample the scene behind the surface with `SampleSceneColor` at the screen UV, offset by the
  surface normal: `offset = normalWS.xz * refractionStrength * saturate(depth / 1 m)`, so the
  offset fades to zero at the waterline and the contact edge does not tear.
- Re-sample scene depth at the offset UV. If that depth is in front of the water surface, the
  offset picked up a foreground object (hull, buoy, reeds above water): fall back to the
  unoffset UV. This is the standard fix for refraction halos.
- Stop alpha blending the surface over the bed. Compose the colour in the shader and output
  opaque, keeping only the existing `contact` fade at the very edge. The water already renders in
  the transparent queue, so the opaque texture does not contain the water itself.

### 1b. Absorption and scattering by depth

- Replace the scalar `depthVariation` blend with per-channel Beer-Lambert transmittance along the
  refracted path: `T = exp(-sigma_rgb * pathLength)`, where `pathLength` is the optical depth
  already computed (`opticalDepth`).
- Derive `sigma` from one intuitive parameter, the Secchi depth `Z_SD`, through
  `K_d ≈ 1.7 / Z_SD` (Poole and Atkins), times an RGB ratio that absorbs blue most for sediment
  laden river water. Start with `Z_SD = 1.2 m` and a ratio near `(0.75, 1.0, 1.6)`; both are
  estimates to tune against reference photos of Volga water.
- Final water colour: `refracted * T + scatterColor * lighting * (1 - T)`, then the existing
  Fresnel reflection on top. `scatterColor` is the green-brown in-scattered colour.
- Let rain raise turbidity slightly (lower `Z_SD`) through the existing `_RiverRain` global.
- Material values are scene-owned: set them in `RiverWaterAndSkyBuilder`, not by hand in scenes.

### 1c. Reflection fallback and filtering

- First check in a capture that the live sky probe from `RiverLighting` now shows the cloud sky.
  If it does, replace the screen-edge clamp: blend from the planar reflection to
  `GlossyEnvironmentReflection` as `reflectionUV` approaches or leaves the 0 to 1 range. If the probe
  is still black, fix the probe first; the clamp stays until then.
- Give the planar reflection texture a roughness-matched blur chain: generate mips with a
  separable Gaussian whose width follows the GGX lobe for each mip's roughness (a small compute
  shader or command buffer after the reflection camera renders), instead of box-filtered
  automatic mips.
- Night: stretch the reflection lookup along the view direction by the ripple slope variance, so
  navigation lights reflect as vertical streaks rather than blurred discs.

### 1d. Specular antialiasing

- Add normal variance to roughness before the glint and the reflection mip are computed. Either
  Toksvig from the length of the mip-filtered ripple normal, or the screen-space variant from
  `ddx`/`ddy` of the normal. Clamp the added variance so near water keeps its sharp sun path.

### Step 1 acceptance

- The hull below the waterline and the bank bed within about 1 m of depth are visible and tinted
  green-brown; deep water shows no bed.
- No refraction halo around the hull, buoys or reeds (inspect the bank-side capture at 200 %).
- No black or clamped fringe at the screen edges in the long reach views.
- With a still camera, temporal luminance variance over open water drops compared with the
  baseline (less sparkle), while the sun glint path stays.
- `WaterWeatherCheck` and `ShipWakeRuntimeCheck` still pass; the wake and foam read the same.
- Frame time grows by no more than about 1 ms at the target resolution.

## Step 2: Height fog with sun in-scattering

### Design

- One shared function in `Shaders/RiverFog.hlsl`: density falls off exponentially with height,
  `density(h) = d0 * exp(-(h - h0) / H)`, integrated in closed form along the view ray, plus an
  optional second thin layer just above the water (radiation fog) with `H` of a few metres.
- In-scattered colour: the ambient fog colour from the sky SH, brightened toward the sun with a
  Henyey-Greenstein phase term (`g` about 0.6) and dimmed by cloud cover. Transmittance multiplies
  the scene colour.
- Apply it in two places with the same function:
  - a full-screen `RiverFogFeature` (URP renderer feature) after opaques and the sky, reading
    the depth texture, so URP Lit objects such as the vessel, buoys and marks get the same fog as
    the custom shaders;
  - inside the water shader, because the water is transparent and draws after that pass.
- Remove `MixFog` from `RiverGround`, `RiverFoliage`, `RiverBark` and the water, and stop enabling
  `RenderSettings.fog` for rendering, so nothing is fogged twice. Replace the sky shader's
  `unity_FogColor` horizon blend with the same fog colour so the horizon matches the terrain.
- Rain particles are transparent and will need the fog function or a matched tint.

### Weather and time of day

- `WeatherController.ApplyFog` publishes shader globals instead of `RenderSettings` values:
  base density, height falloff, radiation layer thickness and density, and colour.
- Map the existing fog levels (0, 30, 65, 100 %) and rain to those parameters, and add a dawn
  radiation-fog look when the sun is low and wind is light.
- Night: in-scattering from the moon only. Halos around navigation lights need volumetric fog and
  stay in phase 5.

### Step 2 acceptance

- In fog, bank tops and trees are clearer than the water surface at the same distance.
- A capture toward the low sun is warmer and brighter than the same view away from it.
- The vessel, buoys and marks fade exactly like the terrain behind them (no bright URP Lit objects
  in fog).
- `WaterWeatherCheck` fog attenuation still passes (update its threshold only with a before and
  after comparison, never to hide a regression).
- Night fog does not glow; frame time grows by no more than about 0.5 ms.

## Step 3: Flow map from the simulated current

### Bake

- `Scripts/Visuals/RiverFlowMap.cs` builds an `RGHalf` texture covering the water bounds (for
  Gorodets about 512 x 2048 texels, roughly 1 m per texel) by calling
  `CurrentFieldProvider.Sample` for each texel. The values are the same velocities the ship
  physics uses, so the visible and simulated current agree.
- Publish the texture and its world bounds as shader globals. Re-bake when the provider's regions
  change (for example the future discharge presets) through a version counter.
- `RiverTrainingScene` uses `RiverCurrentZone` triggers rather than a provider. Either give it a
  `CurrentFieldProvider` or teach the bake to rasterise zones; decide before building.
- Slower flow near the banks and eddies behind obstacles would also change ship behaviour. Add them
  to `CurrentFieldProvider` with tests, not only to the texture, so physics and visuals stay one
  source of truth.
- Scene-owned setup goes in the scene builders, per `CLAUDE.md`.

### Shader

- Replace the global `_FlowDirection` with the sampled velocity. Advect ripple normal UVs, the
  streak noise and the foam pattern with the two-phase flow-map method (Vlachos 2010): two UV
  sets offset by `flow * phase`, blended by a triangle weight so the reset is invisible.
- Keep the wind-driven ripples as a separate component moving with the wind, as today.
- Show shear where regions meet: streak intensity from the velocity gradient of the flow map.
- Advection changes normals only, not displacement, so water motion vectors and TAA behave as
  now.

### Step 3 acceptance

- EditMode test: texels of the baked map match `CurrentFieldProvider.Sample` at random water
  positions within 0.02 m/s.
- With a still camera, ripple drift measured between frames in a crop (phase correlation) points
  along the current within about 20 degrees in each Gorodets current region.
- Visible shear line at a region boundary in the overhead capture; no visible pulsing from the
  phase reset.
- Frame time grows by no more than about 0.3 ms.

## Tests to add

- EditMode: Beer-Lambert transmittance from Secchi depth (C# mirror of the shader function), height
  fog closed-form integral against numeric integration, weather to fog parameter mapping (denser
  fog never lowers density), flow-map bake against the provider, builder sets the new material
  properties in both scenes.
- Batch: `GraphicsPhaseTwoCheck` stages for each step's acceptance, run with rendering enabled in a
  dedicated editor like the other graphics checks.

## Risks and decisions

- **Target hardware** is undecided; set it before judging frame times.
- **Opaque texture resolution:** the PC asset downsamples it 2x. That suits turbid water; full
  resolution costs memory and bandwidth.
- **Look change at the shoreline:** switching from alpha blending to composed refraction changes
  how shallows read. Compare captures side by side before tuning anything else.
- **Custom water or Crest:** the research document suggests prototyping Crest on one reach before
  large custom water work. Steps 1 and 2 are worthwhile either way; decide before Step 3 and the
  later persistent-foam work.
- **More vessels:** keep every feature vessel independent. Refraction shows whatever hull is in the
  water; do not bake ship dimensions or positions into water or fog parameters. Open boats would
  need a water excluder inside the hull.

## Out of scope for phase 2

- Real terrain from DEM and OpenStreetMap, terrain materials and vegetation ecology (phase 3).
- Physically based sky, volumetric clouds, interactive heightfield waves, FFT wind waves (phase 4).
- Volumetric fog halos around navigation lights; Crest or HDRP decision (phase 5).
- Persistent advected foam texture: a natural follow-up once the flow map exists.

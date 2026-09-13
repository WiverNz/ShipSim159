# Graphics Realism: Research and Improvement Approach

Status: research document, written 2026-09-13. Nothing in it is implemented yet. It
collects established techniques from rendering literature, production talks and engine
documentation, compares them with the current ShipSim159 renderer, and proposes an order
of work. Numeric ranges quoted for albedo, illuminance or water optics are typical
literature values for tuning, not measurements of the Gorodets reach.

## 1. What "realistic" has to mean for this simulator

The viewer is a navigator on the bridge of a 138 m river ship, about 10 to 15 m above the
water, looking along a fairway 100 to 300 m wide for minutes at a time. That view is judged
on a few things, in rough order of importance:

1. **Light that behaves consistently.** Sun, sky, ambient light, fog and reflections all come
   from one sky state. Most "CG look" comes from these disagreeing, for example a blue sky
   reflected in water while the banks are lit by a flat grey ambient.
2. **Correct scale and distance cues.** Aerial perspective, detail fading, tree heights,
   bank heights and human structures (houses, pylons, churches, navigation signs) tell the
   eye how far away things are. In a navigation simulator this is also a functional need.
3. **Water that reads as a river.** Turbid green-brown colour, sky-dominated reflections at
   grazing angles, current-aligned texture, wind patches, and interaction with hulls,
   buoys and banks.
4. **Natural irregularity.** Vegetation grouped by ecology instead of scattered evenly,
   terrain materials that follow slope, height and moisture, and no visible tiling.
5. **Temporal stability.** No shimmering foliage, sparkling specular aliasing or popping
   LODs. Movement makes aliasing much more visible than a still screenshot does.

## 2. Current state and observed gaps

The current pipeline (URP 17, Unity 6000.6) already has:

- `RiverWater.shader`: tileable ripple normal map, Schlick Fresnel, planar reflection
  (`RiverPlanarReflection`, 768 px), depth-based shoreline colour, Kelvin wake, bow wave and
  propeller wash from `ShipWakeController`.
- `RiverSky.shader`: analytic gradient sky, sun disc, 2D fBm cloud deck, weather and wind
  driven through shader globals.
- `RiverGround.shader`: procedural albedo from vertex colour and noise, no textures.
- `RiverFoliage.shader` and generated tree, bush and reed meshes with 3 LODs.
- SMAA, HDR grading, ACES tonemapping, bloom, SSAO, exponential squared fog.

Gaps visible in the captures in `Logs/Wake/` and `Logs/Landscape/`:

| Area | Gap | Why it matters |
|---|---|---|
| Lighting | Trilight ambient is a fixed colour set in code, not derived from the sky | Banks and hull do not change with clouds, sun angle or overcast |
| Lighting | Reflection probe not rebaked for the new sky | Metallic and glossy surfaces reflect a stale or black environment |
| Atmosphere | Only exponential squared fog, same colour in every direction | No sun-side glow, no blue shift of distant banks, horizon looks like a wall |
| Sky | Clouds are a flat 2D layer, no cloud shadows on land or water | Large-scale light variation is missing, which is a strong outdoor realism cue |
| Terrain | Albedo is noise, not photographic; no macro variation from real land use | Reads as a painted surface from altitude |
| Vegetation | Uniform scatter, similar size trees, no grass layer, lollipop silhouettes | The most obvious "game" signal in wide shots |
| Water | No flow map: ripples scroll in one global direction | Real rivers show current lines, eddies behind obstacles, slower water near banks |
| Water | No refraction or absorption through depth | Shallow edges and the hull at the waterline look pasted on |
| Water | No interaction with banks, buoys or rain beyond simple rings | Buoys and banks ignore passing waves |
| Post | No auto exposure, non-physical light units | Night, overcast and sunset need manual per-state tuning |
| AA | SMAA is not temporal | Foliage alpha edges and water glints shimmer in motion |
| Scene | Almost no human structures | Scale and distance are hard to judge, and the scene does not look like the Volga |

## 3. Principles from the literature

### 3.1 Physically based rendering with calibrated inputs

Real-Time Rendering (Akenine-Möller et al.), Physically Based Rendering (Pharr et al.), Karis
(2013) and Lagarde and de Rousiers (2014) agree on one working rule: get the inputs into
physically plausible ranges first, then tune artistically. The renderer cannot recover
from albedos that are twice too bright or light levels that are arbitrary.

Typical broadband albedo ranges used for material authoring:

| Surface | Albedo (linear, approx.) |
|---|---|
| Green grass, meadow | 0.15 to 0.25 |
| Dry grass | 0.25 to 0.30 |
| Deciduous canopy | 0.10 to 0.18 |
| Conifer canopy | 0.05 to 0.12 |
| Dry sandy soil, beach sand | 0.25 to 0.40 |
| Wet soil, wet sand | 0.05 to 0.15 |
| Concrete | 0.30 to 0.40 |
| Asphalt | 0.05 to 0.10 |
| Water body colour (diffuse part only) | 0.02 to 0.06 |

Typical illuminance for exposure design:

| Condition | Illuminance |
|---|---|
| Clear midday sun on a horizontal surface | about 100 000 lx |
| Overcast day | about 1 000 to 10 000 lx |
| Sunrise or sunset | about 400 lx |
| Full moon | about 0.1 to 0.3 lx |
| Clear starlit night | about 0.001 lx |

The dynamic range between day and night is about eight orders of magnitude, which is why
night needs exposure adaptation rather than a darker sky material.

### 3.2 Reference-driven workflow

Production teams (Horizon Zero Dawn, Ghost of Tsushima, Far Cry 5) work from photographic
reference of the real place. For ShipSim159 this means:

1. Collect photographs and video of the Volga near Gorodets in the target seasons and
   weathers, ideally from a ship's bridge height.
2. Build a small "grey card" validation scene: 18 % grey sphere, white and black spheres,
   a chrome sphere, placed near the water. Compare their appearance with photos under the
   same sun elevation.
3. Compare luminance histograms and colour of sky, water and banks between capture and
   photo, not only the overall impression.
4. Keep the existing batch capture harness (`LandscapeRuntimeCheck`, `ShipWakeRuntimeCheck`)
   as the tool for fixed-camera before and after comparisons.

## 4. Atmosphere, sky and light

### 4.1 One atmosphere model drives everything

Bruneton and Neyret (2008) and Hillaire (2020) describe physically based sky models with
Rayleigh scattering (air molecules, blue), Mie scattering (aerosols, haze around the sun)
and ozone absorption. Hillaire's method is designed for real-time: a small transmittance
LUT, a multiple-scattering LUT, a sky-view LUT and a camera-volume "aerial perspective" LUT
of low resolution. The important point is not the sky colour itself but that the same model
produces:

- the sun colour and intensity reaching the ground (reddening at low sun, dimming in haze);
- the sky radiance used for ambient lighting (spherical harmonics or a probe);
- the aerial perspective applied to every distant object, replacing uniform fog;
- the sky reflected in the water.

Recommended adaptation for URP: compute the LUTs in a compute shader or on the CPU when the
time of day or weather changes (they change slowly), then sample them in `RiverSky.shader`,
in a full-screen aerial perspective pass after opaques, and when updating ambient SH.

### 4.2 Clouds

Schneider and Vos (2015, Horizon Zero Dawn "Nubis") and Hillaire (2016, Frostbite) render
volumetric clouds by ray marching a density field built from low-frequency shape noise
(Perlin-Worley) and high-frequency erosion noise, with a coverage and cloud-type map. Light
uses Beer-Lambert extinction along a few steps toward the sun, a "powder" term for dark
edges, and a two-lobe Henyey-Greenstein phase function for silver lining. Cost is controlled
by rendering at quarter resolution and reprojecting temporally.

A lighter intermediate step fits the current code:

1. Keep the 2D cloud deck but make it a thin volume: two or three ray-march steps through a
   slab, with self-shadowing toward the sun. This already adds depth and correct darkening.
2. Use the same cloud density function to project **cloud shadows** onto terrain and water
   (a light cookie on the main light, or a sampled texture in the ground and water shaders).
   Moving cloud shadows are one of the strongest outdoor realism cues and are cheap.
3. Tie cloud cover to ambient: under heavy cover, direct light drops and ambient becomes
   more uniform and grey.

### 4.3 Fog and volumetric light

Wronski (2014, Assassin's Creed IV) introduced froxel-based volumetric fog: a low-resolution
3D texture aligned to the camera frustum that accumulates in-scattered light and
transmittance. It gives visible sun shafts, halos around navigation lights in fog and
correct fog lighting at night. URP 17 has no built-in volumetric fog (HDRP has), so the
options are a custom froxel pass or a simpler analytic **exponential height fog with
directional in-scattering**: density falls off with altitude and the fog colour brightens
toward the sun. For river mornings, a thin dense layer just above the water (radiation fog)
is a characteristic look that height fog handles well.

### 4.4 Ambient and reflection probes

- Update ambient SH from the sky model whenever time of day or weather changes, instead of
  the hard-coded trilight colours in `DayNightController` and `ShipSimulatorVisualUpgrade`.
- Re-render a reflection probe (or a low-resolution cubemap) of the sky after sky changes.
  The water currently has to clamp at screen edges because the probe is stale.
- URP 17 supports Adaptive Probe Volumes (APV) for baked indirect light. Baking APV for the
  static banks and vegetation gives ground bounce, darker areas under trees and less flat
  shading, at little runtime cost.

## 5. Water

### 5.1 Surface shape

**Wind waves.** Tessendorf (2001) synthesises the surface with an FFT of a wave spectrum
(Phillips, or better JONSWAP). Horvath (2015) gives practical directional spectra for
graphics, including fetch-limited JONSWAP and the TMA spectrum, which corrects for finite
depth. For a river the fetch is short (hundreds of metres), so the spectrum is dominated by
short waves: significant height of a few centimetres to a few tens of centimetres even in
strong wind. A small FFT (64² or 128²) per cascade on the GPU, with two cascades, is enough.
The current single normal map can remain as a far-distance fallback.

**Current and flow.** Vlachos (2010, Portal 2) uses flow maps: a 2D velocity texture that
advects normal-map UVs, blended between two phases to hide the reset. Neyret (2003) analyses
advected textures more generally. Gonzalez-Ochoa (2016, Uncharted 4 rapids) shows how
flow-aligned detail, foam and normal intensity make rivers read correctly. For ShipSim159
the flow map should be **baked from the same `CurrentFieldProvider` and `FairwayRoute` data
that the physics uses**, so the visible current and the simulated current agree. Add slower
flow near banks, shear lines where regions meet, and eddies behind bridge piers, groynes and
moored objects.

**Interactive waves.** Options in increasing cost and fidelity:

| Technique | Source | Good for |
|---|---|---|
| Analytic Kelvin wake from track (current) | Kelvin stationary phase | Steady ship wake, cheap |
| Wave particles | Yuksel, House and Keyser (2007) | Many moving objects, reflections off banks |
| Heightfield simulation (iWave) in a ship-following render texture | Tessendorf (2004) | Waves hitting the hull, buoys bobbing, bank reflection in a local area |
| Water wave packets | Jeschke and Wojtan (2017) | Dispersive waves over large areas with obstacles |

A pragmatic path is to keep the analytic wake for the far field and add a local GPU
heightfield (for example 256² covering 300 m around the ship) that receives impulses from
the hull, buoys and rain and reflects from bank depth. Buoys and small craft can then sample
this height for visual bobbing.

### 5.2 Optics of a turbid river

Premože and Ashikhmin (2001) and Mobley (1994, *Light and Water*) describe natural water as
an absorbing and scattering medium. For rendering:

- **Fresnel** with index of refraction 1.333 (F0 about 0.02). Already in place.
- **Absorption and scattering by depth** with Beer-Lambert per colour channel:
  `transmittance = exp(-sigma_t * path_length)`. River water is dominated by sediment and
  dissolved organic matter, so blue is absorbed strongly and the in-scattered colour is
  green-brown.
- A useful field link is the empirical relation between the diffuse attenuation coefficient
  and the Secchi depth: `K_d ≈ 1.7 / Z_SD` (Poole and Atkins). Lowland reservoirs and large
  rivers often have Secchi depths of roughly 1 to 2 m, which sets the visible depth of the
  bottom near banks. Treat it as a tunable parameter until local data is found.
- **Refraction**: URP's Opaque Texture lets the water shader sample the scene behind the
  surface with a normal-based offset, and the depth texture gives the path length. This makes
  the hull below the waterline, shallow bottoms and reeds look embedded in the water.
- **Subsurface light in waves**: thin wave crests backlit by the sun brighten toward the
  scattering colour. A cheap approximation uses wave height and the view-sun angle.
- **Specular antialiasing**: Bruneton, Neyret and Holzschuch (2010) filter ocean normals into
  roughness with distance; Toksvig (2005) and LEAN mapping (Olano and Baker, 2010) do the same
  for normal maps. This removes sparkle in motion and keeps a correct sun glitter path.

### 5.3 Reflections

The planar reflection is the right base for a flat river: it is exact for banks, the ship and
bridges. Improvements:

- Blur the planar texture by roughness with a proper GGX-prefiltered mip chain rather than
  plain mip sampling.
- Use a sky probe from section 4.4 where the planar texture has no data, instead of clamping.
- Reflections of navigation lights at night are long vertical streaks on rippled water.
  Stretching the reflection lookup along the view direction by ripple slope variance
  reproduces this.
- Screen-space reflections are not needed for the water plane but can help small puddles on
  deck in rain.

### 5.4 Foam, shoreline and weather on water

- Tessendorf's FFT method gives a foam mask from the Jacobian determinant of the horizontal
  displacement (where waves fold). Dupuy and Bruneton (2012) render whitecaps statistically
  for distant water.
- Persistent foam should be a **simulated texture**: foam is injected at the bow, propellers,
  breaking crests and obstacles, then advected by the flow map and decays over time. Sea of
  Thieves (SIGGRAPH 2018 talk) and the Crest ocean system use this approach. It gives long
  lingering wake lines and foam collecting in eddies and along current convergence lines.
- Shoreline: a wet band on the ground shader tied to the actual water level, swash that
  moves with passing ship waves, and debris or foam lines along the bank.
- Rain: animated ripple normal sequence (splash rings) scaled by rain intensity, reduced
  reflection sharpness, and darker, glossier wet materials on land and deck.

### 5.5 Build, adopt, or switch pipeline

| Option | Strengths | Costs and risks |
|---|---|---|
| Continue custom URP shaders | Full control, already integrated with wake, weather and physics data | Every feature above has to be written and maintained |
| Crest ocean system (open source, Unity, URP and HDRP) | FFT waves, dynamic wave simulation, flow, persistent foam, underwater, tested at scale | Designed around oceans first; needs adapting to a narrow river mesh and to the current field |
| HDRP water system | Official river surface type with current maps, deformers, foam generators, water excluder for hull interior, integrated volumetric clouds, physically based sky, volumetric fog, physical light units | Whole project must move to HDRP; custom URP shaders and planar reflection need porting; higher GPU cost; desktop only |

Recommendation: stay on URP for the next iterations (the project already depends on URP
behaviour and tests), and prototype a Crest river reach in a branch before committing to
larger custom water work. Re-evaluate HDRP only if volumetric clouds, volumetric fog and
physical light units become required together, because that is where HDRP's advantage is
largest.

## 6. Terrain and landscape

### 6.1 Use the real geography

- **Elevation**: Copernicus DEM GLO-30 (30 m global) gives the valley shape, terraces and
  high right bank of the Volga. Upsample with procedural detail near the water.
- **Land use and features**: OpenStreetMap gives waterways, riverbanks, settlements,
  buildings, roads, power lines and forests. These can drive vegetation masks and structure
  placement.
- **Colour**: Sentinel-2 imagery (10 m) gives macro colour variation for distant terrain,
  blended into close-up materials. Distant land then reads like the real landscape.
- **Bathymetry and fairway**: navigation charts of the waterway give channel edges and
  depths. This also serves the physics (section references in `ShipDynamicsRealismApproach.md`).

Real geography also makes leading marks, buoys and landmarks line up with what trainees know
from real passages.

### 6.2 Terrain materials

Production terrain talks (Widmark 2012, Battlefield 3; Moore 2018, Far Cry 5) layer a small
number of photographic material sets with rules:

- **Rules**: slope (steep banks become soil, clay or sand), height above water (wet sediment,
  mud, sand, then grass), moisture (distance to water and flow accumulation), and land use.
- **Materials**: photogrammetry texture sets with albedo, normal, roughness and height. CC0
  libraries such as Poly Haven and ambientCG are usable starting points.
- **Tiling removal**: stochastic texturing (Heitz and Neyret 2018; Deliot and Heitz 2019, the
  Unity procedural stochastic texturing sample) samples a texture at random offsets per
  triangular cell and blends with histogram preservation, so repeats disappear.
- **Height-based blending** between layers so grass grows between stones and sand fills
  cracks, instead of soft alpha blends.
- **Triplanar mapping** on steep bank faces to avoid stretching.
- **Wetness** as a function of distance above the current water level: darker albedo, higher
  smoothness, optionally a puddle mask. When the scenario water level changes, the wet line
  moves with it.

### 6.3 Vegetation built on ecology

The Volga floodplain has a recognisable zonation. Placing species by these zones matters more
than individual tree quality.

| Zone (height above normal water) | Typical vegetation |
|---|---|
| Water edge, shallows | Common reed (*Phragmites*), sedges, reedmace, water plants |
| Low floodplain, frequently flooded | Shrub willows, willow thickets, sandbars with sparse grass |
| Floodplain | White willow and black poplar trees, alder, wet meadows |
| River terraces, higher banks | Birch, aspen, pine on sandy soils, oak and lime in places, dry meadows, farmland |

Placement methods:

- Horizon Zero Dawn's GPU procedural placement (van Muijden, 2017): density maps per species
  are computed from layered rules (height, slope, moisture, distance to water, noise), then
  instances are generated on the GPU per tile near the camera.
- Poisson-disc sampling with clustering produces groves and gaps instead of even scatter.
  Tree scale should follow real height distributions (for example mature willows and poplars
  15 to 25 m, birches 15 to 20 m, pines 20 to 30 m).
- **Grass**: GPU-instanced blades or cards near the camera with density falling off by
  distance, as in Ghost of Tsushima (GDC 2021). From the bridge most grass is 50 m or more
  away, so a detail layer plus a good ground texture is often enough.
- **Wind**: Sousa (2007, GPU Gems 3 chapter 16, Crysis) combines main bending, detail leaf
  flutter and edge flutter driven by vertex colour, with gusts from a scrolling noise field.
  Tie the wind direction to `WeatherController`.
- **Leaves**: two-sided lighting with translucency, per-instance colour variation, ambient
  occlusion toward the trunk, and a specular term for waxy leaves.
- **Distance**: SpeedTree models or high-quality photogrammetry trees with LODs and
  **octahedral impostors** (Brucks, 2018) for far trees, so the far tree line keeps its
  silhouette and lighting instead of turning into billboards that face the camera.

### 6.4 Human structures and scale cues

For a river simulator, structures are the strongest realism and scale cue: villages with
wooden houses and fences, church domes, pylons and power lines crossing the river, bank
protection (riprap, concrete slabs) near settlements and locks, landing stages, moored
barges, and standard inland navigation signs. Even simple modular kits placed from
OpenStreetMap data change the impression of the scene more than extra trees.

## 7. Camera, exposure and post-processing

- **Exposure**: use physical camera settings (EV100) and implement auto exposure from a
  luminance histogram with a slow adaptation speed (URP has no built-in auto exposure). This
  lets day, overcast, dusk and night share one light setup.
- **Tonemapping**: ACES is acceptable, but it desaturates and shifts hues at high brightness.
  Compare with URP's Neutral tonemapper or an AgX-style curve against photographs. Remove
  global saturation boosts; realism comes from correct light, not extra saturation.
- **Colour grading**: derive a LUT by matching test captures to reference photos, and keep
  white balance consistent with the sky model.
- **Antialiasing**: temporal antialiasing or URP's Spatial-Temporal Post-processing (STP) in
  Unity 6 removes foliage and glint shimmer far better than SMAA. It needs motion vectors for
  animated vegetation and the water to avoid ghosting.
- **Lens effects**: no depth of field or motion blur for the bridge view (the human eye does
  not see them in that situation). Keep vignette subtle. Consider rain droplets and wipers on
  the bridge window as a dedicated effect.
- **Bloom**: energy-conserving, low intensity; navigation lights in fog should rely on
  volumetric scattering rather than bloom alone.

## 8. Weather and time of day

- Weather state should drive, from one place: cloud coverage and type, sun and ambient
  intensity, fog density and height, wind for water spectrum and vegetation, surface wetness,
  rain ripples on water and rain particles.
- Night: moonlight at 0.1 to 0.3 lx with auto exposure, star field, settlement lights, the
  ship's own deck lights, navigation lights with volumetric halos and elongated reflections.
- Dawn and dusk: low sun, long shadows, warm light through haze, and radiation fog layers over
  the water are highly characteristic of river mornings.

## 9. Performance budget

Target desktop hardware should be decided first. As a planning example at 1080p and 60 Hz
(16.6 ms):

| System | Budget (example) |
|---|---|
| Opaque terrain and vegetation | 4 to 5 ms |
| Water surface, reflection render | 2 to 3 ms |
| Sky, clouds, aerial perspective | 1 to 2 ms |
| Shadows | 2 ms |
| Post-processing and TAA or STP | 1.5 ms |
| Ship model, UI, reserve | 3 ms |

Tools: Unity Profiler and Frame Debugger, GPU vendor tools, and the capture harness extended
with a timed fly-through that logs frame times.

## 10. Proposed roadmap for ShipSim159

Ordered by impact per effort. Each step should be verified with fixed-camera captures and a
frame-time log before and after.

| Phase | Work | Impact | Effort |
|---|---|---|---|
| 1 | Sky-driven ambient SH and reflection probe refresh; cloud shadows on terrain and water; remove saturation boost; auto exposure | High | Low to medium |
| 1 | TAA or STP with motion vectors for foliage and water | High in motion | Medium |
| 2 | Aerial perspective and height fog with sun in-scattering, replacing uniform fog | High | Medium |
| 2 | Water refraction and depth absorption; GGX-prefiltered planar reflection; specular antialiasing | High | Medium |
| 2 | Flow map baked from `CurrentFieldProvider`; flow-aligned ripples and advected foam texture | High for a river | Medium |
| 3 | Real DEM and OpenStreetMap-driven terrain for the Gorodets reach; Sentinel-2 macro colour | Very high | High |
| 3 | Photogrammetry terrain materials with stochastic tiling, height blending and wetness line | High | Medium |
| 3 | Ecological vegetation zonation, clustered placement, impostors, better tree assets | Very high | High |
| 3 | Settlement, pylon, bank protection and landing stage kits placed from OSM | Very high | Medium to high |
| 4 | Physically based sky LUTs (Hillaire 2020); thin volumetric cloud layer | Medium to high | Medium to high |
| 4 | Local GPU heightfield for interactive waves; buoy bobbing; bank reflection | Medium | High |
| 4 | Small FFT wind-wave cascade replacing the static normal map near the camera | Medium | Medium |
| 5 | Froxel volumetric fog for night navigation lights | Medium | High |
| 5 | Decide on Crest adoption or HDRP migration after the phase 2 prototype | Strategic | Evaluation |

## 11. References

Rendering fundamentals

- T. Akenine-Möller, E. Haines, N. Hoffman et al., *Real-Time Rendering*, 4th ed., CRC Press, 2018.
- M. Pharr, W. Jakob, G. Humphreys, *Physically Based Rendering*, 4th ed., MIT Press, 2023.
- B. Karis, "Real Shading in Unreal Engine 4", SIGGRAPH Physically Based Shading course, 2013.
- S. Lagarde, C. de Rousiers, "Moving Frostbite to Physically Based Rendering 3.0", SIGGRAPH course, 2014.

Sky, atmosphere, clouds, fog

- E. Bruneton, F. Neyret, "Precomputed Atmospheric Scattering", Computer Graphics Forum (EGSR), 2008.
- S. Hillaire, "A Scalable and Production Ready Sky and Atmosphere Rendering Technique", Computer Graphics Forum (EGSR), 2020.
- S. Hillaire, "Physically Based Sky, Atmosphere and Cloud Rendering in Frostbite", SIGGRAPH course, 2016.
- A. Schneider, N. Vos, "The Real-time Volumetric Cloudscapes of Horizon Zero Dawn", SIGGRAPH Advances in Real-Time Rendering, 2015.
- B. Wronski, "Volumetric Fog and Lighting", SIGGRAPH Advances in Real-Time Rendering, 2014.

Water

- J. Tessendorf, "Simulating Ocean Water", SIGGRAPH course notes, 2001.
- J. Tessendorf, "Interactive Water Surfaces", *Game Programming Gems 4*, 2004.
- C. J. Horvath, "Empirical Directional Wave Spectra for Computer Graphics", DigiPro, 2015.
- C. Yuksel, D. House, J. Keyser, "Wave Particles", ACM Transactions on Graphics (SIGGRAPH), 2007.
- S. Jeschke, C. Wojtan, "Water Wave Packets", ACM Transactions on Graphics (SIGGRAPH), 2017.
- A. Vlachos, "Water Flow in Portal 2", SIGGRAPH Advances in Real-Time Rendering, 2010.
- F. Neyret, "Advected Textures", Symposium on Computer Animation, 2003.
- C. Gonzalez-Ochoa, "Rendering Rapids in Uncharted 4", SIGGRAPH Advances in Real-Time Rendering, 2016.
- S. Premože, M. Ashikhmin, "Rendering Natural Waters", Computer Graphics Forum, 2001.
- C. D. Mobley, *Light and Water: Radiative Transfer in Natural Waters*, Academic Press, 1994.
- E. Bruneton, F. Neyret, N. Holzschuch, "Real-time Realistic Ocean Lighting using Seamless Transitions from Geometry to BRDF", Computer Graphics Forum (Eurographics), 2010.
- J. Dupuy, E. Bruneton, "Real-time Animation and Rendering of Ocean Whitecaps", SIGGRAPH Asia Technical Briefs, 2012.
- M. Toksvig, "Mipmapping Normal Maps", Journal of Graphics Tools, 2005.
- M. Olano, D. Baker, "LEAN Mapping", ACM Symposium on Interactive 3D Graphics and Games, 2010.
- "The Technical Art of Sea of Thieves", SIGGRAPH Talks, 2018.
- Crest ocean system for Unity (Wave Harmonic), open source.
- Unity, "The new Water System in Unity 2022 LTS and 2023.1": https://unity.com/blog/engine-platform/new-hdrp-water-system-in-2022-lts-and-2023-1
- Unity, HDRP Water System overview: https://docs.unity3d.com/Packages/com.unity.render-pipelines.high-definition@14.0/manual/WaterSystem-Overview.html
- Unity-Technologies/WaterScenes sample repository (river with current map, deformers, foam generators, water excluder): https://github.com/Unity-Technologies/WaterScenes

Terrain and vegetation

- M. Widmark, "Terrain in Battlefield 3", GDC, 2012.
- J. Moore, "Terrain Rendering in Far Cry 5", GDC, 2018.
- J. van Muijden, "GPU-Based Run-Time Procedural Placement in Horizon: Zero Dawn", GDC, 2017.
- E. Wohllaib, "Procedural Grass in Ghost of Tsushima", GDC, 2021.
- T. Sousa, "Vegetation Procedural Animation and Shading in Crysis", *GPU Gems 3*, chapter 16, 2007.
- R. Brucks, "Octahedral Impostors", 2018.
- E. Heitz, F. Neyret, "High-Performance By-Example Noise using a Histogram-Preserving Blending Operator", Proceedings of the ACM on Computer Graphics and Interactive Techniques (HPG), 2018.
- T. Deliot, E. Heitz, "Procedural Stochastic Textures by Tiling and Blending", *GPU Zen 2*, 2019.

Data sources

- Copernicus DEM GLO-30 global elevation model.
- OpenStreetMap.
- Copernicus Sentinel-2 multispectral imagery.

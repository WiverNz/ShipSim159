# Luch 14352 reference reconstruction

## Target and evidence, 2026-10-19

The user selected the updated 1994 project 14352, with a raised forward
wheelhouse, isolated forward side window, eight saloon windows and long aft
machinery casing. It is not the modern Luch concept or a 14351 replica.

- [Vympel archive photograph, about 1994](https://fleetphoto.ru/photo/478451/):
  primary appearance evidence, credited to Vympel History. The archive identifies
  the updated project 14352; the yard number is uncertain (`201?`). Used for the
  light roof, long low cabin, rounded windows and swept wheelhouse transition.
- [Vympel specifications](https://vympel.ru/projects/skeg-ships-and-hovercrafts/passazhirskoe-sudno-dlya-malykh-rek-tipa-luch/):
  overall length 23.72 m, hull depth 1.25 m, skeg height .45 m and fixed-part height
  3.60 m from baseline. The model retains the earlier documented interpretation
  of the conflicting 3.85/4.53 m beam figures, without claiming to resolve them.
- [SPK Fleet family arrangement](https://www.spkfleet.ru/luch.htm): side elevation,
  longitudinal section and deck plan provide rounded bow corners, accommodation
  layout and machinery context. The previously downloaded arrangement was
  inspected locally; the linked image server failed during this review.
  It is family-level evidence, not a verified 14352 offsets drawing.
- [2012 photograph including Luch-202](https://fleetphoto.ru/photo/42996/): the
  rear vessel supports the clipped wheelhouse corners and rounded glazing.
  The foreground Luch-10 is project 14351 and was not substituted for 14352.

## Reconstruction choices

`04_reference_shape.py` replaces the coarse bow with closer stations around
rounded plan corners, retains the wet deck and skegs, and gives the roof a shallow
continuous camber. The closed wheelhouse body, window frames and roof use shared
perimeter controls. Rear shoulders meet the saloon without blocking the isolated
forward window. Bridge window frames have rounded inner corners. Railings follow
the revised bow outline. A light roof follows the archive's tonal appearance;
its precise paint color cannot be recovered from that black-and-white photograph.

The lengths, small radii, window corners, roof camber and local plate shapes are
estimated. Retained machinery, waterjet, cushion seals, deck fittings and interior
proxies remain simplified. The model is not a surveyed or engineering-validated
replica. Physics, collision shapes and hydrodynamic calibration are unchanged.

Reference photographs remain in ignored `Logs/Luch14352/References`, outside the
runtime export. No reference image was used as a texture. Authored review renders
are in `Previews/reference-*.png`. The pre-edit Blender file is retained under
`Logs/Luch14352/ReferenceReview/before-reference-rebuild.blend`.

## PBR material pass, 2026-10-19

`05_materials.py` creates seven deterministic original 1024 x 1024 tile sets:
ivory paint, teal paint, charcoal hull paint, non-slip deck, rubber, aluminium
and seat fabric. Each has base color, tangent normal and packed metal/smoothness
maps (R metallic, A smoothness); the Blender shader decodes the same maps as URP.
The generator uses periodic noise, physical relief depths and a two-metre UV tile.
Paint includes restrained streaking and surface variation; bare aluminium has
brushing, rubber has fine wear and the exposed deck uses a separate grip material.
Glass uses neutral tint and adjusted transparency, not an opaque photo texture.

No external texture pack, generated AI image or reference-photo texture was used.
These are original procedural assets, reproducible from the checked-in script.
The PNG files and Unity .meta files are runtime assets in Models/Luch14352/Textures.
Only base color is imported as sRGB; normals and packed masks are linear, repeating,
mipmapped with anisotropic filtering. Eleven material slots are used per LOD.
The final export uses 65,148 / 29,316 / 7,820 triangles; the far LOD was reduced to
meet its existing 8,000-triangle budget, with closed geometry checks still passing.
`03_finish_export.py` applies materials on each rebuild. Unity's integrator binds
all maps by material name. The material integration test verifies UVs, texture
presence, import color space, normal type and shader keywords across all LODs.

Texture-review images are `Previews/textured-quarter.png` and
`Previews/textured-bow-detail.png`. Backup: Logs/Luch14352/ReferenceReview/before-textures.blend.

LOD2 omits distant railings, searchlights and small mast fittings instead of
collapsing thin shells. Texture imports finish before prefab editing to prevent
model reimports from resetting instance overrides or duplicating visuals.

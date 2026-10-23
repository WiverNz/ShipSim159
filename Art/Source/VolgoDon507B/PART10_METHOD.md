# Part 10 method fixed before hull edits

The 600 x 316 pixel body plan is traced in `part10-trace.json`. The source image
was inspected at enlarged display size; all saved coordinates refer to native
pixels. Three pixels represent two-pixel placement uncertainty plus approximately
half the visible stroke. Lettering and merged line portions are omitted.

Assumptions: 135 m calculation length divided into 20 equal intervals, station 0
at +67.5 m and 20 at -67.5 m. This is an estimated mapping, not a verified AP/FP
location. Half-beam is 8.25 m. The labelled design waterline is assigned the
selected 3.53 m draft. Numbered horizontal lines are not assigned integer metres.
Baseline and waterline skew are rectified separately on either side of centre.
These assumptions preserve the Part 9 uncertainty and do not certify 507B offsets.

Trace points become normalized half-breadth/height samples. Gaps between sampled
stations, intermediate waterlines and buttocks are derived by interpolation and
must be labelled derived, not traced. Flat central sections follow the grouped
section labels. The current waterline and above-water geometry constrain the fit.
The new underwater surface therefore may depart from the family trace; record
those differences rather than widening the three-pixel source tolerance.

Retain the current hull above the selected waterline, existing materials and both
UV channels. Preserve the other 48 source meshes, mechanisms, waterline, keel and
outer length/beam datums. Keep a local archival hull scene in the blend. Replace
only the lower shell where sparse triangles prevent fair sectional interpolation.
Transfer its texture coordinates from the archived mesh and preserve the inverted
legacy-V relationship. Do not scale geometry to match a desired displacement.

Build a separate capped, symmetric hydrostatic surface, excluded from gameplay
exports. Its volume is a measured consequence, not a mass calibration. Check
edge incidence, winding, connectedness, zero-area faces, self-intersections and
finite positive volume. Its declared upper cap limits later hydrostatic use.
Check propeller/nozzle clearance at centre and both hard-over positions, and
report existing mechanism self-intersections separately from hull contact.

Compare source sample residuals, retained-waterline fit and visual exports. Use
the Part 9 0.02 m integration tolerance. Preserve fallback hashes and physics;
Part 11 will decide how the new geometry informs buoyancy and loading.

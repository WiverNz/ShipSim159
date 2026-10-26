# MN-52 T-77 parameters and provenance

See `Art/Source/MN52_T77/README.md` for the complete photograph/reference register,
geometry assumptions, source workflow, coupling architecture and spawn procedure.
`Art/Source/MN52_T77/VALIDATION.md` records executed checks.

The selected geometric baseline is 28.8 m overall, 27.2 m constructive length,
6.0 m moulded beam, approximately 6.3 m including guards, 1.5 m side height,
1.2 m loaded and 0.5 m empty draft. Loaded/empty masses are 153/52.5 tonnes.
The operator's 1990 T-77 listing supports 28.8 x 6.31 m and 100 t capacity:
https://su-zholdary.kz/filialy/semeyskiy-filial/potrebitelyam/harakteristiki-sudov/

The exact identity is MN-52, built 6 October 1989 at Aral, yard 694, completed
at Khlebnikovsky; registry 147515. Historical port Serpukhov. Sources:
https://fleetphoto.ru/vessel/3081/ and https://russianships.net/id232858

The 28.2 m/140 t Korabel entry and FleetPhoto's inconsistent 59 t deadweight
are not averaged into the chosen targets. The 100.5 t difference between
full and light displacement is the data payload budget, not a surveyed tank plan.

No public verified MN-52 body-lines plan was recovered. Below-water sections
are inferred, fitted to the two stated displacement/draft pairs, and used by
both the Blender hull and the existing section hydrostatics. Resistance,
MMG derivatives, windage, centre of gravity and inertias remain estimates.
The barge has zero propulsion and no player input component. Joint forces and
collisions act through separate SI-mass Rigidbodies. As in the existing game,
hydrodynamic accelerations include model added mass while PhysX contact/joint
impulses use Rigidbody inertia; this is not a fully coupled added-mass solve.

The requested centreline Serpukhov + MN-52 formation is a plausible simulated
configuration, not established historical rigging. Photo 4585 shows RT-291
alongside MN-52, and is not cited as evidence of Serpukhov pushing it.

Do not present the prototype handling, coupling limits, grounding or wake
amplitudes as training validated.

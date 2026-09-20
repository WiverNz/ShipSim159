using System;
using ShipSimulator.Physics;
using ShipSimulator.Visuals;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ShipSimulator.Editor
{
    // Generates a vessel model for a type that has no imported mesh. The hull comes from the vessel data
    // (length, beam, depth, draft, propeller and rudder positions); raised decks, superstructure sizes and
    // liveries are estimated from photographs of the type, not taken from drawings.
    public static class ProceduralVesselBuilder
    {
        public enum DeckKind { DryCargo, Tanker, Passenger }

        public enum SectionKind { FullForm, Vee }

        public sealed class Design
        {
            public string Id;
            public string AssetName;
            public string MenuName;
            public string VesselClass;
            public DeckKind Kind;
            public SectionKind Section;
            // Vee hulls only: chine rise as a fraction of the side height.
            public float Deadrise;
            // Length of the bow entrance as a fraction of the vessel; 0 keeps the default full-form value.
            public float EntranceFraction;
            public float BilgeRadius;
            public float CabinStartFraction;
            public float CabinEndFraction;
            public float CabinWidthFraction;
            public float CabinHeight;
            public float WheelhouseHeight;
            public bool Foils;
            public bool Skegs;
            public bool BowRamp;
            public float ForecastleFraction;
            public float ForecastleHeight;
            public float PoopFraction;
            public float PoopHeight;
            public float HouseLength;
            public float HouseWidthFraction;
            public int Tiers;
            public float TierHeight;
            public float ForemastHeight;
            public float AftMastHeight;
            public int Holds;
            public Color Hull;
            public Color Bottom;
            public Color Deck;
            public Color House;
            public Color Hatch;
            public Color Pipe;
            public Color Funnel;
            public Color FunnelTop;

            public string JsonPath => "Assets/ShipSimulator/Data/Vessels/" + AssetName + ".json";
            public string ModelFolder => "Assets/ShipSimulator/Models/" + AssetName;
            public string PrefabPath => "Assets/ShipSimulator/Prefabs/Vessels/" + AssetName + ".prefab";
        }

        public static readonly Design VolgoBalt = new Design
        {
            Id = "volgobalt-295ar",
            AssetName = "VolgoBalt295AR",
            MenuName = "VOLGO-BALT\n2-95A/R",
            VesselClass = "RIVER-SEA\nCARGO VESSEL",
            Kind = DeckKind.DryCargo,
            BilgeRadius = 0.8f,
            ForecastleFraction = 0.12f,
            ForecastleHeight = 2.4f,
            PoopFraction = 0.2f,
            PoopHeight = 2.4f,
            HouseLength = 16f,
            HouseWidthFraction = 0.8f,
            Tiers = 2,
            TierHeight = 2.6f,
            ForemastHeight = 7.5f,
            AftMastHeight = 5f,
            Holds = 4,
            Hull = new Color(0.12f, 0.16f, 0.2f),
            Bottom = new Color(0.46f, 0.17f, 0.13f),
            Deck = new Color(0.36f, 0.23f, 0.18f),
            House = new Color(0.9f, 0.91f, 0.88f),
            Hatch = new Color(0.31f, 0.4f, 0.35f),
            Pipe = new Color(0.55f, 0.56f, 0.54f),
            Funnel = new Color(0.9f, 0.91f, 0.88f),
            FunnelTop = new Color(0.07f, 0.07f, 0.08f)
        };

        public static readonly Design Volgoneft = new Design
        {
            Id = "volgoneft-1577",
            AssetName = "Volgoneft1577",
            MenuName = "VOLGONEFT\n1577",
            VesselClass = "RIVER-SEA\nOIL TANKER",
            Kind = DeckKind.Tanker,
            BilgeRadius = 0.9f,
            ForecastleFraction = 0.1f,
            ForecastleHeight = 2.2f,
            PoopFraction = 0.18f,
            PoopHeight = 2.4f,
            HouseLength = 16f,
            HouseWidthFraction = 0.78f,
            Tiers = 2,
            TierHeight = 2.6f,
            ForemastHeight = 8f,
            AftMastHeight = 5.5f,
            Holds = 0,
            Hull = new Color(0.1f, 0.11f, 0.12f),
            Bottom = new Color(0.52f, 0.18f, 0.14f),
            Deck = new Color(0.3f, 0.36f, 0.31f),
            House = new Color(0.91f, 0.91f, 0.89f),
            Hatch = new Color(0.4f, 0.44f, 0.41f),
            Pipe = new Color(0.62f, 0.6f, 0.55f),
            Funnel = new Color(0.91f, 0.91f, 0.89f),
            FunnelTop = new Color(0.07f, 0.07f, 0.08f)
        };

        public static readonly Design Meteor = new Design
        {
            Id = "meteor-342u",
            AssetName = "Meteor342U",
            MenuName = "METEOR\n342U",
            VesselClass = "RIVER PASSENGER\nHYDROFOIL",
            Kind = DeckKind.Passenger,
            Section = SectionKind.Vee,
            Deadrise = 0.42f,
            EntranceFraction = 0.3f,
            BilgeRadius = 0.3f,
            ForecastleFraction = 0f,
            PoopFraction = 0f,
            CabinStartFraction = 0.08f,
            CabinEndFraction = 0.78f,
            CabinWidthFraction = 0.88f,
            CabinHeight = 2.3f,
            WheelhouseHeight = 1.7f,
            Foils = true,
            Hull = new Color(0.82f, 0.84f, 0.85f),
            Bottom = new Color(0.5f, 0.17f, 0.14f),
            Deck = new Color(0.35f, 0.38f, 0.4f),
            House = new Color(0.93f, 0.94f, 0.93f),
            Hatch = new Color(0.11f, 0.29f, 0.5f),
            Pipe = new Color(0.6f, 0.62f, 0.63f),
            Funnel = new Color(0.93f, 0.94f, 0.93f),
            FunnelTop = new Color(0.11f, 0.29f, 0.5f)
        };

        public static readonly Design Luch = new Design
        {
            Id = "luch-14352",
            AssetName = "Luch14352",
            MenuName = "LUCH\n14352",
            VesselClass = "RIVER PASSENGER\nAIR CUSHION",
            Kind = DeckKind.Passenger,
            Section = SectionKind.FullForm,
            EntranceFraction = 0.2f,
            BilgeRadius = 0.25f,
            ForecastleFraction = 0f,
            PoopFraction = 0f,
            CabinStartFraction = 0.12f,
            CabinEndFraction = 0.74f,
            CabinWidthFraction = 0.9f,
            CabinHeight = 2f,
            WheelhouseHeight = 1.6f,
            Skegs = true,
            BowRamp = true,
            Hull = new Color(0.86f, 0.87f, 0.86f),
            Bottom = new Color(0.45f, 0.16f, 0.13f),
            Deck = new Color(0.36f, 0.4f, 0.42f),
            House = new Color(0.94f, 0.94f, 0.92f),
            Hatch = new Color(0.13f, 0.35f, 0.45f),
            Pipe = new Color(0.6f, 0.62f, 0.63f),
            Funnel = new Color(0.94f, 0.94f, 0.92f),
            FunnelTop = new Color(0.13f, 0.35f, 0.45f)
        };

        private enum Part { Bottom, Hull, Deck, House, Glass, Fittings, Hatch, Pipe, Funnel, FunnelTop }

        private static readonly int PartCount = Enum.GetValues(typeof(Part)).Length;

        private static int P(Part part) => (int)part;

        private sealed class HullForm
        {
            public const int ArcSegments = 4;
            public const int LowerWall = 3;
            public const int UpperWall = 5;
            public const int RingCount = 2 + ArcSegments + LowerWall + UpperWall;

            public readonly float Beam;
            public readonly float Depth;
            public readonly float Draft;
            public readonly float Stern;
            public readonly float Bow;
            private readonly float entrance;
            private readonly float run;
            private readonly float bilgeRadius;
            private readonly SectionKind section;
            private readonly float deadrise;

            public HullForm(VesselData data, float bilge) : this(data, bilge, SectionKind.FullForm, 0f, 0f)
            {
            }

            public HullForm(VesselData data, float bilge, SectionKind sectionKind, float deadriseFraction,
                float entranceFraction)
            {
                section = sectionKind;
                deadrise = deadriseFraction;
                VesselDimensions d = data.dimensions;
                Beam = d.beamMouldedM;
                Depth = d.depthMouldedM;
                Draft = d.loadedDraftM;
                float overhang = d.lengthOverallM - d.lengthBetweenPerpendicularsM;
                Stern = -0.5f * d.lengthBetweenPerpendicularsM - 0.55f * overhang;
                Bow = 0.5f * d.lengthBetweenPerpendicularsM + 0.45f * overhang;
                entrance = (entranceFraction > 0f ? entranceFraction : 0.17f) * d.lengthOverallM;
                run = 0.22f * d.lengthOverallM;
                bilgeRadius = bilge;
            }

            public float DeckY => Depth - Draft;

            private float BowFraction(float z) => Mathf.Clamp01((z - (Bow - entrance)) / entrance);
            private float SternFraction(float z) => Mathf.Clamp01((Stern + run - z) / run);

            // Height of the hull bottom above the baseline: a spoon bow and a stern cut up over the propellers.
            public float Keel(float z)
            {
                float bow = BowFraction(z);
                if (bow > 0.45f) return Depth * Mathf.Pow((bow - 0.45f) / 0.55f, 2.2f);
                float stern = SternFraction(z);
                return stern > 0f ? (Draft + 0.35f) * (1f - Mathf.Pow(1f - stern, 2.2f)) : 0f;
            }

            // Lower waterlines are finer than the deck at both ends, which gives flare forward and a wide transom.
            public float HalfBreadth(float z, float heightAboveBase)
            {
                float h = Mathf.Clamp01(heightAboveBase / Depth);
                float half = 0.5f * Beam;
                float bow = BowFraction(z);
                if (bow > 0f) return Mathf.Lerp(half, 0.12f, Mathf.Pow(bow, Mathf.Lerp(1.7f, 3.2f, h)));
                float stern = SternFraction(z);
                if (stern > 0f)
                    return Mathf.Lerp(half, half * Mathf.Lerp(0.5f, 0.92f, h), Mathf.Pow(stern, Mathf.Lerp(1.2f, 4f, h)));
                return half;
            }

            public float DeckHalfBreadth(float z) => HalfBreadth(z, Depth);

            // Half section from the centreline bottom to the deck edge, with a fixed point count so stations
            // connect, and a row at the paint line so the boot top stays straight.
            public Vector2[] Section(float z, float paintLine)
            {
                var points = new Vector2[RingCount];
                float keel = Keel(z);
                float wallStart;
                if (section == SectionKind.Vee)
                {
                    // Straight deadrise from the centreline keel out to the chine.
                    float rise = Mathf.Max(0.15f, deadrise * (Depth - keel));
                    wallStart = keel + rise;
                    float chine = HalfBreadth(z, wallStart);
                    points[0] = new Vector2(0f, keel);
                    for (int a = 1; a <= ArcSegments + 1; a++)
                    {
                        float t = a / (float)(ArcSegments + 1);
                        points[a] = new Vector2(t * chine, keel + t * rise);
                    }
                }
                else
                {
                    float radius = Mathf.Max(0f, Mathf.Min(bilgeRadius, Mathf.Min(HalfBreadth(z, keel) * 0.6f, (Depth - keel) * 0.35f)));
                    wallStart = keel + radius;
                    float corner = Mathf.Max(0f, HalfBreadth(z, wallStart) - radius);
                    points[0] = new Vector2(0f, keel);
                    points[1] = new Vector2(corner, keel);
                    for (int a = 1; a <= ArcSegments; a++)
                    {
                        float angle = a / (float)ArcSegments * 0.5f * Mathf.PI;
                        points[1 + a] = new Vector2(corner + radius * Mathf.Sin(angle), wallStart - radius * Mathf.Cos(angle));
                    }
                }
                float paint = Mathf.Clamp(paintLine, wallStart, Depth);
                int index = 1 + ArcSegments;
                for (int k = 1; k <= LowerWall; k++)
                {
                    float y = Mathf.Lerp(wallStart, paint, k / (float)LowerWall);
                    points[++index] = new Vector2(HalfBreadth(z, y), y);
                }
                for (int k = 1; k <= UpperWall; k++)
                {
                    float y = Mathf.Lerp(paint, Depth, k / (float)UpperWall);
                    points[++index] = new Vector2(HalfBreadth(z, y), y);
                }
                return points;
            }
        }

        private struct Fixtures
        {
            public float PoopTop;
            public float ForecastleTop;
            public float ForecastleAft;
            public float PoopFront;
            public float WheelhouseFloor;
            public float WheelhouseFront;
            public float WheelhouseRoof;
            public float WingHalfWidth;
            public float AftMastTop;
            public float AftMastZ;
            public float ForemastTop;
            public float ForemastZ;
        }

        public static GameObject Build(Design design)
        {
            // The authored 14352 model owns its visual hierarchy; keep catalogue rebuilds on that path.
            if (design.Id == Luch.Id) return Luch14352ModelIntegrator.UpdatePrefab();
            TextAsset json = AssetDatabase.LoadAssetAtPath<TextAsset>(design.JsonPath);
            if (json == null) throw new InvalidOperationException("Vessel data not found: " + design.JsonPath);
            VesselData data = JsonUtility.FromJson<VesselData>(json.text);
            if (!VesselDataValidator.TryValidate(data, out string error))
                throw new InvalidOperationException(design.AssetName + " data is invalid: " + error);

            var form = new HullForm(data, design.BilgeRadius, design.Section, design.Deadrise, design.EntranceFraction);
            var mesh = new ProceduralMesh(PartCount);
            BuildHull(mesh, form);
            Fixtures fixtures = BuildDecks(mesh, form, design, data);
            if (design.Kind == DeckKind.Passenger)
            {
                BuildPassengerDeck(mesh, form, design, ref fixtures);
                if (design.Foils) BuildFoils(mesh, form, data);
                if (design.Skegs) BuildSkegs(mesh, form);
                if (design.BowRamp) BuildBowRamp(mesh, form);
            }
            else
            {
                BuildSuperstructure(mesh, form, design, ref fixtures);
                if (design.Kind == DeckKind.DryCargo) BuildHatches(mesh, form, design, fixtures);
                else BuildTankerDeck(mesh, form, fixtures);
            }
            BuildAppendages(mesh, form, data);

            EnsureFolder(design.ModelFolder);
            EnsureFolder(design.ModelFolder + "/Materials");
            Mesh meshAsset = SaveAsset(mesh.Build(design.AssetName), design.ModelFolder + "/" + design.AssetName + "Mesh.asset");
            Material[] materials = SaveMaterials(design);

            var root = new GameObject(design.AssetName);
            Rigidbody body = root.AddComponent<Rigidbody>();
            body.interpolation = RigidbodyInterpolation.Interpolate;
            root.AddComponent<VesselDataLoader>().Configure(json);
            root.AddComponent<ShipPhysicsController>();
            VesselDimensions d = data.dimensions;
            float sideX = -(0.5f * form.Beam - 0.35f);
            VesselLayout layout = root.AddComponent<VesselLayout>();
            layout.ConfigureCameraViews(CameraViews(meshAsset.bounds));
            // Generated wheelhouses are opaque solids, without an interior. Put the eye just beyond
            // the front glazing, at window height, rather than inside the cabin or its low roof.
            float eyeHeight = fixtures.WheelhouseFloor +
                0.62f * (fixtures.WheelhouseRoof - fixtures.WheelhouseFloor);
            layout.Configure(design.Id, d.lengthOverallM / 138.3f,
                new Vector3(-1f, eyeHeight, fixtures.WheelhouseFront + 0.35f),
                new Vector3(-1f, eyeHeight - 0.3f, fixtures.WheelhouseFront + 80f),
                new Vector3(sideX, fixtures.WheelhouseFloor + 1.3f, fixtures.WheelhouseFront - 1.1f), fixtures.WheelhouseFloor + 0.3f,
                new Vector3(0f, fixtures.ForemastTop, fixtures.ForemastZ), fixtures.ForecastleTop,
                new Vector3(0f, fixtures.AftMastTop, fixtures.AftMastZ), fixtures.WheelhouseRoof,
                new Vector3(0f, fixtures.PoopTop + 1.2f, form.Stern + 0.4f), fixtures.PoopTop);

            var visual = new GameObject("DetailedVisual");
            visual.transform.SetParent(root.transform, false);
            visual.AddComponent<MeshFilter>().sharedMesh = meshAsset;
            visual.AddComponent<MeshRenderer>().sharedMaterials = materials;

            AddCollisionHull(root, data);
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, design.PrefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        // Orbit camera views for this model, in model metres and in the order ShipFollowCamera names them.
        // Taken from the built silhouette so that every view stands clear of the hull, the cabin and the
        // masts: scaling the default 507B set by length alone would put the near views inside a short
        // passenger craft.
        private static Vector3[] CameraViews(Bounds bounds)
        {
            float top = Mathf.Max(bounds.max.y, 1f);
            float halfBeam = Mathf.Max(Mathf.Max(bounds.max.x, -bounds.min.x), 1f);
            float bow = bounds.max.z;
            float stern = bounds.min.z;
            float length = Mathf.Max(bow - stern, 1f);
            return new[]
            {
                new Vector3(halfBeam + 0.20f * length, 0.9f * top + 0.03f * length, stern - 0.45f * length),
                new Vector3(0f, top + Mathf.Max(1.5f, 0.03f * length), -0.10f * length),
                new Vector3(0f, top + 0.45f * length, -0.12f * length),
                new Vector3(-(halfBeam + 0.25f * length), 0.6f * top, -0.08f * length),
                new Vector3(halfBeam + 0.25f * length, 0.6f * top, -0.08f * length),
                new Vector3(0f, 0.75f * top, bow + 0.22f * length),
                new Vector3(0f, 0.80f * top, stern - 0.26f * length),
                new Vector3(-(halfBeam + 0.12f * length), 0.45f * top, -0.18f * length)
            };
        }

        private static void BuildHull(ProceduralMesh mesh, HullForm form)
        {
            int count = Mathf.CeilToInt((form.Bow - form.Stern) / 0.6f) + 1;
            int ring = HullForm.RingCount;
            var z = new float[count];
            var sections = new Vector2[count][];
            var starboard = new int[count, ring];
            var port = new int[count, ring];
            for (int i = 0; i < count; i++)
            {
                z[i] = Mathf.Lerp(form.Stern, form.Bow, i / (float)(count - 1));
                sections[i] = form.Section(z[i], form.Draft + 0.2f);
                for (int j = 0; j < ring; j++)
                {
                    Vector2 point = sections[i][j];
                    starboard[i, j] = mesh.Vertex(new Vector3(point.x, point.y - form.Draft, z[i]));
                    port[i, j] = mesh.Vertex(new Vector3(-point.x, point.y - form.Draft, z[i]));
                }
            }

            float axisY = 0.55f * form.Depth - form.Draft;
            for (int i = 0; i < count - 1; i++)
            for (int j = 0; j < ring - 1; j++)
            {
                SideQuad(mesh, starboard, i, j, axisY);
                SideQuad(mesh, port, i, j, axisY);
            }

            for (int j = 0; j < ring - 1; j++)
            {
                EndStrip(mesh, sections[0], j, z[0], form.Draft, -1f);
                EndStrip(mesh, sections[count - 1], j, z[count - 1], form.Draft, 1f);
            }

            float deckY = form.DeckY;
            for (int i = 0; i < count - 1; i++)
            {
                float a = sections[i][ring - 1].x;
                float b = sections[i + 1][ring - 1].x;
                mesh.FlatQuad(P(Part.Deck), new Vector3(-a, deckY, z[i]), new Vector3(a, deckY, z[i]),
                    new Vector3(b, deckY, z[i + 1]), new Vector3(-b, deckY, z[i + 1]), Vector3.up);
            }
        }

        private static void SideQuad(ProceduralMesh mesh, int[,] side, int i, int j, float axisY)
        {
            int a = side[i, j];
            int b = side[i + 1, j];
            int c = side[i + 1, j + 1];
            int d = side[i, j + 1];
            Vector3 center = 0.5f * (mesh.Position(a) + mesh.Position(c));
            int part = center.y < 0.2f ? P(Part.Bottom) : P(Part.Hull);
            mesh.Quad(part, a, b, c, d, new Vector3(center.x, center.y - axisY, 0f));
        }

        private static void EndStrip(ProceduralMesh mesh, Vector2[] section, int j, float z, float draft, float direction)
        {
            Vector2 lower = section[j];
            Vector2 upper = section[j + 1];
            int part = 0.5f * (lower.y + upper.y) - draft < 0.2f ? P(Part.Bottom) : P(Part.Hull);
            mesh.FlatQuad(part, new Vector3(-lower.x, lower.y - draft, z), new Vector3(lower.x, lower.y - draft, z),
                new Vector3(upper.x, upper.y - draft, z), new Vector3(-upper.x, upper.y - draft, z), new Vector3(0f, 0f, direction));
        }

        private static Fixtures BuildDecks(ProceduralMesh mesh, HullForm form, Design design, VesselData data)
        {
            float length = data.dimensions.lengthOverallM;
            var fixtures = new Fixtures
            {
                PoopFront = form.Stern + design.PoopFraction * length,
                ForecastleAft = form.Bow - design.ForecastleFraction * length,
                PoopTop = form.DeckY + design.PoopHeight,
                ForecastleTop = form.DeckY + design.ForecastleHeight
            };
            if (design.PoopFraction <= 0f)
            {
                fixtures.PoopFront = form.Stern;
                fixtures.PoopTop = form.DeckY;
            }
            if (design.ForecastleFraction <= 0f)
            {
                fixtures.ForecastleAft = form.Bow;
                fixtures.ForecastleTop = form.DeckY;
            }
            if (design.PoopFraction > 0f) RaisedDeck(mesh, form, form.Stern, fixtures.PoopFront, design.PoopHeight);
            if (design.ForecastleFraction > 0f) RaisedDeck(mesh, form, fixtures.ForecastleAft, form.Bow, design.ForecastleHeight);
            Railing(mesh, form, fixtures.PoopFront + 0.5f, fixtures.ForecastleAft - 0.5f, form.DeckY, 0.35f);
            if (design.PoopFraction > 0f) Railing(mesh, form, form.Stern + 0.5f, fixtures.PoopFront, fixtures.PoopTop, 0.35f);
            if (design.Kind == DeckKind.Passenger) return fixtures;
            // Forecastle bulwark with a windlass and anchors below it.
            BulwarkOnRaisedDeck(mesh, form, fixtures.ForecastleAft + 1f, form.Bow - 1.5f, fixtures.ForecastleTop);
            mesh.Box(P(Part.Fittings), new Vector3(0f, fixtures.ForecastleTop + 0.45f, form.Bow - 8f), new Vector3(3.2f, 0.9f, 1.4f));
            float anchorZ = form.Bow - 6f;
            float anchorX = form.HalfBreadth(anchorZ, form.Depth - 0.9f);
            for (int side = -1; side <= 1; side += 2)
                mesh.Box(P(Part.Fittings), new Vector3(side * (anchorX + 0.05f), form.DeckY - 0.9f, anchorZ),
                    new Vector3(0.25f, 1.2f, 0.9f), Quaternion.Euler(0f, side * 18f, 0f));

            fixtures.ForemastZ = fixtures.ForecastleAft + 3f;
            fixtures.ForemastTop = fixtures.ForecastleTop + design.ForemastHeight;
            mesh.Cylinder(P(Part.Fittings), new Vector3(0f, fixtures.ForecastleTop, fixtures.ForemastZ),
                new Vector3(0f, fixtures.ForemastTop - 0.4f, fixtures.ForemastZ), 0.14f);
            mesh.Cylinder(P(Part.Fittings), new Vector3(-1.4f, fixtures.ForemastTop - 2.2f, fixtures.ForemastZ),
                new Vector3(1.4f, fixtures.ForemastTop - 2.2f, fixtures.ForemastZ), 0.06f, 6);
            return fixtures;
        }

        // One long passenger cabin with window bands, a raised wheelhouse near its front and a short mast.
        private static void BuildPassengerDeck(ProceduralMesh mesh, HullForm form, Design design, ref Fixtures fixtures)
        {
            float deckY = form.DeckY;
            float aft = Mathf.Lerp(form.Stern, form.Bow, design.CabinStartFraction);
            float fore = Mathf.Lerp(form.Stern, form.Bow, design.CabinEndFraction);
            float width = form.Beam * design.CabinWidthFraction;
            float height = design.CabinHeight;
            float roof = deckY + height;
            float middle = 0.5f * (aft + fore);

            mesh.Box(P(Part.House), new Vector3(0f, deckY + 0.5f * height, middle), new Vector3(width, height, fore - aft));
            mesh.Box(P(Part.House), new Vector3(0f, roof + 0.06f, middle), new Vector3(width + 0.25f, 0.12f, fore - aft + 0.25f));
            // Window band with mullions, the length of the saloons.
            for (int side = -1; side <= 1; side += 2)
            {
                mesh.Box(P(Part.Glass), new Vector3(side * (0.5f * width + 0.03f), deckY + 0.62f * height, middle),
                    new Vector3(0.06f, 0.95f, fore - aft - 2.4f));
                for (float z = aft + 1.6f; z < fore - 1.2f; z += 1.8f)
                    mesh.Box(P(Part.Fittings), new Vector3(side * (0.5f * width + 0.05f), deckY + 0.62f * height, z),
                        new Vector3(0.09f, 1.05f, 0.14f));
            }
            // Tapered front over the narrowing bow, ending in a raked windscreen.
            const int noseSteps = 3;
            float noseLength = Mathf.Min(4.5f, 0.25f * (fore - aft));
            for (int s = 0; s < noseSteps; s++)
            {
                float z0 = fore + s * noseLength / noseSteps;
                float z1 = fore + (s + 1) * noseLength / noseSteps;
                float noseWidth = Mathf.Lerp(width, width * 0.45f, (s + 0.5f) / noseSteps);
                float noseHeight = Mathf.Lerp(height, height * 0.72f, (s + 0.5f) / noseSteps);
                mesh.Box(P(Part.House), new Vector3(0f, deckY + 0.5f * noseHeight, 0.5f * (z0 + z1)),
                    new Vector3(noseWidth, noseHeight, z1 - z0));
                mesh.Box(P(Part.Glass), new Vector3(0f, deckY + 0.62f * noseHeight, z1 + 0.02f),
                    new Vector3(noseWidth - 0.5f, 0.85f, 0.05f));
                for (int side = -1; side <= 1; side += 2)
                    mesh.Box(P(Part.Glass), new Vector3(side * (0.5f * noseWidth + 0.03f), deckY + 0.62f * noseHeight,
                        0.5f * (z0 + z1)), new Vector3(0.05f, 0.8f, (z1 - z0) * 0.8f));
            }
            // Blue band along the cabin side, the usual river passenger livery.
            for (int side = -1; side <= 1; side += 2)
                mesh.Box(P(Part.Hatch), new Vector3(side * (0.5f * width + 0.02f), deckY + 0.22f * height, middle),
                    new Vector3(0.05f, 0.3f, fore - aft));

            float wheelLength = Mathf.Min(3.4f, 0.25f * (fore - aft));
            float wheelFore = fore - 1.2f;
            float wheelWidth = width * 0.62f;
            fixtures.WheelhouseFloor = roof;
            fixtures.WheelhouseFront = wheelFore;
            fixtures.WheelhouseRoof = roof + design.WheelhouseHeight;
            fixtures.WingHalfWidth = 0.5f * wheelWidth;
            mesh.Box(P(Part.House), new Vector3(0f, roof + 0.5f * design.WheelhouseHeight, wheelFore - 0.5f * wheelLength),
                new Vector3(wheelWidth, design.WheelhouseHeight, wheelLength));
            mesh.Box(P(Part.House), new Vector3(0f, fixtures.WheelhouseRoof + 0.06f, wheelFore - 0.5f * wheelLength),
                new Vector3(wheelWidth + 0.3f, 0.12f, wheelLength + 0.3f));
            mesh.Box(P(Part.Glass), new Vector3(0f, roof + 0.62f * design.WheelhouseHeight, wheelFore + 0.03f),
                new Vector3(wheelWidth - 0.5f, 0.9f, 0.06f));
            for (int side = -1; side <= 1; side += 2)
                mesh.Box(P(Part.Glass), new Vector3(side * (0.5f * wheelWidth + 0.03f), roof + 0.62f * design.WheelhouseHeight,
                    wheelFore - 0.5f * wheelLength), new Vector3(0.06f, 0.85f, wheelLength - 0.8f));

            fixtures.AftMastZ = wheelFore - wheelLength - 0.6f;
            fixtures.AftMastTop = fixtures.WheelhouseRoof + 2.6f;
            // A craft this size carries one masthead light; the rig draws a second one lower and just
            // forward of it on the same mast.
            fixtures.ForemastZ = fixtures.AftMastZ + 0.5f;
            fixtures.ForemastTop = fixtures.AftMastTop - 1.4f;
            fixtures.ForecastleTop = fixtures.WheelhouseRoof;
            mesh.Cylinder(P(Part.Fittings), new Vector3(0f, fixtures.WheelhouseRoof, fixtures.AftMastZ),
                new Vector3(0f, fixtures.AftMastTop, fixtures.AftMastZ), 0.07f, 8);
            // Engine casing on the aft deck, kept inside the narrowing transom.
            float casingZ = aft - 1.2f;
            float casingWidth = Mathf.Min(width * 0.5f, 2f * form.DeckHalfBreadth(casingZ) - 0.8f);
            if (casingWidth > 0.4f)
                mesh.Box(P(Part.Fittings), new Vector3(0f, deckY + 0.45f, casingZ), new Vector3(casingWidth, 0.9f, 1.6f));
        }

        private static void BuildFoils(ProceduralMesh mesh, HullForm form, VesselData data)
        {
            float span = data.dimensions.beamOverallM;
            float depth = data.support != null ? data.support.supportedDraftM : 1.2f;
            float length = form.Bow - form.Stern;
            BuildFoil(mesh, form, form.Stern + 0.76f * length, span, 0.95f, depth);
            BuildFoil(mesh, form, form.Stern + 0.16f * length, span * 0.68f, 0.8f, depth * 0.95f);
        }

        private static void BuildFoil(ProceduralMesh mesh, HullForm form, float z, float span, float chord, float depth)
        {
            float foilY = -depth + 0.09f;
            mesh.Box(P(Part.Fittings), new Vector3(0f, foilY, z), new Vector3(span, 0.18f, chord));
            for (int side = -1; side <= 1; side += 2)
            {
                mesh.Box(P(Part.Fittings), new Vector3(side * (0.5f * span - 0.05f), foilY + 0.35f, z), new Vector3(0.1f, 0.8f, chord));
                float strutX = side * 0.19f * span;
                float top = form.Keel(z) - form.Draft;
                mesh.Box(P(Part.Fittings), new Vector3(strutX, 0.5f * (foilY + top), z),
                    new Vector3(0.16f, Mathf.Max(0.4f, top - foilY), chord * 0.85f));
            }
        }

        // Side hulls that stay in the water and hold the cushion between them.
        private static void BuildSkegs(ProceduralMesh mesh, HullForm form)
        {
            float length = form.Bow - form.Stern;
            float aft = form.Stern + 0.04f * length;
            float fore = form.Stern + 0.82f * length;
            float half = 0.5f * form.Beam;
            for (int side = -1; side <= 1; side += 2)
                mesh.Box(P(Part.Hull), new Vector3(side * (half - 0.38f), -form.Draft + 0.3f, 0.5f * (aft + fore)),
                    new Vector3(0.72f, 0.95f, fore - aft));
            // The cushion space between the skegs reads as a shadowed tunnel.
            mesh.Box(P(Part.Fittings), new Vector3(0f, -form.Draft + 0.62f, 0.5f * (aft + fore)),
                new Vector3(2f * (half - 0.78f), 0.3f, fore - aft));
        }

        private static void BuildBowRamp(ProceduralMesh mesh, HullForm form)
        {
            float z = form.Bow - 1.6f;
            mesh.Box(P(Part.Fittings), new Vector3(0f, form.DeckY + 0.35f, z), new Vector3(1.9f, 0.14f, 3.2f),
                Quaternion.Euler(18f, 0f, 0f));
            mesh.Box(P(Part.Fittings), new Vector3(0f, form.DeckY + 0.9f, z - 1.8f), new Vector3(2.1f, 1.1f, 0.12f));
        }

        private static void RaisedDeck(ProceduralMesh mesh, HullForm form, float start, float end, float height)
        {
            float baseY = form.DeckY;
            float topY = baseY + height;
            int steps = Mathf.Max(2, Mathf.CeilToInt((end - start) / 0.6f));
            float previousZ = start;
            float previousHalf = form.DeckHalfBreadth(start);
            for (int s = 1; s <= steps; s++)
            {
                float z = Mathf.Lerp(start, end, s / (float)steps);
                float half = form.DeckHalfBreadth(z);
                for (int side = -1; side <= 1; side += 2)
                    mesh.FlatQuad(P(Part.Hull), new Vector3(side * previousHalf, baseY, previousZ), new Vector3(side * half, baseY, z),
                        new Vector3(side * half, topY, z), new Vector3(side * previousHalf, topY, previousZ), new Vector3(side, 0f, 0f));
                mesh.FlatQuad(P(Part.Deck), new Vector3(-previousHalf, topY, previousZ), new Vector3(previousHalf, topY, previousZ),
                    new Vector3(half, topY, z), new Vector3(-half, topY, z), Vector3.up);
                previousZ = z;
                previousHalf = half;
            }
            float startHalf = form.DeckHalfBreadth(start);
            mesh.FlatQuad(P(Part.Hull), new Vector3(-startHalf, baseY, start), new Vector3(startHalf, baseY, start),
                new Vector3(startHalf, topY, start), new Vector3(-startHalf, topY, start), Vector3.back);
            float endHalf = form.DeckHalfBreadth(end);
            mesh.FlatQuad(P(Part.Hull), new Vector3(-endHalf, baseY, end), new Vector3(endHalf, baseY, end),
                new Vector3(endHalf, topY, end), new Vector3(-endHalf, topY, end), Vector3.forward);
        }

        private static void Railing(ProceduralMesh mesh, HullForm form, float start, float end, float deckY, float inset)
        {
            int posts = Mathf.Max(2, Mathf.CeilToInt((end - start) / 1.8f));
            for (int side = -1; side <= 1; side += 2)
            {
                Vector3 previousTop = Vector3.zero;
                Vector3 previousMid = Vector3.zero;
                for (int p = 0; p <= posts; p++)
                {
                    float z = Mathf.Lerp(start, end, p / (float)posts);
                    float x = side * (form.DeckHalfBreadth(z) - inset);
                    Vector3 foot = new Vector3(x, deckY, z);
                    Vector3 top = foot + Vector3.up * 1.05f;
                    Vector3 mid = foot + Vector3.up * 0.55f;
                    mesh.Box(P(Part.House), foot + Vector3.up * 0.525f, new Vector3(0.05f, 1.05f, 0.05f));
                    if (p > 0)
                    {
                        mesh.Beam(P(Part.House), previousTop, top, 0.05f, 0.05f);
                        mesh.Beam(P(Part.House), previousMid, mid, 0.035f, 0.035f);
                    }
                    previousTop = top;
                    previousMid = mid;
                }
            }
        }

        private static void BulwarkOnRaisedDeck(ProceduralMesh mesh, HullForm form, float start, float end, float deckY)
        {
            int steps = Mathf.Max(2, Mathf.CeilToInt((end - start) / 0.8f));
            for (int side = -1; side <= 1; side += 2)
            for (int s = 0; s < steps; s++)
            {
                float z0 = Mathf.Lerp(start, end, s / (float)steps);
                float z1 = Mathf.Lerp(start, end, (s + 1) / (float)steps);
                float x0 = side * form.DeckHalfBreadth(z0);
                float x1 = side * form.DeckHalfBreadth(z1);
                mesh.FlatQuad(P(Part.Hull), new Vector3(x0, deckY, z0), new Vector3(x1, deckY, z1),
                    new Vector3(x1, deckY + 1f, z1), new Vector3(x0, deckY + 1f, z0), new Vector3(side, 0f, 0f));
                mesh.FlatQuad(P(Part.House), new Vector3(x0 - side * 0.12f, deckY, z0), new Vector3(x1 - side * 0.12f, deckY, z1),
                    new Vector3(x1 - side * 0.12f, deckY + 1f, z1), new Vector3(x0 - side * 0.12f, deckY + 1f, z0), new Vector3(-side, 0f, 0f));
            }
        }

        private static void BuildSuperstructure(ProceduralMesh mesh, HullForm form, Design design, ref Fixtures fixtures)
        {
            float houseAft = form.Stern + 3f;
            float houseFore = houseAft + design.HouseLength;
            float width = form.Beam * design.HouseWidthFraction;
            float y = fixtures.PoopTop;
            float tierFore = houseFore;
            for (int t = 0; t < design.Tiers; t++)
            {
                tierFore = houseFore - t * 1.4f;
                float tierLength = tierFore - houseAft;
                mesh.Box(P(Part.House), new Vector3(0f, y + 0.5f * design.TierHeight, houseAft + 0.5f * tierLength),
                    new Vector3(width, design.TierHeight, tierLength));
                mesh.Box(P(Part.House), new Vector3(0f, y + design.TierHeight + 0.06f, houseAft + 0.5f * tierLength),
                    new Vector3(width + 0.4f, 0.12f, tierLength + 0.4f));
                WindowRow(mesh, width, y + 0.55f * design.TierHeight, houseAft + 1.6f, tierFore - 1.6f, tierFore);
                y += design.TierHeight;
            }

            // Wheelhouse at the front of the top tier, with bridge wings out to the ship's side.
            const float wheelLength = 6f;
            float wheelFore = tierFore - 0.4f;
            float wheelWidth = width * 0.86f;
            fixtures.WheelhouseFloor = y;
            fixtures.WheelhouseFront = wheelFore;
            fixtures.WheelhouseRoof = y + design.TierHeight;
            mesh.Box(P(Part.House), new Vector3(0f, y + 0.5f * design.TierHeight, wheelFore - 0.5f * wheelLength),
                new Vector3(wheelWidth, design.TierHeight, wheelLength));
            mesh.Box(P(Part.House), new Vector3(0f, fixtures.WheelhouseRoof + 0.07f, wheelFore - 0.5f * wheelLength),
                new Vector3(wheelWidth + 0.5f, 0.14f, wheelLength + 0.5f));
            mesh.Box(P(Part.Glass), new Vector3(0f, y + 0.62f * design.TierHeight, wheelFore + 0.03f),
                new Vector3(wheelWidth - 0.6f, 1.15f, 0.06f));
            for (int side = -1; side <= 1; side += 2)
            {
                mesh.Box(P(Part.Glass), new Vector3(side * (0.5f * wheelWidth + 0.03f), y + 0.62f * design.TierHeight, wheelFore - 2.2f),
                    new Vector3(0.06f, 1.1f, 3.2f));
                float wingSpan = 0.5f * form.Beam - 0.5f * wheelWidth;
                mesh.Box(P(Part.House), new Vector3(side * (0.5f * wheelWidth + 0.5f * wingSpan), y + 0.1f, wheelFore - 1.1f),
                    new Vector3(wingSpan, 0.2f, 2.2f));
                mesh.Box(P(Part.House), new Vector3(side * (0.5f * form.Beam - 0.05f), y + 0.6f, wheelFore - 1.1f),
                    new Vector3(0.1f, 1f, 2.2f));
                mesh.Box(P(Part.House), new Vector3(side * (0.5f * wheelWidth + 0.5f * wingSpan), y + 0.6f, wheelFore - 0.05f),
                    new Vector3(wingSpan, 1f, 0.1f));
            }
            fixtures.WingHalfWidth = 0.5f * form.Beam;

            fixtures.AftMastZ = wheelFore - 3.5f;
            fixtures.AftMastTop = fixtures.WheelhouseRoof + design.AftMastHeight;
            mesh.Cylinder(P(Part.Fittings), new Vector3(0f, fixtures.WheelhouseRoof, fixtures.AftMastZ),
                new Vector3(0f, fixtures.AftMastTop - 0.4f, fixtures.AftMastZ), 0.12f);
            mesh.Cylinder(P(Part.Fittings), new Vector3(-1.6f, fixtures.AftMastTop - 1.8f, fixtures.AftMastZ),
                new Vector3(1.6f, fixtures.AftMastTop - 1.8f, fixtures.AftMastZ), 0.05f, 6);
            mesh.Box(P(Part.Fittings), new Vector3(0f, fixtures.WheelhouseRoof + 0.5f, fixtures.AftMastZ - 1.5f), new Vector3(2.2f, 0.12f, 0.35f));

            // Funnel on the top tier aft of the wheelhouse.
            float funnelZ = wheelFore - wheelLength - 2.2f;
            const float funnelHeight = 4.4f;
            mesh.Box(P(Part.Funnel), new Vector3(0f, y + 0.5f * funnelHeight, funnelZ), new Vector3(1.8f, funnelHeight, 2.8f));
            mesh.Box(P(Part.FunnelTop), new Vector3(0f, y + funnelHeight + 0.3f, funnelZ), new Vector3(1.86f, 0.6f, 2.86f));

            // Lifeboats on the poop between the house and the ship's side.
            float boatX = 0.25f * (width + form.Beam);
            float boatWidth = Mathf.Min(1.5f, 0.5f * (form.Beam - width) - 0.1f);
            for (int side = -1; side <= 1; side += 2)
                mesh.Box(P(Part.Hatch), new Vector3(side * boatX, fixtures.PoopTop + 1.9f, houseAft + 6f),
                    new Vector3(boatWidth, 1f, 6f));
        }

        private static void WindowRow(ProceduralMesh mesh, float width, float y, float aft, float fore, float front)
        {
            for (float z = aft; z <= fore; z += 2.4f)
                for (int side = -1; side <= 1; side += 2)
                    mesh.Box(P(Part.Glass), new Vector3(side * (0.5f * width + 0.03f), y, z), new Vector3(0.06f, 0.85f, 1.1f));
            for (float x = -0.5f * width + 1.3f; x <= 0.5f * width - 1.3f; x += 2f)
                mesh.Box(P(Part.Glass), new Vector3(x, y, front + 0.03f), new Vector3(1.1f, 0.85f, 0.06f));
        }

        private static void BuildHatches(ProceduralMesh mesh, HullForm form, Design design, Fixtures fixtures)
        {
            float aft = fixtures.PoopFront + 3f;
            float fore = fixtures.ForecastleAft - 3f;
            const float gap = 2.2f;
            float length = (fore - aft - gap * (design.Holds - 1)) / design.Holds;
            float width = form.Beam * 0.68f;
            float deckY = form.DeckY;
            for (int h = 0; h < design.Holds; h++)
            {
                float center = aft + h * (length + gap) + 0.5f * length;
                mesh.Box(P(Part.Hatch), new Vector3(0f, deckY + 0.65f, center), new Vector3(width, 1.3f, length));
                mesh.Box(P(Part.Hatch), new Vector3(0f, deckY + 1.45f, center), new Vector3(width + 0.3f, 0.3f, length + 0.2f));
                for (float z = center - 0.5f * length + 3.2f; z < center + 0.5f * length - 1f; z += 3.2f)
                    mesh.Box(P(Part.Fittings), new Vector3(0f, deckY + 1.62f, z), new Vector3(width + 0.32f, 0.05f, 0.12f));
            }
        }

        private static void BuildTankerDeck(ProceduralMesh mesh, HullForm form, Fixtures fixtures)
        {
            float deckY = form.DeckY;
            float aft = fixtures.PoopFront + 0.5f;
            float fore = fixtures.ForecastleAft - 0.5f;
            float middle = 0.5f * (aft + fore);

            // Raised fore-and-aft walkway over the cargo pipes.
            mesh.Box(P(Part.Fittings), new Vector3(0f, deckY + 2.1f, middle), new Vector3(1.1f, 0.12f, fore - aft));
            for (float z = aft + 1f; z < fore; z += 4f)
                for (int side = -1; side <= 1; side += 2)
                {
                    mesh.Cylinder(P(Part.Fittings), new Vector3(side * 0.45f, deckY, z), new Vector3(side * 0.45f, deckY + 2.05f, z), 0.07f, 6, false);
                    mesh.Cylinder(P(Part.Pipe), new Vector3(side * 0.55f, deckY + 2.15f, z), new Vector3(side * 0.55f, deckY + 3.05f, z), 0.03f, 5, false);
                }
            for (int side = -1; side <= 1; side += 2)
                mesh.Cylinder(P(Part.Pipe), new Vector3(side * 0.55f, deckY + 3.05f, aft), new Vector3(side * 0.55f, deckY + 3.05f, fore), 0.035f, 6);

            foreach (float x in new[] { -2.4f, -1.7f, 1.7f, 2.4f })
            {
                mesh.Cylinder(P(Part.Pipe), new Vector3(x, deckY + 0.6f, aft), new Vector3(x, deckY + 0.6f, fore), 0.16f, 8);
                for (float z = aft + 2f; z < fore; z += 5f)
                    mesh.Box(P(Part.Fittings), new Vector3(x, deckY + 0.22f, z), new Vector3(0.25f, 0.44f, 0.25f));
            }

            // Cargo manifold and hose cranes amidships.
            float half = 0.5f * form.Beam;
            foreach (float offset in new[] { -1.2f, 0f, 1.2f })
                mesh.Cylinder(P(Part.Pipe), new Vector3(-(half - 1.4f), deckY + 0.9f, offset), new Vector3(half - 1.4f, deckY + 0.9f, offset), 0.14f, 8);
            for (int side = -1; side <= 1; side += 2)
            {
                mesh.Box(P(Part.Fittings), new Vector3(side * (half - 1.4f), deckY + 0.9f, 0f), new Vector3(0.6f, 0.7f, 3f));
                Vector3 postTop = new Vector3(side * (half - 2.4f), deckY + 5f, 3.5f);
                mesh.Cylinder(P(Part.Fittings), new Vector3(side * (half - 2.4f), deckY, 3.5f), postTop, 0.22f);
                mesh.Cylinder(P(Part.Fittings), postTop, new Vector3(side * 3f, deckY + 6.2f, 7.5f), 0.12f, 8);
            }

            for (float z = aft + 5f; z < fore - 4f; z += 9f)
                for (int side = -1; side <= 1; side += 2)
                    mesh.Box(P(Part.Hatch), new Vector3(side * 4.8f, deckY + 0.28f, z), new Vector3(1.4f, 0.55f, 1.4f));
            for (float z = aft + 9f; z < fore - 4f; z += 18f)
                for (int side = -1; side <= 1; side += 2)
                {
                    mesh.Cylinder(P(Part.Pipe), new Vector3(side * (half - 1.2f), deckY, z), new Vector3(side * (half - 1.2f), deckY + 3.2f, z), 0.08f, 6);
                    mesh.Box(P(Part.Fittings), new Vector3(side * (half - 1.2f), deckY + 3.35f, z), new Vector3(0.35f, 0.3f, 0.35f));
                }
        }

        private static void BuildAppendages(ProceduralMesh mesh, HullForm form, VesselData data)
        {
            VesselPropeller propeller = data.propeller;
            float radius = 0.5f * propeller.diameterM;
            for (int i = 0; i < propeller.count; i++)
            {
                float z = propeller.longitudinalPositionsM[i];
                float x = propeller.lateralPositionsM[i];
                float hullBottom = form.Keel(z) - form.Draft;
                float y = Mathf.Min(-form.Draft + radius + 0.3f, hullBottom - radius - 0.15f);
                mesh.Cylinder(P(Part.Fittings), new Vector3(x, y, z - 0.15f), new Vector3(x, y, z + 0.15f), radius, 16);
                mesh.Cylinder(P(Part.Fittings), new Vector3(x, y, z - 0.6f), new Vector3(x, y, z + 0.8f), 0.28f, 10);
                mesh.Cylinder(P(Part.Fittings), new Vector3(x, y, z + 0.8f), new Vector3(x, Mathf.Min(hullBottom + 0.3f, y + 1.2f), z + 7f), 0.14f, 8);
            }

            VesselRudder rudder = data.rudder;
            float chord = rudder.areaPerRudderM2 / rudder.spanM;
            float rudderTop = form.Keel(rudder.longitudinalPositionM) - form.Draft + 0.1f;
            for (int i = 0; i < rudder.count; i++)
                mesh.Box(P(Part.Fittings), new Vector3(rudder.lateralPositionsM[i], rudderTop - 0.5f * rudder.spanM, rudder.longitudinalPositionM),
                    new Vector3(0.35f, rudder.spanM, chord));
        }

        private static void AddCollisionHull(GameObject root, VesselData data)
        {
            VesselDimensions d = data.dimensions;
            float bottom = -d.loadedDraftM;
            float top = d.depthMouldedM - d.loadedDraftM + 1f;
            float height = top - bottom;
            float center = 0.5f * (top + bottom);
            var hull = new GameObject("CollisionHull");
            hull.transform.SetParent(root.transform, false);
            AddBox(hull, "MidshipCollision", new Vector3(0f, center, 0f), new Vector3(d.beamOverallM, height, 0.6f * d.lengthOverallM));
            AddBox(hull, "BowCollision", new Vector3(0f, center + 0.2f, 0.38f * d.lengthOverallM), new Vector3(0.7f * d.beamOverallM, height - 0.4f, 0.22f * d.lengthOverallM));
            AddBox(hull, "SternCollision", new Vector3(0f, center + 0.2f, -0.39f * d.lengthOverallM), new Vector3(0.85f * d.beamOverallM, height - 0.4f, 0.2f * d.lengthOverallM));
        }

        private static void AddBox(GameObject parent, string name, Vector3 center, Vector3 size)
        {
            var section = new GameObject(name);
            section.transform.SetParent(parent.transform, false);
            BoxCollider collider = section.AddComponent<BoxCollider>();
            collider.center = center;
            collider.size = size;
        }

        private static Material[] SaveMaterials(Design design)
        {
            (string name, Color color, float smoothness, float metallic)[] specs =
            {
                ("Bottom", design.Bottom, 0.25f, 0f),
                ("Hull", design.Hull, 0.38f, 0f),
                ("Deck", design.Deck, 0.18f, 0f),
                ("Superstructure", design.House, 0.42f, 0f),
                ("Glass", new Color(0.04f, 0.07f, 0.09f), 0.92f, 0f),
                ("Fittings", new Color(0.16f, 0.17f, 0.18f), 0.32f, 0.25f),
                ("Hatch", design.Hatch, 0.3f, 0f),
                ("Pipe", design.Pipe, 0.36f, 0.15f),
                ("Funnel", design.Funnel, 0.42f, 0f),
                ("FunnelTop", design.FunnelTop, 0.2f, 0f)
            };
            var materials = new Material[specs.Length];
            Shader lit = Shader.Find("Universal Render Pipeline/Lit");
            for (int i = 0; i < specs.Length; i++)
            {
                var material = new Material(lit) { name = design.AssetName + specs[i].name };
                material.SetColor("_BaseColor", specs[i].color);
                material.SetFloat("_Smoothness", specs[i].smoothness);
                material.SetFloat("_Metallic", specs[i].metallic);
                materials[i] = SaveAsset(material, $"{design.ModelFolder}/Materials/{design.AssetName}{specs[i].name}.mat");
            }
            return materials;
        }

        // Updates an existing asset in place so prefab and scene references keep their GUIDs.
        private static T SaveAsset<T>(T asset, string path) where T : Object
        {
            // The importer warns when the main object name differs from the file name.
            asset.name = System.IO.Path.GetFileNameWithoutExtension(path);
            T existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(asset, path);
                return asset;
            }
            EditorUtility.CopySerialized(asset, existing);
            EditorUtility.SetDirty(existing);
            Object.DestroyImmediate(asset);
            return existing;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int slash = path.LastIndexOf('/');
            EnsureFolder(path.Substring(0, slash));
            AssetDatabase.CreateFolder(path.Substring(0, slash), path.Substring(slash + 1));
        }
    }
}

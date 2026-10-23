using System;
using ShipSimulator.Physics;
using UnityEngine;

namespace ShipSimulator.Visuals
{
    // Presentation only, for each cargo hold: an invisible stencil quad in the hatch opening that keeps the
    // river surface, which passes through the hull, from showing inside the hold; and a heap of nominal bulk
    // cargo sized from the mass the loading model already carries for that compartment. It adds no mass,
    // collider or force. The cargo type is not modelled, so the heap uses one estimated bulk density; a mass
    // that would overfill the hold is drawn full and reported.
    public sealed class HoldInteriorVisual : MonoBehaviour
    {
        [Serializable]
        public sealed class Hold
        {
            public string name;
            public int compartment;
            // Vessel-local centre of the hold floor and the clear floor footprint inside the lining, metres.
            public Vector3 floorCentre;
            public Vector2 footprint;
            // Highest heap top that stays below the closed covers and their machinery.
            public float maxHeightM;
            // Vessel-local height of the opening's water mask, just under the closed covers.
            public float maskHeight;
        }

        // Estimated nominal bulk cargo (sand or gravel class), not a documented historical cargo.
        [SerializeField, Min(100f)] private float bulkDensityKgM3 = 1600f;
        // Heap sides fall this far inside the footprint, as an angle of repose would leave them.
        [SerializeField, Min(0f)] private float shoulderM = 0.6f;
        [SerializeField] private Material material;
        [SerializeField] private Material waterMask;
        [SerializeField] private Hold[] holds = Array.Empty<Hold>();

        private ShipPhysicsController ship;
        private Transform[] heaps;
        private GameObject[] masks;
        private float[] shown;

        public float BulkDensityKgM3 => bulkDensityKgM3;
        public GameObject WaterMask(int index) => masks[index];
        public Transform Heap(int index) => heaps[index];
        public int HoldCount => holds.Length;
        public Hold Definition(int index) => holds[index];
        // Heap height drawn for each hold, and whether its mass needed more room than the hold has.
        public float HeightM(int index) => shown != null ? HeightFor(index, MassOf(index)) : 0f;
        public bool Overfilled(int index) => RequiredHeightM(index, MassOf(index)) > holds[index].maxHeightM + 1e-4f;

        private float[] previewMass;

        public void Configure(Material cargoMaterial, Material maskMaterial, float densityKgM3, Hold[] definitions)
        {
            material = cargoMaterial;
            waterMask = maskMaterial;
            bulkDensityKgM3 = densityKgM3;
            holds = definitions ?? Array.Empty<Hold>();
            Rebuild();
        }

        // Edit-time and test preview with explicit compartment masses; runtime follows the ship instead.
        public void Show(float[] compartmentMassKg)
        {
            previewMass = compartmentMassKg != null ? (float[])compartmentMassKg.Clone() : null;
            Refresh(true);
        }

        public float RequiredHeightM(int index, float massKg)
        {
            Hold hold = holds[index];
            return massKg / (bulkDensityKgM3 * Mathf.Max(HeapArea(hold), 1e-3f));
        }

        private void Awake()
        {
            ship = GetComponent<ShipPhysicsController>();
            Rebuild();
        }

        private void LateUpdate() => Refresh(false);

        private float MassOf(int index)
        {
            int compartment = holds[index].compartment;
            if (previewMass != null && compartment < previewMass.Length) return previewMass[compartment];
            if (ship != null && ship.HasLoading && compartment < ship.Data.loading.compartments.Length)
                return ship.CompartmentMassKg(compartment);
            return 0f;
        }

        private float HeightFor(int index, float massKg) =>
            Mathf.Clamp(RequiredHeightM(index, massKg), 0f, holds[index].maxHeightM);

        private void Refresh(bool force)
        {
            if (heaps == null || heaps.Length != holds.Length) Rebuild();
            for (int i = 0; i < holds.Length; i++)
            {
                float height = HeightFor(i, MassOf(i));
                if (!force && Mathf.Abs(height - shown[i]) < 0.002f) continue;
                shown[i] = height;
                Hold hold = holds[i];
                bool visible = height > 0.01f;
                heaps[i].gameObject.SetActive(visible);
                if (!visible) continue;
                heaps[i].localPosition = hold.floorCentre;
                heaps[i].localScale = new Vector3(1f, height, 1f);
            }
        }

        // The heap's mean plan area: a frustum whose top is the footprint less the shoulder on every side.
        private float HeapArea(Hold hold)
        {
            float topX = Mathf.Max(hold.footprint.x - 2f * shoulderM, 0.1f * hold.footprint.x);
            float topZ = Mathf.Max(hold.footprint.y - 2f * shoulderM, 0.1f * hold.footprint.y);
            float bottom = hold.footprint.x * hold.footprint.y;
            float top = topX * topZ;
            return (bottom + top + Mathf.Sqrt(bottom * top)) / 3f;
        }

        private void Rebuild()
        {
            if (heaps != null)
                foreach (Transform heap in heaps)
                    if (heap != null) DestroyImmediateOrLater(heap.gameObject);
            if (masks != null)
                foreach (GameObject mask in masks)
                    if (mask != null) DestroyImmediateOrLater(mask);
            heaps = new Transform[holds.Length];
            masks = new GameObject[holds.Length];
            shown = new float[holds.Length];
            for (int i = 0; i < holds.Length; i++)
            {
                var heap = new GameObject("Cargo_" + holds[i].name, typeof(MeshFilter), typeof(MeshRenderer));
                heap.hideFlags = HideFlags.DontSave;
                heap.transform.SetParent(transform, false);
                heap.GetComponent<MeshFilter>().sharedMesh = HeapMesh(holds[i]);
                var renderer = heap.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                heap.SetActive(false);
                heaps[i] = heap.transform;
                shown[i] = -1f;
                masks[i] = MaskQuad(holds[i]);
            }
        }

        private GameObject MaskQuad(Hold hold)
        {
            var mask = new GameObject("WaterMask_" + hold.name, typeof(MeshFilter), typeof(MeshRenderer));
            mask.hideFlags = HideFlags.DontSave;
            mask.transform.SetParent(transform, false);
            mask.transform.localPosition = new Vector3(hold.floorCentre.x, hold.maskHeight, hold.floorCentre.z);
            float x = 0.5f * hold.footprint.x, z = 0.5f * hold.footprint.y;
            var mesh = new Mesh { name = "HoldWaterMask_" + hold.name, hideFlags = HideFlags.DontSave };
            mesh.SetVertices(new[] { new Vector3(-x, 0, -z), new Vector3(-x, 0, z), new Vector3(x, 0, z), new Vector3(x, 0, -z) });
            mesh.SetTriangles(new[] { 0, 1, 2, 0, 2, 3 }, 0);
            mesh.RecalculateBounds();
            mask.GetComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = mask.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = waterMask;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
            mask.SetActive(waterMask != null);
            return mask;
        }

        private static void DestroyImmediateOrLater(GameObject target)
        {
            if (Application.isPlaying) Destroy(target);
            else DestroyImmediate(target);
        }

        // Unit-height frustum standing on y = 0, scaled vertically to the cargo height.
        private Mesh HeapMesh(Hold hold)
        {
            float bx = 0.5f * hold.footprint.x, bz = 0.5f * hold.footprint.y;
            float tx = Mathf.Max(bx - shoulderM, 0.05f * bx), tz = Mathf.Max(bz - shoulderM, 0.05f * bz);
            Vector3[] bottom = { new(-bx, 0, -bz), new(bx, 0, -bz), new(bx, 0, bz), new(-bx, 0, bz) };
            Vector3[] top = { new(-tx, 1, -tz), new(tx, 1, -tz), new(tx, 1, tz), new(-tx, 1, tz) };
            var vertices = new System.Collections.Generic.List<Vector3>();
            var uvs = new System.Collections.Generic.List<Vector2>();
            var triangles = new System.Collections.Generic.List<int>();
            void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
            {
                int start = vertices.Count;
                vertices.AddRange(new[] { a, b, c, d });
                // World-scale planar UVs so the grain does not stretch with the heap.
                foreach (Vector3 v in new[] { a, b, c, d }) uvs.Add(new Vector2(v.x * 0.25f, v.z * 0.25f + v.y * 0.25f));
                triangles.AddRange(new[] { start, start + 1, start + 2, start, start + 2, start + 3 });
            }
            Quad(top[0], top[3], top[2], top[1]);
            for (int k = 0; k < 4; k++)
            {
                int n = (k + 1) % 4;
                Quad(bottom[k], top[k], top[n], bottom[n]);
            }
            var mesh = new Mesh { name = "CargoHeap_" + hold.name, hideFlags = HideFlags.DontSave };
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}

using ShipSimulator.Physics;
using UnityEngine;

namespace ShipSimulator.Visuals
{
    // Draws chains and mooring lines along the shapes GroundTackle solved in the last physics step, between the
    // attachment points as they are this frame. Runs after VesselMechanisms has posed the anchors.
    [DefaultExecutionOrder(100)]
    public sealed class GroundTackleVisual : MonoBehaviour
    {
        private const int ChainPoints = 28;
        private const int LinePoints = 18;
        // One texture tile shows two links of 4 d each for the archive 43 mm bow chain.
        private const float ChainTileM = 2 * 4 * 0.043f;

        [SerializeField] private Material chainMaterial;
        [SerializeField] private Material ropeMaterial;
        [SerializeField, Min(0.01f)] private float chainWidthM = 0.15f;
        // About twice a 40 mm rope, so the line reads at bridge distances.
        [SerializeField, Min(0.01f)] private float ropeWidthM = 0.08f;

        private GroundTackle tackle;
        private VesselMechanisms mechanisms;
        private LineRenderer[] chains = new LineRenderer[0];
        private LineRenderer[] ropes = new LineRenderer[0];
        private readonly Vector3[] chainBuffer = new Vector3[ChainPoints];
        private readonly Vector3[] ropeBuffer = new Vector3[LinePoints + 1];

        public void Configure(Material chain, Material rope, float chainWidth = 0.15f, float ropeWidth = 0.08f)
        {
            chainMaterial = chain;
            ropeMaterial = rope;
            chainWidthM = chainWidth;
            ropeWidthM = ropeWidth;
        }

        private void Awake()
        {
            tackle = GetComponent<GroundTackle>();
            mechanisms = GetComponent<VesselMechanisms>();
        }

        private void LateUpdate() => Draw();

        // Public so a check can time it on its own.
        public void Draw()
        {
            if (tackle == null || !tackle.IsFitted) return;
            if (chains.Length != tackle.AnchorCount) chains = Create("AnchorChain_", tackle.AnchorCount, chainMaterial, chainWidthM, true);
            if (ropes.Length != tackle.StationCount) ropes = Create("MooringLine_", tackle.StationCount, ropeMaterial, ropeWidthM, false);
            for (int i = 0; i < chains.Length; i++) DrawChain(i);
            for (int s = 0; s < ropes.Length; s++) DrawLine(s);
        }

        private void DrawChain(int i)
        {
            LineRenderer line = chains[i];
            AnchorPhase phase = tackle.Phase(i);
            bool visible = (phase == AnchorPhase.Hanging || phase == AnchorPhase.Bottom) &&
                tackle.ChainOutM(i) > GroundTackle.UnseatChainM;
            line.enabled = visible;
            if (!visible) return;
            Vector3 hawse = tackle.HawseWorld(i);
            Vector3 shackle = mechanisms != null && i < mechanisms.AnchorPivotCount ? mechanisms.ChainEndWorld(i) : tackle.ShackleWorld(i);
            if (phase == AnchorPhase.Hanging)
            {
                line.positionCount = 2;
                line.SetPosition(0, hawse);
                line.SetPosition(1, shackle);
                return;
            }
            CatenaryShape shape = tackle.ChainShape(i);
            for (int k = 0; k < ChainPoints; k++)
                chainBuffer[k] = Catenary.Point(shape, shackle, hawse, k / (float)(ChainPoints - 1));
            line.positionCount = ChainPoints;
            line.SetPositions(chainBuffer);
        }

        private void DrawLine(int station)
        {
            LineRenderer line = ropes[station];
            int k = tackle.LineAt(station);
            line.enabled = k >= 0 && ropeMaterial != null;
            if (!line.enabled) return;
            Vector3 lead = tackle.LineShip(k).LeadWorld, shore = tackle.LineShore(k).LeadWorld;
            bool shipHigh = tackle.LineShipHigh(k);
            CatenaryShape shape = tackle.LineShape(k);
            // From the shore bollard to the ship's lead, then inboard to the bollard it is made fast on.
            for (int n = 0; n < LinePoints; n++)
            {
                float t = n / (float)(LinePoints - 1);
                ropeBuffer[n] = shipHigh ? Catenary.Point(shape, shore, lead, t) : Catenary.Point(shape, lead, shore, 1 - t);
            }
            ropeBuffer[LinePoints] = tackle.LineShip(k).transform.position;
            line.positionCount = LinePoints + 1;
            line.SetPositions(ropeBuffer);
        }

        private LineRenderer[] Create(string prefix, int count, Material material, float width, bool chain)
        {
            foreach (Transform child in transform)
                if (child.name.StartsWith(prefix)) Destroy(child.gameObject);
            var created = new LineRenderer[count];
            for (int i = 0; i < count; i++)
            {
                var holder = new GameObject(prefix + i, typeof(LineRenderer)) { hideFlags = HideFlags.DontSave };
                holder.transform.SetParent(transform, false);
                LineRenderer line = holder.GetComponent<LineRenderer>();
                line.useWorldSpace = true;
                line.sharedMaterial = material;
                line.widthMultiplier = width;
                line.numCapVertices = chain ? 0 : 2;
                line.textureMode = chain ? LineTextureMode.Tile : LineTextureMode.Stretch;
                if (chain) line.textureScale = new Vector2(1 / ChainTileM, 1);
                line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                line.enabled = false;
                created[i] = line;
            }
            return created;
        }
    }
}

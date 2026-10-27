using UnityEngine;

namespace ShipSimulator.Physics
{
    public enum MooringPointKind { Ship, Shore }

    // A named place a mooring line is made fast. The transform is the bollard; the lead is where the line
    // leaves the hull or quay edge, and where its force acts. Ids are stable across scene rebuilds and are
    // what saves refer to.
    public sealed class MooringPoint : MonoBehaviour
    {
        [SerializeField] private string id;
        [SerializeField] private string label;
        [SerializeField] private MooringPointKind kind;
        [SerializeField] private Vector3 leadLocal;
        // Display order on the vessel: bow first.
        [SerializeField] private int order;

        public string Id => id;
        public string CanonicalLabel => string.IsNullOrEmpty(label) ? id : label;
        public string Label => ShipSimulator.UI.Localization.Source(CanonicalLabel);
        public MooringPointKind Kind => kind;
        public int Order => order;
        public Vector3 LeadWorld => transform.TransformPoint(leadLocal);

        public void Configure(string pointId, string pointLabel, MooringPointKind pointKind, Vector3 leadLocalPosition,
            int displayOrder = 0)
        {
            id = pointId;
            label = pointLabel;
            kind = pointKind;
            leadLocal = leadLocalPosition;
            order = displayOrder;
        }
    }
}

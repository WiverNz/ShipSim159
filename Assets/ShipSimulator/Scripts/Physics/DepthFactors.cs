namespace ShipSimulator.Physics
{
    // Multipliers on deep-water hull coefficients in shallow water; all are 1 in deep water.
    public readonly struct DepthFactors
    {
        public readonly float SwayAddedMass;
        public readonly float YawAddedInertia;
        public readonly float Yv;
        public readonly float Nr;
        public readonly float Nv;
        public readonly float Coupled;
        public readonly float Sway;
        public readonly float Yaw;
        public readonly float Mixed;

        public DepthFactors(float swayAddedMass, float yawAddedInertia, float yv, float nr, float nv,
            float coupled, float sway, float yaw, float mixed)
        {
            SwayAddedMass = swayAddedMass;
            YawAddedInertia = yawAddedInertia;
            Yv = yv;
            Nr = nr;
            Nv = nv;
            Coupled = coupled;
            Sway = sway;
            Yaw = yaw;
            Mixed = mixed;
        }

        public static DepthFactors Deep => new DepthFactors(1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f);
    }
}

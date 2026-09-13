using UnityEngine;

namespace ShipSimulator.Physics
{
    public struct HullForces
    {
        public float X;
        public float Y;
        public float N;
        public float ManoeuvringWeight;
    }

    // MMG hull forces blended into a cross-flow drag strip model at low speed, where the U^2 normalisation
    // of the MMG polynomial breaks down. Straight-ahead resistance is added separately.
    // Beyond MMG, which is written for ahead motion: terms that are lift or Munk moment scale with u
    // rather than U, so they reverse astern, and drift terms in X change sign with u.
    public sealed class HullForceModel
    {
        private readonly VesselParameters p;
        private readonly float[] stationX;
        private readonly float stationLength;

        public HullForceModel(VesselParameters parameters)
        {
            p = parameters;
            int stations = parameters.Data.hull.crossFlowStations;
            stationX = new float[stations];
            stationLength = parameters.Lpp / stations;
            for (int i = 0; i < stations; i++)
                stationX[i] = (i + 0.5f) * stationLength - 0.5f * parameters.Lpp;
        }

        public int StationCount => stationX.Length;
        public float StationPosition(int index) => stationX[index];

        // stationSwayCurrent: sway component of (local current - midship current) per station, or null.
        public HullForces Evaluate(float u, float v, float r, in DepthFactors depth, float[] stationSwayCurrent)
        {
            VesselHull h = p.Data.hull;
            float lpp = p.Lpp;
            float draft = p.Draft;
            float speed = Mathf.Sqrt(u * u + v * v);
            float scale = 0.5f * p.Rho * lpp * draft;
            float yv = p.HullYv * depth.Yv;
            float crossFlowDrag = Mathf.Max(0.5f, -(yv + h.yVVV * depth.Sway));
            float weight = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(h.lowSpeedBlendStartMps, h.lowSpeedBlendEndMps, speed));
            var forces = new HullForces { ManoeuvringWeight = weight };

            if (weight > 0f)
            {
                float reference = Mathf.Max(speed, Mathf.Max(h.lowSpeedBlendStartMps, 0.05f));
                float vp = v / reference;
                float rp = Mathf.Clamp(r * lpp / reference, -h.maxYawRatePrime, h.maxYawRatePrime);
                float quadratic = reference * reference;
                float direction = (float)System.Math.Tanh(u / 0.2f);
                float x = quadratic * (h.xVV * depth.Sway * vp * vp + h.xVR * depth.Coupled * vp * rp +
                    h.xRR * depth.Mixed * rp * rp + h.xVVVV * depth.Sway * vp * vp * vp * vp) * direction;
                float y = yv * reference * v + p.HullYr * depth.Coupled * u * r * lpp +
                    quadratic * depth.Sway * (h.yVVV * vp * vp * vp + h.yVVR * vp * vp * rp + h.yVRR * vp * rp * rp) +
                    quadratic * depth.Yaw * h.yRRR * rp * rp * rp;
                float n = p.HullNv * depth.Nv * u * v + p.HullNr * depth.Nr * reference * r * lpp +
                    quadratic * (h.nVVV * depth.Sway * vp * vp * vp + h.nVVR * depth.Mixed * vp * vp * rp +
                    h.nVRR * depth.Mixed * vp * rp * rp + h.nRRR * depth.Yaw * rp * rp * rp);
                forces.X += weight * scale * x;
                forces.Y += weight * scale * y;
                forces.N += weight * scale * lpp * n;

                // Current shear: local lift and cross-flow drag differences relative to the midship current.
                if (stationSwayCurrent != null)
                {
                    float liftY = 0f, liftN = 0f, dragY = 0f, dragN = 0f;
                    for (int i = 0; i < stationX.Length; i++)
                    {
                        float local = v + stationX[i] * r;
                        float shifted = local - stationSwayCurrent[i];
                        float liftDelta = -stationSwayCurrent[i] * stationLength;
                        float dragDelta = (shifted * Mathf.Abs(shifted) - local * Mathf.Abs(local)) * stationLength;
                        liftY += liftDelta;
                        liftN += stationX[i] * liftDelta;
                        dragY += dragDelta;
                        dragN += stationX[i] * dragDelta;
                    }
                    float liftScale = 0.5f * p.Rho * draft * yv * reference;
                    float dragScale = -0.5f * p.Rho * draft * crossFlowDrag;
                    forces.Y += weight * (liftScale * liftY + dragScale * dragY);
                    forces.N += weight * (liftScale * liftN + dragScale * dragN);
                }
            }

            if (weight < 1f)
            {
                float sumY = 0f, sumN = 0f;
                for (int i = 0; i < stationX.Length; i++)
                {
                    float local = v + stationX[i] * r - (stationSwayCurrent != null ? stationSwayCurrent[i] : 0f);
                    float drag = local * Mathf.Abs(local) * stationLength;
                    sumY += drag;
                    sumN += stationX[i] * drag;
                }
                float dragScale = -0.5f * p.Rho * draft * crossFlowDrag;
                forces.Y += (1f - weight) * dragScale * sumY;
                forces.N += (1f - weight) * dragScale * sumN;
            }
            return forces;
        }
    }
}

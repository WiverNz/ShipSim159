using System;
using ShipSimulator.Audio;
using UnityEngine;

namespace ShipSimulator.Editor
{
    // The 507B's audio emitter points; its soundscape is installed by ShipAudioBuilder like every vessel's. Points
    // are in vessel-root coordinates (x starboard, y up from the loaded waterline, z forward). Sourced from the model: windlass gypsies (mechanisms.json), propellers,
    // hatch covers. Estimated: the engines (Part 5), the exhaust uptake behind the wheelhouse (the model has no
    // funnel object), the hull-side water points and the stern capstan.
    public static class VolgoDonAudioIntegration
    {
        private static readonly (string name, Vector3 local)[] Points =
        {
            ("Engine_Port", new Vector3(-3f, -1f, -54f)), ("Engine_Starboard", new Vector3(3f, -1f, -54f)),
            ("Water_Bow", new Vector3(0f, 0f, 65f)),
            ("Propeller_Port_Audio", new Vector3(-3.6f, -2.18f, -64.65f)), ("Propeller_Starboard_Audio", new Vector3(3.6f, -2.18f, -64.65f)),
            ("HatchMachinery_Aft", new Vector3(0f, 3f, -29f)), ("HatchMachinery_Fore", new Vector3(0f, 3f, 43f)),
            ("Windlass_Audio", new Vector3(0f, 4f, 57.5f)),
            ("Exhaust_Port", new Vector3(-1.2f, 11.5f, -47.5f)), ("Exhaust_Starboard", new Vector3(1.2f, 11.5f, -47.5f)),
            ("Hull_Port", new Vector3(-8.35f, 0.5f, 10f)), ("Hull_Starboard", new Vector3(8.35f, 0.5f, 10f)),
            ("Windlass_Port", new Vector3(-1.09f, 4.5f, 60.6f)), ("Windlass_Starboard", new Vector3(1.09f, 4.5f, 60.6f)),
            ("Capstan_Stern", new Vector3(0f, 3.5f, -66.5f))
        };

        public static void Install(GameObject root)
        {
            AddPoints(root);
            ShipAudioBuilder.InstallSoundscape(root);
        }

        // Adds the points that are missing and moves existing ones to their listed place.
        public static void AddPoints(GameObject root)
        {
            GameObject visual = (root.transform.Find("DetailedVisual") ??
                throw new InvalidOperationException("The 507B prefab has no DetailedVisual.")).gameObject;
            Transform group = visual.transform.Find(VesselSoundscape.PointsName);
            if (group == null)
            {
                group = new GameObject(VesselSoundscape.PointsName).transform;
                group.SetParent(visual.transform, false);
            }
            Transform vessel = visual.transform.parent;
            foreach ((string name, Vector3 local) in Points)
            {
                Transform point = group.Find(name);
                if (point == null)
                {
                    point = new GameObject(name).transform;
                    point.SetParent(group, false);
                }
                point.position = vessel.TransformPoint(local);
            }
        }
    }
}

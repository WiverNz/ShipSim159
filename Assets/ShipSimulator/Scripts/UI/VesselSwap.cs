using ShipSimulator.CameraSystem;
using ShipSimulator.Physics;
using ShipSimulator.Visuals;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ShipSimulator.UI
{
    // Scenes are built with one vessel. Replacing it with the chosen catalogue vessel must happen before any
    // Start runs, because the HUD and weather bind to the ship they find then.
    public static class VesselSwap
    {
        public static string IdOf(ShipPhysicsController ship)
        {
            if (ship == null) return null;
            VesselLayout layout = ship.GetComponent<VesselLayout>();
            return layout != null ? layout.VesselId : VesselCatalogue.DefaultVesselId;
        }

        public static ShipPhysicsController Apply(VesselCatalogue catalogue, string vesselId)
        {
            ShipPhysicsController current = Object.FindAnyObjectByType<ShipPhysicsController>();
            if (current == null || catalogue == null) return current;
            VesselCatalogue.Entry entry = catalogue.Find(vesselId);
            if (entry == null || entry.prefab == null || IdOf(current) == entry.id) return current;
            return Replace(current, entry.prefab);
        }

        public static ShipPhysicsController Replace(ShipPhysicsController current, GameObject prefab)
        {
            GameObject previous = current.gameObject;
            Scene scene = previous.scene;
            // Built under an inactive parent so its components exist before Awake reads them.
            var holder = new GameObject("Vessel swap");
            holder.SetActive(false);
            SceneManager.MoveGameObjectToScene(holder, scene);
            GameObject vessel = Object.Instantiate(prefab, previous.transform.position, previous.transform.rotation,
                holder.transform);
            vessel.name = previous.name;
            ShipPhysicsController ship = vessel.GetComponent<ShipPhysicsController>();

            GroundingController previousGrounding = previous.GetComponent<GroundingController>();
            GroundingController grounding = null;
            if (previousGrounding != null)
            {
                grounding = vessel.AddComponent<GroundingController>();
                grounding.Configure(ship, previousGrounding.Bathymetry);
            }

            vessel.transform.SetParent(null, true);
            Object.DestroyImmediate(holder);

            foreach (ShipFollowCamera camera in Object.FindObjectsByType<ShipFollowCamera>(FindObjectsInactive.Include))
                if (camera.Target == current) camera.SetTarget(ship);
            foreach (ShipTelemetryUI hud in Object.FindObjectsByType<ShipTelemetryUI>(FindObjectsInactive.Include))
                if (hud.Ship == current) hud.SetShip(ship);
            foreach (GorodetsScenarioController mission in
                     Object.FindObjectsByType<GorodetsScenarioController>(FindObjectsInactive.Include))
                if (mission.Ship == current) mission.SetShip(ship, grounding);

            // Immediate, so lookups made later in this frame cannot find the replaced vessel.
            previous.SetActive(false);
            Object.DestroyImmediate(previous);
            return ship;
        }
    }
}

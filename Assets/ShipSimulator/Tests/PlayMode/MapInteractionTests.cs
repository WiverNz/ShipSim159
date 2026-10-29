using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using ShipSimulator.Physics;
using ShipSimulator.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace ShipSimulator.Tests
{
    public sealed class MapInteractionTests
    {
        private GameObject vessel, hudObject, mark;
        private float range;
        private bool north, expanded, legend;
        [SetUp]
        public void Setup()
        {
            range = VoyageSettings.MapRange; north = VoyageSettings.MapNorthUp;
            expanded = VoyageSettings.MapExpanded; legend = VoyageSettings.MapLegend;
            VoyageSettings.SetMapRange(300); VoyageSettings.SetMapNorthUp(false);
            VoyageSettings.SetMapExpanded(false); VoyageSettings.SetMapLegend(false);
        }
        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if (hudObject != null) Object.Destroy(hudObject);
            if (vessel != null) Object.Destroy(vessel);
            if (mark != null) Object.Destroy(mark);
            VoyageSettings.SetMapRange(range); VoyageSettings.SetMapNorthUp(north);
            VoyageSettings.SetMapExpanded(expanded); VoyageSettings.SetMapLegend(legend);
            yield return null;
        }
        [UnityTest]
        public IEnumerator ActualMarksPanZoomOrientationAndRestoreShareOneProjection()
        {
            vessel = TestVessel.Create(new Vector3(120, 0, 200));
            var ship = vessel.GetComponent<ShipPhysicsController>(); ship.ManualStepping = true; ship.Body.isKinematic = true;
            vessel.transform.rotation = Quaternion.Euler(0, 90, 0);
            mark = GameObject.CreatePrimitive(PrimitiveType.Cube); mark.name = "Right Red Buoy regression";
            mark.transform.position = vessel.transform.position + Vector3.right * 40;
            hudObject = new GameObject("Interactive map HUD");
            var hud = hudObject.AddComponent<ShipTelemetryUI>(); hud.SetShip(ship);
            yield return null; yield return null;
            ship.Body.rotation = Quaternion.Euler(0, 90, 0);
            vessel.transform.rotation = ship.Body.rotation;
            UnityEngine.Physics.SyncTransforms();
            yield return null; yield return null;
            var chart = hud.MapViewport;
            var marker = chart.Find("MovingWorld/" + mark.name) as RectTransform;
            Assert.That(marker, Is.Not.Null);
            foreach (bool up in new[] { false, true })
            foreach (float zoom in new[] { 100f, 800f, 4000f })
            {
                VoyageSettings.SetMapNorthUp(up); VoyageSettings.SetMapRange(zoom);
                yield return null; yield return null;
                Vector2 point = hud.CurrentMapProjection.Project(mark.transform.position);
                Assert.That(Vector2.Distance(point, marker.anchoredPosition), Is.LessThan(.01f));
                var own = chart.Find("Vessel") as RectTransform;
                Assert.That(Mathf.DeltaAngle(own.localEulerAngles.z, up ? -ship.transform.eulerAngles.y : 0), Is.EqualTo(0).Within(.01f));
            }
            VoyageSettings.SetMapExpanded(true); yield return null; yield return null;
            var interaction = chart.GetComponent<MapInteraction>();
            var pointer = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left,
                position = new Vector2(400, 300), delta = new Vector2(80, 20) };
            interaction.OnBeginDrag(pointer); interaction.OnDrag(pointer);
            Assert.That(MapInteraction.Dragging, Is.True); Assert.That(hud.MapFollowing, Is.False);
            pointer.button = PointerEventData.InputButton.Right;
            interaction.OnBeginDrag(pointer); interaction.OnEndDrag(pointer);
            Assert.That(MapInteraction.Dragging, Is.True, "Another mouse button cannot release the left-button map gesture.");
            pointer.button = PointerEventData.InputButton.Left;
            yield return null; yield return null;
            Vector3 center = hud.CurrentMapProjection.Center;
            Assert.That(Vector3.Distance(center, ship.transform.position), Is.GreaterThan(1));
            interaction.OnEndDrag(pointer); Assert.That(MapInteraction.Dragging, Is.False);
            mark.transform.position += Vector3.right * 20;
            yield return null; yield return null;
            Assert.That(Vector2.Distance(marker.anchoredPosition, hud.CurrentMapProjection.Project(mark.transform.position)), Is.LessThan(.01f));
            hud.RecenterMap(); yield return null; yield return null;
            Assert.That(hud.MapFollowing, Is.True);
            Assert.That(Vector3.Distance(hud.CurrentMapProjection.Center, ship.transform.position), Is.LessThan(.01f));
            hud.PanMap(new Vector2(20, 30));
            ship.ResetToStart(); yield return null; yield return null;
            Assert.That(hud.MapFollowing, Is.True, "Recovery must clear a stale panned centre.");
            Assert.That(Vector3.Distance(hud.CurrentMapProjection.Center, ship.transform.position), Is.LessThan(.01f));
            var engines = hudObject.transform.Find("TelegraphBlock");
            Assert.That(engines.gameObject.activeInHierarchy, Is.True);
        }
    }
}

using System.Collections;
using NUnit.Framework;
using UnityEngine.TestTools;
using ShipSimulator.CameraSystem;
using ShipSimulator.Persistence;
using ShipSimulator.Physics;
using ShipSimulator.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;

namespace ShipSimulator.Tests
{
    public sealed class CameraInteractionTests
    {
        private InputTestFixture input;
        private Mouse mouse;
        private GameObject vessel, cameraObject, canvasObject, eventsObject;
        private ShipFollowCamera follow;
        private ShipPhysicsController ship;
        private float sensitivity, zoom, timeScale;
        private Vector2 WorldPointer => new Vector2(Screen.width - 5, Screen.height - 5);

        [SetUp]
        public void SetUp()
        {
            input = new InputTestFixture(); input.Setup();
            mouse = InputSystem.AddDevice<Mouse>();
            sensitivity = VoyageSettings.CameraSensitivity;
            zoom = VoyageSettings.ZoomSensitivity;
            timeScale = Time.timeScale;
            VoyageSettings.SetSensitivity(1); VoyageSettings.SetZoomSensitivity(1);
            vessel = TestVessel.Create(Vector3.zero);
            ship = vessel.GetComponent<ShipPhysicsController>();
            ship.ManualStepping = true;
            cameraObject = new GameObject("Camera input regression", typeof(Camera));
            follow = cameraObject.AddComponent<ShipFollowCamera>();
            follow.SetTarget(ship);
            cameraObject.SendMessage("OnApplicationFocus", true);
            Tick(false);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(cameraObject);
            Object.DestroyImmediate(vessel);
            if (canvasObject != null) Object.DestroyImmediate(canvasObject);
            if (eventsObject != null) Object.DestroyImmediate(eventsObject);
            VoyageSettings.SetSensitivity(sensitivity); VoyageSettings.SetZoomSensitivity(zoom);
            Time.timeScale = timeScale;
            input.TearDown();
            Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
        }

        private void Tick(bool held, Vector2 delta = default, float wheel = 0, bool ui = false)
        {
            var state = new MouseState { position = ui ? new Vector2(50, 50) : WorldPointer,
                delta = delta, scroll = new Vector2(0, wheel) }.WithButton(MouseButton.Right, held);
            InputSystem.QueueStateEvent(mouse, state);
            InputSystem.Update();
            cameraObject.SendMessage("LateUpdate");
        }
        private void Settle() => follow.Advance(2);
        private void MakeUi()
        {
            if (EventSystem.current == null) eventsObject = new GameObject("Camera input events", typeof(EventSystem));
            canvasObject = new GameObject("Pointer ownership canvas", typeof(Canvas), typeof(GraphicRaycaster));
            canvasObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var panel = new GameObject("Map-like UI surface", typeof(RectTransform), typeof(Image));
            var rect = panel.GetComponent<RectTransform>(); rect.SetParent(canvasObject.transform, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.zero;
            rect.anchoredPosition = Vector2.zero; rect.sizeDelta = new Vector2(100, 100);
            Canvas.ForceUpdateCanvases();
        }

        [UnityTest]
        public IEnumerator UiOwnsGesturesStartedOverItWhileWorldDragRetainsOwnership()
        {
            MakeUi();
            yield return null;
            cameraObject.SendMessage("OnApplicationFocus", true);
            Tick(false, ui: true);
            Assert.That(GameplayInput.PointerOverUi(), Is.True, "The test UI must participate in real graphic raycasts.");
            var initial = follow.CaptureState();
            Tick(false, wheel: 1, ui: true); Settle();
            Assert.That(follow.CaptureState().distance, Is.EqualTo(initial.distance).Within(.001f));
            Tick(true, new Vector2(100, 30), 1, true);
            Tick(true, new Vector2(100, 30), 1); Settle();
            Assert.That(follow.OwnsOrbitGesture, Is.False);
            Assert.That(follow.CaptureState().yaw, Is.EqualTo(initial.yaw).Within(.001f));
            Assert.That(follow.CaptureState().distance, Is.EqualTo(initial.distance).Within(.001f));
            Tick(false);
            Tick(true, new Vector2(900, 900)); Settle();
            Assert.That(follow.OwnsOrbitGesture, Is.True);
            Assert.That(follow.CaptureState().yaw, Is.EqualTo(initial.yaw).Within(.001f), "Acquisition must discard pre-lock deltas.");
            Tick(true, new Vector2(100, 0), ui: true); Settle();
            Assert.That(Mathf.DeltaAngle(initial.yaw, follow.CaptureState().yaw), Is.EqualTo(6).Within(.01f));
            Assert.That(follow.CaptureState().pitch, Is.EqualTo(initial.pitch).Within(.001f));
            Tick(false);
            Assert.That(follow.OwnsOrbitGesture, Is.False);
            Assert.That(Cursor.lockState, Is.EqualTo(CursorLockMode.None));
        }

        [UnityTest]
        public IEnumerator RealMapWheelAndPanCannotAlsoMoveTheCamera()
        {
            float oldRange = VoyageSettings.MapRange;
            bool oldExpanded = VoyageSettings.MapExpanded;
            bool hadEvents = EventSystem.current != null;
            cameraObject.tag = "MainCamera";
            canvasObject = new GameObject("Interactive map input HUD");
            var hud = canvasObject.AddComponent<ShipTelemetryUI>(); hud.SetShip(ship);
            try
            {
                yield return null; yield return null;
                if (!hadEvents) eventsObject = EventSystem.current.gameObject;
                VoyageSettings.SetMapRange(300);
                cameraObject.SendMessage("OnApplicationFocus", true); Tick(false);
                Canvas.ForceUpdateCanvases();
                Vector2 pointer = RectTransformUtility.WorldToScreenPoint(null, hud.MapViewport.position);
                var state = new MouseState { position = pointer, scroll = Vector2.up };
                InputSystem.QueueStateEvent(mouse, state); InputSystem.Update();
                var before = follow.CaptureState();
                cameraObject.SendMessage("LateUpdate"); Settle();
                Assert.That(follow.CaptureState().distance, Is.EqualTo(before.distance).Within(.001f));
                var interaction = hud.MapViewport.GetComponent<MapInteraction>();
                float gain = EventSystem.current.currentInputModule is UnityEngine.InputSystem.UI.InputSystemUIInputModule module ? module.scrollDeltaPerTick : 1;
                var data = new PointerEventData(EventSystem.current) { position = pointer, scrollDelta = Vector2.up * gain,
                    button = PointerEventData.InputButton.Left, delta = new Vector2(30, 10) };
                interaction.OnScroll(data);
                Assert.That(VoyageSettings.MapRange, Is.EqualTo(300 * Mathf.Exp(-.2f)).Within(.01f));
                Tick(false); Tick(true); Tick(true, new Vector2(100, 0));
                Assert.That(follow.OwnsOrbitGesture, Is.True);
                float range = VoyageSettings.MapRange;
                interaction.OnScroll(data);
                Assert.That(VoyageSettings.MapRange, Is.EqualTo(range), "A world orbit retains wheel ownership across UI.");
                Tick(false);
                VoyageSettings.SetMapExpanded(true); yield return null; yield return null;
                interaction.OnBeginDrag(data); interaction.OnDrag(data);
                Assert.That(MapInteraction.Dragging, Is.True);
                Tick(true, new Vector2(100, 0));
                Assert.That(follow.OwnsOrbitGesture, Is.False, "Dragging the map outside its bounds must still block camera input.");
                interaction.SendMessage("OnApplicationFocus", false);
                Assert.That(MapInteraction.Dragging, Is.False);
                Tick(false);
            }
            finally
            {
                VoyageSettings.SetMapRange(oldRange); VoyageSettings.SetMapExpanded(oldExpanded);
            }
        }

        [TestCase("focus")] [TestCase("disable")] [TestCase("navigator")] [TestCase("target")]
        public void LifecycleReleasesCursorAndHeldButtonCannotReacquire(string change)
        {
            Tick(true); Tick(true, new Vector2(100, 50));
            Assert.That(follow.OwnsOrbitGesture, Is.True);
            if (change == "focus") cameraObject.SendMessage("OnApplicationFocus", false);
            if (change == "disable") follow.enabled = false;
            if (change == "navigator") follow.SetView(8);
            if (change == "target") follow.SetTarget(ship);
            Assert.That(follow.OwnsOrbitGesture, Is.False);
            Assert.That(Cursor.lockState, Is.EqualTo(CursorLockMode.None));
            Assert.That(Cursor.visible, Is.True);
            cameraObject.SendMessage("OnApplicationFocus", true);
            follow.enabled = true;
            Tick(true, new Vector2(1000, 1000));
            Tick(true, new Vector2(1000, 1000));
            Assert.That(follow.OwnsOrbitGesture, Is.False);
        }

        [TestCase(1)] [TestCase(2)] [TestCase(4)]
        public void IndependentGainsAreUnaffectedBySimulationSpeed(float speed)
        {
            Time.timeScale = speed;
            VoyageSettings.SetSensitivity(.5f); VoyageSettings.SetZoomSensitivity(2);
            var initial = follow.CaptureState();
            Tick(true); Tick(true, new Vector2(100, 0), 1); Settle();
            var result = follow.CaptureState();
            Assert.That(Mathf.DeltaAngle(initial.yaw, result.yaw), Is.EqualTo(3).Within(.01f));
            Assert.That(result.pitch, Is.EqualTo(initial.pitch).Within(.001f));
            Assert.That(result.distance / initial.distance, Is.EqualTo(.64f).Within(.001f));
        }

        [Test]
        public void TiltDoesNotLiftExternalCameraAndRestoreClearsTrackingHistory()
        {
            Vector3 before = cameraObject.transform.position;
            vessel.transform.rotation = Quaternion.Euler(12, 0, 15);
            Settle();
            Assert.That(Vector3.Distance(before, cameraObject.transform.position), Is.LessThan(.001f));
            Tick(true); Tick(true, new Vector2(100, 0), 1);
            var state = follow.CaptureState();
            ship.RestoreVoyage(new VoyageSave { position = new Vector3(200, 0, 300), rotation = Quaternion.identity });
            Tick(true, new Vector2(1000, 1000));
            Vector3 restored = cameraObject.transform.position;
            Assert.That(follow.OwnsOrbitGesture, Is.False);
            Assert.That(follow.CaptureState().yaw, Is.EqualTo(state.yaw).Within(.001f));
            Assert.That(Vector3.Distance(restored - ship.transform.position,
                new Vector3(0, 3.5f, 10) + CameraOrbit.ToOffset(state.yaw, state.pitch, state.distance)), Is.LessThan(.01f));
            Settle();
            Assert.That(Vector3.Distance(restored, cameraObject.transform.position), Is.LessThan(.001f));
        }

        [Test]
        public void OldOriginBasedSaveMigratesAndNewSaveCapturesVisiblePoseWithoutResidualMotion()
        {
            var legacy = JsonUtility.FromJson<CameraSave>("{\"view\":0,\"yaw\":20,\"pitch\":30,\"distance\":80}");
            follow.RestoreState(legacy);
            Assert.That(Vector3.Distance(cameraObject.transform.position, CameraOrbit.ToOffset(20, 30, 80)), Is.LessThan(.001f));
            Tick(false); Tick(true); Tick(true, new Vector2(100, 30), 1);
            Vector3 visible = cameraObject.transform.position;
            var save = JsonUtility.FromJson<CameraSave>(JsonUtility.ToJson(follow.CaptureState()));
            follow.SetView(6); follow.RestoreState(save); Settle();
            Assert.That(Vector3.Distance(visible, cameraObject.transform.position), Is.LessThan(.001f));
        }
    }
}

using System.IO;
using NUnit.Framework;
using ShipSimulator.Persistence;
using ShipSimulator.Physics;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ShipSimulator.Tests
{
    public sealed class BuoyPhysicsTests
    {
        [Test]
        public void Save_RejectsDuplicateIdsAndNonFiniteOrInvalidMotion()
        {
            var valid = new BuoySave { id = "Navigation/Right Red Buoy 01" };
            Assert.DoesNotThrow(() => BuoySave.Validate(null));
            Assert.DoesNotThrow(() => BuoySave.Validate(new[] { valid }));
            Assert.Throws<InvalidDataException>(() => BuoySave.Validate(new[] { valid, valid }));
            valid.velocity.x = float.NaN;
            Assert.Throws<InvalidDataException>(() => BuoySave.Validate(new[] { valid }));
            valid.velocity = Vector3.zero; valid.rotation = new Quaternion();
            Assert.Throws<InvalidDataException>(() => BuoySave.Validate(new[] { valid }));
        }

        [TestCase("RiverTrainingScene")]
        [TestCase("GorodetsTrainingScene")]
        [TestCase("SerpukhovZatonScene")]
        public void SceneMarks_HaveUniqueIdsDynamicSolidContactsAndEnabledLayerPairs(string name)
        {
            var scene = EditorSceneManager.OpenScene("Assets/ShipSimulator/Scenes/" + name + ".unity", OpenSceneMode.Additive);
            try
            {
                var ids = new System.Collections.Generic.HashSet<string>();
                int count = 0;
                var audit = new System.Text.StringBuilder("object,type,enabled,trigger,layer,body,mass,ccd,owner\n");
                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    foreach (Collider shape in root.GetComponentsInChildren<Collider>(true))
                    {
                        var body = shape.attachedRigidbody;
                        string owner = shape.GetComponentInParent<MooredBuoy>() != null ? "MooredBuoy/PhysX" :
                            shape.GetComponentInParent<BathymetryContactSurface>() != null ? "GroundingController for ships; PhysX for other bodies" : "PhysX";
                        audit.AppendLine($"{root.name}/{shape.name},{shape.GetType().Name},{shape.enabled},{shape.isTrigger}," +
                            $"{shape.gameObject.layer},{body?.name},{body?.mass},{body?.collisionDetectionMode},{owner}");
                    }
                    if (root.name != "Navigation") continue;
                    foreach (Transform mark in root.transform)
                    {
                        if (!mark.name.Contains("Buoy")) continue;
                        var buoy = mark.GetComponent<MooredBuoy>();
                        Assert.That(buoy, Is.Not.Null, mark.name);
                        Assert.That(ids.Add(buoy.StableId), Is.True);
                        var body = mark.GetComponent<Rigidbody>();
                        Assert.That(body.isKinematic, Is.False);
                        Assert.That(body.mass, Is.EqualTo(BuoyParameters.Load().massKg));
                        Assert.That(body.collisionDetectionMode, Is.EqualTo(CollisionDetectionMode.ContinuousDynamic));
                        foreach (Collider collider in mark.GetComponentsInChildren<Collider>())
                        {
                            if (!collider.enabled) continue;
                            Assert.That(collider.isTrigger, Is.False);
                            Assert.That(collider.attachedRigidbody, Is.EqualTo(body));
                            Assert.That(UnityEngine.Physics.GetIgnoreLayerCollision(collider.gameObject.layer, 0), Is.False);
                        }
                        count++;
                    }
                }
                Assert.That(count, Is.GreaterThan(10));
                Directory.CreateDirectory("Logs/BuoyPart5");
                File.WriteAllText("Logs/BuoyPart5/" + name + "-colliders.csv", audit.ToString());
            }
            finally { EditorSceneManager.CloseScene(scene, true); }
        }
    }
}

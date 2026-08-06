#if UNITY_EDITOR
using NUnit.Framework;
using SlimyJam.InputSystem;
using SlimyJam.Level;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SlimyJam.Tests
{
    /// <summary>
    /// Editor script'iyle üretilen sahnenin bütünlüğü: bileşenler var mı, serialized referanslar bağlı mı?
    /// Sahne <c>Tools ▸ Slimy Jam ▸ Create All Scenes</c> ile yeniden üretilebilir.
    /// </summary>
    public sealed class SceneIntegrityTests
    {
        private const string PlayableScenePath = "Assets/SlimyJam/Scenes/SlimyJam.unity";
        private const string AuthoringScenePath = "Assets/SlimyJam/Scenes/SlimyJam_Authoring.unity";

        [Test]
        public void PlayableSceneHasWiredGameplayObjects()
        {
            var scene = EditorSceneManager.OpenScene(PlayableScenePath, OpenSceneMode.Single);
            Assert.IsTrue(scene.IsValid(), $"Could not open {PlayableScenePath}");

            var gameManager = Object.FindFirstObjectByType<SlimyGameManager>();
            var generator = Object.FindFirstObjectByType<SlimyLevelGenerator>();
            var input = Object.FindFirstObjectByType<SlimyInputController>();
            var cameraController = Object.FindFirstObjectByType<SlimyCameraController>();

            Assert.IsNotNull(gameManager, "SlimyGameManager missing from the playable scene.");
            Assert.IsNotNull(generator, "SlimyLevelGenerator missing from the playable scene.");
            Assert.IsNotNull(input, "SlimyInputController missing from the playable scene.");
            Assert.IsNotNull(cameraController, "SlimyCameraController missing from the playable scene.");
            Assert.IsNotNull(Camera.main, "Scene has no MainCamera.");
            Assert.IsNotNull(Object.FindFirstObjectByType<Light>(), "Scene has no light.");

            AssertSerializedReferenceAssigned(generator, "config");
            AssertSerializedReferenceAssigned(generator, "gameManager");
            AssertSerializedReferenceAssigned(generator, "cameraController");
            AssertSerializedReferenceAssigned(input, "gameManager");
            AssertSerializedReferenceAssigned(input, "cameraController");
            AssertSerializedReferenceAssigned(cameraController, "targetCamera");
        }

        [Test]
        public void PlayableSceneTargetsAnExistingLevel()
        {
            EditorSceneManager.OpenScene(PlayableScenePath, OpenSceneMode.Single);

            var generator = Object.FindFirstObjectByType<SlimyLevelGenerator>();
            var serialized = new UnityEditor.SerializedObject(generator);
            var path = serialized.FindProperty("levelResourcePath").stringValue;

            Assert.IsNotEmpty(path);
            Assert.IsNotNull(Resources.Load<TextAsset>(path), $"Level resource '{path}' does not exist.");
        }

        [Test]
        public void AuthoringSceneContainsSplinesRopesAndHoles()
        {
            var scene = EditorSceneManager.OpenScene(AuthoringScenePath, OpenSceneMode.Single);
            Assert.IsTrue(scene.IsValid(), $"Could not open {AuthoringScenePath}");

            Assert.IsNotNull(Object.FindFirstObjectByType<Authoring.SlimyLevelAuthoring>());
            Assert.IsNotNull(Object.FindFirstObjectByType<Authoring.RopeAuthoring>());
            Assert.IsNotNull(Object.FindFirstObjectByType<Authoring.HoleAuthoring>());
            Assert.Greater(Object.FindObjectsByType<Dreamteck.Splines.SplineComputer>(FindObjectsSortMode.None).Length,
                0, "Authoring scene has no spline to bake.");
        }

        private static void AssertSerializedReferenceAssigned(Object target, string fieldName)
        {
            var serialized = new UnityEditor.SerializedObject(target);
            var property = serialized.FindProperty(fieldName);

            Assert.IsNotNull(property, $"{target.GetType().Name}.{fieldName} not found.");
            Assert.IsNotNull(property.objectReferenceValue,
                $"{target.GetType().Name}.{fieldName} is not assigned in the scene.");
        }
    }
}
#endif

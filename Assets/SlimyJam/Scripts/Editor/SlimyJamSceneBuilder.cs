using System.IO;
using Dreamteck.Splines;
using SlimyJam.Authoring;
using SlimyJam.Core;
using SlimyJam.InputSystem;
using SlimyJam.Level;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SlimyJam.EditorTools
{
    /// <summary>
    /// Oynanabilir sahneyi ve authoring sahnesini tek tıkla kurar. Prefab bağımlılığı yoktur;
    /// tüm görseller runtime'da üretilir.
    /// </summary>
    public static class SlimyJamSceneBuilder
    {
        private const string SceneFolder = "Assets/SlimyJam/Scenes";
        private const string ConfigPath = "Assets/SlimyJam/SlimyConfig.asset";

        /// <summary>
        /// Her iki sahneyi de üretir. Batch modda da çalıştırılabilir:
        /// <c>Unity -batchmode -quit -projectPath . -executeMethod SlimyJam.EditorTools.SlimyJamSceneBuilder.CreateAllScenes</c>
        /// </summary>
        [MenuItem("Tools/Slimy Jam/Create All Scenes", false, 2)]
        public static void CreateAllScenes()
        {
            CreatePlayableScene();
            CreateAuthoringScene();
        }

        [MenuItem("Tools/Slimy Jam/Create Playable Scene", false, 0)]
        public static void CreatePlayableScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            CreateCamera();
            CreateLight();

            var config = GetOrCreateConfig();

            var root = new GameObject("SlimyJam");
            var gameManager = root.AddComponent<SlimyGameManager>();
            var generator = root.AddComponent<SlimyLevelGenerator>();
            var input = root.AddComponent<SlimyInputController>();

            var cameraController = Camera.main.gameObject.AddComponent<SlimyCameraController>();

            AssignPrivate(generator, "config", config);
            AssignPrivate(generator, "gameManager", gameManager);
            AssignPrivate(generator, "cameraController", cameraController);
            AssignPrivate(input, "gameManager", gameManager);
            AssignPrivate(input, "cameraController", cameraController);
            AssignPrivate(cameraController, "targetCamera", Camera.main);

            Directory.CreateDirectory(SceneFolder);
            var path = Path.Combine(SceneFolder, "SlimyJam.unity");
            EditorSceneManager.SaveScene(scene, path);

            Debug.Log($"[SlimyJam] Playable scene created at {path}. Press Play to run Level_1.");
        }

        [MenuItem("Tools/Slimy Jam/Create Authoring Scene", false, 1)]
        public static void CreateAuthoringScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            CreateCamera();
            CreateLight();

            var root = new GameObject("LevelAuthoring");
            var authoring = root.AddComponent<SlimyLevelAuthoring>();
            authoring.levelIndex = 99;

            CreateExampleSpline(root.transform, "Path_Horizontal",
                new Vector3(0f, 0f, 0f), new Vector3(8f, 0f, 0f));
            CreateExampleSpline(root.transform, "Path_Vertical",
                new Vector3(4f, 0f, -3f), new Vector3(4f, 0f, 3f));

            var ropeObject = new GameObject("Rope_Green");
            ropeObject.transform.SetParent(root.transform, false);
            var rope = ropeObject.AddComponent<RopeAuthoring>();
            rope.ropeId = 1;
            for (int i = 0; i < 3; i++)
            {
                var marker = new GameObject($"Unit_{i}");
                marker.transform.SetParent(ropeObject.transform, false);
                marker.transform.position = new Vector3(1f + i, 0f, 0f);
            }

            var holeObject = new GameObject("Hole_Green");
            holeObject.transform.SetParent(root.transform, false);
            holeObject.transform.position = new Vector3(8f, 0f, 0f);
            holeObject.AddComponent<HoleAuthoring>().holeId = 10;

            Directory.CreateDirectory(SceneFolder);
            var path = Path.Combine(SceneFolder, "SlimyJam_Authoring.unity");
            EditorSceneManager.SaveScene(scene, path);

            Debug.Log($"[SlimyJam] Authoring scene created at {path}. " +
                      "Şekli spline'larla düzenleyip LevelAuthoring üzerinden 'Bake to JSON' çalıştırın.");
        }

        private static void CreateExampleSpline(Transform parent, string name, Vector3 start, Vector3 end)
        {
            var splineObject = new GameObject(name);
            splineObject.transform.SetParent(parent, false);

            var computer = splineObject.AddComponent<SplineComputer>();
            computer.type = Spline.Type.Linear;
            computer.SetPoints(new[] { new SplinePoint(start), new SplinePoint(end) }, SplineComputer.Space.World);
        }

        private static void CreateCamera()
        {
            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            var camera = cameraObject.AddComponent<Camera>();
            camera.fieldOfView = 45f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.10f, 0.12f, 0.16f);
            cameraObject.transform.rotation = Quaternion.Euler(55f, 0f, 0f);
            cameraObject.transform.position = new Vector3(0f, 12f, -8f);
        }

        private static void CreateLight()
        {
            var lightObject = new GameObject("Directional Light");
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.1f;
            lightObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        }

        private static SlimyConfig GetOrCreateConfig()
        {
            var config = AssetDatabase.LoadAssetAtPath<SlimyConfig>(ConfigPath);
            if (config != null) return config;

            config = ScriptableObject.CreateInstance<SlimyConfig>();
            Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath));
            AssetDatabase.CreateAsset(config, ConfigPath);
            AssetDatabase.SaveAssets();
            return config;
        }

        private static void AssignPrivate(Object target, string fieldName, Object value)
        {
            var serialized = new SerializedObject(target);
            var property = serialized.FindProperty(fieldName);
            if (property == null)
            {
                Debug.LogWarning($"[SlimyJam] Field '{fieldName}' not found on {target.GetType().Name}.");
                return;
            }

            property.objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}

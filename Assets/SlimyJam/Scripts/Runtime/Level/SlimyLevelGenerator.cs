using System.Collections.Generic;
using SlimyJam.Core;
using SlimyJam.Data;
using SlimyJam.Gameplay;
using SlimyJam.Visual;
using UnityEngine;

namespace SlimyJam.Level
{
    /// <summary>
    /// Seçili level JSON'unu okuyup sahnenin logical ve visual runtime nesnelerini oluşturur (GDD 13.1).
    /// Procedural level üretmez; authored datayı instantiate eder.
    ///
    /// Yükleme sırası (GDD 13.2):
    /// Load JSON -> Build Node Lookup -> Connect Graph -> Register Holes -> Register Ropes
    /// -> Build Occupancy Map -> Build Visual Splines -> Frame Camera -> Start Playing
    /// </summary>
    public sealed class SlimyLevelGenerator : MonoBehaviour
    {
        [Header("Level")]
        [Tooltip("Resources/SlimyLevels altındaki dosya adı (uzantısız).")]
        [SerializeField] private string levelResourcePath = "SlimyLevels/Level_1";

        [Tooltip("Atanırsa Resources yerine bu TextAsset kullanılır.")]
        [SerializeField] private TextAsset levelAsset;

        [Header("References")]
        [SerializeField] private SlimyConfig config;
        [SerializeField] private SlimyGameManager gameManager;
        [SerializeField] private SlimyCameraController cameraController;

        [Header("Visual")]
        [SerializeField] private Material groundMaterial;

        private readonly List<Rope> _ropes = new List<Rope>();
        private readonly List<Hole> _holes = new List<Hole>();

        public SlimyLevelContext Context { get; private set; }

        private void Start()
        {
            Application.targetFrameRate = 60;
            Generate();
        }

        public void Generate()
        {
            EnsureReferences();

            var data = LoadLevelData();
            if (data == null) return;

            if (!LevelDataValidator.Validate(data, out var error))
            {
                // GDD 13.3: incomplete gameplay başlatılmaz.
                gameManager.FailLoad(error);
                return;
            }

            Context = SlimyLevelContext.Build(data, config);

            var levelRoot = new GameObject($"Level_{data.levelIndex}").transform;
            levelRoot.SetParent(transform, false);

            BuildGround(levelRoot);
            BuildHoles(levelRoot);
            BuildRopes(levelRoot);
            FrameCamera();

            gameManager.Bind(Context, _ropes, _holes);
        }

        private void EnsureReferences()
        {
            if (config == null) config = SlimyConfig.CreateDefault();
            if (gameManager == null) gameManager = FindFirstObjectByType<SlimyGameManager>();
            if (gameManager == null) gameManager = gameObject.AddComponent<SlimyGameManager>();
            if (cameraController == null) cameraController = FindFirstObjectByType<SlimyCameraController>();
            if (cameraController == null && Camera.main != null)
            {
                cameraController = Camera.main.gameObject.AddComponent<SlimyCameraController>();
            }

            if (groundMaterial == null) groundMaterial = SlimyPalette.GetMaterial(new Color(0.86f, 0.88f, 0.92f));
        }

        private SlimyLevelData LoadLevelData()
        {
            var json = levelAsset != null ? levelAsset.text : Resources.Load<TextAsset>(levelResourcePath)?.text;

            if (string.IsNullOrEmpty(json))
            {
                gameManager.FailLoad($"Level json not found at Resources/{levelResourcePath}");
                return null;
            }

            try
            {
                return SlimyLevelJson.Parse(json);
            }
            catch (System.Exception exception)
            {
                gameManager.FailLoad($"Level json could not be parsed: {exception.Message}");
                return null;
            }
        }

        private void BuildGround(Transform parent)
        {
            GroundVisualBuilder.Build(Context.Graph, config.groundWidth, parent, groundMaterial);
        }

        private void BuildHoles(Transform parent)
        {
            _holes.Clear();
            var holeRoot = new GameObject("Holes").transform;
            holeRoot.SetParent(parent, false);

            for (int i = 0; i < Context.Holes.Count; i++)
            {
                var model = Context.Holes[i];
                var holeObject = new GameObject($"Hole_{model.HoleId}_{model.Color}");
                holeObject.transform.SetParent(holeRoot, false);

                var hole = holeObject.AddComponent<Hole>();
                hole.Initialize(model, Context.Graph.GetPosition(model.NodeId), config.groundWidth * 0.62f);
                _holes.Add(hole);
            }
        }

        private void BuildRopes(Transform parent)
        {
            _ropes.Clear();
            var ropeRoot = new GameObject("Ropes").transform;
            ropeRoot.SetParent(parent, false);

            for (int i = 0; i < Context.ActiveRopes.Count; i++)
            {
                var model = Context.ActiveRopes[i];
                var ropeObject = new GameObject($"Rope_{model.RopeId}_{model.Color}");
                ropeObject.transform.SetParent(ropeRoot, false);

                var adapter = DreamteckSplineAdapter.Create("Spline", ropeObject.transform);
                var rope = ropeObject.AddComponent<Rope>();
                rope.Initialize(model, Context, adapter);
                _ropes.Add(rope);
            }
        }

        private void FrameCamera()
        {
            if (cameraController == null) return;

            cameraController.SetPlanes(Context.Graph.Bounds.center.y, config.ropeHeight);
            cameraController.Frame(Context.Graph.Bounds);
        }
    }
}

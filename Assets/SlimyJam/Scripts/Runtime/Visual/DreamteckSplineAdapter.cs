using System.Collections.Generic;
using Dreamteck.Splines;
using UnityEngine;

namespace SlimyJam.Visual
{
    /// <summary>
    /// Dreamteck Splines implementasyonu: logical unit pozisyonlarından bir SplineComputer besler,
    /// gövdeyi TubeGenerator ile render eder. Gameplay bu sınıfa hiçbir karar sormaz.
    /// </summary>
    [RequireComponent(typeof(SplineComputer))]
    public sealed class DreamteckSplineAdapter : MonoBehaviour, ISplineAdapter
    {
        private SplineComputer _computer;
        private TubeGenerator _tube;
        private MeshRenderer _renderer;
        private SplinePoint[] _points = new SplinePoint[0];

        public Transform Transform => transform;

        public static DreamteckSplineAdapter Create(string objectName, Transform parent)
        {
            var root = new GameObject(objectName);
            root.transform.SetParent(parent, false);

            var computer = root.AddComponent<SplineComputer>();
            computer.type = Spline.Type.CatmullRom;
            computer.sampleRate = 12;

            var adapter = root.AddComponent<DreamteckSplineAdapter>();
            adapter.Bind(computer);
            return adapter;
        }

        private void Bind(SplineComputer computer)
        {
            _computer = computer;

            var meshObject = new GameObject("Mesh");
            meshObject.transform.SetParent(transform, false);

            _tube = meshObject.AddComponent<TubeGenerator>();
            _tube.spline = _computer;
            _tube.sides = 10;
            _tube.capMode = TubeGenerator.CapMethod.Round;
            _tube.updateMethod = SplineUser.UpdateMethod.LateUpdate;
            _renderer = meshObject.GetComponent<MeshRenderer>();
        }

        public void Configure(Color color, float radius, int sampleRate, Material material)
        {
            if (_computer == null) _computer = GetComponent<SplineComputer>();
            _computer.sampleRate = Mathf.Max(2, sampleRate);

            if (_tube != null)
            {
                _tube.size = radius * 2f;
                _tube.color = color;
            }

            if (_renderer == null || material == null) return;

            _renderer.sharedMaterial = material;
        }

        public void UpdatePoints(IReadOnlyList<Vector3> points)
        {
            if (_computer == null || points == null || points.Count < 2) return;

            if (_points.Length != points.Count) _points = new SplinePoint[points.Count];

            for (int i = 0; i < points.Count; i++)
            {
                _points[i].position = points[i];
                _points[i].normal = Vector3.up;
                _points[i].size = 1f;
                _points[i].color = Color.white;
                _points[i].type = SplinePoint.Type.SmoothMirrored;
            }

            _computer.SetPoints(_points, SplineComputer.Space.World);
        }

        public void SetVisible(bool visible)
        {
            if (_renderer != null) _renderer.enabled = visible;
        }
    }
}

using System.Collections.Generic;
using SlimyJam.Core;
using SlimyJam.Level;
using SlimyJam.Visual;
using UnityEngine;

namespace SlimyJam.Gameplay
{
    public enum RopeState
    {
        /// <summary>Board üzerinde available, seçili değil.</summary>
        Idle,

        /// <summary>Bir endpoint aktif; pointer takip ediliyor.</summary>
        Dragging,

        /// <summary>Release sonrası en yakın node merkezine görsel tamamlama.</summary>
        Snapping,

        /// <summary>Gameplay occupancy'den kaldırıldı; hole çekim animasyonu oynuyor.</summary>
        Collecting,

        /// <summary>Runtime aktif rope setinden çıkarıldı.</summary>
        Collected
    }

    /// <summary>
    /// Rope'un sahne temsili: logical model + movement controller + görsel spline (GDD 11.3, 14.1).
    /// Logical kararların hiçbiri burada verilmez.
    /// </summary>
    public sealed class Rope : MonoBehaviour
    {
        private const float ContainedVisualRadiusFactor = 0.55f;
        private const float ContainedVisualLiftFactor = 1.75f;

        private readonly List<Vector3> _renderPoints = new List<Vector3>();

        private SlimyLevelContext _context;
        private ISplineAdapter _splineAdapter;
        private float _ropeHeight;
        private float _baseRadius;
        private Transform _keyMarker;
        private Rope _containerRope;
        private readonly List<int> _containerUnitIndices = new List<int>();

        public RopeModel Model { get; private set; }
        public RopeMovementController Movement { get; private set; }
        public RopeState State { get; private set; } = RopeState.Idle;

        public bool IsSelectable => !Model.IsContained && (State == RopeState.Idle || State == RopeState.Snapping);

        public void Initialize(RopeModel model, SlimyLevelContext context, ISplineAdapter splineAdapter)
        {
            Model = model;
            _context = context;
            _splineAdapter = splineAdapter;
            _ropeHeight = context.Config.ropeHeight;
            _baseRadius = context.Config.ropeRadius;

            Movement = context.CreateMovementController(model);
            Movement.ReleaseCompleted += OnReleaseCompleted;

            RefreshAppearance();
            EnsureKeyMarker();

            RefreshVisual();
        }

        private void OnDestroy()
        {
            if (Movement != null) Movement.ReleaseCompleted -= OnReleaseCompleted;
        }

        public Vector3 GetEndpointPosition(RopeEnd end)
        {
            return Movement.GetEndpointPosition(end) + Vector3.up * _ropeHeight;
        }

        public void BeginDrag(RopeEnd end)
        {
            State = RopeState.Dragging;
            Movement.BeginDrag(end);
        }

        public void Drag(Vector3 pointerWorldPosition, float deltaTime)
        {
            if (State != RopeState.Dragging) return;
            Movement.Tick(pointerWorldPosition, deltaTime);
            RefreshVisual();
        }

        public void Release()
        {
            if (State != RopeState.Dragging) return;
            State = RopeState.Snapping;
            Movement.BeginRelease();
        }

        public void BeginCollecting()
        {
            State = RopeState.Collecting;
        }

        public void MarkCollected()
        {
            State = RopeState.Collected;
            if (_keyMarker != null) _keyMarker.gameObject.SetActive(false);
        }

        private void Update()
        {
            if (!Movement.IsSnapping) return;

            Movement.Tick(transform.position, Time.deltaTime);
            RefreshVisual();
        }

        private void OnReleaseCompleted()
        {
            if (State == RopeState.Snapping) State = RopeState.Idle;
            RefreshVisual();
            ReleaseCompleted?.Invoke(this);
        }

        public event System.Action<Rope> ReleaseCompleted;
        public event System.Action<Rope> VisualRefreshed;

        public void SetContainedVisualSource(Rope container, IReadOnlyList<int> containerUnitIndices)
        {
            _containerRope = container;
            _containerUnitIndices.Clear();
            if (containerUnitIndices != null)
            {
                for (int i = 0; i < containerUnitIndices.Count; i++) _containerUnitIndices.Add(containerUnitIndices[i]);
            }

            RefreshAppearance();
            RefreshVisual();
        }

        public void ClearContainedVisualSource()
        {
            _containerRope = null;
            _containerUnitIndices.Clear();
            RefreshAppearance();
            RefreshVisual();
        }

        public void RefreshAppearance()
        {
            if (Model == null || _splineAdapter == null || _context == null) return;

            var color = Model.IsColorRevealed ? SlimyPalette.Get(Model.Color) : SlimyPalette.Hidden;
            var radius = Model.IsContained ? _baseRadius * ContainedVisualRadiusFactor : _baseRadius;
            _splineAdapter.Configure(color, radius, _context.Config.splineSampleRate, SlimyPalette.GetMaterial(color));
        }

        public Vector3 GetUnitWorldPosition(int index, float extraLift = 0f)
        {
            return Movement.GetUnitPosition(index) + Vector3.up * (_ropeHeight + extraLift);
        }

        public void RefreshVisual()
        {
            if (Model == null) return;

            _renderPoints.Clear();
            var useContainer = Model.IsContained &&
                               _containerRope != null &&
                               _containerUnitIndices.Count == Model.Length;

            for (int i = 0; i < Model.Length; i++)
            {
                _renderPoints.Add(useContainer
                    ? _containerRope.GetUnitWorldPosition(_containerUnitIndices[i], GetContainedVisualLift())
                    : GetUnitWorldPosition(i));
            }

            // Tek unit'lik rope da görünür olmalı; spline en az iki nokta ister.
            if (_renderPoints.Count == 1)
            {
                var single = _renderPoints[0];
                _renderPoints[0] = single + Vector3.left * 0.01f;
                _renderPoints.Add(single + Vector3.right * 0.01f);
            }

            _splineAdapter.UpdatePoints(_renderPoints);
            UpdateKeyMarker();
            VisualRefreshed?.Invoke(this);
        }

        public IReadOnlyList<Vector3> RenderPoints => _renderPoints;

        public ISplineAdapter SplineAdapter => _splineAdapter;

        private float GetContainedVisualLift()
        {
            return Mathf.Max(_baseRadius * ContainedVisualLiftFactor, 0.2f);
        }

        private void EnsureKeyMarker()
        {
            if (!Model.HasKey) return;

            var marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            marker.name = "KeyMarker";
            var collider = marker.GetComponent<Collider>();
            if (collider != null) Destroy(collider);

            _keyMarker = marker.transform;
            _keyMarker.SetParent(transform, false);
            _keyMarker.localScale = Vector3.one * Mathf.Max(0.08f, _baseRadius * 1.7f);

            var renderer = marker.GetComponent<MeshRenderer>();
            if (renderer != null) renderer.sharedMaterial = SlimyPalette.GetMaterial(SlimyPalette.Key);
        }

        private void UpdateKeyMarker()
        {
            if (_keyMarker == null) return;

            var visible = Model.HasKey && State != RopeState.Collected;
            _keyMarker.gameObject.SetActive(visible);
            if (!visible || _renderPoints.Count == 0) return;

            _keyMarker.position = _renderPoints[_renderPoints.Count / 2] + Vector3.up * 0.12f;
        }
    }
}

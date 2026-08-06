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
        private readonly List<Vector3> _renderPoints = new List<Vector3>();

        private SlimyLevelContext _context;
        private ISplineAdapter _splineAdapter;
        private float _ropeHeight;

        public RopeModel Model { get; private set; }
        public RopeMovementController Movement { get; private set; }
        public RopeState State { get; private set; } = RopeState.Idle;

        public bool IsSelectable => State == RopeState.Idle || State == RopeState.Snapping;

        public void Initialize(RopeModel model, SlimyLevelContext context, ISplineAdapter splineAdapter)
        {
            Model = model;
            _context = context;
            _splineAdapter = splineAdapter;
            _ropeHeight = context.Config.ropeHeight;

            Movement = context.CreateMovementController(model);
            Movement.ReleaseCompleted += OnReleaseCompleted;

            _splineAdapter.Configure(SlimyPalette.Get(model.Color), context.Config.ropeRadius,
                context.Config.splineSampleRate, SlimyPalette.GetMaterial(model.Color));

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

        public void RefreshVisual()
        {
            if (Model == null) return;

            _renderPoints.Clear();
            for (int i = 0; i < Model.Length; i++)
            {
                _renderPoints.Add(Movement.GetUnitPosition(i) + Vector3.up * _ropeHeight);
            }

            // Tek unit'lik rope da görünür olmalı; spline en az iki nokta ister.
            if (_renderPoints.Count == 1)
            {
                var single = _renderPoints[0];
                _renderPoints[0] = single + Vector3.left * 0.01f;
                _renderPoints.Add(single + Vector3.right * 0.01f);
            }

            _splineAdapter.UpdatePoints(_renderPoints);
        }

        public IReadOnlyList<Vector3> RenderPoints => _renderPoints;

        public ISplineAdapter SplineAdapter => _splineAdapter;
    }
}

using System;
using System.Collections;
using System.Collections.Generic;
using SlimyJam.Data;
using SlimyJam.Gameplay;
using SlimyJam.Visual;
using UnityEngine;

namespace SlimyJam.Level
{
    public enum SlimyGameState
    {
        /// <summary>JSON yükleme ve LevelGenerator kurulumu. Input kapalı.</summary>
        Loading,

        /// <summary>Selection, drag, pathfinding, movement ve collection. Input açık.</summary>
        Playing,

        /// <summary>Son rope collection sonrası gameplay durduruldu. Input kapalı.</summary>
        Completing,

        /// <summary>Level complete sonucu dış sisteme bildirildi. Input kapalı.</summary>
        Completed
    }

    /// <summary>
    /// Game state, aktif rope sayısı, collection bildirimi ve win transition (GDD 11, 14.1).
    /// </summary>
    public sealed class SlimyGameManager : MonoBehaviour
    {
        private readonly List<Vector3> _pullBuffer = new List<Vector3>();
        private readonly List<Vector3> _pullSource = new List<Vector3>();
        private readonly List<RopeModel> _containedRopeBuffer = new List<RopeModel>();

        private SlimyLevelContext _context;
        private readonly List<Rope> _ropes = new List<Rope>();
        private readonly List<Hole> _holes = new List<Hole>();
        private readonly List<Wall> _walls = new List<Wall>();

        public static SlimyGameManager Instance { get; private set; }

        public SlimyGameState State { get; private set; } = SlimyGameState.Loading;
        public SlimyLevelContext Context => _context;
        public IReadOnlyList<Rope> Ropes => _ropes;

        public event Action<SlimyGameState> StateChanged;
        public event Action LevelCompleted;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            Unsubscribe();
        }

        public void Bind(SlimyLevelContext context, List<Rope> ropes, List<Hole> holes, List<Wall> walls = null)
        {
            Unsubscribe();

            _context = context;
            _ropes.Clear();
            _ropes.AddRange(ropes);
            _holes.Clear();
            _holes.AddRange(holes);
            _walls.Clear();
            if (walls != null) _walls.AddRange(walls);

            _context.Collection.CollectionStarted += OnCollectionStarted;
            _context.Collection.HolesRemoved += OnHolesRemoved;
            _context.Collection.LevelCompleted += OnLevelCompleted;
            _context.Elements.RopeStateChanged += OnRopeStateChanged;
            _context.Elements.HoleStateChanged += OnHoleStateChanged;
            _context.Elements.WallRemoved += OnWallRemoved;
            _context.Elements.ContainedRopesReleased += OnContainedRopesReleased;

            for (int i = 0; i < _ropes.Count; i++)
            {
                _ropes[i].Movement.CollectionTriggered += OnMovementCollectionTriggered;
                _ropes[i].Movement.StepCommitted += OnRopeStepCommitted;
                _ropes[i].ReleaseCompleted += OnRopeReleaseCompleted;
                _ropes[i].VisualRefreshed += OnRopeVisualRefreshed;
            }

            ConfigureContainedRopeVisuals();

            SetState(SlimyGameState.Playing);
        }

        private void Unsubscribe()
        {
            if (_context != null)
            {
                _context.Collection.CollectionStarted -= OnCollectionStarted;
                _context.Collection.HolesRemoved -= OnHolesRemoved;
                _context.Collection.LevelCompleted -= OnLevelCompleted;
                if (_context.Elements != null)
                {
                    _context.Elements.RopeStateChanged -= OnRopeStateChanged;
                    _context.Elements.HoleStateChanged -= OnHoleStateChanged;
                    _context.Elements.WallRemoved -= OnWallRemoved;
                    _context.Elements.ContainedRopesReleased -= OnContainedRopesReleased;
                }
            }

            for (int i = 0; i < _ropes.Count; i++)
            {
                if (_ropes[i] == null) continue;
                _ropes[i].Movement.CollectionTriggered -= OnMovementCollectionTriggered;
                _ropes[i].Movement.StepCommitted -= OnRopeStepCommitted;
                _ropes[i].ReleaseCompleted -= OnRopeReleaseCompleted;
                _ropes[i].VisualRefreshed -= OnRopeVisualRefreshed;
            }
        }

        public Rope FindRope(RopeModel model)
        {
            for (int i = 0; i < _ropes.Count; i++)
            {
                if (_ropes[i] != null && _ropes[i].Model == model) return _ropes[i];
            }

            return null;
        }

        /// <summary>Hole node'una girildiğinde tetiklenir (GDD 10.2 / 8.7).</summary>
        private void OnMovementCollectionTriggered(RopeModel model, int holeId, RopeEnd end)
        {
            var hole = _context.Collection.GetHole(holeId);
            if (hole == null) return;

            _context.Collection.BeginCollection(model, hole);
        }

        /// <summary>Release sonrası yakın çekim kontrolü (GDD 10.3).</summary>
        private void OnRopeReleaseCompleted(Rope rope)
        {
            if (State != SlimyGameState.Playing) return;
            if (rope.State == RopeState.Collecting || rope.State == RopeState.Collected) return;

            var hole = _context.Collection.FindReleaseCollectionTarget(rope.Model, rope.Movement.ActiveEnd);
            if (hole == null) return;

            _context.Collection.BeginCollection(rope.Model, hole);
        }

        private void OnCollectionStarted(RopeModel model, HoleModel hole)
        {
            var rope = FindRope(model);
            if (rope == null) return;

            rope.BeginCollecting();
            StartCoroutine(PlayPullIntoHole(rope, hole));
        }

        private void OnRopeStepCommitted(RopeModel model)
        {
            _context.Elements.SyncContainedRopes(model);
            RefreshContainedRopeVisuals(model);
        }

        private void OnRopeVisualRefreshed(Rope rope)
        {
            if (rope == null) return;
            RefreshContainedRopeVisuals(rope.Model);
        }

        private void OnRopeStateChanged(RopeModel model)
        {
            var rope = FindRope(model);
            if (rope == null) return;

            rope.RefreshAppearance();
            rope.RefreshVisual();
        }

        private void OnHoleStateChanged(HoleModel model)
        {
            var hole = FindHole(model);
            if (hole != null) hole.RefreshVisual();
        }

        private void OnWallRemoved(WallModel model)
        {
            for (int i = _walls.Count - 1; i >= 0; i--)
            {
                var wall = _walls[i];
                if (wall == null || wall.Model != model) continue;

                _walls.RemoveAt(i);
                wall.Remove();
                return;
            }
        }

        private void OnContainedRopesReleased(RopeModel outer, List<RopeModel> released)
        {
            for (int i = 0; i < released.Count; i++)
            {
                var rope = FindRope(released[i]);
                if (rope == null) continue;

                rope.ClearContainedVisualSource();
            }
        }

        /// <summary>
        /// Mekanik kaldırma zaten yapıldı; bu yalnızca görseldir ve logical blocking'i geciktirmez (GDD 10.4).
        /// </summary>
        private IEnumerator PlayPullIntoHole(Rope rope, HoleModel hole)
        {
            var holePosition = _context.Graph.GetPosition(hole.NodeId) + Vector3.up * _context.Config.ropeHeight;

            _pullSource.Clear();
            var points = rope.RenderPoints;

            // Hole'a yakın olan uçtan içeri çekilir.
            var headIsCloser = points.Count == 0 ||
                               (points[0] - holePosition).sqrMagnitude <=
                               (points[points.Count - 1] - holePosition).sqrMagnitude;

            _pullSource.Add(holePosition);
            if (headIsCloser)
            {
                for (int i = 0; i < points.Count; i++) _pullSource.Add(points[i]);
            }
            else
            {
                for (int i = points.Count - 1; i >= 0; i--) _pullSource.Add(points[i]);
            }

            var totalLength = PolylineUtility.GetLength(_pullSource);
            var duration = Mathf.Max(0.05f, _context.Config.holePullDuration);
            var elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                var t = Mathf.Clamp01(elapsed / duration);
                PolylineUtility.TrimFromStart(_pullSource, totalLength * t, _pullBuffer);

                if (_pullBuffer.Count >= 2)
                {
                    // Kırpılan uç hole'un içinde kalır.
                    _pullBuffer[0] = holePosition;
                    rope.SplineAdapter.UpdatePoints(_pullBuffer);
                }

                yield return null;
            }

            rope.SplineAdapter.SetVisible(false);
            rope.MarkCollected();
            Destroy(rope.gameObject);
        }

        private void OnHolesRemoved(RopeColor color, List<HoleModel> removed)
        {
            for (int i = _holes.Count - 1; i >= 0; i--)
            {
                var hole = _holes[i];
                if (hole == null || !removed.Contains(hole.Model)) continue;

                _holes.RemoveAt(i);
                hole.Remove();
            }
        }

        private Hole FindHole(HoleModel model)
        {
            for (int i = 0; i < _holes.Count; i++)
            {
                if (_holes[i] != null && _holes[i].Model == model) return _holes[i];
            }

            return null;
        }

        private void ConfigureContainedRopeVisuals()
        {
            for (int i = 0; i < _ropes.Count; i++)
            {
                var rope = _ropes[i];
                if (rope == null) continue;

                if (!_context.Elements.TryGetContainmentVisualSource(rope.Model, out var outerModel,
                        out var unitIndices)) continue;

                var outerRope = FindRope(outerModel);
                if (outerRope != null) rope.SetContainedVisualSource(outerRope, unitIndices);
            }
        }

        private void RefreshContainedRopeVisuals(RopeModel outer)
        {
            _context.Elements.GetContainedRopes(outer, _containedRopeBuffer);
            for (int i = 0; i < _containedRopeBuffer.Count; i++)
            {
                var rope = FindRope(_containedRopeBuffer[i]);
                if (rope != null) rope.RefreshVisual();
            }
        }

        private void OnLevelCompleted()
        {
            SetState(SlimyGameState.Completing);
            StartCoroutine(CompleteAfterAnimations());
        }

        private IEnumerator CompleteAfterAnimations()
        {
            yield return new WaitForSeconds(_context.Config.holePullDuration + 0.1f);
            SetState(SlimyGameState.Completed);
            LevelCompleted?.Invoke();
        }

        private void SetState(SlimyGameState state)
        {
            if (State == state) return;
            State = state;
            StateChanged?.Invoke(state);
        }

        public void FailLoad(string error)
        {
            SetState(SlimyGameState.Loading);
            Debug.LogError($"[SlimyJam] Level load failed, gameplay not started:\n{error}", this);
        }
    }
}

using System;
using SlimyJam.Core;
using SlimyJam.Pathfinding;
using UnityEngine;

namespace SlimyJam.Gameplay
{
    /// <summary>
    /// Continuous progress, midpoint transition, snap ve max speed davranışını RopeModel'den ayırır (GDD 14.2).
    /// Bir logical adım, aynı uzunlukta iki zincir arasındaki geçiştir; i. unit
    /// Lerp(FromChain[i], ToChain[i], progress) ile konumlanır.
    /// </summary>
    public sealed class RopeMovementController
    {
        private const float Epsilon = 0.0005f;
        private const int MaxStepsPerFrame = 32;

        private readonly GraphRepository _graph;
        private readonly StepPlanner _stepPlanner;
        private readonly PointerTargetResolver _resolver;
        private readonly SlimyConfig _config;
        private readonly PointerTarget _target = new PointerTarget();
        private readonly StepPlan _step = new StepPlan();

        private RopeModel _rope;
        private bool _hasStep;
        private float _progress;

        private bool _snapping;
        private float _snapFrom;
        private float _snapTo;
        private float _snapElapsed;

        /// <summary>Adım midpoint'i geçtiğinde veya serbest uç matching hole'a girdiğinde tetiklenir.</summary>
        public event Action<RopeModel, int, RopeEnd> CollectionTriggered;

        /// <summary>Release sonrası snap tamamlandı; hole collection kontrolü burada yapılır (GDD 8.4 adım 6).</summary>
        public event Action ReleaseCompleted;

        public RopeEnd ActiveEnd { get; private set; }
        public bool IsDragging { get; private set; }
        public bool IsSnapping => _snapping;
        public bool HasStepInFlight => _hasStep;
        public float Progress => _progress;
        public StepPlan CurrentStep => _hasStep ? _step : null;
        public PointerTarget CurrentTarget => _target;

        public RopeMovementController(RopeModel rope, GraphRepository graph, StepPlanner stepPlanner,
            PointerTargetResolver resolver, SlimyConfig config)
        {
            _rope = rope;
            _graph = graph;
            _stepPlanner = stepPlanner;
            _resolver = resolver;
            _config = config;
        }

        public void BeginDrag(RopeEnd activeEnd)
        {
            ActiveEnd = activeEnd;
            IsDragging = true;
            _snapping = false;
            _resolver.ResetCache();
        }

        /// <summary>Pointer release: en yakın node merkezine görsel snap başlatılır (GDD 8.4).</summary>
        public void BeginRelease()
        {
            IsDragging = false;

            if (!_hasStep)
            {
                _snapping = false;
                ReleaseCompleted?.Invoke();
                return;
            }

            _snapFrom = _progress;
            _snapTo = _progress < _config.midpointThreshold ? 0f : 1f;
            _snapElapsed = 0f;
            _snapping = !Mathf.Approximately(_snapFrom, _snapTo);

            if (_snapping) return;

            FinishSnap();
            ReleaseCompleted?.Invoke();
        }

        public void CancelImmediately()
        {
            IsDragging = false;
            _snapping = false;

            if (_hasStep)
            {
                // Commit edilmiş adım tamamlanır, edilmemiş adım geri alınır; logical state tutarlı kalır.
                _progress = _progress < _config.midpointThreshold ? 0f : 1f;
                FinishSnap();
            }

            ReleaseCompleted?.Invoke();
        }

        public void Tick(Vector3 pointerWorldPosition, float deltaTime)
        {
            if (_snapping)
            {
                TickSnap(deltaTime);
                return;
            }

            if (!IsDragging || _rope == null) return;

            var remaining = _config.maximumMoveSpeed * deltaTime;
            var guard = 0;

            while (remaining > Epsilon && guard++ < MaxStepsPerFrame)
            {
                _resolver.Resolve(_rope, ActiveEnd, pointerWorldPosition, _target);

                if (_hasStep)
                {
                    if (!AdvanceInFlightStep(ref remaining)) break;
                }
                else
                {
                    if (!StartStepTowardsTarget()) break;
                }
            }
        }

        /// <summary>Uçuştaki adımı ilerletir/geri sarar. Devam edilebiliyorsa true döner.</summary>
        private bool AdvanceInFlightStep(ref float remaining)
        {
            var stepLength = GetStepLength();
            var committed = _progress >= _config.midpointThreshold;
            var cap = committed ? ResolveCommittedCap() : ResolvePendingCap();

            if (!committed && !CapIsValid(cap))
            {
                // Oyuncu adımı yarıda bırakıp başka yöne gidiyor: henüz commit edilmediği için geri sarılır.
                return RewindStep(ref remaining, stepLength);
            }

            if (_progress >= cap - Epsilon)
            {
                remaining = 0f;
                return false;
            }

            var distanceToCap = (cap - _progress) * stepLength;
            var moved = Mathf.Min(remaining, distanceToCap);
            var newProgress = _progress + moved / Mathf.Max(stepLength, Epsilon);

            if (!committed && newProgress >= _config.midpointThreshold)
            {
                if (!TryCrossMidpoint())
                {
                    // Midpoint geçilemedi: endpoint midpoint'in hemen öncesinde durur (GDD 8.7, 9.3).
                    _progress = Mathf.Max(0f, _config.midpointThreshold - Epsilon);
                    remaining = 0f;
                    return false;
                }

                if (!_hasStep)
                {
                    // Collection tetiklendi; hareket sona erdi.
                    remaining = 0f;
                    return false;
                }
            }

            _progress = newProgress;
            remaining -= moved;

            if (_progress < 1f - Epsilon) return false;

            _progress = 0f;
            _hasStep = false;
            _step.Reset();
            return true;
        }

        private bool CapIsValid(float cap) => cap >= 0f;

        /// <summary>Commit edilmemiş adım için hedef progress; adım artık istenmiyorsa -1.</summary>
        private float ResolvePendingCap()
        {
            var desired = GetDesiredNextNode(out var cap);
            return desired == _step.DestinationNodeId ? cap : -1f;
        }

        /// <summary>
        /// Commit edilmiş adım için hedef progress. Pointer geldiğimiz segmente geri döndüyse endpoint
        /// midpoint ile node merkezi arasında pointer'ı takip eder; aksi hâlde adım tamamlanır.
        /// </summary>
        private float ResolveCommittedCap()
        {
            var activeIndex = GetActiveIndex();
            var previousNodeId = _step.FromChain[activeIndex];

            if (_target.PathNodes.Count == 0 && _target.SegmentOtherNodeId == previousNodeId)
            {
                return Mathf.Max(_config.midpointThreshold, 1f - _target.SegmentFraction);
            }

            return 1f;
        }

        private bool RewindStep(ref float remaining, float stepLength)
        {
            var distanceToStart = _progress * stepLength;
            var moved = Mathf.Min(remaining, distanceToStart);
            _progress -= moved / Mathf.Max(stepLength, Epsilon);
            remaining -= moved;

            if (_progress > Epsilon) return false;

            _progress = 0f;
            _hasStep = false;
            _step.Reset();
            return true;
        }

        private bool StartStepTowardsTarget()
        {
            var destination = GetDesiredNextNode(out var cap);
            if (destination < 0 || cap <= Epsilon) return false;
            if (!_stepPlanner.TryPlanStep(_rope, ActiveEnd, destination, _step)) return false;

            _hasStep = true;
            _progress = 0f;
            return true;
        }

        /// <summary>
        /// Hedefteki bir sonraki node ve o adım için üst sınır. Ara path node'larında sınır 1'dir;
        /// son segment üzerinde pointer projection'ının fractional konumudur (GDD 7.1 adım 7).
        /// </summary>
        private int GetDesiredNextNode(out float cap)
        {
            cap = 0f;
            if (!_target.HasTarget) return -1;

            if (_target.PathNodes.Count > 0)
            {
                cap = 1f;
                return _target.PathNodes[0];
            }

            if (_target.SegmentOtherNodeId < 0) return -1;

            cap = _target.SegmentFraction;
            return _target.SegmentOtherNodeId;
        }

        /// <summary>Midpoint geçildi: occupancy commit edilir veya collection başlar (GDD 8.2, 10.2).</summary>
        private bool TryCrossMidpoint()
        {
            if (_step.IsTerminal)
            {
                var holeId = _step.CollectionHoleId;
                var end = _step.CollectionEnd;
                _hasStep = false;
                _progress = 0f;
                IsDragging = false;
                _step.Reset();
                CollectionTriggered?.Invoke(_rope, holeId, end);
                return true;
            }

            return _stepPlanner.TryCommit(_rope, _step);
        }

        private void TickSnap(float deltaTime)
        {
            _snapElapsed += deltaTime;
            var duration = Mathf.Max(_config.snapDuration, 0.01f);
            var t = Mathf.Clamp01(_snapElapsed / duration);
            _progress = Mathf.Lerp(_snapFrom, _snapTo, t);

            if (t < 1f) return;

            _snapping = false;
            FinishSnap();
            ReleaseCompleted?.Invoke();
        }

        private void FinishSnap()
        {
            if (!_hasStep)
            {
                _progress = 0f;
                return;
            }

            // Occupancy midpoint kuralıyla zaten karara bağlanmıştı; snap yalnızca görsel tamamlamadır.
            _progress = 0f;
            _hasStep = false;
            _step.Reset();
        }

        private float GetStepLength()
        {
            var activeIndex = GetActiveIndex();
            var from = _graph.GetPosition(_step.FromChain[activeIndex]);
            var to = _graph.GetPosition(_step.ToChain[activeIndex]);
            return Mathf.Max(Vector3.Distance(from, to), Epsilon);
        }

        private int GetActiveIndex() => ActiveEnd == RopeEnd.Head ? 0 : _step.FromChain.Count - 1;

        /// <summary>Render için i. rope unit'inin dünya konumu.</summary>
        public Vector3 GetUnitPosition(int index)
        {
            if (!_hasStep) return _graph.GetPosition(_rope.Nodes[index]);

            var from = _graph.GetPosition(_step.FromChain[index]);
            var to = _graph.GetPosition(_step.ToChain[index]);
            return Vector3.Lerp(from, to, _progress);
        }

        public Vector3 GetEndpointPosition(RopeEnd end)
        {
            return GetUnitPosition(end == RopeEnd.Head ? 0 : _rope.Length - 1);
        }
    }
}

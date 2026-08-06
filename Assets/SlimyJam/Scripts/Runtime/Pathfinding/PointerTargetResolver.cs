using System.Collections.Generic;
using SlimyJam.Core;
using SlimyJam.Gameplay;
using UnityEngine;

namespace SlimyJam.Pathfinding
{
    /// <summary>
    /// Pointer'ı en yakın graph segmentine project eder ve segmentin iki ucu üzerinden rota maliyetini
    /// karşılaştırarak hedefi çözer (GDD 7.1). Pathfinding her frame değil, yalnızca hedef segment/entry node
    /// veya occupancy değiştiğinde yeniden çalışır (GDD 7.2).
    /// </summary>
    public sealed class PointerTargetResolver
    {
        private readonly GraphRepository _graph;
        private readonly NodeOccupancyMap _occupancy;
        private readonly PathfindingService _pathfinding;
        private readonly SlimyConfig _config;

        private readonly List<int> _pathToA = new List<int>();
        private readonly List<int> _pathToB = new List<int>();

        private GraphSegment _lastSegment = GraphSegment.None;
        private GraphSegment _cachedSegment = GraphSegment.None;
        private int _cachedStartNodeId = -1;
        private int _cachedOccupancyVersion = -1;
        private int _cachedRopeVersion = -1;
        private float _pathCostToA = float.PositiveInfinity;
        private float _pathCostToB = float.PositiveInfinity;

        public PointerTargetResolver(GraphRepository graph, NodeOccupancyMap occupancy,
            PathfindingService pathfinding, SlimyConfig config)
        {
            _graph = graph;
            _occupancy = occupancy;
            _pathfinding = pathfinding;
            _config = config;
        }

        public void ResetCache()
        {
            _lastSegment = GraphSegment.None;
            _cachedSegment = GraphSegment.None;
            _cachedStartNodeId = -1;
            _cachedOccupancyVersion = -1;
            _cachedRopeVersion = -1;
            _pathCostToA = float.PositiveInfinity;
            _pathCostToB = float.PositiveInfinity;
        }

        public void Resolve(RopeModel rope, RopeEnd activeEnd, Vector3 pointerWorldPosition, PointerTarget result)
        {
            if (!_graph.TryFindNearestSegment(pointerWorldPosition, out var segment, out var projection, out _))
            {
                result.Clear();
                return;
            }

            segment = ApplyHysteresis(segment, pointerWorldPosition, ref projection);
            _lastSegment = segment;

            var startNodeId = rope.GetEndpointNodeId(activeEnd);
            var positionA = _graph.GetPosition(segment.NodeAId);
            var positionB = _graph.GetPosition(segment.NodeBId);

            // Rota maliyetleri yalnızca segment, start node veya occupancy değiştiğinde yeniden hesaplanır;
            // pointer aynı segment üzerinde gezerken sadece fractional hedef güncellenir (GDD 7.2).
            if (!IsCostCacheValid(rope, activeEnd, segment))
            {
                _pathCostToA = _pathfinding.GetPathCost(rope, activeEnd, segment.NodeAId, _pathToA);
                _pathCostToB = _pathfinding.GetPathCost(rope, activeEnd, segment.NodeBId, _pathToB);

                _cachedSegment = segment;
                _cachedStartNodeId = startNodeId;
                _cachedOccupancyVersion = _occupancy.Version;
                _cachedRopeVersion = rope.Version;
            }

            var costA = _pathCostToA + Vector3.Distance(positionA, projection);
            var costB = _pathCostToB + Vector3.Distance(positionB, projection);

            // Eşit maliyette segmentin küçük ID'li ucu (NodeA) kazanır - deterministik tie-break.
            var useA = costA <= costB;
            var entryNodeId = useA ? segment.NodeAId : segment.NodeBId;
            var otherNodeId = segment.Other(entryNodeId);
            var chosenPath = useA ? _pathToA : _pathToB;

            var reachable = !float.IsPositiveInfinity(useA ? costA : costB);
            result.PathNodes.Clear();
            for (int i = 0; i < chosenPath.Count; i++) result.PathNodes.Add(chosenPath[i]);

            result.IsComplete = reachable;
            result.EntryNodeId = result.PathNodes.Count > 0 ? result.PathNodes[result.PathNodes.Count - 1] : startNodeId;

            if (reachable && result.EntryNodeId == entryNodeId && segment.Length > Mathf.Epsilon)
            {
                result.SegmentOtherNodeId = otherNodeId;
                result.SegmentFraction = Mathf.Clamp01(
                    Vector3.Distance(projection, _graph.GetPosition(entryNodeId)) / segment.Length);
            }
            else
            {
                // Prefix'te kaldıysak segment üzerinde ek continuous ilerleme yoktur.
                result.SegmentOtherNodeId = -1;
                result.SegmentFraction = 0f;
            }
        }

        /// <summary>
        /// Yeni segment yalnızca belirgin biçimde daha yakınsa değişir; junction çevresindeki
        /// target oscillation'ı (path jitter) engellenir (GDD 18).
        /// </summary>
        private GraphSegment ApplyHysteresis(GraphSegment candidate, Vector3 pointerWorldPosition,
            ref Vector3 projection)
        {
            if (!_lastSegment.IsValid || _lastSegment.Equals(candidate)) return candidate;
            if (_config == null || _config.segmentTargetHysteresis <= 0f) return candidate;

            var previousA = _graph.GetPosition(_lastSegment.NodeAId);
            var previousB = _graph.GetPosition(_lastSegment.NodeBId);
            var previousFraction = SegmentSpatialHash.ClosestPointFraction(previousA, previousB, pointerWorldPosition);
            var previousProjection = Vector3.Lerp(previousA, previousB, previousFraction);

            var previousDistance = Vector3.Distance(previousProjection, pointerWorldPosition);
            var candidateDistance = Vector3.Distance(projection, pointerWorldPosition);

            if (previousDistance - candidateDistance > _config.segmentTargetHysteresis) return candidate;

            projection = previousProjection;
            return _lastSegment;
        }

        private bool IsCostCacheValid(RopeModel rope, RopeEnd activeEnd, GraphSegment segment)
        {
            return _cachedSegment.IsValid
                   && _cachedSegment.Equals(segment)
                   && _cachedStartNodeId == rope.GetEndpointNodeId(activeEnd)
                   && _cachedOccupancyVersion == _occupancy.Version
                   && _cachedRopeVersion == rope.Version;
        }
    }
}

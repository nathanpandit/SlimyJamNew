using System.Collections.Generic;
using SlimyJam.Core;
using SlimyJam.Gameplay;
using UnityEngine;

namespace SlimyJam.Pathfinding
{
    /// <summary>
    /// Connected node graph üzerinde rota üretimi (GDD 14.1). Blocked prefix ve stabil tie-break destekler.
    /// Hot path'te allocation yapmamak için tüm çalışma yapıları field olarak tutulur (GDD 18).
    /// </summary>
    public sealed class PathfindingService
    {
        private readonly GraphRepository _graph;
        private readonly NodeOccupancyMap _occupancy;

        private readonly Dictionary<int, float> _gScore = new Dictionary<int, float>();
        private readonly Dictionary<int, int> _cameFrom = new Dictionary<int, int>();
        private readonly HashSet<int> _closed = new HashSet<int>();
        private readonly Dictionary<int, int> _ownChainIndex = new Dictionary<int, int>();
        private readonly List<int> _intendedPath = new List<int>();
        private readonly MinHeap _open = new MinHeap();
        private RopeModel _searchRope;

        public PathfindingService(GraphRepository graph, NodeOccupancyMap occupancy)
        {
            _graph = graph;
            _occupancy = occupancy;
        }

        /// <summary>
        /// Rope'un active endpoint'inden hedefe rota. Sonuç start node'unu içermez; sırayla girilecek node'lardır.
        /// Hedefe geçerli rota yoksa intended path'in blocked olmayan prefix'i yazılır (GDD 7.4)
        /// ve <c>false</c> döner.
        /// </summary>
        public bool TryFindPath(RopeModel rope, RopeEnd activeEnd, int targetNodeId, List<int> result)
        {
            result.Clear();

            var startNodeId = rope.GetEndpointNodeId(activeEnd);
            if (startNodeId == targetNodeId) return true;

            _searchRope = rope;
            BuildOwnChainIndex(rope, activeEnd);

            if (RunSearch(startNodeId, targetNodeId, true, result)) return true;

            // Geçerli rota yok: occupancy'yi yok sayarak niyet edilen rotayı bul ve ilk blocked node'da kes.
            _intendedPath.Clear();
            if (!RunSearch(startNodeId, targetNodeId, false, _intendedPath)) return false;

            var previous = startNodeId;
            for (int i = 0; i < _intendedPath.Count; i++)
            {
                var nodeId = _intendedPath[i];
                if (!CanEnter(previous, nodeId, rope)) break;

                result.Add(nodeId);
                previous = nodeId;

                // Hole terminaldir; ötesine geçilemez.
                if (_occupancy.GetOccupant(nodeId) is IHoleOccupant) break;
            }

            return false;
        }

        /// <summary>Rota maliyeti; ulaşılamıyorsa <see cref="float.PositiveInfinity"/>.</summary>
        public float GetPathCost(RopeModel rope, RopeEnd activeEnd, int targetNodeId, List<int> pathBuffer)
        {
            if (!TryFindPath(rope, activeEnd, targetNodeId, pathBuffer)) return float.PositiveInfinity;

            var cost = 0f;
            var previous = rope.GetEndpointNodeId(activeEnd);
            for (int i = 0; i < pathBuffer.Count; i++)
            {
                cost += Vector3.Distance(_graph.GetPosition(previous), _graph.GetPosition(pathBuffer[i]));
                previous = pathBuffer[i];
            }

            return cost;
        }

        private void BuildOwnChainIndex(RopeModel rope, RopeEnd activeEnd)
        {
            _ownChainIndex.Clear();
            for (int i = 0; i < rope.Length; i++)
            {
                _ownChainIndex[rope.GetNodeFromEnd(activeEnd, i)] = i;
            }
        }

        /// <summary>
        /// Traversal izni (GDD 7.5). Kendi gövdesi yalnızca reverse zinciri boyunca - yani active endpoint'ten
        /// içeri doğru sırayla - kullanılabilir; ortadan bir body node'una atlanamaz.
        /// </summary>
        private bool CanEnter(int fromNodeId, int toNodeId, RopeModel rope)
        {
            if (!_occupancy.TryGetOccupant(toNodeId, out var occupant)) return true;

            switch (occupant)
            {
                case IHoleOccupant hole:
                    return TraversalRules.CanEnterHole(rope, hole);
                case IWallOccupant:
                    return false;
                case IRopeOccupant otherRope when otherRope.RopeId != rope.RopeId:
                    return false;
            }

            if (!_ownChainIndex.TryGetValue(toNodeId, out var toIndex)) return false;
            if (!_ownChainIndex.TryGetValue(fromNodeId, out var fromIndex)) return false;
            return toIndex == fromIndex + 1;
        }

        private bool RunSearch(int startNodeId, int targetNodeId, bool respectOccupancy, List<int> result)
        {
            _gScore.Clear();
            _cameFrom.Clear();
            _closed.Clear();
            _open.Clear();

            var targetPosition = _graph.GetPosition(targetNodeId);
            _gScore[startNodeId] = 0f;
            _open.Push(startNodeId, Heuristic(startNodeId, targetPosition));

            while (_open.TryPop(out var current))
            {
                if (current == targetNodeId)
                {
                    Reconstruct(startNodeId, targetNodeId, result);
                    return true;
                }

                if (!_closed.Add(current)) continue;

                // Hole node'una girildiğinde geçiş terminaldir; oradan ilerlenemez (GDD 7.5).
                if (current != startNodeId && _occupancy.GetOccupant(current) is IHoleOccupant) continue;

                var neighbours = _graph.GetNeighbours(current);
                var currentPosition = _graph.GetPosition(current);
                var currentCost = _gScore[current];

                for (int i = 0; i < neighbours.Count; i++)
                {
                    var neighbourId = neighbours[i];
                    if (_closed.Contains(neighbourId)) continue;
                    if (respectOccupancy && !CanEnterForSearch(current, neighbourId)) continue;

                    var neighbourPosition = _graph.GetPosition(neighbourId);
                    var tentative = currentCost + Vector3.Distance(currentPosition, neighbourPosition);

                    if (_gScore.TryGetValue(neighbourId, out var existing) && tentative >= existing - CostEpsilon)
                    {
                        continue;
                    }

                    _gScore[neighbourId] = tentative;
                    _cameFrom[neighbourId] = current;
                    _open.Push(neighbourId, tentative + Heuristic(neighbourId, targetPosition));
                }
            }

            return false;
        }

        private bool CanEnterForSearch(int fromNodeId, int toNodeId) => CanEnter(fromNodeId, toNodeId, _searchRope);

        private float Heuristic(int nodeId, Vector3 targetPosition)
        {
            return Vector3.Distance(_graph.GetPosition(nodeId), targetPosition);
        }

        private void Reconstruct(int startNodeId, int targetNodeId, List<int> result)
        {
            result.Clear();
            var current = targetNodeId;
            while (current != startNodeId)
            {
                result.Add(current);
                current = _cameFrom[current];
            }

            result.Reverse();
        }

        private const float CostEpsilon = 0.0001f;

        /// <summary>
        /// Eşit f-score'da ekleme sırasına göre stabil davranan basit binary heap.
        /// Ekleme sırası connectedNodeIds sırasını izlediğinden tie-break deterministiktir (GDD 7.2).
        /// </summary>
        private sealed class MinHeap
        {
            private readonly List<int> _nodes = new List<int>();
            private readonly List<float> _priorities = new List<float>();
            private readonly List<int> _order = new List<int>();
            private int _counter;

            public void Clear()
            {
                _nodes.Clear();
                _priorities.Clear();
                _order.Clear();
                _counter = 0;
            }

            public void Push(int node, float priority)
            {
                _nodes.Add(node);
                _priorities.Add(priority);
                _order.Add(_counter++);

                var child = _nodes.Count - 1;
                while (child > 0)
                {
                    var parent = (child - 1) / 2;
                    if (!IsBetter(child, parent)) break;
                    Swap(child, parent);
                    child = parent;
                }
            }

            public bool TryPop(out int node)
            {
                if (_nodes.Count == 0)
                {
                    node = -1;
                    return false;
                }

                node = _nodes[0];
                var last = _nodes.Count - 1;
                Swap(0, last);
                _nodes.RemoveAt(last);
                _priorities.RemoveAt(last);
                _order.RemoveAt(last);

                var parent = 0;
                while (true)
                {
                    var left = parent * 2 + 1;
                    var right = left + 1;
                    var best = parent;

                    if (left < _nodes.Count && IsBetter(left, best)) best = left;
                    if (right < _nodes.Count && IsBetter(right, best)) best = right;
                    if (best == parent) break;

                    Swap(parent, best);
                    parent = best;
                }

                return true;
            }

            private bool IsBetter(int a, int b)
            {
                var difference = _priorities[a] - _priorities[b];
                if (difference < -CostEpsilon) return true;
                if (difference > CostEpsilon) return false;
                return _order[a] < _order[b];
            }

            private void Swap(int a, int b)
            {
                (_nodes[a], _nodes[b]) = (_nodes[b], _nodes[a]);
                (_priorities[a], _priorities[b]) = (_priorities[b], _priorities[a]);
                (_order[a], _order[b]) = (_order[b], _order[a]);
            }
        }
    }
}

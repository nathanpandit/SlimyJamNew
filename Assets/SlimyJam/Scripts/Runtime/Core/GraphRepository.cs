using System.Collections.Generic;
using SlimyJam.Data;
using UnityEngine;

namespace SlimyJam.Core
{
    /// <summary>
    /// Node lookup, komşuluk ve segment spatial query verisini merkezi tutar (GDD 14.2).
    /// Level süresince immutable'dır; occupancy ayrı tutulur.
    /// </summary>
    public sealed class GraphRepository
    {
        private readonly Dictionary<int, GroundNode> _nodesById;
        private readonly List<GroundNode> _nodes;
        private readonly List<GraphSegment> _segments;
        private readonly SegmentSpatialHash _spatialHash;

        public IReadOnlyList<GroundNode> Nodes => _nodes;
        public IReadOnlyList<GraphSegment> Segments => _segments;
        public Bounds Bounds { get; }

        public GraphRepository(IReadOnlyList<GroundNodeData> nodeData)
        {
            _nodes = new List<GroundNode>(nodeData.Count);
            _nodesById = new Dictionary<int, GroundNode>(nodeData.Count);

            for (int i = 0; i < nodeData.Count; i++)
            {
                var data = nodeData[i];
                var node = new GroundNode(data.id, data.position, new List<int>(data.connectedNodeIds));
                _nodes.Add(node);
                _nodesById[node.Id] = node;
            }

            _segments = BuildSegments();
            _spatialHash = new SegmentSpatialHash(this, _segments);
            Bounds = CalculateBounds();
        }

        public bool TryGetNode(int id, out GroundNode node) => _nodesById.TryGetValue(id, out node);

        public GroundNode GetNode(int id) => _nodesById[id];

        public Vector3 GetPosition(int id) => _nodesById[id].Position;

        public List<int> GetNeighbours(int id) => _nodesById[id].ConnectedNodeIds;

        public bool AreConnected(int a, int b)
        {
            return _nodesById.TryGetValue(a, out var node) && node.ConnectedNodeIds.Contains(b);
        }

        /// <summary>Pointer'ın graph üzerindeki en yakın projection noktası (GDD 7.1 adım 2-4).</summary>
        public bool TryFindNearestSegment(Vector3 worldPosition, out GraphSegment segment, out Vector3 projection,
            out float fractionFromA)
        {
            return _spatialHash.TryFindNearest(worldPosition, out segment, out projection, out fractionFromA);
        }

        public int FindNearestNode(Vector3 worldPosition)
        {
            var best = -1;
            var bestDistance = float.MaxValue;
            for (int i = 0; i < _nodes.Count; i++)
            {
                var distance = (_nodes[i].Position - worldPosition).sqrMagnitude;
                if (distance >= bestDistance) continue;
                bestDistance = distance;
                best = _nodes[i].Id;
            }

            return best;
        }

        /// <summary>Bir segment üzerindeki noktayı verilen node'dan itibaren fraction cinsinden döndürür.</summary>
        public Vector3 EvaluateSegment(int fromNodeId, int toNodeId, float fraction)
        {
            return Vector3.Lerp(GetPosition(fromNodeId), GetPosition(toNodeId), Mathf.Clamp01(fraction));
        }

        public float GetSegmentLength(int a, int b) => Vector3.Distance(GetPosition(a), GetPosition(b));

        private List<GraphSegment> BuildSegments()
        {
            var seen = new HashSet<long>();
            var segments = new List<GraphSegment>();

            for (int i = 0; i < _nodes.Count; i++)
            {
                var node = _nodes[i];
                for (int j = 0; j < node.ConnectedNodeIds.Count; j++)
                {
                    var otherId = node.ConnectedNodeIds[j];
                    if (!_nodesById.TryGetValue(otherId, out var other)) continue;

                    var low = Mathf.Min(node.Id, otherId);
                    var high = Mathf.Max(node.Id, otherId);
                    var key = ((long)low << 32) | (uint)high;
                    if (!seen.Add(key)) continue;

                    var length = Vector3.Distance(_nodesById[low].Position, _nodesById[high].Position);
                    segments.Add(new GraphSegment(low, high, length));
                }
            }

            return segments;
        }

        private Bounds CalculateBounds()
        {
            if (_nodes.Count == 0) return new Bounds(Vector3.zero, Vector3.zero);

            var bounds = new Bounds(_nodes[0].Position, Vector3.zero);
            for (int i = 1; i < _nodes.Count; i++)
            {
                bounds.Encapsulate(_nodes[i].Position);
            }

            return bounds;
        }
    }
}

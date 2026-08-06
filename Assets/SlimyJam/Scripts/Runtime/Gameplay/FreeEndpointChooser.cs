using SlimyJam.Core;
using UnityEngine;

namespace SlimyJam.Gameplay
{
    /// <summary>
    /// Reverse adımında serbest ucun uzayacağı node'u seçer (GDD 8.6).
    /// Öncelik: 1) 0 derece (düz devam), 2) en küçük unsigned açı, 3) connectedNodeIds list order.
    /// </summary>
    public sealed class FreeEndpointChooser
    {
        private const float StraightAngleThreshold = 0.05f;
        private const float AngleEpsilon = 0.01f;

        private readonly GraphRepository _graph;
        private readonly NodeOccupancyMap _occupancy;

        public FreeEndpointChooser(GraphRepository graph, NodeOccupancyMap occupancy)
        {
            _graph = graph;
            _occupancy = occupancy;
        }

        /// <summary>Aday yoksa -1 döner; bu durumda reverse logical adım gerçekleşmez (GDD 8.6/7).</summary>
        public int Choose(RopeModel rope, RopeEnd freeEnd)
        {
            if (rope.Length < 2) return -1;

            var freeNodeId = rope.GetEndpointNodeId(freeEnd);
            var innerNodeId = rope.GetNodeFromEnd(freeEnd, 1);
            if (innerNodeId < 0) return -1;

            var freePosition = _graph.GetPosition(freeNodeId);
            var outward = (freePosition - _graph.GetPosition(innerNodeId)).normalized;

            var neighbours = _graph.GetNeighbours(freeNodeId);
            var bestCandidate = -1;
            var bestAngle = float.MaxValue;

            for (int i = 0; i < neighbours.Count; i++)
            {
                var candidateId = neighbours[i];
                if (rope.Contains(candidateId)) continue;
                if (!TraversalRules.IsAvailableForFreeEndpoint(_occupancy, candidateId, rope)) continue;

                var candidateDirection = (_graph.GetPosition(candidateId) - freePosition).normalized;
                var angle = UnsignedAngle(outward, candidateDirection);

                // Düz devam eden aday varsa doğrudan seçilir.
                if (angle <= StraightAngleThreshold) return candidateId;

                // Eşit (veya float gürültüsü kadar yakın) açıda connectedNodeIds sırasında önce gelen kazanır.
                if (angle >= bestAngle - AngleEpsilon) continue;

                bestAngle = angle;
                bestCandidate = candidateId;
            }

            return bestCandidate;
        }

        public static float UnsignedAngle(Vector3 outward, Vector3 candidate)
        {
            var dot = Mathf.Clamp(Vector3.Dot(outward, candidate), -1f, 1f);
            return Mathf.Acos(dot) * Mathf.Rad2Deg;
        }
    }
}

using System.Collections.Generic;
using UnityEngine;

namespace SlimyJam.Core
{
    /// <summary>
    /// Nearest-segment aramasını büyük graph'ta ucuz tutmak için uniform spatial hash (GDD 18 "Spatial query maliyeti").
    /// Level düzlemsel olduğundan hash, graph'ın en geniş iki eksenine göre kurulur.
    /// </summary>
    public sealed class SegmentSpatialHash
    {
        private const float CellSize = 2f;

        private readonly GraphRepository _graph;
        private readonly List<GraphSegment> _segments;
        private readonly Dictionary<long, List<int>> _cells = new Dictionary<long, List<int>>();
        private readonly int _axisU;
        private readonly int _axisV;
        private int _minCellU;
        private int _maxCellU;
        private int _minCellV;
        private int _maxCellV;

        public SegmentSpatialHash(GraphRepository graph, List<GraphSegment> segments)
        {
            _graph = graph;
            _segments = segments;

            (_axisU, _axisV) = PickAxes(graph.Bounds);
            Build();
        }

        private static (int, int) PickAxes(Bounds bounds)
        {
            var size = bounds.size;
            // En küçük yayılıma sahip ekseni at; kalan iki eksen hash düzlemini oluşturur.
            if (size.y <= size.x && size.y <= size.z) return (0, 2);
            if (size.z <= size.x && size.z <= size.y) return (0, 1);
            return (1, 2);
        }

        private void Build()
        {
            for (int i = 0; i < _segments.Count; i++)
            {
                var segment = _segments[i];
                var a = _graph.GetPosition(segment.NodeAId);
                var b = _graph.GetPosition(segment.NodeBId);

                var minU = Mathf.FloorToInt(Mathf.Min(a[_axisU], b[_axisU]) / CellSize);
                var maxU = Mathf.FloorToInt(Mathf.Max(a[_axisU], b[_axisU]) / CellSize);
                var minV = Mathf.FloorToInt(Mathf.Min(a[_axisV], b[_axisV]) / CellSize);
                var maxV = Mathf.FloorToInt(Mathf.Max(a[_axisV], b[_axisV]) / CellSize);

                for (int u = minU; u <= maxU; u++)
                {
                    for (int v = minV; v <= maxV; v++)
                    {
                        var key = Key(u, v);
                        if (!_cells.TryGetValue(key, out var list))
                        {
                            list = new List<int>();
                            _cells[key] = list;
                        }

                        list.Add(i);
                    }
                }

            }

            var bounds = _graph.Bounds;
            _minCellU = Mathf.FloorToInt(bounds.min[_axisU] / CellSize);
            _maxCellU = Mathf.FloorToInt(bounds.max[_axisU] / CellSize);
            _minCellV = Mathf.FloorToInt(bounds.min[_axisV] / CellSize);
            _maxCellV = Mathf.FloorToInt(bounds.max[_axisV] / CellSize);
        }

        public bool TryFindNearest(Vector3 point, out GraphSegment segment, out Vector3 projection,
            out float fractionFromA)
        {
            segment = GraphSegment.None;
            projection = point;
            fractionFromA = 0f;

            if (_segments.Count == 0) return false;

            var centerU = Mathf.FloorToInt(point[_axisU] / CellSize);
            var centerV = Mathf.FloorToInt(point[_axisV] / CellSize);

            var bestDistance = float.MaxValue;
            var bestIndex = -1;

            // Pointer graph'ın çok dışındaysa bile tüm dolu hücrelere ulaşacak kadar ring taranır.
            var maxRing = Mathf.Max(
                Mathf.Max(Mathf.Abs(centerU - _minCellU), Mathf.Abs(centerU - _maxCellU)),
                Mathf.Max(Mathf.Abs(centerV - _minCellV), Mathf.Abs(centerV - _maxCellV))) + 1;

            for (int ring = 0; ring <= maxRing; ring++)
            {
                // Bir önceki ring'de bulunan en iyi aday, bu ring'in olabilecek en yakın noktasından
                // daha yakınsa aramayı bitir.
                if (bestIndex >= 0 && (ring - 1) * CellSize > bestDistance) break;

                for (int u = centerU - ring; u <= centerU + ring; u++)
                {
                    for (int v = centerV - ring; v <= centerV + ring; v++)
                    {
                        // Yalnızca ring'in dış çeperi.
                        if (ring > 0 && Mathf.Abs(u - centerU) != ring && Mathf.Abs(v - centerV) != ring) continue;
                        if (!_cells.TryGetValue(Key(u, v), out var list)) continue;

                        for (int i = 0; i < list.Count; i++)
                        {
                            var index = list[i];
                            var candidate = _segments[index];
                            var a = _graph.GetPosition(candidate.NodeAId);
                            var b = _graph.GetPosition(candidate.NodeBId);
                            var t = ClosestPointFraction(a, b, point);
                            var closest = Vector3.Lerp(a, b, t);
                            var distance = Vector3.Distance(closest, point);

                            // Eşit mesafede stabil sonuç: segment listesi sırası (node ID sırası) belirler.
                            if (distance >= bestDistance) continue;

                            bestDistance = distance;
                            bestIndex = index;
                            projection = closest;
                            fractionFromA = t;
                        }
                    }
                }
            }

            if (bestIndex < 0)
            {
                // Spatial hash boş kaldıysa (beklenmedik) brute-force'a düş.
                return TryFindNearestBruteForce(point, out segment, out projection, out fractionFromA);
            }

            segment = _segments[bestIndex];
            return true;
        }

        private bool TryFindNearestBruteForce(Vector3 point, out GraphSegment segment, out Vector3 projection,
            out float fractionFromA)
        {
            segment = GraphSegment.None;
            projection = point;
            fractionFromA = 0f;

            var bestDistance = float.MaxValue;
            for (int i = 0; i < _segments.Count; i++)
            {
                var candidate = _segments[i];
                var a = _graph.GetPosition(candidate.NodeAId);
                var b = _graph.GetPosition(candidate.NodeBId);
                var t = ClosestPointFraction(a, b, point);
                var closest = Vector3.Lerp(a, b, t);
                var distance = Vector3.Distance(closest, point);
                if (distance >= bestDistance) continue;

                bestDistance = distance;
                segment = candidate;
                projection = closest;
                fractionFromA = t;
            }

            return bestDistance < float.MaxValue;
        }

        public static float ClosestPointFraction(Vector3 a, Vector3 b, Vector3 point)
        {
            var ab = b - a;
            var lengthSquared = ab.sqrMagnitude;
            if (lengthSquared <= Mathf.Epsilon) return 0f;
            return Mathf.Clamp01(Vector3.Dot(point - a, ab) / lengthSquared);
        }

        private static long Key(int u, int v) => ((long)u << 32) ^ (uint)v;
    }
}

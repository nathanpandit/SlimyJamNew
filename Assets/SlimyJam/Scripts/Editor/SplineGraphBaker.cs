using System.Collections.Generic;
using System.Text;
using Dreamteck.Splines;
using SlimyJam.Authoring;
using SlimyJam.Data;
using UnityEngine;

namespace SlimyJam.EditorTools
{
    /// <summary>
    /// Spline authoring verisini normalized ground node graph'a dönüştürür (GDD 4.2, 14.2 SplineGraphBaker).
    /// Runtime'a dahil değildir; çıktısı yalnızca JSON level datasıdır.
    ///
    /// Kurallar:
    /// 1. Her spline'ın uzunluğu tam sayı adet logical parçaya normalize edilir.
    /// 2. Komşu node merkezleri arasında tam olarak bir dünya birimi bulunur.
    /// 3. Spline başlangıç ve bitişleri node üretir.
    /// 4. Weld toleransı içindeki örnekler tek node'da birleşir; görsel kesişmeler ortak node paylaşır.
    /// 5. Her bağlantı bidirectional kaydedilir.
    /// </summary>
    public static class SplineGraphBaker
    {
        private const int FirstNodeId = 100;

        public sealed class BakeResult
        {
            public SlimyLevelData Data;
            public readonly List<string> Warnings = new List<string>();
            public string Error;
            public bool Success => Error == null;
        }

        public static BakeResult Bake(SlimyLevelAuthoring authoring)
        {
            var result = new BakeResult();

            if (authoring == null)
            {
                result.Error = "No SlimyLevelAuthoring provided.";
                return result;
            }

            var splines = authoring.GetComponentsInChildren<SplineComputer>(true);
            if (splines.Length == 0)
            {
                result.Error = "No SplineComputer found under the authoring root.";
                return result;
            }

            var welder = new NodeWelder(authoring.weldTolerance);
            var edges = new HashSet<long>();
            var connections = new Dictionary<int, List<int>>();

            for (int i = 0; i < splines.Length; i++)
            {
                BakeSpline(splines[i], welder, connections, edges, result);
            }

            var data = new SlimyLevelData { levelIndex = authoring.levelIndex };
            for (int i = 0; i < welder.Nodes.Count; i++)
            {
                var id = FirstNodeId + i;
                data.groundNodes.Add(new GroundNodeData
                {
                    id = id,
                    position = welder.Nodes[i],
                    connectedNodeIds = connections.TryGetValue(id, out var list) ? list : new List<int>()
                });
            }

            BakeHoles(authoring, welder, data, result);
            BakeRopes(authoring, welder, data, result);

            result.Data = data;

            if (!LevelDataValidator.Validate(data, out var validationError))
            {
                result.Error = validationError;
            }

            return result;
        }

        private static void BakeSpline(SplineComputer spline, NodeWelder welder,
            Dictionary<int, List<int>> connections, HashSet<long> edges, BakeResult result)
        {
            spline.RebuildImmediate();

            var length = spline.CalculateLength();
            var segmentCount = Mathf.Max(1, Mathf.RoundToInt(length));
            var unitLength = length / segmentCount;

            if (Mathf.Abs(unitLength - 1f) > 0.15f)
            {
                result.Warnings.Add(
                    $"'{spline.name}' length {length:0.00} normalizes to {segmentCount} segments of " +
                    $"{unitLength:0.00} units. Adjust the spline so segments land near 1.0 world unit (GDD 4.2).");
            }

            var pointCount = spline.isClosed ? segmentCount : segmentCount + 1;
            var ids = new int[pointCount];

            var percent = 0.0;
            for (int i = 0; i < pointCount; i++)
            {
                var position = spline.EvaluatePosition(percent);
                ids[i] = welder.GetOrCreate(position);
                if (i < pointCount - 1) percent = spline.Travel(percent, unitLength, Spline.Direction.Forward);
            }

            for (int i = 1; i < pointCount; i++)
            {
                Connect(connections, edges, ids[i - 1], ids[i], result);
            }

            if (spline.isClosed && pointCount > 2)
            {
                Connect(connections, edges, ids[pointCount - 1], ids[0], result);
            }
        }

        private static void Connect(Dictionary<int, List<int>> connections, HashSet<long> edges, int a, int b,
            BakeResult result)
        {
            if (a == b)
            {
                result.Warnings.Add($"Two consecutive samples welded into the same node ({a}); " +
                                    "weld tolerance may be too large.");
                return;
            }

            var key = ((long)Mathf.Min(a, b) << 32) | (uint)Mathf.Max(a, b);
            if (!edges.Add(key)) return;

            GetList(connections, a).Add(b);
            GetList(connections, b).Add(a);
        }

        private static List<int> GetList(Dictionary<int, List<int>> connections, int id)
        {
            if (connections.TryGetValue(id, out var list)) return list;

            list = new List<int>();
            connections[id] = list;
            return list;
        }

        private static void BakeHoles(SlimyLevelAuthoring authoring, NodeWelder welder, SlimyLevelData data,
            BakeResult result)
        {
            var holes = authoring.CollectHoles();
            for (int i = 0; i < holes.Count; i++)
            {
                var hole = holes[i];
                var nodeId = welder.FindNearest(hole.transform.position, authoring.markerSnapDistance);
                if (nodeId < 0)
                {
                    result.Warnings.Add($"Hole '{hole.name}' is not close enough to any baked node; skipped.");
                    continue;
                }

                data.holes.Add(new HoleData { id = hole.holeId, color = hole.color, nodeId = FirstNodeId + nodeId });
            }
        }

        private static void BakeRopes(SlimyLevelAuthoring authoring, NodeWelder welder, SlimyLevelData data,
            BakeResult result)
        {
            var ropes = authoring.CollectRopes();
            for (int i = 0; i < ropes.Count; i++)
            {
                var rope = ropes[i];
                var points = rope.GetWorldPoints();
                if (points.Count == 0)
                {
                    result.Warnings.Add($"Rope '{rope.name}' has no markers; skipped.");
                    continue;
                }

                var ropeData = new RopeData { id = rope.ropeId, color = rope.color };
                var valid = true;

                for (int p = 0; p < points.Count; p++)
                {
                    var nodeId = welder.FindNearest(points[p], authoring.markerSnapDistance);
                    if (nodeId < 0)
                    {
                        result.Warnings.Add(
                            $"Rope '{rope.name}' marker {p} is not close enough to any baked node; rope skipped.");
                        valid = false;
                        break;
                    }

                    ropeData.occupiedNodeIds.Add(FirstNodeId + nodeId);
                }

                if (valid) data.ropes.Add(ropeData);
            }
        }

        public static string DescribeResult(BakeResult result)
        {
            var builder = new StringBuilder();

            if (result.Data != null)
            {
                builder.AppendLine($"nodes: {result.Data.groundNodes.Count}, ropes: {result.Data.ropes.Count}, " +
                                   $"holes: {result.Data.holes.Count}");
            }

            for (int i = 0; i < result.Warnings.Count; i++)
            {
                builder.AppendLine($"WARNING: {result.Warnings[i]}");
            }

            if (!result.Success) builder.AppendLine($"ERROR: {result.Error}");

            return builder.ToString();
        }

        /// <summary>
        /// Aynı konuma düşen örnekleri tek logical node'da birleştirir. Kesişen spline'ların ortak node
        /// paylaşması bu adımda garanti edilir (GDD 4.2 kural 4, GDD 18 "Intersection mismatch").
        /// </summary>
        private sealed class NodeWelder
        {
            private readonly float _tolerance;
            private readonly Dictionary<long, List<int>> _cells = new Dictionary<long, List<int>>();

            public readonly List<Vector3> Nodes = new List<Vector3>();

            public NodeWelder(float tolerance)
            {
                _tolerance = Mathf.Max(0.01f, tolerance);
            }

            public int GetOrCreate(Vector3 position)
            {
                var existing = FindNearest(position, _tolerance);
                if (existing >= 0) return existing;

                var index = Nodes.Count;
                Nodes.Add(position);
                GetCell(OwnCellKey(position)).Add(index);
                return index;
            }

            public int FindNearest(Vector3 position, float maxDistance)
            {
                var best = -1;
                var bestDistance = maxDistance;

                foreach (var key in CellKeys(position))
                {
                    if (!_cells.TryGetValue(key, out var list)) continue;

                    for (int i = 0; i < list.Count; i++)
                    {
                        var distance = Vector3.Distance(Nodes[list[i]], position);
                        if (distance >= bestDistance) continue;

                        bestDistance = distance;
                        best = list[i];
                    }
                }

                return best;
            }

            private List<int> GetCell(long key)
            {
                if (_cells.TryGetValue(key, out var list)) return list;

                list = new List<int>();
                _cells[key] = list;
                return list;
            }

            private static long OwnCellKey(Vector3 position)
            {
                return ((long)Mathf.FloorToInt(position.x) << 32) ^ (uint)Mathf.FloorToInt(position.z);
            }

            /// <summary>Arama, 1 birimlik hücrenin kendisi ve komşularını tarar; sınırdaki weld kaçmaz.</summary>
            private static IEnumerable<long> CellKeys(Vector3 position)
            {
                var x = Mathf.FloorToInt(position.x);
                var z = Mathf.FloorToInt(position.z);

                for (int dx = -1; dx <= 1; dx++)
                {
                    for (int dz = -1; dz <= 1; dz++)
                    {
                        yield return ((long)(x + dx) << 32) ^ (uint)(z + dz);
                    }
                }
            }
        }
    }
}

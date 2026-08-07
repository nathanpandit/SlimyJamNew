using System.Collections.Generic;
using SlimyJam.Core;
using SlimyJam.Data;
using SlimyJam.Gameplay;
using SlimyJam.Level;
using UnityEngine;

namespace SlimyJam.Tests
{
    /// <summary>
    /// Testler için tam sayı lattice üzerinde level kurar; komşu hücreler bir dünya birimi aralıklıdır.
    /// </summary>
    public sealed class TestLevelBuilder
    {
        private readonly Dictionary<Vector2Int, int> _ids = new Dictionary<Vector2Int, int>();
        private readonly List<Vector2Int> _order = new List<Vector2Int>();
        private readonly HashSet<long> _edges = new HashSet<long>();
        private readonly SlimyLevelData _data = new SlimyLevelData { levelIndex = 1 };

        public int NodeId(int x, int z) => _ids[new Vector2Int(x, z)];

        public TestLevelBuilder Path(params Vector2Int[] cells)
        {
            for (int i = 0; i < cells.Length; i++)
            {
                Ensure(cells[i]);
                if (i == 0) continue;

                var delta = cells[i] - cells[i - 1];
                Debug.Assert(Mathf.Abs(delta.x) + Mathf.Abs(delta.y) == 1,
                    $"{cells[i - 1]} -> {cells[i]} is not one unit apart");
                Connect(_ids[cells[i - 1]], _ids[cells[i]]);
            }

            return this;
        }

        public TestLevelBuilder Line(int x0, int z0, int x1, int z1)
        {
            var cells = new List<Vector2Int>();
            if (x0 == x1)
            {
                var step = z1 >= z0 ? 1 : -1;
                for (var z = z0; z != z1 + step; z += step) cells.Add(new Vector2Int(x0, z));
            }
            else
            {
                var step = x1 >= x0 ? 1 : -1;
                for (var x = x0; x != x1 + step; x += step) cells.Add(new Vector2Int(x, z0));
            }

            return Path(cells.ToArray());
        }

        public TestLevelBuilder Rope(int id, RopeColor color, params Vector2Int[] cells)
        {
            var rope = new RopeData { id = id, color = color };
            for (int i = 0; i < cells.Length; i++) rope.occupiedNodeIds.Add(_ids[cells[i]]);
            _data.ropes.Add(rope);
            return this;
        }

        public TestLevelBuilder Hole(int id, RopeColor color, Vector2Int cell)
        {
            _data.holes.Add(new HoleData { id = id, color = color, nodeId = _ids[cell] });
            return this;
        }

        public TestLevelBuilder Wall(int id, Vector2Int cell, int ropeCollectionCount)
        {
            _data.walls.Add(new WallData
            {
                id = id,
                nodeId = _ids[cell],
                ropeCollectionCount = ropeCollectionCount
            });
            return this;
        }

        public SlimyLevelData BuildData()
        {
            _data.groundNodes.Clear();

            var connections = new Dictionary<int, List<int>>();
            foreach (var cell in _order) connections[_ids[cell]] = new List<int>();

            foreach (var edge in _edges)
            {
                var a = (int)(edge >> 32);
                var b = (int)(edge & 0xFFFFFFFF);
                connections[a].Add(b);
                connections[b].Add(a);
            }

            foreach (var cell in _order)
            {
                var id = _ids[cell];
                connections[id].Sort();
                _data.groundNodes.Add(new GroundNodeData
                {
                    id = id,
                    position = new Vector3(cell.x, 0f, cell.y),
                    connectedNodeIds = connections[id]
                });
            }

            return _data;
        }

        public SlimyLevelContext BuildContext(SlimyConfig config = null)
        {
            return SlimyLevelContext.Build(BuildData(), config ?? CreateConfig());
        }

        public static SlimyConfig CreateConfig()
        {
            var config = ScriptableObject.CreateInstance<SlimyConfig>();
            config.maximumMoveSpeed = 100f;
            config.snapDuration = 0.05f;
            config.segmentTargetHysteresis = 0f;
            return config;
        }

        private void Ensure(Vector2Int cell)
        {
            if (_ids.ContainsKey(cell)) return;
            _ids[cell] = 100 + _order.Count;
            _order.Add(cell);
        }

        private void Connect(int a, int b)
        {
            var low = Mathf.Min(a, b);
            var high = Mathf.Max(a, b);
            _edges.Add(((long)low << 32) | (uint)high);
        }

        public static Vector2Int C(int x, int z) => new Vector2Int(x, z);

        public static RopeModel FindRope(SlimyLevelContext context, int ropeId)
        {
            for (int i = 0; i < context.ActiveRopes.Count; i++)
            {
                if (context.ActiveRopes[i].RopeId == ropeId) return context.ActiveRopes[i];
            }

            return null;
        }
    }
}

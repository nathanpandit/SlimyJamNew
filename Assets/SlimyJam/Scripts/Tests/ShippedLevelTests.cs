using NUnit.Framework;
using SlimyJam.Data;
using SlimyJam.Gameplay;
using SlimyJam.Level;
using UnityEngine;

namespace SlimyJam.Tests
{
    /// <summary>Resources altındaki hazır level'lar yüklenebilir ve GDD 13.3 doğrulamasından geçmelidir.</summary>
    public sealed class ShippedLevelTests
    {
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        [TestCase(5)]
        public void ShippedLevelLoadsAndValidates(int levelIndex)
        {
            var asset = Resources.Load<TextAsset>($"SlimyLevels/Level_{levelIndex}");
            Assert.IsNotNull(asset, $"Level_{levelIndex}.json is missing from Resources/SlimyLevels.");

            var data = SlimyLevelJson.Parse(asset.text);
            Assert.IsNotNull(data);
            Assert.AreEqual(levelIndex, data.levelIndex);
            Assert.IsTrue(LevelDataValidator.Validate(data, out var error), error);

            var context = SlimyLevelContext.Build(data, TestLevelBuilder.CreateConfig());

            // Her rope için matching hole bulunmalı; aksi hâlde level çözülemez.
            for (int i = 0; i < context.ActiveRopes.Count; i++)
            {
                var rope = context.ActiveRopes[i];
                var hasHole = false;
                for (int h = 0; h < context.Holes.Count; h++)
                {
                    if (context.Holes[h].Color != rope.Color) continue;
                    hasHole = true;
                    break;
                }

                Assert.IsTrue(hasHole, $"Rope {rope.RopeId} ({rope.Color}) has no matching hole.");
            }
        }

        [Test]
        public void EveryNodeIsReachableFromEveryRopeEndpoint()
        {
            var asset = Resources.Load<TextAsset>("SlimyLevels/Level_4");
            var data = SlimyLevelJson.Parse(asset.text);
            var context = SlimyLevelContext.Build(data, TestLevelBuilder.CreateConfig());

            // Graph tek parça olmalı: her segment aynı bileşende.
            var visited = new System.Collections.Generic.HashSet<int>();
            var queue = new System.Collections.Generic.Queue<int>();
            queue.Enqueue(context.Graph.Nodes[0].Id);
            visited.Add(context.Graph.Nodes[0].Id);

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                var neighbours = context.Graph.GetNeighbours(current);
                for (int i = 0; i < neighbours.Count; i++)
                {
                    if (visited.Add(neighbours[i])) queue.Enqueue(neighbours[i]);
                }
            }

            Assert.AreEqual(context.Graph.Nodes.Count, visited.Count, "Level graph is not fully connected.");
        }

        [Test]
        public void RopeChainsAreGeometricallyOneUnitApart()
        {
            for (int levelIndex = 1; levelIndex <= 5; levelIndex++)
            {
                var asset = Resources.Load<TextAsset>($"SlimyLevels/Level_{levelIndex}");
                var data = SlimyLevelJson.Parse(asset.text);
                var context = SlimyLevelContext.Build(data, TestLevelBuilder.CreateConfig());

                for (int s = 0; s < context.Graph.Segments.Count; s++)
                {
                    var segment = context.Graph.Segments[s];
                    Assert.AreEqual(1f, segment.Length, 0.02f,
                        $"Level {levelIndex}: segment {segment.NodeAId}-{segment.NodeBId} is not one world unit.");
                }
            }
        }

        [Test]
        public void RopeCanReachItsHoleOnAnEmptyBoard()
        {
            // Level 1 tutorial: tek rope, engelsiz koridor. Tail ucundan hole'a rota bulunmalı.
            var asset = Resources.Load<TextAsset>("SlimyLevels/Level_1");
            var data = SlimyLevelJson.Parse(asset.text);
            var context = SlimyLevelContext.Build(data, TestLevelBuilder.CreateConfig());

            var rope = context.ActiveRopes[0];
            var hole = context.Holes[0];
            var path = new System.Collections.Generic.List<int>();

            Assert.IsTrue(context.Pathfinding.TryFindPath(rope, RopeEnd.Tail, hole.NodeId, path),
                "Tutorial level must be solvable by dragging the tail into the hole.");
            Assert.AreEqual(hole.NodeId, path[path.Count - 1]);
        }
    }
}

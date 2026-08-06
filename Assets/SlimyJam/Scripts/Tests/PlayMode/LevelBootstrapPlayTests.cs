using System.Collections;
using NUnit.Framework;
using SlimyJam.Gameplay;
using SlimyJam.Level;
using UnityEngine;
using UnityEngine.TestTools;

namespace SlimyJam.Tests
{
    /// <summary>
    /// Uçtan uca bootstrap: generator gerçekten graph, hole, rope ve Dreamteck spline nesnelerini kuruyor mu,
    /// oyun Playing state'ine geçiyor mu? (GDD 13.2 yükleme sırası)
    /// </summary>
    public sealed class LevelBootstrapPlayTests
    {
        private GameObject _root;
        private GameObject _cameraObject;

        [SetUp]
        public void SetUp()
        {
            _cameraObject = new GameObject("TestCamera") { tag = "MainCamera" };
            var camera = _cameraObject.AddComponent<Camera>();
            camera.transform.rotation = Quaternion.Euler(55f, 0f, 0f);
            _cameraObject.AddComponent<SlimyCameraController>();

            _root = new GameObject("SlimyJamTestRoot");
        }

        [TearDown]
        public void TearDown()
        {
            if (_root != null) Object.DestroyImmediate(_root);
            if (_cameraObject != null) Object.DestroyImmediate(_cameraObject);
        }

        [UnityTest]
        public IEnumerator GeneratorBuildsPlayableLevelFromResources()
        {
            _root.AddComponent<SlimyGameManager>();
            var generator = _root.AddComponent<SlimyLevelGenerator>();

            // Start() generator'ı kendisi çalıştırır; bir frame bekleyip sonucu doğrula.
            yield return null;

            var gameManager = _root.GetComponent<SlimyGameManager>();

            Assert.AreEqual(SlimyGameState.Playing, gameManager.State,
                "Level should be loaded and gameplay started.");
            Assert.IsNotNull(generator.Context);
            Assert.Greater(generator.Context.Graph.Nodes.Count, 0);
            Assert.AreEqual(1, gameManager.Ropes.Count, "Level_1 has a single rope.");

            var rope = gameManager.Ropes[0];
            Assert.IsNotNull(rope.Model);
            Assert.AreEqual(3, rope.Model.Length);
            Assert.IsNotNull(rope.SplineAdapter, "Rope must have a spline adapter.");
            Assert.IsNotNull(rope.GetComponentInChildren<Dreamteck.Splines.SplineComputer>(),
                "Dreamteck spline was not created for the rope.");
            Assert.IsNotNull(rope.GetComponentInChildren<Dreamteck.Splines.TubeGenerator>(),
                "Rope body mesh generator was not created.");

            // Occupancy: rope'un tuttuğu node'lar dolu, aradaki koridor boş olmalı.
            var occupancy = generator.Context.Occupancy;
            for (int i = 0; i < rope.Model.Length; i++)
            {
                Assert.IsFalse(occupancy.IsFree(rope.Model.Nodes[i]));
            }

            Assert.AreEqual(1, generator.Context.Holes.Count);
            Assert.IsFalse(occupancy.IsFree(generator.Context.Holes[0].NodeId), "Hole occupies its node.");
        }

        [UnityTest]
        public IEnumerator DraggingTailIntoHoleCompletesTheLevel()
        {
            _root.AddComponent<SlimyGameManager>();
            var generator = _root.AddComponent<SlimyLevelGenerator>();

            yield return null;

            var gameManager = _root.GetComponent<SlimyGameManager>();
            var rope = gameManager.Ropes[0];
            var context = generator.Context;
            var holePosition = context.Graph.GetPosition(context.Holes[0].NodeId);

            rope.BeginDrag(RopeEnd.Tail);

            // Parmağı hole'un üzerinde tutup rope'un yolu kat etmesini bekle.
            var guard = 0;
            while (gameManager.State == SlimyGameState.Playing && guard++ < 600)
            {
                rope.Drag(holePosition, 0.02f);
                yield return null;
            }

            Assert.AreNotEqual(SlimyGameState.Playing, gameManager.State,
                "Dragging the tail onto the matching hole should finish the level.");
            Assert.AreEqual(0, context.ActiveRopes.Count, "All ropes collected.");
        }
    }
}

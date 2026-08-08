using System.Collections.Generic;
using NUnit.Framework;
using SlimyJam.Core;
using SlimyJam.Data;
using SlimyJam.Gameplay;
using SlimyJam.InputSystem;
using SlimyJam.Level;
using UnityEngine;
using static SlimyJam.Tests.TestLevelBuilder;

namespace SlimyJam.Tests
{
    /// <summary>GDD 17'deki acceptance senaryolarının otomatik karşılıkları.</summary>
    public sealed class AcceptanceTests
    {
        // ---------------------------------------------------------------- SEL

        private sealed class FakeEndpoints : IEndpointCandidateSource
        {
            public readonly List<(Vector3 head, Vector3 tail)> Ropes = new List<(Vector3, Vector3)>();
            public int Count => Ropes.Count;
            public bool IsSelectable(int index) => true;

            public Vector3 GetEndpointPosition(int index, RopeEnd end)
            {
                return end == RopeEnd.Head ? Ropes[index].head : Ropes[index].tail;
            }
        }

        [Test]
        public void SEL_01_ClosestEndpointWithinRadiusIsSelected()
        {
            var source = new FakeEndpoints();
            source.Ropes.Add((new Vector3(0f, 0f, 0f), new Vector3(3f, 0f, 0f)));
            source.Ropes.Add((new Vector3(1f, 0f, 0f), new Vector3(5f, 0f, 0f)));

            var selected = EndpointSelector.TrySelect(source, new Vector3(0.9f, 0f, 0f), 1.25f,
                out var index, out var end);

            Assert.IsTrue(selected);
            Assert.AreEqual(1, index, "Physically closest endpoint wins.");
            Assert.AreEqual(RopeEnd.Head, end);
        }

        [Test]
        public void SEL_02_EqualDistanceKeepsRopeListOrder()
        {
            var source = new FakeEndpoints();
            source.Ropes.Add((new Vector3(-1f, 0f, 0f), new Vector3(-5f, 0f, 0f)));
            source.Ropes.Add((new Vector3(1f, 0f, 0f), new Vector3(5f, 0f, 0f)));

            EndpointSelector.TrySelect(source, Vector3.zero, 1.25f, out var index, out var end);

            Assert.AreEqual(0, index, "Earlier rope in list order wins the tie.");
            Assert.AreEqual(RopeEnd.Head, end);
        }

        [Test]
        public void SEL_03_PointerNearBodyOnlySelectsNothing()
        {
            var source = new FakeEndpoints();
            source.Ropes.Add((new Vector3(0f, 0f, 0f), new Vector3(6f, 0f, 0f)));

            var selected = EndpointSelector.TrySelect(source, new Vector3(3f, 0f, 0f), 1.25f, out _, out _);

            Assert.IsFalse(selected, "Body segments are not selection candidates.");
        }

        // --------------------------------------------------------------- PATH

        [Test]
        public void PATH_02_PointerOutsideGraphProjectsToNearestSegment()
        {
            var builder = new TestLevelBuilder().Line(0, 0, 4, 0);
            builder.Rope(1, RopeColor.Green, C(0, 0), C(1, 0));
            var context = builder.BuildContext();

            var found = context.Graph.TryFindNearestSegment(new Vector3(2.5f, 0f, 9f), out var segment,
                out var projection, out _);

            Assert.IsTrue(found);
            Assert.AreEqual(new Vector3(2.5f, 0f, 0f), projection);
            Assert.IsTrue(segment.Contains(builder.NodeId(2, 0)) && segment.Contains(builder.NodeId(3, 0)));
        }

        [Test]
        public void PATH_04_BlockedNodeTruncatesPathToLastAvailableNode()
        {
            // Koridor: 0..5. Yabancı rope 3 ve 4'ü tutuyor; yeşil rope 3'ten öteye geçemez.
            var builder = new TestLevelBuilder().Line(0, 0, 5, 0);
            builder.Rope(1, RopeColor.Green, C(1, 0), C(0, 0));
            builder.Rope(2, RopeColor.Blue, C(3, 0), C(4, 0));
            var context = builder.BuildContext();

            var green = FindRope(context, 1);
            var path = new List<int>();
            var complete = context.Pathfinding.TryFindPath(green, RopeEnd.Head, builder.NodeId(5, 0), path);

            Assert.IsFalse(complete, "No valid route exists past the blocking rope.");
            CollectionAssert.AreEqual(new[] { builder.NodeId(2, 0) }, path,
                "Rope advances to the last available node before the blocked one.");
        }

        // --------------------------------------------------------------- MOVE

        [Test]
        public void MOVE_01_HeadForwardShiftsChainFromFront()
        {
            var builder = new TestLevelBuilder().Line(0, 0, 5, 0);
            builder.Rope(1, RopeColor.Green, C(3, 0), C(2, 0), C(1, 0));
            var context = builder.BuildContext();
            var rope = FindRope(context, 1);

            var plan = new StepPlan();
            Assert.IsTrue(context.StepPlanner.TryPlanStep(rope, RopeEnd.Head, builder.NodeId(4, 0), plan));
            Assert.IsTrue(context.StepPlanner.TryCommit(rope, plan));

            CollectionAssert.AreEqual(
                new[] { builder.NodeId(4, 0), builder.NodeId(3, 0), builder.NodeId(2, 0) },
                rope.Nodes);
            Assert.IsTrue(context.Occupancy.IsFree(builder.NodeId(1, 0)), "Tail node is released.");
        }

        [Test]
        public void MOVE_02_TailForwardShiftsChainFromBack()
        {
            var builder = new TestLevelBuilder().Line(0, 0, 5, 0);
            builder.Rope(1, RopeColor.Green, C(1, 0), C(2, 0), C(3, 0));
            var context = builder.BuildContext();
            var rope = FindRope(context, 1);

            var plan = new StepPlan();
            Assert.IsTrue(context.StepPlanner.TryPlanStep(rope, RopeEnd.Tail, builder.NodeId(4, 0), plan));
            Assert.IsTrue(context.StepPlanner.TryCommit(rope, plan));

            CollectionAssert.AreEqual(
                new[] { builder.NodeId(2, 0), builder.NodeId(3, 0), builder.NodeId(4, 0) },
                rope.Nodes);
            Assert.IsTrue(context.Occupancy.IsFree(builder.NodeId(1, 0)), "Head node is released.");
        }

        [Test]
        public void MOVE_03_OccupancyUpdatesOnlyAfterMidpoint()
        {
            var builder = new TestLevelBuilder().Line(0, 0, 6, 0);
            builder.Rope(1, RopeColor.Green, C(2, 0), C(1, 0), C(0, 0));
            var config = CreateConfig();
            config.maximumMoveSpeed = 1f;
            var context = builder.BuildContext(config);
            var rope = FindRope(context, 1);
            var movement = context.CreateMovementController(rope);

            movement.BeginDrag(RopeEnd.Head);

            // 0.3 birim ilerle: midpoint geçilmedi.
            movement.Tick(new Vector3(6f, 0f, 0f), 0.3f);
            Assert.AreEqual(builder.NodeId(2, 0), rope.HeadNodeId, "Occupancy holds before the midpoint.");
            Assert.IsTrue(context.Occupancy.IsFree(builder.NodeId(3, 0)));

            // Toplam 0.6 birim: midpoint geçildi.
            movement.Tick(new Vector3(6f, 0f, 0f), 0.3f);
            Assert.AreEqual(builder.NodeId(3, 0), rope.HeadNodeId, "Destination is taken after the midpoint.");
        }

        [Test]
        public void MOVE_04_ReleaseBeforeMidpointSnapsBackToSourceNode()
        {
            var builder = new TestLevelBuilder().Line(0, 0, 6, 0);
            builder.Rope(1, RopeColor.Green, C(2, 0), C(1, 0), C(0, 0));
            var config = CreateConfig();
            config.maximumMoveSpeed = 1f;
            var context = builder.BuildContext(config);
            var rope = FindRope(context, 1);
            var movement = context.CreateMovementController(rope);

            movement.BeginDrag(RopeEnd.Head);
            movement.Tick(new Vector3(6f, 0f, 0f), 0.3f);
            movement.BeginRelease();

            for (int i = 0; i < 10; i++) movement.Tick(Vector3.zero, 0.05f);

            Assert.AreEqual(builder.NodeId(2, 0), rope.HeadNodeId);
            Assert.AreEqual(context.Graph.GetPosition(builder.NodeId(2, 0)),
                movement.GetEndpointPosition(RopeEnd.Head));
        }

        [Test]
        public void MOVE_05_ReleaseAfterMidpointSnapsToDestinationNode()
        {
            var builder = new TestLevelBuilder().Line(0, 0, 6, 0);
            builder.Rope(1, RopeColor.Green, C(2, 0), C(1, 0), C(0, 0));
            var config = CreateConfig();
            config.maximumMoveSpeed = 1f;
            var context = builder.BuildContext(config);
            var rope = FindRope(context, 1);
            var movement = context.CreateMovementController(rope);

            movement.BeginDrag(RopeEnd.Head);
            movement.Tick(new Vector3(6f, 0f, 0f), 0.7f);
            movement.BeginRelease();

            for (int i = 0; i < 10; i++) movement.Tick(Vector3.zero, 0.05f);

            Assert.AreEqual(builder.NodeId(3, 0), rope.HeadNodeId);
            Assert.AreEqual(context.Graph.GetPosition(builder.NodeId(3, 0)),
                movement.GetEndpointPosition(RopeEnd.Head));
        }

        // ---------------------------------------------------------------- REV

        [Test]
        public void REV_01_HeadIntoOwnBodyExtendsFreeTailStraightAhead()
        {
            // A(1,0) B(2,0) C(3,0) D(4,0) -> tail düz devamı E(5,0)
            var builder = new TestLevelBuilder().Line(0, 0, 6, 0);
            builder.Rope(1, RopeColor.Green, C(1, 0), C(2, 0), C(3, 0), C(4, 0));
            var context = builder.BuildContext();
            var rope = FindRope(context, 1);

            var plan = new StepPlan();
            Assert.IsTrue(context.StepPlanner.TryPlanStep(rope, RopeEnd.Head, builder.NodeId(2, 0), plan));
            Assert.AreEqual(StepKind.Reverse, plan.Kind);
            Assert.IsTrue(context.StepPlanner.TryCommit(rope, plan));

            CollectionAssert.AreEqual(
                new[] { builder.NodeId(2, 0), builder.NodeId(3, 0), builder.NodeId(4, 0), builder.NodeId(5, 0) },
                rope.Nodes);
            Assert.IsTrue(context.Occupancy.IsFree(builder.NodeId(1, 0)));
        }

        [Test]
        public void REV_02_WithoutStraightCandidateSmallestUnsignedAngleWins()
        {
            // Tail (2,0)'da; düz devam (3,0) yok. Adaylar: (2,1) ve (2,-1) -> ikisi de 90 derece.
            // Bu testte 90 derecelik tek aday bırakmak için (2,-1) yolunu açmıyoruz.
            var builder = new TestLevelBuilder()
                .Line(0, 0, 2, 0)
                .Line(2, 0, 2, 2);
            builder.Rope(1, RopeColor.Green, C(0, 0), C(1, 0), C(2, 0));
            var context = builder.BuildContext();
            var rope = FindRope(context, 1);

            var candidate = context.FreeEndpointChooser.Choose(rope, RopeEnd.Tail);

            Assert.AreEqual(builder.NodeId(2, 1), candidate);
        }

        [Test]
        public void REV_03_EqualAnglesResolveByConnectedNodeListOrder()
        {
            // Tail (2,0); düz yok, iki simetrik 90 derece aday: (2,1) ve (2,-1).
            var builder = new TestLevelBuilder()
                .Line(0, 0, 2, 0)
                .Line(2, 0, 2, 2)
                .Line(2, 0, 2, -2);
            builder.Rope(1, RopeColor.Green, C(0, 0), C(1, 0), C(2, 0));
            var context = builder.BuildContext();
            var rope = FindRope(context, 1);

            var tailNode = context.Graph.GetNode(builder.NodeId(2, 0));
            var candidate = context.FreeEndpointChooser.Choose(rope, RopeEnd.Tail);

            var expected = -1;
            for (int i = 0; i < tailNode.ConnectedNodeIds.Count; i++)
            {
                var id = tailNode.ConnectedNodeIds[i];
                if (id == builder.NodeId(2, 1) || id == builder.NodeId(2, -1))
                {
                    expected = id;
                    break;
                }
            }

            Assert.AreEqual(expected, candidate, "connectedNodeIds order decides symmetric ties.");
        }

        [Test]
        public void REV_04_NoFreeEndpointCandidateRejectsReverseStep()
        {
            // Tail (2,0) çıkmaz uçta: reverse için aday yok.
            var builder = new TestLevelBuilder().Line(0, 0, 2, 0);
            builder.Rope(1, RopeColor.Green, C(0, 0), C(1, 0), C(2, 0));
            var context = builder.BuildContext();
            var rope = FindRope(context, 1);

            var plan = new StepPlan();
            var planned = context.StepPlanner.TryPlanStep(rope, RopeEnd.Head, builder.NodeId(1, 0), plan);

            Assert.IsFalse(planned);
            CollectionAssert.AreEqual(
                new[] { builder.NodeId(0, 0), builder.NodeId(1, 0), builder.NodeId(2, 0) },
                rope.Nodes);
        }

        // -------------------------------------------------------------- BLOCK

        [Test]
        public void BLOCK_01_OtherRopeBlocksDestination()
        {
            var builder = new TestLevelBuilder().Line(0, 0, 5, 0);
            builder.Rope(1, RopeColor.Green, C(1, 0), C(0, 0));
            builder.Rope(2, RopeColor.Blue, C(2, 0), C(3, 0));
            var context = builder.BuildContext();
            var rope = FindRope(context, 1);

            var plan = new StepPlan();
            Assert.IsFalse(context.StepPlanner.TryPlanStep(rope, RopeEnd.Head, builder.NodeId(2, 0), plan));
        }

        [Test]
        public void BLOCK_02_NonAdjacentOwnBodyIsNotTraversable()
        {
            // U şeklinde rope: head (0,0), body (0,1),(1,1), tail (1,0). Head'in komşusu (1,0) = kendi tail'i.
            var builder = new TestLevelBuilder()
                .Path(C(0, 0), C(0, 1), C(1, 1), C(1, 0))
                .Path(C(0, 0), C(1, 0));
            builder.Rope(1, RopeColor.Green, C(0, 0), C(0, 1), C(1, 1), C(1, 0));
            var context = builder.BuildContext();
            var rope = FindRope(context, 1);

            var plan = new StepPlan();
            Assert.IsFalse(context.StepPlanner.TryPlanStep(rope, RopeEnd.Head, builder.NodeId(1, 0), plan),
                "Only the directly adjacent body node may be entered by the reverse rule.");
        }

        // --------------------------------------------------------------- HOLE

        [Test]
        public void HOLE_01_WrongColourHoleBlocksLikeAnyOccupiedNode()
        {
            var builder = new TestLevelBuilder().Line(0, 0, 4, 0);
            builder.Rope(1, RopeColor.Green, C(1, 0), C(0, 0));
            builder.Hole(10, RopeColor.Blue, C(2, 0));
            var context = builder.BuildContext();
            var rope = FindRope(context, 1);

            var plan = new StepPlan();
            Assert.IsFalse(context.StepPlanner.TryPlanStep(rope, RopeEnd.Head, builder.NodeId(2, 0), plan));
        }

        [Test]
        public void HOLE_02_MatchingHoleStepIsTerminalAndStartsCollection()
        {
            var builder = new TestLevelBuilder().Line(0, 0, 4, 0);
            builder.Rope(1, RopeColor.Green, C(1, 0), C(0, 0));
            builder.Hole(10, RopeColor.Green, C(2, 0));
            var config = CreateConfig();
            config.maximumMoveSpeed = 1f;
            var context = builder.BuildContext(config);
            var rope = FindRope(context, 1);
            var movement = context.CreateMovementController(rope);

            RopeModel collected = null;
            var collectedHoleId = -1;
            movement.CollectionTriggered += (model, holeId, _) =>
            {
                collected = model;
                collectedHoleId = holeId;
            };

            movement.BeginDrag(RopeEnd.Head);
            movement.Tick(new Vector3(2f, 0f, 0f), 0.4f);
            Assert.IsNull(collected, "Collection only starts after the midpoint.");

            movement.Tick(new Vector3(2f, 0f, 0f), 0.4f);

            Assert.AreSame(rope, collected);
            Assert.AreEqual(10, collectedHoleId);
            Assert.IsFalse(movement.IsDragging, "Rope control is cut immediately.");
        }

        [Test]
        public void HOLE_03_InactiveEndpointAdjacentToHoleTriggersReleaseCollection()
        {
            // Tail (2,0), hole (3,0)'a connected. Active endpoint head'dir.
            var builder = new TestLevelBuilder().Line(0, 0, 3, 0);
            builder.Rope(1, RopeColor.Green, C(0, 0), C(1, 0), C(2, 0));
            builder.Hole(10, RopeColor.Green, C(3, 0));
            var context = builder.BuildContext();
            var rope = FindRope(context, 1);

            var hole = context.Collection.FindReleaseCollectionTarget(rope, RopeEnd.Head);

            Assert.IsNotNull(hole);
            Assert.AreEqual(10, hole.HoleId);
        }

        [Test]
        public void HOLE_04_HoleStaysBlockingWhileSameColourRopeRemains()
        {
            var builder = new TestLevelBuilder().Line(0, 0, 6, 0);
            builder.Rope(1, RopeColor.Green, C(0, 0), C(1, 0));
            builder.Rope(2, RopeColor.Green, C(4, 0), C(5, 0));
            builder.Hole(10, RopeColor.Green, C(3, 0));
            var context = builder.BuildContext();
            var first = FindRope(context, 1);

            context.Collection.BeginCollection(first, context.Collection.GetHole(10));

            Assert.AreEqual(1, context.Collection.Holes.Count, "Hole remains while a green rope is on the board.");
            Assert.IsFalse(context.Occupancy.IsFree(builder.NodeId(3, 0)), "Hole node stays occupied.");
        }

        [Test]
        public void HOLE_05_LastRopeOfColourRemovesAllHolesOfThatColour()
        {
            var builder = new TestLevelBuilder().Line(0, 0, 6, 0);
            builder.Rope(1, RopeColor.Green, C(0, 0), C(1, 0));
            builder.Rope(2, RopeColor.Blue, C(5, 0), C(6, 0));
            builder.Hole(10, RopeColor.Green, C(3, 0));
            builder.Hole(11, RopeColor.Blue, C(4, 0));
            var context = builder.BuildContext();
            var green = FindRope(context, 1);

            context.Collection.BeginCollection(green, context.Collection.GetHole(10));

            Assert.AreEqual(1, context.Collection.Holes.Count);
            Assert.IsTrue(context.Occupancy.IsFree(builder.NodeId(3, 0)), "Removed hole frees its node.");
            Assert.IsFalse(context.Occupancy.IsFree(builder.NodeId(4, 0)), "Other colour hole is untouched.");
        }

        // ---------------------------------------------------------------- WIN

        [Test]
        public void WIN_01_LastRopeCollectedRaisesLevelCompleted()
        {
            var builder = new TestLevelBuilder().Line(0, 0, 4, 0);
            builder.Rope(1, RopeColor.Green, C(0, 0), C(1, 0));
            builder.Hole(10, RopeColor.Green, C(3, 0));
            var context = builder.BuildContext();

            var completed = false;
            context.Collection.LevelCompleted += () => completed = true;

            context.Collection.BeginCollection(FindRope(context, 1), context.Collection.GetHole(10));

            Assert.IsTrue(completed);
            Assert.AreEqual(0, context.ActiveRopes.Count);
        }

        // --------------------------------------------------------------- ELEMENTS

        [Test]
        public void ELEM_01_HiddenRopeAndHoleBlockUntilCollectionReveal()
        {
            var builder = new TestLevelBuilder()
                .Line(0, 0, 5, 0)
                .Line(10, 0, 12, 0);
            builder.Rope(1, RopeColor.Green, C(0, 0), C(1, 0));
            builder.Hole(10, RopeColor.Green, C(2, 0));
            builder.Rope(2, RopeColor.Blue, C(10, 0), C(11, 0));
            builder.Hole(11, RopeColor.Blue, C(12, 0));
            var data = builder.BuildData();
            data.ropes[0].hidden = true;
            data.ropes[0].revealAfterCollections = 1;
            data.holes[0].hidden = true;
            data.holes[0].revealAfterCollections = 1;

            var context = SlimyLevelContext.Build(data, CreateConfig());
            var green = FindRope(context, 1);

            Assert.AreEqual(NodeTraversal.BlockedByHole,
                TraversalRules.Evaluate(context.Occupancy, builder.NodeId(2, 0), green));

            context.Collection.BeginCollection(FindRope(context, 2), context.Collection.GetHole(11));

            Assert.IsTrue(green.IsColorRevealed);
            Assert.IsTrue(context.Collection.GetHole(10).IsColorRevealed);
            Assert.AreEqual(NodeTraversal.MatchingHole,
                TraversalRules.Evaluate(context.Occupancy, builder.NodeId(2, 0), green));
        }

        [Test]
        public void ELEM_02_KeyedRopeDecrementsEveryLockedHole()
        {
            var builder = new TestLevelBuilder()
                .Line(0, 0, 3, 0)
                .Line(10, 0, 12, 0)
                .Line(20, 0, 22, 0);
            builder.Rope(1, RopeColor.Green, C(0, 0), C(1, 0));
            builder.Hole(10, RopeColor.Green, C(3, 0));
            builder.Rope(2, RopeColor.Blue, C(10, 0), C(11, 0));
            builder.Hole(11, RopeColor.Blue, C(12, 0));
            builder.Rope(3, RopeColor.Yellow, C(20, 0), C(21, 0));
            builder.Hole(12, RopeColor.Yellow, C(22, 0));
            var data = builder.BuildData();
            data.ropes[0].hasKey = true;
            data.holes[1].lockedKeyCount = 2;
            data.holes[2].lockedKeyCount = 1;

            var context = SlimyLevelContext.Build(data, CreateConfig());

            context.Collection.BeginCollection(FindRope(context, 1), context.Collection.GetHole(10));

            Assert.AreEqual(1, context.Collection.GetHole(11).LockKeysRemaining);
            Assert.IsTrue(context.Collection.GetHole(12).IsUnlocked);
        }

        [Test]
        public void ELEM_03_WallBlocksNodeUntilEnoughRopesCollected()
        {
            var builder = new TestLevelBuilder()
                .Line(0, 0, 4, 0)
                .Line(10, 0, 12, 0);
            builder.Rope(1, RopeColor.Green, C(0, 0), C(1, 0));
            builder.Hole(10, RopeColor.Green, C(4, 0));
            builder.Wall(1000, C(2, 0), 1);
            builder.Rope(2, RopeColor.Blue, C(10, 0), C(11, 0));
            builder.Hole(11, RopeColor.Blue, C(12, 0));

            var context = builder.BuildContext();
            var green = FindRope(context, 1);

            Assert.AreEqual(NodeTraversal.BlockedByWall,
                TraversalRules.Evaluate(context.Occupancy, builder.NodeId(2, 0), green));

            context.Collection.BeginCollection(FindRope(context, 2), context.Collection.GetHole(11));

            Assert.IsTrue(context.Occupancy.IsFree(builder.NodeId(2, 0)));
            Assert.AreEqual(0, context.Elements.Walls.Count);
        }

        [Test]
        public void ELEM_04_ContainedRopeReleasesWhenOuterIsCollected()
        {
            var builder = new TestLevelBuilder().Line(0, 0, 4, 0);
            builder.Rope(1, RopeColor.Green, C(0, 0), C(1, 0), C(2, 0));
            builder.Rope(2, RopeColor.Blue, C(1, 0), C(2, 0));
            builder.Hole(10, RopeColor.Green, C(4, 0));
            var data = builder.BuildData();
            data.ropes[1].containedByRopeId = 1;

            var context = SlimyLevelContext.Build(data, CreateConfig());
            var containedBefore = (IRopeOccupant)context.Occupancy.GetOccupant(builder.NodeId(1, 0));
            Assert.AreEqual(1, containedBefore.RopeId);

            var completed = false;
            context.Collection.LevelCompleted += () => completed = true;
            context.Collection.BeginCollection(FindRope(context, 1), context.Collection.GetHole(10));

            var inner = FindRope(context, 2);
            var containedAfter = (IRopeOccupant)context.Occupancy.GetOccupant(builder.NodeId(1, 0));
            Assert.IsFalse(inner.IsContained);
            Assert.AreEqual(2, containedAfter.RopeId);
            Assert.AreEqual(1, context.ActiveRopes.Count);
            Assert.IsFalse(completed);
        }

        [Test]
        public void ELEM_05_FrozenRopeUnfreezesAfterEnoughRopesCollected()
        {
            var builder = new TestLevelBuilder()
                .Line(0, 0, 4, 0)
                .Line(10, 0, 12, 0);
            builder.Rope(1, RopeColor.Green, C(0, 0), C(1, 0));
            builder.Hole(10, RopeColor.Green, C(4, 0));
            builder.Rope(2, RopeColor.Blue, C(10, 0), C(11, 0));
            builder.Hole(11, RopeColor.Blue, C(12, 0));
            var data = builder.BuildData();
            data.ropes[0].frozen = true;
            data.ropes[0].unfreezeAfterCollections = 1;

            var context = SlimyLevelContext.Build(data, CreateConfig());
            var frozen = FindRope(context, 1);

            Assert.IsTrue(frozen.IsFrozen);

            context.Collection.BeginCollection(FindRope(context, 2), context.Collection.GetHole(11));

            Assert.IsFalse(frozen.IsFrozen);
        }

        // --------------------------------------------------------------- LOAD

        [Test]
        public void LOAD_01_ValidJsonBuildsGraphOccupancyAndHoles()
        {
            var builder = new TestLevelBuilder().Line(0, 0, 4, 0);
            builder.Rope(1, RopeColor.Green, C(0, 0), C(1, 0));
            builder.Hole(10, RopeColor.Green, C(4, 0));
            var data = builder.BuildData();

            Assert.IsTrue(LevelDataValidator.Validate(data, out var error), error);

            var context = SlimyLevelContext.Build(data, CreateConfig());

            Assert.AreEqual(5, context.Graph.Nodes.Count);
            Assert.AreEqual(1, context.ActiveRopes.Count);
            Assert.IsFalse(context.Occupancy.IsFree(builder.NodeId(0, 0)));
            Assert.IsFalse(context.Occupancy.IsFree(builder.NodeId(4, 0)));
            Assert.IsTrue(context.Occupancy.IsFree(builder.NodeId(2, 0)));
        }

        [Test]
        public void LOAD_02_MissingNodeIdProducesLoadError()
        {
            var builder = new TestLevelBuilder().Line(0, 0, 3, 0);
            builder.Rope(1, RopeColor.Green, C(0, 0), C(1, 0));
            var data = builder.BuildData();
            data.ropes[0].occupiedNodeIds[1] = 9999;

            Assert.IsFalse(LevelDataValidator.Validate(data, out var error));
            StringAssert.Contains("9999", error);
        }

        [Test]
        public void LOAD_03_ColourNamesInJsonAreParsed()
        {
            const string json = "{\"levelIndex\":7,\"groundNodes\":[" +
                                "{\"id\":100,\"position\":{\"x\":0,\"y\":0,\"z\":0},\"connectedNodeIds\":[101]}," +
                                "{\"id\":101,\"position\":{\"x\":1,\"y\":0,\"z\":0},\"connectedNodeIds\":[100]}]," +
                                "\"ropes\":[{\"id\":1,\"color\":\"Green\",\"occupiedNodeIds\":[100]}]," +
                                "\"holes\":[{\"id\":10,\"color\":\"Green\",\"nodeId\":101}]}";

            var data = SlimyLevelJson.Parse(json);

            Assert.AreEqual(7, data.levelIndex);
            Assert.AreEqual(RopeColor.Green, data.ropes[0].color);
            Assert.AreEqual(RopeColor.Green, data.holes[0].color);
        }
    }
}

using System.Collections.Generic;
using SlimyJam.Core;

namespace SlimyJam.Gameplay
{
    /// <summary>
    /// Logical adımların validate + commit aşamalarını yönetir (GDD 9.3).
    /// Bir adımın gerekli occupancy değişimlerinden biri bile başarısızsa hiçbir kısmi değişiklik uygulanmaz.
    /// </summary>
    public sealed class StepPlanner
    {
        private readonly GraphRepository _graph;
        private readonly NodeOccupancyMap _occupancy;
        private readonly FreeEndpointChooser _freeEndpointChooser;

        public StepPlanner(GraphRepository graph, NodeOccupancyMap occupancy, FreeEndpointChooser freeEndpointChooser)
        {
            _graph = graph;
            _occupancy = occupancy;
            _freeEndpointChooser = freeEndpointChooser;
        }

        /// <summary>
        /// Active endpoint'in <paramref name="destinationNodeId"/> node'una geçişini planlar.
        /// Occupancy'yi değiştirmez; yalnızca geçerli bir adım tanımlar.
        /// </summary>
        public bool TryPlanStep(RopeModel rope, RopeEnd activeEnd, int destinationNodeId, StepPlan plan)
        {
            plan.Reset();

            var activeNodeId = rope.GetEndpointNodeId(activeEnd);
            if (destinationNodeId == activeNodeId) return false;
            if (!_graph.AreConnected(activeNodeId, destinationNodeId)) return false;

            var traversal = TraversalRules.Evaluate(_occupancy, destinationNodeId, rope);

            switch (traversal)
            {
                case NodeTraversal.Free:
                    return PlanForward(rope, activeEnd, destinationNodeId, -1, plan);

                case NodeTraversal.MatchingHole:
                    var hole = (IHoleOccupant)_occupancy.GetOccupant(destinationNodeId);
                    return PlanForward(rope, activeEnd, destinationNodeId, hole.HoleId, plan);

                case NodeTraversal.OwnBody:
                    return PlanReverse(rope, activeEnd, destinationNodeId, plan);

                default:
                    return false;
            }
        }

        private bool PlanForward(RopeModel rope, RopeEnd activeEnd, int destinationNodeId, int collectionHoleId,
            StepPlan plan)
        {
            rope.CopyNodesTo(plan.FromChain);
            var length = plan.FromChain.Count;

            if (activeEnd == RopeEnd.Head)
            {
                // [A,B,C,D] -> [X,A,B,C]
                plan.ToChain.Add(destinationNodeId);
                for (int i = 0; i < length - 1; i++) plan.ToChain.Add(plan.FromChain[i]);
            }
            else
            {
                // [A,B,C,D] -> [B,C,D,X]
                for (int i = 1; i < length; i++) plan.ToChain.Add(plan.FromChain[i]);
                plan.ToChain.Add(destinationNodeId);
            }

            plan.Kind = StepKind.Forward;
            plan.ActiveEnd = activeEnd;
            plan.DestinationNodeId = destinationNodeId;
            plan.CollectionHoleId = collectionHoleId;
            plan.CollectionEnd = activeEnd;
            return true;
        }

        private bool PlanReverse(RopeModel rope, RopeEnd activeEnd, int destinationNodeId, StepPlan plan)
        {
            // Yalnızca doğrudan adjacent body node'una reverse step atılabilir (GDD 8.7).
            if (rope.Length < 2) return false;
            if (rope.GetNodeFromEnd(activeEnd, 1) != destinationNodeId) return false;

            var freeEnd = rope.Opposite(activeEnd);
            var candidate = _freeEndpointChooser.Choose(rope, freeEnd);
            if (candidate < 0) return false;

            rope.CopyNodesTo(plan.FromChain);
            var length = plan.FromChain.Count;

            if (activeEnd == RopeEnd.Head)
            {
                // [A,B,C,D] -> [B,C,D,E]
                for (int i = 1; i < length; i++) plan.ToChain.Add(plan.FromChain[i]);
                plan.ToChain.Add(candidate);
            }
            else
            {
                // [A,B,C,D] -> [E,A,B,C]
                plan.ToChain.Add(candidate);
                for (int i = 0; i < length - 1; i++) plan.ToChain.Add(plan.FromChain[i]);
            }

            plan.Kind = StepKind.Reverse;
            plan.ActiveEnd = activeEnd;
            plan.DestinationNodeId = destinationNodeId;
            plan.CollectionEnd = freeEnd;

            // Serbest uç matching hole'a girerse genel collection kuralı çalışır (GDD 8.7).
            if (_occupancy.GetOccupant(candidate) is IHoleOccupant hole && hole.Color == rope.Color)
            {
                plan.CollectionHoleId = hole.HoleId;
            }

            return true;
        }

        /// <summary>
        /// Adımı atomik olarak uygular: önce yeni node'ların hâlâ alınabilir olduğu doğrulanır,
        /// sonra eski occupancy kayıtları kaldırılıp yenileri yazılır.
        /// </summary>
        public bool TryCommit(RopeModel rope, StepPlan plan)
        {
            if (plan.Kind == StepKind.None) return false;

            for (int i = 0; i < plan.ToChain.Count; i++)
            {
                var nodeId = plan.ToChain[i];
                if (plan.FromChain.Contains(nodeId)) continue;

                var traversal = TraversalRules.Evaluate(_occupancy, nodeId, rope);
                if (traversal != NodeTraversal.Free) return false;
            }

            for (int i = 0; i < plan.FromChain.Count; i++)
            {
                var nodeId = plan.FromChain[i];
                if (!plan.ToChain.Contains(nodeId)) _occupancy.Free(nodeId);
            }

            rope.SetNodes(plan.ToChain);

            for (int i = 0; i < plan.ToChain.Count; i++)
            {
                _occupancy.Occupy(plan.ToChain[i], rope);
            }

            return true;
        }

        /// <summary>Level yüklenirken rope'un başlangıç occupancy'sini yazar.</summary>
        public void RegisterInitialOccupancy(RopeModel rope)
        {
            var nodes = rope.Nodes;
            for (int i = 0; i < nodes.Count; i++)
            {
                _occupancy.Occupy(nodes[i], rope);
            }
        }

        public void ReleaseOccupancy(IReadOnlyList<int> nodeIds)
        {
            _occupancy.FreeAll(nodeIds);
        }
    }
}

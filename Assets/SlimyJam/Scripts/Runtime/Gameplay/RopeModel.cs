using System.Collections.Generic;
using SlimyJam.Core;
using SlimyJam.Data;

namespace SlimyJam.Gameplay
{
    public enum RopeEnd
    {
        Head = 0,
        Tail = 1
    }

    /// <summary>
    /// Rope'un saf logical temsili: sıralı occupiedNodeIds zinciri (GDD 5.1).
    /// Liste sırası oyun boyunca değişmez; yalnızca içindeki node ID'leri güncellenir.
    /// </summary>
    public sealed class RopeModel : IRopeOccupant
    {
        private readonly List<int> _nodes;

        public int RopeId { get; }
        public RopeColor Color { get; }

        /// <summary>occupiedNodeIds her değiştiğinde artar (path cache invalidation).</summary>
        public int Version { get; private set; }

        public RopeModel(int ropeId, RopeColor color, IReadOnlyList<int> occupiedNodeIds)
        {
            RopeId = ropeId;
            Color = color;
            _nodes = new List<int>(occupiedNodeIds.Count);
            for (int i = 0; i < occupiedNodeIds.Count; i++) _nodes.Add(occupiedNodeIds[i]);
        }

        public IReadOnlyList<int> Nodes => _nodes;
        public int Length => _nodes.Count;
        public int HeadNodeId => _nodes[0];
        public int TailNodeId => _nodes[_nodes.Count - 1];

        public int GetEndpointNodeId(RopeEnd end) => end == RopeEnd.Head ? HeadNodeId : TailNodeId;

        public RopeEnd Opposite(RopeEnd end) => end == RopeEnd.Head ? RopeEnd.Tail : RopeEnd.Head;

        /// <summary>indexFromEnd = 0 endpoint, 1 ona komşu body node'u.</summary>
        public int GetNodeFromEnd(RopeEnd end, int indexFromEnd)
        {
            if (indexFromEnd < 0 || indexFromEnd >= _nodes.Count) return -1;
            return end == RopeEnd.Head ? _nodes[indexFromEnd] : _nodes[_nodes.Count - 1 - indexFromEnd];
        }

        /// <summary>Node zincirde ise verilen uçtan itibaren kaçıncı sırada olduğunu döndürür, değilse -1.</summary>
        public int IndexFromEnd(RopeEnd end, int nodeId)
        {
            for (int i = 0; i < _nodes.Count; i++)
            {
                if (_nodes[i] != nodeId) continue;
                return end == RopeEnd.Head ? i : _nodes.Count - 1 - i;
            }

            return -1;
        }

        public bool Contains(int nodeId) => _nodes.Contains(nodeId);

        /// <summary>Zincirin tamamını yeni sırayla değiştirir. Uzunluk sabit kalmalıdır.</summary>
        public void SetNodes(IReadOnlyList<int> newNodes)
        {
            _nodes.Clear();
            for (int i = 0; i < newNodes.Count; i++) _nodes.Add(newNodes[i]);
            Version++;
        }

        public void CopyNodesTo(List<int> target)
        {
            target.Clear();
            for (int i = 0; i < _nodes.Count; i++) target.Add(_nodes[i]);
        }
    }
}

namespace SlimyJam.Core
{
    /// <summary>
    /// İki bağlı node arasındaki logical segment. Ayrı bir gameplay nesnesi değildir; yalnızca pointer
    /// projection ve continuous hareket için geometrik referanstır (GDD 3, 7.1).
    /// </summary>
    public readonly struct GraphSegment
    {
        /// <summary>Her zaman iki node'un küçük ID'si. Deterministik tie-break için sabit sıra.</summary>
        public readonly int NodeAId;

        public readonly int NodeBId;
        public readonly float Length;

        public GraphSegment(int nodeAId, int nodeBId, float length)
        {
            NodeAId = nodeAId;
            NodeBId = nodeBId;
            Length = length;
        }

        public bool IsValid => NodeAId != NodeBId;

        public int Other(int nodeId) => nodeId == NodeAId ? NodeBId : NodeAId;

        public bool Contains(int nodeId) => nodeId == NodeAId || nodeId == NodeBId;

        public bool Equals(GraphSegment other) => NodeAId == other.NodeAId && NodeBId == other.NodeBId;

        public static readonly GraphSegment None = new GraphSegment(-1, -1, 0f);
    }
}

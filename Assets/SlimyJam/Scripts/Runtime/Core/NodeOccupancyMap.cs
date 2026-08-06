using System.Collections.Generic;

namespace SlimyJam.Core
{
    /// <summary>
    /// O(1) nodeId -> occupant sorgusu. Tek occupancy modeli: ayrı node/edge occupancy yoktur (GDD 9.1, 14.2).
    /// </summary>
    public sealed class NodeOccupancyMap
    {
        private readonly Dictionary<int, INodeOccupant> _occupants = new Dictionary<int, INodeOccupant>();

        /// <summary>Her occupancy değişiminde artar; path cache invalidation için kullanılır (GDD 7.2).</summary>
        public int Version { get; private set; }

        public bool IsFree(int nodeId) => !_occupants.ContainsKey(nodeId);

        public INodeOccupant GetOccupant(int nodeId)
        {
            return _occupants.TryGetValue(nodeId, out var occupant) ? occupant : null;
        }

        public bool TryGetOccupant(int nodeId, out INodeOccupant occupant)
        {
            return _occupants.TryGetValue(nodeId, out occupant);
        }

        public void Occupy(int nodeId, INodeOccupant occupant)
        {
            _occupants[nodeId] = occupant;
            Version++;
        }

        public void Free(int nodeId)
        {
            if (_occupants.Remove(nodeId)) Version++;
        }

        public void FreeAll(IReadOnlyList<int> nodeIds)
        {
            for (int i = 0; i < nodeIds.Count; i++)
            {
                _occupants.Remove(nodeIds[i]);
            }

            Version++;
        }

        public void Clear()
        {
            _occupants.Clear();
            Version++;
        }
    }
}

using SlimyJam.Core;
using SlimyJam.Data;

namespace SlimyJam.Gameplay
{
    public sealed class WallModel : IWallOccupant
    {
        public int WallId { get; }
        public int NodeId { get; }
        public int RopeCollectionsRemaining { get; private set; }
        public bool IsActive { get; private set; } = true;

        public RopeColor Color => RopeColor.Red;

        public WallModel(int wallId, int nodeId, int ropeCollectionCount)
        {
            WallId = wallId;
            NodeId = nodeId;
            RopeCollectionsRemaining = ropeCollectionCount;
        }

        public bool TickCollectionCounter()
        {
            if (!IsActive || RopeCollectionsRemaining <= 0) return false;

            RopeCollectionsRemaining--;
            if (RopeCollectionsRemaining > 0) return false;

            IsActive = false;
            return true;
        }
    }
}

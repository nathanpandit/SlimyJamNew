using SlimyJam.Core;
using SlimyJam.Data;

namespace SlimyJam.Gameplay
{
    /// <summary>
    /// Hole tek bir ground node üzerindedir ve aktif olduğu sürece onu occupy eder (GDD 10.1).
    /// Yanlış renkli rope için normal blocked node, matching rope için terminal collection hedefidir.
    /// </summary>
    public sealed class HoleModel : IHoleOccupant
    {
        public int HoleId { get; }
        public RopeColor Color { get; }
        public int NodeId { get; }
        public bool IsHidden { get; }
        public int RevealCollectionsRemaining { get; private set; }
        public int LockKeysRemaining { get; private set; }
        public bool IsActive { get; private set; } = true;
        public bool IsColorRevealed => !IsHidden || RevealCollectionsRemaining <= 0;
        public bool IsUnlocked => LockKeysRemaining <= 0;

        public HoleModel(int holeId, RopeColor color, int nodeId, bool hidden = false,
            int revealAfterCollections = 0, int lockedKeyCount = 0)
        {
            HoleId = holeId;
            Color = color;
            NodeId = nodeId;
            IsHidden = hidden;
            RevealCollectionsRemaining = hidden ? revealAfterCollections : 0;
            LockKeysRemaining = lockedKeyCount;
        }

        public void Deactivate() => IsActive = false;

        public bool TickRevealCounter()
        {
            if (!IsHidden || RevealCollectionsRemaining <= 0) return false;

            RevealCollectionsRemaining--;
            return RevealCollectionsRemaining == 0;
        }

        public bool UseKey()
        {
            if (LockKeysRemaining <= 0) return false;

            LockKeysRemaining--;
            return LockKeysRemaining == 0;
        }
    }
}

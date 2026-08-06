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
        public bool IsActive { get; private set; } = true;

        public HoleModel(int holeId, RopeColor color, int nodeId)
        {
            HoleId = holeId;
            Color = color;
            NodeId = nodeId;
        }

        public void Deactivate() => IsActive = false;
    }
}

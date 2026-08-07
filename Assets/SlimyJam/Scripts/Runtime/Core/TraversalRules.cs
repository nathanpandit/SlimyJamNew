using SlimyJam.Gameplay;

namespace SlimyJam.Core
{
    public enum NodeTraversal
    {
        /// <summary>Boş ground node - geçilebilir.</summary>
        Free,

        /// <summary>Başka bir rope'un unit'i - blocked.</summary>
        BlockedByOtherRope,

        /// <summary>Yanlış renkli hole - blocked.</summary>
        BlockedByHole,

        BlockedByWall,

        /// <summary>Kendi gövdesi - normal hareket için blocked, yalnızca adjacent reverse kuralıyla kullanılır.</summary>
        OwnBody,

        /// <summary>Matching hole - geçiş terminaldir, collection başlar.</summary>
        MatchingHole
    }

    /// <summary>Path traversal kuralları tek merkezde (GDD 7.5).</summary>
    public static class TraversalRules
    {
        public static NodeTraversal Evaluate(NodeOccupancyMap occupancy, int nodeId, RopeModel rope)
        {
            if (!occupancy.TryGetOccupant(nodeId, out var occupant)) return NodeTraversal.Free;

            switch (occupant)
            {
                case IHoleOccupant hole:
                    return CanEnterHole(rope, hole) ? NodeTraversal.MatchingHole : NodeTraversal.BlockedByHole;
                case IWallOccupant:
                    return NodeTraversal.BlockedByWall;
                case IRopeOccupant otherRope:
                    return otherRope.RopeId == rope.RopeId ? NodeTraversal.OwnBody : NodeTraversal.BlockedByOtherRope;
                default:
                    return NodeTraversal.BlockedByOtherRope;
            }
        }

        /// <summary>
        /// Serbest ucun uzayabileceği node'lar (GDD 8.7): boş node veya matching hole. Kendi gövdesine giremez.
        /// </summary>
        public static bool IsAvailableForFreeEndpoint(NodeOccupancyMap occupancy, int nodeId, RopeModel rope)
        {
            var traversal = Evaluate(occupancy, nodeId, rope);
            return traversal == NodeTraversal.Free || traversal == NodeTraversal.MatchingHole;
        }

        public static bool CanEnterHole(RopeModel rope, IHoleOccupant hole)
        {
            return rope != null &&
                   hole != null &&
                   rope.IsColorRevealed &&
                   hole.IsColorRevealed &&
                   hole.IsUnlocked &&
                   hole.Color == rope.Color;
        }
    }
}

using System.Collections.Generic;

namespace SlimyJam.Pathfinding
{
    /// <summary>
    /// Pointer'ın graph üzerindeki çözülmüş hedefi (GDD 7.1). Rope önce <see cref="PathNodes"/> boyunca
    /// logical adımlar atar, ardından son segment üzerinde <see cref="SegmentFraction"/> kadar continuous ilerler.
    /// </summary>
    public sealed class PointerTarget
    {
        /// <summary>Sırayla girilecek node'lar; active endpoint'in bulunduğu node listede yer almaz.</summary>
        public readonly List<int> PathNodes = new List<int>();

        /// <summary>Path sonundaki node (path boşsa active endpoint'in node'u).</summary>
        public int EntryNodeId = -1;

        /// <summary>Entry'den devam edilecek segment ucu; yoksa -1.</summary>
        public int SegmentOtherNodeId = -1;

        /// <summary>Entry'den SegmentOtherNodeId yönünde 0..1 arası hedef ilerleme.</summary>
        public float SegmentFraction;

        /// <summary>Hedefe tam rota bulunabildi mi? False ise yalnızca blocked prefix kullanılıyordur.</summary>
        public bool IsComplete;

        public bool HasTarget => EntryNodeId >= 0;

        public void Clear()
        {
            PathNodes.Clear();
            EntryNodeId = -1;
            SegmentOtherNodeId = -1;
            SegmentFraction = 0f;
            IsComplete = false;
        }
    }
}

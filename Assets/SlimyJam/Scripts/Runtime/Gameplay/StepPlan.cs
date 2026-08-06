using System.Collections.Generic;

namespace SlimyJam.Gameplay
{
    public enum StepKind
    {
        None,

        /// <summary>Active endpoint boş bir komşu node'a ilerler; zincir follow-the-leader kayar (GDD 8.3).</summary>
        Forward,

        /// <summary>Active endpoint kendi adjacent body node'una ilerler, free endpoint uzar (GDD 8.5).</summary>
        Reverse
    }

    /// <summary>
    /// Tek bir logical adımın validate edilmiş hâli. Adım, aynı uzunlukta iki zincir arasındaki geçiştir;
    /// i. rope unit'i Lerp(FromChain[i], ToChain[i], progress) ile çizilir. Bu temsil forward ve reverse
    /// hareketi tek kod yolunda birleştirir.
    /// </summary>
    public sealed class StepPlan
    {
        public readonly List<int> FromChain = new List<int>();
        public readonly List<int> ToChain = new List<int>();

        public StepKind Kind;
        public RopeEnd ActiveEnd;

        /// <summary>Active endpoint'in gireceği node.</summary>
        public int DestinationNodeId = -1;

        /// <summary>Adım midpoint'i geçtiğinde collection tetikleyecek hole; yoksa -1 (GDD 10.2).</summary>
        public int CollectionHoleId = -1;

        /// <summary>Collection'ı tetikleyen uç (active veya free).</summary>
        public RopeEnd CollectionEnd;

        public bool IsTerminal => CollectionHoleId >= 0;

        public void Reset()
        {
            FromChain.Clear();
            ToChain.Clear();
            Kind = StepKind.None;
            DestinationNodeId = -1;
            CollectionHoleId = -1;
        }
    }
}

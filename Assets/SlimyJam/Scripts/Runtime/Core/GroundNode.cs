using System.Collections.Generic;
using UnityEngine;

namespace SlimyJam.Core
{
    /// <summary>
    /// Bir dünya birimlik logical bölge (GDD 3). Grid tabanlı bir oyundaki tek hücreye karşılık gelir.
    /// Connection'lar bidirectional'dır ve sayıları sınırsızdır (junction'lar 2'den fazla olabilir).
    /// </summary>
    public sealed class GroundNode
    {
        public readonly int Id;
        public readonly Vector3 Position;

        /// <summary>Author edilmiş sıra korunur; eşit maliyet/açı durumlarında tie-break bunun üzerinden yapılır.</summary>
        public readonly List<int> ConnectedNodeIds;

        public GroundNode(int id, Vector3 position, List<int> connectedNodeIds)
        {
            Id = id;
            Position = position;
            ConnectedNodeIds = connectedNodeIds ?? new List<int>();
        }
    }
}

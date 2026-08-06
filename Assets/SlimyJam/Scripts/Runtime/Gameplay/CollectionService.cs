using System;
using System.Collections.Generic;
using SlimyJam.Core;
using SlimyJam.Data;

namespace SlimyJam.Gameplay
{
    /// <summary>
    /// Endpoint-hole adaylarını, renk sayımını ve hole removal sırasını merkezî yönetir (GDD 10, 14.2).
    /// </summary>
    public sealed class CollectionService
    {
        private readonly GraphRepository _graph;
        private readonly NodeOccupancyMap _occupancy;
        private readonly List<HoleModel> _holes;
        private readonly List<RopeModel> _activeRopes;

        /// <summary>Rope mekanik olarak board'dan kaldırıldı; görsel çekim animasyonu başlayabilir.</summary>
        public event Action<RopeModel, HoleModel> CollectionStarted;

        /// <summary>Bir rengin son rope'u toplandı; o renkteki tüm hole'lar kaldırıldı.</summary>
        public event Action<RopeColor, List<HoleModel>> HolesRemoved;

        public event Action LevelCompleted;

        public IReadOnlyList<RopeModel> ActiveRopes => _activeRopes;
        public IReadOnlyList<HoleModel> Holes => _holes;

        public CollectionService(GraphRepository graph, NodeOccupancyMap occupancy, List<HoleModel> holes,
            List<RopeModel> activeRopes)
        {
            _graph = graph;
            _occupancy = occupancy;
            _holes = holes;
            _activeRopes = activeRopes;
        }

        public HoleModel GetHole(int holeId)
        {
            for (int i = 0; i < _holes.Count; i++)
            {
                if (_holes[i].HoleId == holeId) return _holes[i];
            }

            return null;
        }

        /// <summary>
        /// Release sonrası yakın çekim kontrolü (GDD 10.3). Endpoint matching hole'un node'unda veya ona
        /// doğrudan connected bir node'da ise rope çekilir. Öncelik: active endpoint, sonra inactive endpoint,
        /// sonra hole list order.
        /// </summary>
        public HoleModel FindReleaseCollectionTarget(RopeModel rope, RopeEnd activeEnd)
        {
            var hole = FindMatchingHoleAtOrAdjacentTo(rope, rope.GetEndpointNodeId(activeEnd));
            if (hole != null) return hole;

            return FindMatchingHoleAtOrAdjacentTo(rope, rope.GetEndpointNodeId(rope.Opposite(activeEnd)));
        }

        private HoleModel FindMatchingHoleAtOrAdjacentTo(RopeModel rope, int nodeId)
        {
            // Hole list order deterministic sonucu belirler.
            HoleModel best = null;

            for (int i = 0; i < _holes.Count; i++)
            {
                var hole = _holes[i];
                if (!hole.IsActive || hole.Color != rope.Color) continue;

                if (hole.NodeId == nodeId) return hole;

                if (best == null && _graph.AreConnected(nodeId, hole.NodeId)) best = hole;
            }

            return best;
        }

        /// <summary>
        /// Mekanik kaldırma (GDD 10.4): occupancy hemen serbest bırakılır, animasyon logical blocking'i geciktirmez.
        /// </summary>
        public void BeginCollection(RopeModel rope, HoleModel hole)
        {
            if (!_activeRopes.Remove(rope)) return;

            _occupancy.FreeAll(rope.Nodes);
            CollectionStarted?.Invoke(rope, hole);

            ApplyHolePersistence(rope.Color);

            if (_activeRopes.Count == 0) LevelCompleted?.Invoke();
        }

        /// <summary>
        /// GDD 10.5: aynı renkten rope kaldığı sürece o rengin hole'ları aktif ve blocking kalır;
        /// hiç kalmadığında o renkteki bütün hole'lar yok edilir ve node'ları available olur.
        /// </summary>
        private void ApplyHolePersistence(RopeColor color)
        {
            for (int i = 0; i < _activeRopes.Count; i++)
            {
                if (_activeRopes[i].Color == color) return;
            }

            var removed = new List<HoleModel>();
            for (int i = _holes.Count - 1; i >= 0; i--)
            {
                var hole = _holes[i];
                if (hole.Color != color || !hole.IsActive) continue;

                hole.Deactivate();
                _occupancy.Free(hole.NodeId);
                _holes.RemoveAt(i);
                removed.Add(hole);
            }

            if (removed.Count > 0) HolesRemoved?.Invoke(color, removed);
        }

        public void RegisterHoleOccupancy()
        {
            for (int i = 0; i < _holes.Count; i++)
            {
                _occupancy.Occupy(_holes[i].NodeId, _holes[i]);
            }
        }
    }
}

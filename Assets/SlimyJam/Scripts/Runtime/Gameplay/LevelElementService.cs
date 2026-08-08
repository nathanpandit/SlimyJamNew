using System;
using System.Collections.Generic;
using SlimyJam.Core;

namespace SlimyJam.Gameplay
{
    public sealed class LevelElementService
    {
        private sealed class ContainedRopeBinding
        {
            public RopeModel Outer;
            public RopeModel Inner;
            public readonly List<int> OuterIndices = new List<int>();
        }

        private readonly NodeOccupancyMap _occupancy;
        private readonly List<RopeModel> _activeRopes;
        private readonly List<HoleModel> _holes;
        private readonly List<WallModel> _walls;
        private readonly Dictionary<int, List<ContainedRopeBinding>> _containedByOuter =
            new Dictionary<int, List<ContainedRopeBinding>>();
        private readonly Dictionary<int, ContainedRopeBinding> _containedByInner =
            new Dictionary<int, ContainedRopeBinding>();
        private readonly List<RopeModel> _releasedBuffer = new List<RopeModel>();
        private readonly List<int> _nodeBuffer = new List<int>();

        public event Action<RopeModel> RopeStateChanged;
        public event Action<HoleModel> HoleStateChanged;
        public event Action<WallModel> WallRemoved;
        public event Action<RopeModel, List<RopeModel>> ContainedRopesReleased;

        public IReadOnlyList<WallModel> Walls => _walls;

        public LevelElementService(NodeOccupancyMap occupancy, List<RopeModel> activeRopes, List<HoleModel> holes,
            List<WallModel> walls)
        {
            _occupancy = occupancy;
            _activeRopes = activeRopes;
            _holes = holes;
            _walls = walls;

            BuildContainedRopeBindings();
        }

        private void BuildContainedRopeBindings()
        {
            var ropesById = new Dictionary<int, RopeModel>();
            for (int i = 0; i < _activeRopes.Count; i++)
            {
                ropesById[_activeRopes[i].RopeId] = _activeRopes[i];
            }

            for (int i = 0; i < _activeRopes.Count; i++)
            {
                var inner = _activeRopes[i];
                if (!inner.IsContained) continue;
                if (!ropesById.TryGetValue(inner.ContainedByRopeId, out var outer)) continue;

                var binding = new ContainedRopeBinding
                {
                    Outer = outer,
                    Inner = inner
                };

                if (!BuildOuterIndexMap(inner, outer, binding.OuterIndices)) continue;

                if (!_containedByOuter.TryGetValue(outer.RopeId, out var list))
                {
                    list = new List<ContainedRopeBinding>();
                    _containedByOuter[outer.RopeId] = list;
                }

                list.Add(binding);
                _containedByInner[inner.RopeId] = binding;
            }
        }

        private static bool BuildOuterIndexMap(RopeModel inner, RopeModel outer, List<int> result)
        {
            result.Clear();

            for (int i = 0; i < inner.Nodes.Count; i++)
            {
                var nodeId = inner.Nodes[i];
                var index = -1;
                for (int o = 0; o < outer.Nodes.Count; o++)
                {
                    if (outer.Nodes[o] != nodeId) continue;
                    index = o;
                    break;
                }

                if (index < 0) return false;
                result.Add(index);
            }

            return result.Count == inner.Nodes.Count;
        }

        public bool TryGetContainmentVisualSource(RopeModel inner, out RopeModel outer, out IReadOnlyList<int> outerIndices)
        {
            if (_containedByInner.TryGetValue(inner.RopeId, out var binding) && inner.IsContained)
            {
                outer = binding.Outer;
                outerIndices = binding.OuterIndices;
                return true;
            }

            outer = null;
            outerIndices = null;
            return false;
        }

        public void GetContainedRopes(RopeModel outer, List<RopeModel> result)
        {
            result.Clear();
            if (outer == null || !_containedByOuter.TryGetValue(outer.RopeId, out var list)) return;

            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].Inner.IsContained) result.Add(list[i].Inner);
            }
        }

        public void SyncContainedRopes(RopeModel outer)
        {
            if (outer == null || !_containedByOuter.TryGetValue(outer.RopeId, out var list)) return;

            for (int i = 0; i < list.Count; i++)
            {
                var binding = list[i];
                if (!binding.Inner.IsContained) continue;

                BuildContainedNodesFromOuter(binding, _nodeBuffer);
                binding.Inner.SetNodes(_nodeBuffer);
                RopeStateChanged?.Invoke(binding.Inner);
            }
        }

        private static void BuildContainedNodesFromOuter(ContainedRopeBinding binding, List<int> result)
        {
            result.Clear();
            for (int i = 0; i < binding.OuterIndices.Count; i++)
            {
                result.Add(binding.Outer.Nodes[binding.OuterIndices[i]]);
            }
        }

        public void OnRopeCollected(RopeModel rope)
        {
            if (rope == null) return;

            if (rope.HasKey) UseKeyOnLockedHoles();

            TickHiddenRevealCounters();
            TickFrozenCounters();
            TickWalls();
            ReleaseContainedRopes(rope);
        }

        private void UseKeyOnLockedHoles()
        {
            for (int i = 0; i < _holes.Count; i++)
            {
                if (_holes[i].UseKey()) HoleStateChanged?.Invoke(_holes[i]);
            }
        }

        private void TickHiddenRevealCounters()
        {
            for (int i = 0; i < _activeRopes.Count; i++)
            {
                if (_activeRopes[i].TickRevealCounter()) RopeStateChanged?.Invoke(_activeRopes[i]);
            }

            for (int i = 0; i < _holes.Count; i++)
            {
                if (_holes[i].TickRevealCounter()) HoleStateChanged?.Invoke(_holes[i]);
            }
        }

        private void TickWalls()
        {
            for (int i = _walls.Count - 1; i >= 0; i--)
            {
                var wall = _walls[i];
                if (!wall.TickCollectionCounter()) continue;

                _occupancy.Free(wall.NodeId);
                _walls.RemoveAt(i);
                WallRemoved?.Invoke(wall);
            }
        }

        private void TickFrozenCounters()
        {
            for (int i = 0; i < _activeRopes.Count; i++)
            {
                if (_activeRopes[i].TickUnfreezeCounter()) RopeStateChanged?.Invoke(_activeRopes[i]);
            }
        }

        private void ReleaseContainedRopes(RopeModel outer)
        {
            if (!_containedByOuter.TryGetValue(outer.RopeId, out var list)) return;

            _releasedBuffer.Clear();

            for (int i = 0; i < list.Count; i++)
            {
                var binding = list[i];
                if (!binding.Inner.IsContained) continue;

                BuildContainedNodesFromOuter(binding, _nodeBuffer);
                binding.Inner.SetNodes(_nodeBuffer);
                binding.Inner.ReleaseFromContainer();
                RegisterRopeOccupancy(binding.Inner);
                _releasedBuffer.Add(binding.Inner);
            }

            if (_releasedBuffer.Count == 0) return;

            ContainedRopesReleased?.Invoke(outer, new List<RopeModel>(_releasedBuffer));
        }

        private void RegisterRopeOccupancy(RopeModel rope)
        {
            for (int i = 0; i < rope.Nodes.Count; i++)
            {
                _occupancy.Occupy(rope.Nodes[i], rope);
            }
        }

        public void RegisterWallOccupancy()
        {
            for (int i = 0; i < _walls.Count; i++)
            {
                if (_walls[i].IsActive) _occupancy.Occupy(_walls[i].NodeId, _walls[i]);
            }
        }
    }
}

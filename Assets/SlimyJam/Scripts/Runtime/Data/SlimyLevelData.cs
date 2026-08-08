using System;
using System.Collections.Generic;
using UnityEngine;

namespace SlimyJam.Data
{
    /// <summary>
    /// Runtime level formatı (GDD 12.2). Spline control point'leri bu veriye yazılmaz;
    /// hareket yalnızca bake edilmiş ground node graph üzerinden okunur.
    /// </summary>
    [Serializable]
    public sealed class SlimyLevelData
    {
        public int levelIndex;
        public List<GroundNodeData> groundNodes = new List<GroundNodeData>();
        public List<RopeData> ropes = new List<RopeData>();
        public List<HoleData> holes = new List<HoleData>();
        public List<WallData> walls = new List<WallData>();
    }

    [Serializable]
    public sealed class GroundNodeData
    {
        public int id;
        public Vector3 position;
        public List<int> connectedNodeIds = new List<int>();
    }

    [Serializable]
    public sealed class RopeData
    {
        public int id;
        public RopeColor color;
        public List<int> occupiedNodeIds = new List<int>();
        public bool hasKey;
        public bool hidden;
        public int revealAfterCollections;
        public bool frozen;
        public int unfreezeAfterCollections;
        public int containedByRopeId;
    }

    [Serializable]
    public sealed class HoleData
    {
        public int id;
        public RopeColor color;
        public int nodeId;
        public bool hidden;
        public int revealAfterCollections;
        public int lockedKeyCount;
    }

    [Serializable]
    public sealed class WallData
    {
        public int id;
        public int nodeId;
        public int ropeCollectionCount;
    }

    [Serializable]
    public enum RopeColor
    {
        Red = 0,
        Blue = 1,
        Green = 2,
        Yellow = 3,
        Purple = 4,
        Pink = 5,
        Orange = 6,
        DarkBlue = 7
    }
}

using System.Collections.Generic;
using System.Text;

namespace SlimyJam.Data
{
    /// <summary>
    /// GDD 13.3: eksik node ID, kurulamayan connection, geçersiz occupiedNodeIds veya çakışan başlangıç
    /// occupancy varsa gameplay başlatılmaz. Runtime güvenliği; editor validation'ın yerine geçmez.
    /// </summary>
    public static class LevelDataValidator
    {
        public static bool Validate(SlimyLevelData data, out string error)
        {
            var sb = new StringBuilder();

            if (data == null)
            {
                error = "LevelData is null.";
                return false;
            }

            if (data.groundNodes == null || data.groundNodes.Count == 0)
            {
                error = "LevelData contains no ground nodes.";
                return false;
            }

            var nodesById = new Dictionary<int, GroundNodeData>(data.groundNodes.Count);
            foreach (var node in data.groundNodes)
            {
                if (nodesById.ContainsKey(node.id))
                {
                    sb.AppendLine($"Duplicate ground node id {node.id}.");
                    continue;
                }

                nodesById.Add(node.id, node);
            }

            // Connection bütünlüğü: hedef var mı ve bidirectional mı?
            foreach (var node in data.groundNodes)
            {
                if (node.connectedNodeIds == null) continue;

                var seen = new HashSet<int>();
                foreach (var connectedId in node.connectedNodeIds)
                {
                    if (connectedId == node.id)
                    {
                        sb.AppendLine($"Node {node.id} is connected to itself.");
                        continue;
                    }

                    if (!seen.Add(connectedId))
                    {
                        sb.AppendLine($"Node {node.id} lists connection {connectedId} more than once.");
                    }

                    if (!nodesById.TryGetValue(connectedId, out var other))
                    {
                        sb.AppendLine($"Node {node.id} references missing node id {connectedId}.");
                        continue;
                    }

                    if (other.connectedNodeIds == null || !other.connectedNodeIds.Contains(node.id))
                    {
                        sb.AppendLine($"Connection {node.id}->{connectedId} is not bidirectional.");
                    }
                }
            }

            // Occupancy: her node en fazla bir occupant taşır.
            var occupancy = new Dictionary<int, string>();

            if (data.holes != null)
            {
                var holeIds = new HashSet<int>();
                foreach (var hole in data.holes)
                {
                    if (!holeIds.Add(hole.id))
                    {
                        sb.AppendLine($"Duplicate hole id {hole.id}.");
                    }

                    if (!nodesById.ContainsKey(hole.nodeId))
                    {
                        sb.AppendLine($"Hole {hole.id} references missing node id {hole.nodeId}.");
                        continue;
                    }

                    if (occupancy.TryGetValue(hole.nodeId, out var owner))
                    {
                        sb.AppendLine($"Node {hole.nodeId} is occupied by both {owner} and hole {hole.id}.");
                        continue;
                    }

                    occupancy[hole.nodeId] = $"hole {hole.id}";
                }
            }

            if (data.ropes == null || data.ropes.Count == 0)
            {
                sb.AppendLine("LevelData contains no ropes; level would start already completed.");
            }
            else
            {
                var ropeIds = new HashSet<int>();
                foreach (var rope in data.ropes)
                {
                    if (!ropeIds.Add(rope.id))
                    {
                        sb.AppendLine($"Duplicate rope id {rope.id}.");
                    }

                    if (rope.occupiedNodeIds == null || rope.occupiedNodeIds.Count == 0)
                    {
                        sb.AppendLine($"Rope {rope.id} has no occupied nodes.");
                        continue;
                    }

                    for (int i = 0; i < rope.occupiedNodeIds.Count; i++)
                    {
                        var nodeId = rope.occupiedNodeIds[i];
                        if (!nodesById.ContainsKey(nodeId))
                        {
                            sb.AppendLine($"Rope {rope.id} references missing node id {nodeId}.");
                            continue;
                        }

                        if (occupancy.TryGetValue(nodeId, out var owner))
                        {
                            sb.AppendLine($"Node {nodeId} is occupied by both {owner} and rope {rope.id}.");
                        }
                        else
                        {
                            occupancy[nodeId] = $"rope {rope.id}";
                        }

                        if (i == 0) continue;

                        var previousId = rope.occupiedNodeIds[i - 1];
                        if (!nodesById.TryGetValue(previousId, out var previous) ||
                            previous.connectedNodeIds == null ||
                            !previous.connectedNodeIds.Contains(nodeId))
                        {
                            sb.AppendLine(
                                $"Rope {rope.id} occupies {previousId} and {nodeId} which are not connected.");
                        }
                    }
                }
            }

            error = sb.ToString().TrimEnd();
            return error.Length == 0;
        }

    }
}

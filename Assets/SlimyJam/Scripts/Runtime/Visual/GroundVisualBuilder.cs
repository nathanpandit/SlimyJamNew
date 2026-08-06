using System.Collections.Generic;
using SlimyJam.Core;
using UnityEngine;

namespace SlimyJam.Visual
{
    /// <summary>
    /// Bake edilmiş node graph'ı tek bir procedural mesh olarak çizer. Yalnızca görseldir;
    /// gameplay'de collider ya da playable-area nesnesi kullanılmaz (GDD 4.3).
    /// </summary>
    public static class GroundVisualBuilder
    {
        public static GameObject Build(GraphRepository graph, float width, Transform parent, Material material)
        {
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            var halfWidth = Mathf.Max(0.05f, width * 0.5f);

            var segments = graph.Segments;
            for (int i = 0; i < segments.Count; i++)
            {
                var a = graph.GetPosition(segments[i].NodeAId);
                var b = graph.GetPosition(segments[i].NodeBId);
                AddQuad(vertices, triangles, a, b, halfWidth);
            }

            // Junction'larda köşe boşluklarını kapatan node kareleri.
            var nodes = graph.Nodes;
            for (int i = 0; i < nodes.Count; i++)
            {
                AddSquare(vertices, triangles, nodes[i].Position, halfWidth);
            }

            var mesh = new Mesh { name = "SlimyGround" };
            if (vertices.Count > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            var go = new GameObject("Ground");
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = material;
            return go;
        }

        private static void AddQuad(List<Vector3> vertices, List<int> triangles, Vector3 a, Vector3 b, float halfWidth)
        {
            var direction = b - a;
            if (direction.sqrMagnitude <= Mathf.Epsilon) return;

            var perpendicular = Vector3.Cross(direction.normalized, Vector3.up).normalized * halfWidth;
            if (perpendicular.sqrMagnitude <= Mathf.Epsilon)
            {
                perpendicular = Vector3.Cross(direction.normalized, Vector3.forward).normalized * halfWidth;
            }

            var start = vertices.Count;
            vertices.Add(a - perpendicular);
            vertices.Add(a + perpendicular);
            vertices.Add(b + perpendicular);
            vertices.Add(b - perpendicular);

            triangles.Add(start);
            triangles.Add(start + 1);
            triangles.Add(start + 2);
            triangles.Add(start);
            triangles.Add(start + 2);
            triangles.Add(start + 3);
        }

        private static void AddSquare(List<Vector3> vertices, List<int> triangles, Vector3 center, float halfWidth)
        {
            var start = vertices.Count;
            vertices.Add(center + new Vector3(-halfWidth, 0f, -halfWidth));
            vertices.Add(center + new Vector3(-halfWidth, 0f, halfWidth));
            vertices.Add(center + new Vector3(halfWidth, 0f, halfWidth));
            vertices.Add(center + new Vector3(halfWidth, 0f, -halfWidth));

            triangles.Add(start);
            triangles.Add(start + 1);
            triangles.Add(start + 2);
            triangles.Add(start);
            triangles.Add(start + 2);
            triangles.Add(start + 3);
        }
    }
}

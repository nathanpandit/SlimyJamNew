using System.Collections.Generic;
using UnityEngine;

namespace SlimyJam.Visual
{
    public static class PolylineUtility
    {
        public static float GetLength(IReadOnlyList<Vector3> points)
        {
            var length = 0f;
            for (int i = 1; i < points.Count; i++)
            {
                length += Vector3.Distance(points[i - 1], points[i]);
            }

            return length;
        }

        /// <summary>
        /// Polyline'ın baştan <paramref name="distance"/> kadarını kırpar. Hole'a çekilme animasyonunda
        /// rope'un uçtan içeri "yutulmasını" üretir.
        /// </summary>
        public static void TrimFromStart(IReadOnlyList<Vector3> points, float distance, List<Vector3> result)
        {
            result.Clear();
            if (points.Count == 0) return;

            var remaining = distance;
            for (int i = 1; i < points.Count; i++)
            {
                var segmentLength = Vector3.Distance(points[i - 1], points[i]);
                if (remaining > segmentLength)
                {
                    remaining -= segmentLength;
                    continue;
                }

                var t = segmentLength <= Mathf.Epsilon ? 0f : remaining / segmentLength;
                result.Add(Vector3.Lerp(points[i - 1], points[i], t));
                for (int j = i; j < points.Count; j++) result.Add(points[j]);
                return;
            }

            result.Add(points[points.Count - 1]);
        }
    }
}

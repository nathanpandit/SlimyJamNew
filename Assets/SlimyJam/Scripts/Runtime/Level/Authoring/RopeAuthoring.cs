using System.Collections.Generic;
using SlimyJam.Data;
using UnityEngine;

namespace SlimyJam.Authoring
{
    /// <summary>Bir rope'un başlangıç zinciri: sıralı marker'lar head'den tail'e doğru okunur.</summary>
    public sealed class RopeAuthoring : MonoBehaviour
    {
        public int ropeId = 1;
        public RopeColor color = RopeColor.Green;

        [Tooltip("Boş bırakılırsa child transform'lar hiyerarşi sırasıyla kullanılır.")]
        public List<Transform> nodeMarkers = new List<Transform>();

        public List<Vector3> GetWorldPoints()
        {
            var points = new List<Vector3>();

            if (nodeMarkers != null && nodeMarkers.Count > 0)
            {
                for (int i = 0; i < nodeMarkers.Count; i++)
                {
                    if (nodeMarkers[i] != null) points.Add(nodeMarkers[i].position);
                }

                return points;
            }

            for (int i = 0; i < transform.childCount; i++)
            {
                points.Add(transform.GetChild(i).position);
            }

            return points;
        }

        private void OnDrawGizmos()
        {
            var points = GetWorldPoints();
            Gizmos.color = Color.white;
            for (int i = 1; i < points.Count; i++)
            {
                Gizmos.DrawLine(points[i - 1], points[i]);
            }

            for (int i = 0; i < points.Count; i++)
            {
                Gizmos.color = i == 0 ? Color.green : Color.white;
                Gizmos.DrawSphere(points[i], 0.15f);
            }
        }
    }
}

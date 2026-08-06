using UnityEngine;

namespace SlimyJam.Level
{
    /// <summary>
    /// Screen-to-world dönüşümü ve level framing (GDD 14.1). Framing formülü mevcut projedeki
    /// Modules.CameraManager.CameraController ile aynı mantığı izler.
    /// </summary>
    public sealed class SlimyCameraController : MonoBehaviour
    {
        [SerializeField] private Camera targetCamera;
        [SerializeField] private float marginPercentage = 1f;
        [SerializeField] private float boundsPadding = 2.5f;

        /// <summary>Pointer'ın raycast edildiği düzlem; oyuncunun gördüğü rope yüksekliğidir.</summary>
        [SerializeField] private float planeHeight;

        /// <summary>Graph node'larının bulunduğu yükseklik; pointer sonucu bu düzleme indirilir.</summary>
        [SerializeField] private float graphHeight;

        private Camera Camera => targetCamera != null ? targetCamera : targetCamera = Camera.main;

        /// <summary>Pointer raycast düzlemi rope yüksekliğinde, sonuç ise graph düzleminde olur.</summary>
        public void SetPlanes(float graphPlaneHeight, float ropeHeight)
        {
            graphHeight = graphPlaneHeight;
            planeHeight = graphPlaneHeight + ropeHeight;
        }

        public void Frame(Bounds bounds)
        {
            var camera = Camera;
            if (camera == null) return;

            var center = bounds.center;
            var safeHeight = Mathf.Max(1f, Screen.height - Screen.safeArea.y);
            var aspectRatio = (Screen.width - Screen.safeArea.x) / safeHeight * 0.92f;
            if (aspectRatio <= 0.01f) aspectRatio = camera.aspect;

            var minXDistance = bounds.extents.x / aspectRatio + boundsPadding;
            var minZDistance = bounds.extents.z + boundsPadding;
            var minDistance = Mathf.Max(minXDistance, minZDistance);

            if (camera.orthographic)
            {
                camera.orthographicSize = minDistance;
                camera.transform.position = new Vector3(center.x, camera.transform.position.y, center.z);
                return;
            }

            var distance = minDistance * marginPercentage /
                           Mathf.Tan(camera.fieldOfView * 0.5f * Mathf.Deg2Rad);
            var pitch = camera.transform.eulerAngles.x;
            var backOffset = distance * Mathf.Tan((90f - pitch) * Mathf.Deg2Rad);

            camera.transform.position = new Vector3(center.x, distance, center.z - backOffset);
        }

        /// <summary>Screen-space pointer'ı gameplay düzlemindeki world pozisyonuna çevirir (GDD 7.1 adım 1).</summary>
        public bool TryGetWorldPosition(Vector2 screenPosition, out Vector3 worldPosition)
        {
            worldPosition = Vector3.zero;

            var camera = Camera;
            if (camera == null) return false;

            var plane = new Plane(Vector3.up, new Vector3(0f, planeHeight, 0f));
            var ray = camera.ScreenPointToRay(screenPosition);

            if (!plane.Raycast(ray, out var distance)) return false;

            worldPosition = ray.GetPoint(distance);
            worldPosition.y = graphHeight;
            return true;
        }
    }
}

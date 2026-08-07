using SlimyJam.Visual;
using UnityEngine;

namespace SlimyJam.Gameplay
{
    /// <summary>Hole'un sahne temsili. Logical blocking HoleModel + occupancy map üzerinden yürür (GDD 10.1).</summary>
    public sealed class Hole : MonoBehaviour
    {
        private Transform _visual;
        private Transform _lockMarker;
        private MeshRenderer _renderer;

        public HoleModel Model { get; private set; }

        public void Initialize(HoleModel model, Vector3 worldPosition, float radius)
        {
            Model = model;
            transform.position = worldPosition;

            var disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            disc.name = "HoleVisual";
            var collider = disc.GetComponent<Collider>();
            if (collider != null) Destroy(collider);

            _visual = disc.transform;
            _visual.SetParent(transform, false);
            _visual.localScale = new Vector3(radius * 2f, 0.04f, radius * 2f);
            _visual.localPosition = new Vector3(0f, 0.02f, 0f);

            _renderer = disc.GetComponent<MeshRenderer>();
            CreateLockMarker(radius);
            RefreshVisual();
        }

        public void RefreshVisual()
        {
            if (Model == null) return;

            var color = Model.IsColorRevealed ? SlimyPalette.Get(Model.Color) : SlimyPalette.Hidden;
            if (_renderer != null) _renderer.sharedMaterial = SlimyPalette.GetMaterial(color);
            if (_lockMarker != null) _lockMarker.gameObject.SetActive(!Model.IsUnlocked);
        }

        /// <summary>Aynı renkten rope kalmadığında hole yok edilir ve node'u available olur (GDD 10.5).</summary>
        public void Remove()
        {
            Destroy(gameObject);
        }

        private void CreateLockMarker(float radius)
        {
            var marker = GameObject.CreatePrimitive(PrimitiveType.Cube);
            marker.name = "LockMarker";
            var collider = marker.GetComponent<Collider>();
            if (collider != null) Destroy(collider);

            _lockMarker = marker.transform;
            _lockMarker.SetParent(transform, false);
            _lockMarker.localScale = new Vector3(radius * 0.55f, 0.08f, radius * 0.55f);
            _lockMarker.localPosition = new Vector3(0f, 0.09f, 0f);

            var renderer = marker.GetComponent<MeshRenderer>();
            if (renderer != null) renderer.sharedMaterial = SlimyPalette.GetMaterial(SlimyPalette.Lock);
        }
    }
}

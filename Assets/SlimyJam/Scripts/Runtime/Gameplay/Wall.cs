using SlimyJam.Visual;
using UnityEngine;

namespace SlimyJam.Gameplay
{
    public sealed class Wall : MonoBehaviour
    {
        public WallModel Model { get; private set; }

        public void Initialize(WallModel model, Vector3 worldPosition, float size)
        {
            Model = model;
            transform.position = worldPosition;

            var block = GameObject.CreatePrimitive(PrimitiveType.Cube);
            block.name = "WallVisual";
            var collider = block.GetComponent<Collider>();
            if (collider != null) Destroy(collider);

            block.transform.SetParent(transform, false);
            block.transform.localScale = new Vector3(size, size * 0.75f, size);
            block.transform.localPosition = new Vector3(0f, size * 0.35f, 0f);

            var renderer = block.GetComponent<MeshRenderer>();
            if (renderer != null) renderer.sharedMaterial = SlimyPalette.GetMaterial(SlimyPalette.Wall);
        }

        public void Remove()
        {
            Destroy(gameObject);
        }
    }
}

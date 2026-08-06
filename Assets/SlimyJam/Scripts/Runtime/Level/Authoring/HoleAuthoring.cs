using SlimyJam.Data;
using UnityEngine;

namespace SlimyJam.Authoring
{
    /// <summary>Bir hole'un rengi ve konumu; bake sırasında en yakın node'a bağlanır.</summary>
    public sealed class HoleAuthoring : MonoBehaviour
    {
        public int holeId = 10;
        public RopeColor color = RopeColor.Green;

        private void OnDrawGizmos()
        {
            Gizmos.color = Color.black;
            Gizmos.DrawSphere(transform.position, 0.3f);
        }
    }
}

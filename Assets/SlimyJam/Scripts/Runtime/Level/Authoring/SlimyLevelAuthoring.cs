using System.Collections.Generic;
using SlimyJam.Data;
using UnityEngine;

namespace SlimyJam.Authoring
{
    /// <summary>
    /// Level authoring kökü. Alt hiyerarşideki Dreamteck spline'ları yol geometrisini,
    /// <see cref="RopeAuthoring"/> ve <see cref="HoleAuthoring"/> bileşenleri ise başlangıç durumunu tanımlar.
    /// Bu veri build öncesinde node graph'a bake edilir; runtime JSON'a spline yazılmaz (GDD 4.4).
    /// </summary>
    public sealed class SlimyLevelAuthoring : MonoBehaviour
    {
        [Header("Bake")]
        public int levelIndex = 1;

        [Tooltip("Assets altında, bake çıktısının yazılacağı klasör.")]
        public string outputFolder = "Assets/SlimyJam/Resources/SlimyLevels";

        [Tooltip("Bu mesafeden yakın örneklenmiş noktalar tek logical node'da birleştirilir. " +
                 "Görsel kesişmelerin ortak node üretmesini garanti eder (GDD 4.2 kural 4).")]
        [Range(0.05f, 0.5f)]
        public float weldTolerance = 0.25f;

        [Tooltip("Rope/hole marker'ının bağlanabileceği en büyük node mesafesi.")]
        [Range(0.1f, 1.5f)]
        public float markerSnapDistance = 0.6f;

        public List<RopeAuthoring> CollectRopes()
        {
            var result = new List<RopeAuthoring>();
            GetComponentsInChildren(true, result);
            return result;
        }

        public List<HoleAuthoring> CollectHoles()
        {
            var result = new List<HoleAuthoring>();
            GetComponentsInChildren(true, result);
            return result;
        }
    }
}

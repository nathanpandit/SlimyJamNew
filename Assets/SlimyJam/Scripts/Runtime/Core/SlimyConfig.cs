using UnityEngine;

namespace SlimyJam.Core
{
    /// <summary>Ayarlanabilir parametreler (GDD 19). Tek bir ScriptableObject'te toplanır.</summary>
    [CreateAssetMenu(menuName = "Slimy Jam/Config", fileName = "SlimyConfig")]
    public sealed class SlimyConfig : ScriptableObject
    {
        [Header("Selection")]
        [Tooltip("Fat-finger endpoint seçim yarıçapı. Tasarım aralığı 1.0 - 1.5 world unit.")]
        [Range(0.5f, 3f)]
        public float selectionRadius = 1.25f;

        [Header("Movement")]
        [Tooltip("Active endpoint'in pointer'ı takip ederkenki maksimum hızı (unit/sn).")]
        public float maximumMoveSpeed = 14f;

        [Tooltip("Logical destination node'a geçiş sınırı (segment progress).")]
        [Range(0.05f, 0.95f)]
        public float midpointThreshold = 0.5f;

        [Tooltip("Release sonrası en yakın node merkezine görsel tamamlama süresi.")]
        [Range(0.02f, 0.4f)]
        public float snapDuration = 0.1f;

        [Header("Pathfinding")]
        [Tooltip("Junction çevresinde target segment oscillation'ını azaltan eşik.")]
        [Range(0f, 1f)]
        public float segmentTargetHysteresis = 0.2f;

        [Header("Collection")]
        [Tooltip("Hole'a çekilme animasyon süresi. Mechanical removal trigger anında gerçekleşir.")]
        [Range(0.1f, 1.5f)]
        public float holePullDuration = 0.4f;

        [Header("Visual")]
        public float ropeRadius = 0.34f;
        public float groundWidth = 0.7f;
        public float ropeHeight = 0.35f;
        public int splineSampleRate = 12;

        public static SlimyConfig CreateDefault()
        {
            return CreateInstance<SlimyConfig>();
        }
    }
}

using System.Collections.Generic;
using UnityEngine;

namespace SlimyJam.Visual
{
    /// <summary>
    /// Spline paketi seçimi gameplay modelini değiştirmemelidir; paket çağrıları bu arayüzün arkasında kalır
    /// (GDD 14.1 SplineAdapter, "Paket Bağımsızlığı").
    /// </summary>
    public interface ISplineAdapter
    {
        void Configure(Color color, float radius, int sampleRate, Material material);

        /// <summary>Logical node pozisyonlarından türetilmiş rope gövdesini günceller.</summary>
        void UpdatePoints(IReadOnlyList<Vector3> points);

        void SetVisible(bool visible);

        Transform Transform { get; }
    }
}

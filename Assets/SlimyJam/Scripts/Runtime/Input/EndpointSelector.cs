using SlimyJam.Gameplay;
using UnityEngine;

namespace SlimyJam.InputSystem
{
    /// <summary>Seçim adaylarını sağlayan kaynak. Test ve runtime aynı kuralı paylaşır.</summary>
    public interface IEndpointCandidateSource
    {
        int Count { get; }
        bool IsSelectable(int index);
        Vector3 GetEndpointPosition(int index, RopeEnd end);
    }

    /// <summary>
    /// Fat-finger endpoint seçimi (GDD 6.1, 15.1). Yalnızca head ve tail aday değerlendirilir;
    /// body segmentleri mesafe hesabına girmez. Eşit mesafede liste sırası, aynı rope içinde
    /// head -> tail önceliği geçerlidir.
    /// </summary>
    public static class EndpointSelector
    {
        public static bool TrySelect(IEndpointCandidateSource source, Vector3 pointerWorldPosition,
            float selectionRadius, out int index, out RopeEnd end)
        {
            index = -1;
            end = RopeEnd.Head;

            var bestDistance = selectionRadius;

            for (int i = 0; i < source.Count; i++)
            {
                if (!source.IsSelectable(i)) continue;

                for (int e = 0; e < 2; e++)
                {
                    var candidateEnd = e == 0 ? RopeEnd.Head : RopeEnd.Tail;
                    var distance = Vector3.Distance(pointerWorldPosition,
                        source.GetEndpointPosition(i, candidateEnd));

                    // Katı küçüktür: eşit mesafede önce değerlendirilen aday kazanır.
                    if (distance >= bestDistance) continue;

                    bestDistance = distance;
                    index = i;
                    end = candidateEnd;
                }
            }

            return index >= 0;
        }
    }
}

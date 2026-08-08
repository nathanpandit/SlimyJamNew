using System.Collections.Generic;
using SlimyJam.Data;
using UnityEngine;

namespace SlimyJam.Visual
{
    /// <summary>Renk paleti ve runtime material üretimi. Art direction GDD kapsamı dışıdır; okunabilirlik esastır.</summary>
    public static class SlimyPalette
    {
        private static readonly Dictionary<RopeColor, Color> Colors = new Dictionary<RopeColor, Color>
        {
            { RopeColor.Red, new Color(0.87f, 0.24f, 0.24f) },
            { RopeColor.Blue, new Color(0.24f, 0.60f, 0.90f) },
            { RopeColor.Green, new Color(0.32f, 0.76f, 0.36f) },
            { RopeColor.Yellow, new Color(0.97f, 0.80f, 0.20f) },
            { RopeColor.Purple, new Color(0.63f, 0.36f, 0.87f) },
            { RopeColor.Pink, new Color(0.95f, 0.47f, 0.71f) },
            { RopeColor.Orange, new Color(0.95f, 0.55f, 0.20f) },
            { RopeColor.DarkBlue, new Color(0.16f, 0.28f, 0.62f) }
        };

        private static readonly Dictionary<Color, Material> MaterialCache = new Dictionary<Color, Material>();

        public static readonly Color Hidden = new Color(0.55f, 0.58f, 0.62f);
        public static readonly Color Key = new Color(1f, 0.78f, 0.18f);
        public static readonly Color Frozen = new Color(0.46f, 0.88f, 1f);
        public static readonly Color Lock = new Color(0.08f, 0.09f, 0.11f);
        public static readonly Color Wall = new Color(0.12f, 0.14f, 0.17f);

        public static Color Get(RopeColor color)
        {
            return Colors.TryGetValue(color, out var value) ? value : Color.white;
        }

        public static Material GetMaterial(Color color)
        {
            if (MaterialCache.TryGetValue(color, out var cached) && cached != null) return cached;

            var shader = Shader.Find("Universal Render Pipeline/Lit")
                         ?? Shader.Find("Standard")
                         ?? Shader.Find("Unlit/Color");

            var material = new Material(shader) { name = $"Slimy_{ColorUtility.ToHtmlStringRGB(color)}" };
            SetColor(material, color);

            MaterialCache[color] = material;
            return material;
        }

        public static Material GetMaterial(RopeColor color) => GetMaterial(Get(color));

        private static void SetColor(Material material, Color color)
        {
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color")) material.SetColor("_Color", color);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.35f);
        }

        public static void ClearCache() => MaterialCache.Clear();
    }
}

using System;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

namespace SlimyJam.Data
{
    /// <summary>
    /// Level JSON okuma/yazma. GDD 12.3'teki gibi renkler dosyada okunabilir isimle tutulur
    /// ("color": "Green"); JsonUtility enum'ları yalnızca sayı olarak çözdüğü için isimler
    /// deserialize öncesinde sayıya çevrilir.
    /// </summary>
    public static class SlimyLevelJson
    {
        private static readonly Regex ColorPattern =
            new Regex("\"color\"\\s*:\\s*\"(?<name>[A-Za-z]+)\"", RegexOptions.Compiled);

        public static SlimyLevelData Parse(string json)
        {
            if (string.IsNullOrEmpty(json)) return null;

            var normalized = ColorPattern.Replace(json, match =>
            {
                var name = match.Groups["name"].Value;
                if (Enum.TryParse<RopeColor>(name, true, out var color))
                {
                    return $"\"color\":{(int)color}";
                }

                Debug.LogWarning($"[SlimyJam] Unknown rope color '{name}', falling back to Red.");
                return "\"color\":0";
            });

            return JsonUtility.FromJson<SlimyLevelData>(normalized);
        }

        /// <summary>Okunabilir renk isimleriyle JSON üretir (editor bake çıktısı için).</summary>
        public static string ToJson(SlimyLevelData data, bool prettyPrint = true)
        {
            var json = JsonUtility.ToJson(data, prettyPrint);
            return ReplaceColorNumbersWithNames(json);
        }

        private static readonly Regex ColorNumberPattern =
            new Regex("\"color\"\\s*:\\s*(?<value>\\d+)", RegexOptions.Compiled);

        private static string ReplaceColorNumbersWithNames(string json)
        {
            return ColorNumberPattern.Replace(json, match =>
            {
                var value = int.Parse(match.Groups["value"].Value);
                var name = Enum.IsDefined(typeof(RopeColor), value)
                    ? ((RopeColor)value).ToString()
                    : RopeColor.Red.ToString();
                return $"\"color\": \"{name}\"";
            });
        }

        /// <summary>Doğrulama hatalarını okunur biçimde toplar.</summary>
        public static string DescribeValidation(SlimyLevelData data)
        {
            var builder = new StringBuilder();
            builder.AppendLine(LevelDataValidator.Validate(data, out var error) ? "Level data is valid." : error);
            builder.AppendLine($"nodes: {data.groundNodes.Count}, ropes: {data.ropes.Count}, holes: {data.holes.Count}");
            return builder.ToString();
        }
    }
}

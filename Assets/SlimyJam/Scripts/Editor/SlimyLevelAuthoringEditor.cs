using System.IO;
using SlimyJam.Authoring;
using SlimyJam.Data;
using UnityEditor;
using UnityEngine;

namespace SlimyJam.EditorTools
{
    [CustomEditor(typeof(SlimyLevelAuthoring))]
    public sealed class SlimyLevelAuthoringEditor : Editor
    {
        private string _report;

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var authoring = (SlimyLevelAuthoring)target;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Bake", EditorStyles.boldLabel);

            if (GUILayout.Button("Validate (dry run)"))
            {
                var result = SplineGraphBaker.Bake(authoring);
                _report = SplineGraphBaker.DescribeResult(result);
            }

            if (GUILayout.Button("Bake to JSON"))
            {
                _report = BakeAndSave(authoring);
            }

            if (string.IsNullOrEmpty(_report)) return;

            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(_report, MessageType.Info);
        }

        private static string BakeAndSave(SlimyLevelAuthoring authoring)
        {
            var result = SplineGraphBaker.Bake(authoring);
            var report = SplineGraphBaker.DescribeResult(result);

            if (!result.Success)
            {
                Debug.LogError($"[SlimyJam] Bake failed:\n{report}");
                return report;
            }

            Directory.CreateDirectory(authoring.outputFolder);
            var path = Path.Combine(authoring.outputFolder, $"Level_{authoring.levelIndex}.json");
            File.WriteAllText(path, SlimyLevelJson.ToJson(result.Data));
            AssetDatabase.Refresh();

            Debug.Log($"[SlimyJam] Baked level to {path}\n{report}");
            return $"Saved: {path}\n{report}";
        }
    }
}

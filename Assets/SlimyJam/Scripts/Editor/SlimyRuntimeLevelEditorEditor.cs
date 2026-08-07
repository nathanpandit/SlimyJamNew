using SlimyJam.LevelEditing;
using UnityEditor;
using UnityEngine;

namespace SlimyJam.EditorTools
{
    [CustomEditor(typeof(SlimyRuntimeLevelEditor))]
    public sealed class SlimyRuntimeLevelEditorEditor : Editor
    {
        private SerializedProperty levelIndex;
        private SerializedProperty outputFolder;
        private SerializedProperty mode;
        private SerializedProperty paintColor;
        private SerializedProperty hiddenRevealAfterCollections;
        private SerializedProperty lockedHoleKeyCount;
        private SerializedProperty wallRopeCollectionCount;
        private SerializedProperty containedRopeOuterId;
        private SerializedProperty targetCamera;
        private SerializedProperty graphPlaneY;
        private SerializedProperty limitInputToBoard;
        private SerializedProperty strokeSampleMinDistance;
        private SerializedProperty nodePickRadius;
        private SerializedProperty pathEraseRadius;
        private SerializedProperty autoStraighten;
        private SerializedProperty straightenTolerance;
        private SerializedProperty holdShiftForStraightLine;
        private SerializedProperty angleSnap;
        private SerializedProperty customAngleSnapDegrees;
        private SerializedProperty snapToGrid;
        private SerializedProperty gridSize;
        private SerializedProperty snapMode;
        private SerializedProperty cleanCurvesOnRelease;
        private SerializedProperty curveCleanupTolerance;
        private SerializedProperty curveCleanupSmoothingIterations;
        private SerializedProperty unitDistance;
        private SerializedProperty weldTolerance;
        private SerializedProperty mergeShortJunctionEdges;
        private SerializedProperty shortJunctionEdgeMergeUnitFactor;
        private SerializedProperty strokeSimplifyTolerance;
        private SerializedProperty smoothingIterations;
        private SerializedProperty segmentLengthWarningTolerance;
        private SerializedProperty occupantSnapDistance;
        private SerializedProperty preserveOccupantsOnBake;
        private SerializedProperty previewDuringPlay;
        private SerializedProperty showBoard;
        private SerializedProperty boardSize;
        private SerializedProperty boardGridSpacing;
        private SerializedProperty boardGridWidth;
        private SerializedProperty previewLift;
        private SerializedProperty strokePreviewWidth;
        private SerializedProperty groundPreviewWidth;
        private SerializedProperty ropePreviewWidth;
        private SerializedProperty nodePreviewSize;
        private SerializedProperty holePreviewSize;

        private void OnEnable()
        {
            levelIndex = serializedObject.FindProperty("levelIndex");
            outputFolder = serializedObject.FindProperty("outputFolder");
            mode = serializedObject.FindProperty("mode");
            paintColor = serializedObject.FindProperty("paintColor");
            hiddenRevealAfterCollections = serializedObject.FindProperty("hiddenRevealAfterCollections");
            lockedHoleKeyCount = serializedObject.FindProperty("lockedHoleKeyCount");
            wallRopeCollectionCount = serializedObject.FindProperty("wallRopeCollectionCount");
            containedRopeOuterId = serializedObject.FindProperty("containedRopeOuterId");
            targetCamera = serializedObject.FindProperty("targetCamera");
            graphPlaneY = serializedObject.FindProperty("graphPlaneY");
            limitInputToBoard = serializedObject.FindProperty("limitInputToBoard");
            strokeSampleMinDistance = serializedObject.FindProperty("strokeSampleMinDistance");
            nodePickRadius = serializedObject.FindProperty("nodePickRadius");
            pathEraseRadius = serializedObject.FindProperty("pathEraseRadius");
            autoStraighten = serializedObject.FindProperty("autoStraighten");
            straightenTolerance = serializedObject.FindProperty("straightenTolerance");
            holdShiftForStraightLine = serializedObject.FindProperty("holdShiftForStraightLine");
            angleSnap = serializedObject.FindProperty("angleSnap");
            customAngleSnapDegrees = serializedObject.FindProperty("customAngleSnapDegrees");
            snapToGrid = serializedObject.FindProperty("snapToGrid");
            gridSize = serializedObject.FindProperty("gridSize");
            snapMode = serializedObject.FindProperty("snapMode");
            cleanCurvesOnRelease = serializedObject.FindProperty("cleanCurvesOnRelease");
            curveCleanupTolerance = serializedObject.FindProperty("curveCleanupTolerance");
            curveCleanupSmoothingIterations = serializedObject.FindProperty("curveCleanupSmoothingIterations");
            unitDistance = serializedObject.FindProperty("unitDistance");
            weldTolerance = serializedObject.FindProperty("weldTolerance");
            mergeShortJunctionEdges = serializedObject.FindProperty("mergeShortJunctionEdges");
            shortJunctionEdgeMergeUnitFactor = serializedObject.FindProperty("shortJunctionEdgeMergeUnitFactor");
            strokeSimplifyTolerance = serializedObject.FindProperty("strokeSimplifyTolerance");
            smoothingIterations = serializedObject.FindProperty("smoothingIterations");
            segmentLengthWarningTolerance = serializedObject.FindProperty("segmentLengthWarningTolerance");
            occupantSnapDistance = serializedObject.FindProperty("occupantSnapDistance");
            preserveOccupantsOnBake = serializedObject.FindProperty("preserveOccupantsOnBake");
            previewDuringPlay = serializedObject.FindProperty("previewDuringPlay");
            showBoard = serializedObject.FindProperty("showBoard");
            boardSize = serializedObject.FindProperty("boardSize");
            boardGridSpacing = serializedObject.FindProperty("boardGridSpacing");
            boardGridWidth = serializedObject.FindProperty("boardGridWidth");
            previewLift = serializedObject.FindProperty("previewLift");
            strokePreviewWidth = serializedObject.FindProperty("strokePreviewWidth");
            groundPreviewWidth = serializedObject.FindProperty("groundPreviewWidth");
            ropePreviewWidth = serializedObject.FindProperty("ropePreviewWidth");
            nodePreviewSize = serializedObject.FindProperty("nodePreviewSize");
            holePreviewSize = serializedObject.FindProperty("holePreviewSize");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            DrawLevelSection();
            DrawElementsSection();
            DrawInputSection();
            DrawDrawingAssistSection();
            DrawBakeSection();
            DrawPreviewSection();

            serializedObject.ApplyModifiedProperties();

            var levelEditor = (SlimyRuntimeLevelEditor)target;

            EditorGUILayout.Space();
            DrawRuntimeState(levelEditor);
            DrawActions(levelEditor);
            DrawReport(levelEditor);
        }

        private void DrawLevelSection()
        {
            EditorGUILayout.LabelField("Level", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(levelIndex);
            EditorGUILayout.PropertyField(outputFolder);
            EditorGUILayout.PropertyField(mode);
            EditorGUILayout.PropertyField(paintColor);
        }

        private void DrawElementsSection()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Elements", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(hiddenRevealAfterCollections);
            EditorGUILayout.PropertyField(lockedHoleKeyCount);
            EditorGUILayout.PropertyField(wallRopeCollectionCount);
            EditorGUILayout.PropertyField(containedRopeOuterId);
        }

        private void DrawInputSection()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Input", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(targetCamera);
            EditorGUILayout.PropertyField(graphPlaneY);
            EditorGUILayout.PropertyField(limitInputToBoard);
            EditorGUILayout.PropertyField(strokeSampleMinDistance);
            EditorGUILayout.PropertyField(nodePickRadius);
            EditorGUILayout.PropertyField(pathEraseRadius);
        }

        private void DrawDrawingAssistSection()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Drawing Assist", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(autoStraighten);
            EditorGUILayout.PropertyField(straightenTolerance);
            EditorGUILayout.PropertyField(holdShiftForStraightLine);
            EditorGUILayout.PropertyField(angleSnap);
            EditorGUILayout.PropertyField(customAngleSnapDegrees);
            EditorGUILayout.PropertyField(snapToGrid);
            EditorGUILayout.PropertyField(gridSize);
            EditorGUILayout.PropertyField(snapMode);
            EditorGUILayout.PropertyField(cleanCurvesOnRelease);
            EditorGUILayout.PropertyField(curveCleanupTolerance);
            EditorGUILayout.PropertyField(curveCleanupSmoothingIterations);
        }

        private void DrawBakeSection()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Bake", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(unitDistance);
            EditorGUILayout.PropertyField(weldTolerance);
            EditorGUILayout.PropertyField(mergeShortJunctionEdges);
            EditorGUILayout.PropertyField(shortJunctionEdgeMergeUnitFactor);
            EditorGUILayout.PropertyField(strokeSimplifyTolerance);
            EditorGUILayout.PropertyField(smoothingIterations);
            EditorGUILayout.PropertyField(segmentLengthWarningTolerance);
            EditorGUILayout.PropertyField(occupantSnapDistance);
            EditorGUILayout.PropertyField(preserveOccupantsOnBake);
        }

        private void DrawPreviewSection()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Preview", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(previewDuringPlay);
            EditorGUILayout.PropertyField(showBoard);
            EditorGUILayout.PropertyField(boardSize);
            EditorGUILayout.PropertyField(boardGridSpacing);
            EditorGUILayout.PropertyField(boardGridWidth);
            EditorGUILayout.PropertyField(previewLift);
            EditorGUILayout.PropertyField(strokePreviewWidth);
            EditorGUILayout.PropertyField(groundPreviewWidth);
            EditorGUILayout.PropertyField(ropePreviewWidth);
            EditorGUILayout.PropertyField(nodePreviewSize);
            EditorGUILayout.PropertyField(holePreviewSize);
        }

        private static void DrawRuntimeState(SlimyRuntimeLevelEditor levelEditor)
        {
            EditorGUILayout.LabelField("State", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Strokes", levelEditor.StrokeCount.ToString());
            EditorGUILayout.LabelField("Nodes", levelEditor.NodeCount.ToString());
            EditorGUILayout.LabelField("Edges", levelEditor.EdgeCount.ToString());
            EditorGUILayout.LabelField("Ropes", levelEditor.RopeCount.ToString());
            EditorGUILayout.LabelField("Holes", levelEditor.HoleCount.ToString());
            EditorGUILayout.LabelField("Walls", levelEditor.WallCount.ToString());
            EditorGUILayout.LabelField("Current Rope Nodes", levelEditor.CurrentRopeCount.ToString());
            EditorGUILayout.LabelField("Outer Rope", levelEditor.CurrentOuterRopeId == 0
                ? "None"
                : levelEditor.CurrentOuterRopeId.ToString());

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Enter Play Mode to draw paths and paint nodes.", MessageType.Info);
            }

            if (levelEditor.GraphDirty)
            {
                EditorGUILayout.HelpBox("Drawn strokes changed after the last bake.", MessageType.Warning);
            }
        }

        private void DrawActions(SlimyRuntimeLevelEditor levelEditor)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Actions", EditorStyles.boldLabel);

            if (GUILayout.Button("Bake Graph"))
            {
                RunAction(levelEditor, levelEditor.BakeGraph);
            }

            EditorGUILayout.BeginHorizontal();
            using (new EditorGUI.DisabledScope(levelEditor.CurrentRopeCount == 0))
            {
                if (GUILayout.Button("Finish Rope"))
                {
                    RunAction(levelEditor, levelEditor.FinishCurrentRope);
                }

                if (GUILayout.Button("Cancel Rope"))
                {
                    levelEditor.CancelCurrentRope();
                    MarkChanged(levelEditor);
                }
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Validate"))
            {
                levelEditor.ValidateLevelData(out _);
                MarkChanged(levelEditor);
            }

            if (GUILayout.Button("Save Level JSON"))
            {
                SaveLevel(levelEditor);
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Clear Occupants"))
            {
                if (EditorUtility.DisplayDialog("Clear Occupants",
                        "Remove all painted ropes, holes, and walls from the baked graph?", "Clear", "Cancel"))
                {
                    levelEditor.ClearOccupants();
                    MarkChanged(levelEditor);
                }
            }

            if (GUILayout.Button("Clear All"))
            {
                if (EditorUtility.DisplayDialog("Clear All",
                        "Remove all strokes, baked graph data, ropes, and holes?", "Clear", "Cancel"))
                {
                    levelEditor.ClearAll();
                    MarkChanged(levelEditor);
                }
            }

            EditorGUILayout.EndHorizontal();
        }

        private static void DrawReport(SlimyRuntimeLevelEditor levelEditor)
        {
            if (string.IsNullOrEmpty(levelEditor.LastReport)) return;

            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(levelEditor.LastReport, MessageType.Info);
        }

        private void RunAction(SlimyRuntimeLevelEditor levelEditor, System.Func<bool> action)
        {
            action();
            MarkChanged(levelEditor);
        }

        private static void SaveLevel(SlimyRuntimeLevelEditor levelEditor)
        {
            levelEditor.SaveLevelJson();
            MarkChanged(levelEditor);
        }

        private static void MarkChanged(SlimyRuntimeLevelEditor levelEditor)
        {
            EditorUtility.SetDirty(levelEditor);
            SceneView.RepaintAll();
        }
    }
}

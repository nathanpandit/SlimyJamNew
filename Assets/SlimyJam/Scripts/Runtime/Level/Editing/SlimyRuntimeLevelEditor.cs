using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using SlimyJam.Data;
using SlimyJam.Visual;
using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace SlimyJam.LevelEditing
{
    public enum SlimyRuntimeLevelEditorMode
    {
        DrawPath,
        Hole,
        Rope,
        Key,
        Hidden,
        Lock,
        Wall,
        DoubleRope,
        Erase
    }

    public enum SlimyRuntimeLevelEditorSnapMode
    {
        EndpointsOnly,
        WholeStroke
    }

    public enum SlimyRuntimeLevelEditorAngleSnap
    {
        Off,
        FortyFiveDegrees,
        NinetyDegrees,
        CustomDegrees
    }

    /// <summary>
    /// Play Mode authoring tool for drawing freeform paths, baking them into the existing SlimyLevelData
    /// graph format, then painting holes and ropes onto baked nodes.
    /// </summary>
    public sealed class SlimyRuntimeLevelEditor : MonoBehaviour
    {
        private const int FirstNodeId = 100;
        private const int FirstRopeId = 1;
        private const int FirstHoleId = 10;
        private const int FirstWallId = 1000;
        private const float RopePreviewLift = 0.08f;
        private const float ContainedRopePreviewLift = 0.26f;
        private const float IntersectionEpsilon = 0.0001f;

        [Header("Level")]
        [SerializeField] private int levelIndex = 1;
        [SerializeField] private string outputFolder = "Assets/SlimyJam/Resources/SlimyLevels";

        [Header("Mode")]
        [SerializeField] private SlimyRuntimeLevelEditorMode mode = SlimyRuntimeLevelEditorMode.DrawPath;
        [SerializeField] private RopeColor paintColor = RopeColor.Green;

        [Header("Elements")]
        [SerializeField, Min(1)] private int hiddenRevealAfterCollections = 3;
        [SerializeField, Min(1)] private int lockedHoleKeyCount = 3;
        [SerializeField, Min(1)] private int wallRopeCollectionCount = 3;
        [SerializeField, Tooltip("0 paints a normal rope. Choose an outer rope in Double Rope mode to paint an inner rope inside it.")]
        private int containedRopeOuterId;

        [Header("Input")]
        [SerializeField] private Camera targetCamera;
        [SerializeField] private float graphPlaneY;
        [SerializeField] private bool limitInputToBoard = true;
        [SerializeField, Min(0.01f)] private float strokeSampleMinDistance = 0.08f;
        [SerializeField, Min(0.01f)] private float nodePickRadius = 0.45f;
        [SerializeField, Min(0.01f)] private float pathEraseRadius = 0.35f;

        [Header("Drawing Assist")]
        [SerializeField] private bool autoStraighten = true;
        [SerializeField, Min(0f)] private float straightenTolerance = 0.25f;
        [SerializeField] private bool holdShiftForStraightLine = true;
        [SerializeField] private SlimyRuntimeLevelEditorAngleSnap angleSnap = SlimyRuntimeLevelEditorAngleSnap.FortyFiveDegrees;
        [SerializeField, Range(1f, 90f)] private float customAngleSnapDegrees = 5f;
        [SerializeField] private bool snapToGrid = true;
        [SerializeField, Min(0.05f)] private float gridSize = 0.5f;
        [SerializeField] private SlimyRuntimeLevelEditorSnapMode snapMode = SlimyRuntimeLevelEditorSnapMode.EndpointsOnly;
        [SerializeField] private bool cleanCurvesOnRelease = true;
        [SerializeField, Range(0f, 0.35f)] private float curveCleanupTolerance = 0.08f;
        [SerializeField, Range(0, 4)] private int curveCleanupSmoothingIterations = 1;

        [Header("Bake")]
        [SerializeField, Min(0.1f)] private float unitDistance = 1f;
        [SerializeField, Range(0.05f, 0.5f)] private float weldTolerance = 0.25f;
        [SerializeField] private bool mergeShortJunctionEdges = true;
        [SerializeField, Range(0.1f, 0.99f)] private float shortJunctionEdgeMergeUnitFactor = 0.99f;
        [SerializeField, Range(0f, 0.35f)] private float strokeSimplifyTolerance = 0.06f;
        [SerializeField, Range(0, 4)] private int smoothingIterations = 2;
        [SerializeField, Min(0.01f)] private float segmentLengthWarningTolerance = 0.15f;
        [SerializeField, Range(0.1f, 2f)] private float occupantSnapDistance = 0.65f;
        [SerializeField] private bool preserveOccupantsOnBake = true;

        [Header("Preview")]
        [SerializeField] private bool previewDuringPlay = true;
        [SerializeField] private bool showBoard = true;
        [SerializeField, Min(2f)] private float boardSize = 16f;
        [SerializeField, Min(0.25f)] private float boardGridSpacing = 1f;
        [SerializeField, Min(0.005f)] private float boardGridWidth = 0.025f;
        [SerializeField, Min(0f)] private float previewLift = 0.05f;
        [SerializeField, Min(0.02f)] private float strokePreviewWidth = 0.08f;
        [SerializeField, Min(0.02f)] private float groundPreviewWidth = 0.22f;
        [SerializeField, Min(0.02f)] private float ropePreviewWidth = 0.34f;
        [SerializeField, Min(0.02f)] private float nodePreviewSize = 0.22f;
        [SerializeField, Min(0.02f)] private float holePreviewSize = 0.45f;

        [SerializeField] private SlimyLevelData bakedData = new SlimyLevelData();
        [SerializeField] private List<StrokeData> strokes = new List<StrokeData>();
        [SerializeField] private List<int> currentRopeNodeIds = new List<int>();
        [SerializeField] private RopeColor currentRopeColor = RopeColor.Green;
        [SerializeField] private bool graphDirty;
        [SerializeField, TextArea(3, 10)] private string lastReport;

        private StrokeData currentStroke;
        private readonly List<Vector3> currentRawStrokePoints = new List<Vector3>();
        private Transform previewRoot;

        public int LevelIndex => levelIndex;
        public string OutputFolder => outputFolder;
        public string LastReport => lastReport;
        public int StrokeCount => strokes?.Count ?? 0;
        public int NodeCount => bakedData?.groundNodes?.Count ?? 0;
        public int EdgeCount => CountEdges(bakedData);
        public int RopeCount => bakedData?.ropes?.Count ?? 0;
        public int HoleCount => bakedData?.holes?.Count ?? 0;
        public int WallCount => bakedData?.walls?.Count ?? 0;
        public int CurrentRopeCount => currentRopeNodeIds?.Count ?? 0;
        public int CurrentOuterRopeId => containedRopeOuterId;
        public bool HasBakedGraph => NodeCount > 0;
        public bool GraphDirty => graphDirty;
        public SlimyLevelData BakedData => bakedData;

        private Camera Camera => targetCamera != null ? targetCamera : targetCamera = Camera.main;

        [Serializable]
        private sealed class StrokeData
        {
            public List<Vector3> points = new List<Vector3>();
        }

        private sealed class WorkingSegment
        {
            public int StrokeIndex;
            public int SegmentIndex;
            public Vector3 A;
            public Vector3 B;
            public readonly List<SegmentSplit> Splits = new List<SegmentSplit>();
        }

        private struct SegmentSplit
        {
            public float T;
            public Vector3 Position;

            public SegmentSplit(float t, Vector3 position)
            {
                T = t;
                Position = position;
            }
        }

        private sealed class HoleSnapshot
        {
            public int Id;
            public RopeColor Color;
            public Vector3 Position;
            public bool Hidden;
            public int RevealAfterCollections;
            public int LockedKeyCount;
        }

        private sealed class RopeSnapshot
        {
            public int Id;
            public RopeColor Color;
            public readonly List<Vector3> Positions = new List<Vector3>();
            public bool HasKey;
            public bool Hidden;
            public int RevealAfterCollections;
            public int ContainedByRopeId;
        }

        private sealed class WallSnapshot
        {
            public int Id;
            public Vector3 Position;
            public int RopeCollectionCount;
        }

        private enum EditorKey
        {
            Escape,
            Enter,
            P,
            R,
            H,
            E,
            F,
            C,
            S,
            B,
            X,
            V
        }

        private void Awake()
        {
#if !UNITY_EDITOR
            enabled = false;
            lastReport = "Slimy Runtime Level Editor is disabled outside the Unity Editor.";
#endif
        }

        private void OnEnable()
        {
            EnsureCollections();
            RebuildPreview();
        }

        private void OnDisable()
        {
            DestroyPreview();
        }

        private void OnValidate()
        {
            EnsureCollections();
            levelIndex = Mathf.Max(1, levelIndex);
            unitDistance = Mathf.Max(0.1f, unitDistance);
            strokeSampleMinDistance = Mathf.Max(0.01f, strokeSampleMinDistance);
            nodePickRadius = Mathf.Max(0.01f, nodePickRadius);
            pathEraseRadius = Mathf.Max(0.01f, pathEraseRadius);
            hiddenRevealAfterCollections = Mathf.Max(1, hiddenRevealAfterCollections);
            lockedHoleKeyCount = Mathf.Max(1, lockedHoleKeyCount);
            wallRopeCollectionCount = Mathf.Max(1, wallRopeCollectionCount);
            containedRopeOuterId = Mathf.Max(0, containedRopeOuterId);
            straightenTolerance = Mathf.Max(0f, straightenTolerance);
            customAngleSnapDegrees = Mathf.Clamp(customAngleSnapDegrees, 1f, 90f);
            gridSize = Mathf.Max(0.05f, gridSize);
            weldTolerance = Mathf.Max(0.01f, weldTolerance);
            shortJunctionEdgeMergeUnitFactor = Mathf.Clamp(shortJunctionEdgeMergeUnitFactor, 0.1f, 0.99f);
            occupantSnapDistance = Mathf.Max(0.01f, occupantSnapDistance);
            segmentLengthWarningTolerance = Mathf.Max(0.01f, segmentLengthWarningTolerance);
            boardSize = Mathf.Max(2f, boardSize);
            boardGridSpacing = Mathf.Max(0.25f, boardGridSpacing);
            boardGridWidth = Mathf.Max(0.005f, boardGridWidth);
        }

        private void Update()
        {
#if !UNITY_EDITOR
            return;
#else
            if (!Application.isPlaying) return;

            HandleKeyboard();

            if (!TryReadPointer(out var screenPosition, out var pressed, out var held, out var released))
            {
                return;
            }

            if (mode == SlimyRuntimeLevelEditorMode.DrawPath)
            {
                HandleDrawInput(screenPosition, pressed, held, released);
                return;
            }

            if (mode == SlimyRuntimeLevelEditorMode.Erase)
            {
                if (pressed || held) HandleEraseInput(screenPosition, pressed);
                return;
            }

            if (pressed)
            {
                HandleNodePaintInput(screenPosition);
            }
#endif
        }

        public bool BakeGraph()
        {
            EnsureCollections();
            var warnings = new List<string>();

            if (strokes.Count == 0)
            {
                SetReport("Bake failed: draw at least one path stroke first.");
                return false;
            }

            var holeSnapshots = new List<HoleSnapshot>();
            var ropeSnapshots = new List<RopeSnapshot>();
            var wallSnapshots = new List<WallSnapshot>();
            if (preserveOccupantsOnBake)
            {
                CaptureOccupants(holeSnapshots, ropeSnapshots, wallSnapshots);
            }

            var segments = BuildWorkingSegments(warnings);
            if (segments.Count == 0)
            {
                SetReport("Bake failed: no usable path segments were produced from the drawn strokes.");
                return false;
            }

            SplitSegmentsAtIntersections(segments, warnings);

            var welder = new NodeWelder(weldTolerance);
            var connections = new Dictionary<int, List<int>>();
            var edges = new HashSet<long>();

            for (int i = 0; i < segments.Count; i++)
            {
                ConnectSplitSegment(segments[i], welder, connections, edges, warnings);
            }

            if (welder.Nodes.Count == 0)
            {
                SetReport("Bake failed: no graph nodes were created.");
                return false;
            }

            var data = new SlimyLevelData { levelIndex = levelIndex };
            for (int i = 0; i < welder.Nodes.Count; i++)
            {
                var id = FirstNodeId + i;
                data.groundNodes.Add(new GroundNodeData
                {
                    id = id,
                    position = welder.Nodes[i],
                    connectedNodeIds = connections.TryGetValue(id, out var list) ? list : new List<int>()
                });
            }

            MergeShortJunctionEdges(data, warnings);

            if (preserveOccupantsOnBake)
            {
                RestoreOccupants(holeSnapshots, ropeSnapshots, wallSnapshots, data, warnings);
            }

            bakedData = data;
            currentRopeNodeIds.Clear();
            graphDirty = false;

            ValidateLevelData(out var validationReport);
            SetReport(BuildBakeReport(warnings, validationReport));
            RebuildPreview();
            return true;
        }

        public bool FinishCurrentRope()
        {
            EnsureCollections();

            if (currentRopeNodeIds.Count < 2)
            {
                SetReport("Rope needs at least 2 nodes before it can be finished.");
                return false;
            }

            var rope = new RopeData
            {
                id = NextRopeId(),
                color = currentRopeColor,
                occupiedNodeIds = new List<int>(currentRopeNodeIds),
                containedByRopeId = containedRopeOuterId
            };

            if (!ValidateRopeChain(rope, out var error))
            {
                SetReport($"Rope could not be finished: {error}");
                return false;
            }

            bakedData.ropes.Add(rope);
            currentRopeNodeIds.Clear();
            if (rope.containedByRopeId > 0) containedRopeOuterId = 0;

            SetReport(rope.containedByRopeId > 0
                ? $"Added inner rope {rope.id} ({rope.color}) inside rope {rope.containedByRopeId}."
                : $"Added rope {rope.id} ({rope.color}) with {rope.occupiedNodeIds.Count} nodes.");
            RebuildPreview();
            return true;
        }

        public void CancelCurrentRope()
        {
            EnsureCollections();

            if (currentRopeNodeIds.Count == 0)
            {
                SetReport("No current rope to cancel.");
                return;
            }

            currentRopeNodeIds.Clear();
            SetReport("Current rope cancelled.");
            RebuildPreview();
        }

        public void ClearOccupants()
        {
            EnsureCollections();
            bakedData.ropes.Clear();
            bakedData.holes.Clear();
            bakedData.walls.Clear();
            currentRopeNodeIds.Clear();
            containedRopeOuterId = 0;
            SetReport("Cleared all painted ropes, holes, and walls.");
            RebuildPreview();
        }

        public void ClearAll()
        {
            EnsureCollections();
            strokes.Clear();
            currentStroke = null;
            currentRawStrokePoints.Clear();
            currentRopeNodeIds.Clear();
            bakedData = new SlimyLevelData { levelIndex = levelIndex };
            graphDirty = false;
            containedRopeOuterId = 0;
            SetReport("Cleared strokes, baked graph, ropes, holes, and walls.");
            RebuildPreview();
        }

        public bool ValidateLevelData(out string report)
        {
            EnsureCollections();

            var data = CloneData(bakedData);
            data.levelIndex = levelIndex;

            var errors = new List<string>();
            var warnings = new List<string>();

            if (data.groundNodes.Count == 0)
            {
                errors.Add("No baked graph exists. Draw paths, then press Bake Graph.");
            }

            if (graphDirty)
            {
                errors.Add("Drawn strokes changed after the last bake. Press Bake Graph before saving.");
            }

            if (currentRopeNodeIds.Count > 0)
            {
                errors.Add("A rope is still being painted. Finish it or cancel it before saving.");
            }

            if (!LevelDataValidator.Validate(data, out var runtimeError))
            {
                errors.Add(runtimeError);
            }

            AddEditorValidation(data, errors, warnings);
            report = BuildValidationReport(data, errors, warnings);
            SetReport(report);
            return errors.Count == 0;
        }

        public bool TryBuildExportData(out SlimyLevelData data, out string report)
        {
            data = null;
            if (!ValidateLevelData(out report)) return false;

            data = CloneData(bakedData);
            data.levelIndex = levelIndex;
            return true;
        }

        public void MarkSaved(string path)
        {
            SetReport($"Saved: {path}\n{lastReport}");
        }

        public bool SaveLevelJson()
        {
            if (!TryBuildExportData(out var data, out var report))
            {
                Debug.LogError($"[SlimyJam] Runtime level editor save failed:\n{report}");
                return false;
            }

            Directory.CreateDirectory(outputFolder);
            var path = Path.Combine(outputFolder, $"Level_{levelIndex}.json");
            File.WriteAllText(path, SlimyLevelJson.ToJson(data));
            RefreshAssetDatabase();

            MarkSaved(path);
            Debug.Log($"[SlimyJam] Saved runtime-authored level to {path}");
            return true;
        }

        private static void RefreshAssetDatabase()
        {
#if UNITY_EDITOR
            var assetDatabase = Type.GetType("UnityEditor.AssetDatabase, UnityEditor");
            var refresh = assetDatabase?.GetMethod("Refresh", Type.EmptyTypes);
            refresh?.Invoke(null, null);
#endif
        }

        public void RebuildPreview()
        {
            if (!previewDuringPlay || !Application.isPlaying) return;

            EnsureCollections();
            EnsurePreviewRoot();
            ClearPreviewChildren();

            DrawBoardPreview();
            DrawStrokePreview();
            DrawGraphPreview();
            DrawHolePreview();
            DrawWallPreview();
            DrawRopePreview();
        }

        private void HandleKeyboard()
        {
            if (WasKeyPressed(EditorKey.Escape))
            {
                if (currentStroke != null)
                {
                    currentStroke = null;
                    currentRawStrokePoints.Clear();
                    SetReport("Current stroke cancelled.");
                    RebuildPreview();
                    return;
                }

                CancelCurrentRope();
                return;
            }

            if (WasKeyPressed(EditorKey.P))
            {
                SetEditorMode(SlimyRuntimeLevelEditorMode.DrawPath);
                return;
            }

            if (WasKeyPressed(EditorKey.R))
            {
                SetEditorMode(SlimyRuntimeLevelEditorMode.Rope);
                return;
            }

            if (WasKeyPressed(EditorKey.H))
            {
                SetEditorMode(SlimyRuntimeLevelEditorMode.Hole);
                return;
            }

            if (WasKeyPressed(EditorKey.E))
            {
                SetEditorMode(SlimyRuntimeLevelEditorMode.Erase);
                return;
            }

            if (WasKeyPressed(EditorKey.F) || WasKeyPressed(EditorKey.Enter))
            {
                FinishCurrentRope();
                return;
            }

            if (WasKeyPressed(EditorKey.C))
            {
                CancelCurrentRope();
                return;
            }

            if (WasKeyPressed(EditorKey.S))
            {
                SaveLevelJson();
                return;
            }

            if (WasKeyPressed(EditorKey.B))
            {
                BakeGraph();
                return;
            }

            if (WasKeyPressed(EditorKey.X))
            {
                ClearAll();
                return;
            }

            if (WasKeyPressed(EditorKey.V))
            {
                ValidateLevelData(out _);
                return;
            }

            for (int i = 1; i <= 9; i++)
            {
                if (!WasNumberKeyPressed(i)) continue;

                SetPaintColorFromShortcut(i);
                return;
            }
        }

        private void SetEditorMode(SlimyRuntimeLevelEditorMode nextMode)
        {
            if (mode == nextMode)
            {
                SetReport($"Mode is already {FormatModeName(mode)}.");
                return;
            }

            if (currentStroke != null)
            {
                currentStroke = null;
                currentRawStrokePoints.Clear();
                RebuildPreview();
            }

            mode = nextMode;
            SetReport($"Mode set to {FormatModeName(mode)}.");
        }

        private void SetPaintColorFromShortcut(int number)
        {
            var colors = (RopeColor[])Enum.GetValues(typeof(RopeColor));
            var index = number - 1;
            if (index < 0 || index >= colors.Length)
            {
                SetReport($"No rope color is mapped to number {number}.");
                return;
            }

            paintColor = colors[index];
            if (currentRopeNodeIds.Count == 0) currentRopeColor = paintColor;
            SetReport($"Paint color set to {number}: {paintColor}.");
        }

        private static string FormatModeName(SlimyRuntimeLevelEditorMode value)
        {
            return value == SlimyRuntimeLevelEditorMode.DrawPath ? "Path" : value.ToString();
        }

        private void HandleDrawInput(Vector2 screenPosition, bool pressed, bool held, bool released)
        {
            if (pressed)
            {
                BeginStroke(screenPosition);
                return;
            }

            if (released && currentStroke != null)
            {
                AddStrokePoint(screenPosition, true);
                EndStroke();
                return;
            }

            if (held && currentStroke != null)
            {
                AddStrokePoint(screenPosition, false);
            }
        }

        private void BeginStroke(Vector2 screenPosition)
        {
            if (!TryGetWorldPosition(screenPosition, out var worldPosition)) return;

            currentStroke = new StrokeData();
            currentRawStrokePoints.Clear();
            currentRawStrokePoints.Add(ProjectToGraphPlane(worldPosition));
            RefreshCurrentStrokePreview();
            RebuildPreview();
        }

        private void AddStrokePoint(Vector2 screenPosition, bool force)
        {
            if (currentStroke == null || !TryGetWorldPosition(screenPosition, out var worldPosition)) return;

            var point = ProjectToGraphPlane(worldPosition);
            if (!force && currentRawStrokePoints.Count > 0 &&
                DistanceSqrXZ(currentRawStrokePoints[currentRawStrokePoints.Count - 1], point) <
                strokeSampleMinDistance * strokeSampleMinDistance)
            {
                return;
            }

            currentRawStrokePoints.Add(point);
            RefreshCurrentStrokePreview();
            RebuildPreview();
        }

        private void EndStroke()
        {
            if (currentStroke == null) return;

            var finalPoints = BuildAssistedStroke(currentRawStrokePoints, IsStraightLineModifierActive(), false,
                out var straightened, out var snapped, out var cleaned);

            if (finalPoints.Count < 2 || PolylineLength(finalPoints) < strokeSampleMinDistance)
            {
                currentStroke = null;
                currentRawStrokePoints.Clear();
                SetReport("Stroke discarded because it was too short.");
                RebuildPreview();
                return;
            }

            currentStroke.points.Clear();
            currentStroke.points.AddRange(finalPoints);
            strokes.Add(currentStroke);
            currentStroke = null;
            currentRawStrokePoints.Clear();
            graphDirty = HasBakedGraph;
            SetReport(
                $"Added {BuildStrokeAssistLabel(straightened, snapped, cleaned)}stroke {strokes.Count}. " +
                "Press Bake Graph when you are ready to update the node graph.");
            RebuildPreview();
        }

        private void RefreshCurrentStrokePreview()
        {
            if (currentStroke == null) return;

            currentStroke.points.Clear();
            var points = BuildAssistedStroke(currentRawStrokePoints, IsStraightLineModifierActive(), true,
                out _, out _, out _);
            currentStroke.points.AddRange(points);
        }

        private List<Vector3> BuildAssistedStroke(IReadOnlyList<Vector3> source, bool forceStraight, bool forPreview,
            out bool straightened, out bool snapped, out bool cleaned)
        {
            straightened = false;
            snapped = false;
            cleaned = false;

            var points = CopyProjectedPoints(source);
            if (points.Count == 0) return points;

            if (points.Count == 1)
            {
                if (snapToGrid)
                {
                    points[0] = SnapPoint(points[0]);
                    snapped = true;
                }

                ClampPointsToBoard(points);
                return points;
            }

            var shouldStraighten = forceStraight || (!forPreview && autoStraighten && IsMostlyStraight(points));
            if (shouldStraighten)
            {
                straightened = true;
                return BuildStraightenedStroke(points, out snapped);
            }

            if (!forPreview && cleanCurvesOnRelease)
            {
                points = CleanCurveStroke(points);
                cleaned = true;
            }

            points = ApplyStrokeSnapping(points, out snapped);
            ClampPointsToBoard(points);
            return RemoveTinySegments(points, Mathf.Min(strokeSampleMinDistance * 0.5f, gridSize * 0.25f));
        }

        private List<Vector3> CopyProjectedPoints(IReadOnlyList<Vector3> source)
        {
            var result = new List<Vector3>();
            if (source == null) return result;

            for (int i = 0; i < source.Count; i++)
            {
                result.Add(ProjectToGraphPlane(source[i]));
            }

            return result;
        }

        private bool IsMostlyStraight(IReadOnlyList<Vector3> points)
        {
            if (points == null || points.Count < 2) return false;
            if (points.Count == 2) return true;

            var start = points[0];
            var end = points[points.Count - 1];
            var chordLength = Mathf.Sqrt(DistanceSqrXZ(start, end));
            if (chordLength < strokeSampleMinDistance * 2f) return false;

            var toleranceSqr = straightenTolerance * straightenTolerance;
            for (int i = 1; i < points.Count - 1; i++)
            {
                if (DistancePointToSegmentSqrXZ(points[i], start, end) > toleranceSqr) return false;
            }

            return PolylineLength(points) <= chordLength * 1.25f;
        }

        private List<Vector3> BuildStraightenedStroke(IReadOnlyList<Vector3> points, out bool snapped)
        {
            snapped = snapToGrid;

            var rawStart = points[0];
            var rawEnd = points[points.Count - 1];
            var start = snapToGrid ? SnapPoint(rawStart) : rawStart;
            var end = rawEnd;

            if (angleSnap != SlimyRuntimeLevelEditorAngleSnap.Off)
            {
                var rawDelta = new Vector2(rawEnd.x - rawStart.x, rawEnd.z - rawStart.z);
                end = GetAngleSnappedEnd(start, rawDelta);
            }
            else if (snapToGrid)
            {
                end = SnapPoint(rawEnd);
            }

            var result = new List<Vector3> { start, ProjectToGraphPlane(end) };
            ClampPointsToBoard(result);
            return RemoveTinySegments(result, 0.001f);
        }

        private Vector3 GetAngleSnappedEnd(Vector3 start, Vector2 rawDelta)
        {
            var length = rawDelta.magnitude;
            if (length <= Mathf.Epsilon) return start;

            var step = GetAngleSnapStep();
            var angle = Mathf.Atan2(rawDelta.y, rawDelta.x) * Mathf.Rad2Deg;
            var snappedAngle = Mathf.Round(angle / step) * step * Mathf.Deg2Rad;
            var direction = new Vector2(Mathf.Cos(snappedAngle), Mathf.Sin(snappedAngle));

            if (!snapToGrid)
            {
                return new Vector3(
                    start.x + direction.x * length,
                    graphPlaneY,
                    start.z + direction.y * length);
            }

            var size = Mathf.Max(0.05f, gridSize);
            if (angleSnap == SlimyRuntimeLevelEditorAngleSnap.CustomDegrees)
            {
                var snappedDistance = Mathf.Round(length / size) * size;
                return new Vector3(
                    start.x + direction.x * snappedDistance,
                    graphPlaneY,
                    start.z + direction.y * snappedDistance);
            }

            var xSign = Mathf.Sign(Mathf.Abs(direction.x) <= 0.001f ? rawDelta.x : direction.x);
            var zSign = Mathf.Sign(Mathf.Abs(direction.y) <= 0.001f ? rawDelta.y : direction.y);

            if (Mathf.Abs(direction.x) > 0.9f)
            {
                var distance = Mathf.Round(length / size) * size;
                return new Vector3(start.x + xSign * distance, graphPlaneY, start.z);
            }

            if (Mathf.Abs(direction.y) > 0.9f)
            {
                var distance = Mathf.Round(length / size) * size;
                return new Vector3(start.x, graphPlaneY, start.z + zSign * distance);
            }

            var component = Mathf.Round(length / Mathf.Sqrt(2f) / size) * size;
            return new Vector3(start.x + xSign * component, graphPlaneY, start.z + zSign * component);
        }

        private float GetAngleSnapStep()
        {
            switch (angleSnap)
            {
                case SlimyRuntimeLevelEditorAngleSnap.NinetyDegrees:
                    return 90f;
                case SlimyRuntimeLevelEditorAngleSnap.CustomDegrees:
                    return Mathf.Clamp(customAngleSnapDegrees, 1f, 90f);
                case SlimyRuntimeLevelEditorAngleSnap.FortyFiveDegrees:
                default:
                    return 45f;
            }
        }

        private List<Vector3> CleanCurveStroke(IReadOnlyList<Vector3> source)
        {
            var result = RemoveTinySegments(source, strokeSampleMinDistance * 0.5f);

            if (curveCleanupTolerance > 0f)
            {
                result = SimplifyPolyline(result, curveCleanupTolerance);
            }

            for (int i = 0; i < curveCleanupSmoothingIterations; i++)
            {
                result = SmoothPolyline(result);
            }

            return result;
        }

        private List<Vector3> ApplyStrokeSnapping(IReadOnlyList<Vector3> source, out bool snapped)
        {
            var result = new List<Vector3>();
            if (source == null)
            {
                snapped = false;
                return result;
            }

            result.AddRange(source);
            if (!snapToGrid || result.Count == 0)
            {
                snapped = false;
                return result;
            }

            snapped = true;
            if (snapMode == SlimyRuntimeLevelEditorSnapMode.WholeStroke)
            {
                for (int i = 0; i < result.Count; i++) result[i] = SnapPoint(result[i]);
                return result;
            }

            result[0] = SnapPoint(result[0]);
            result[result.Count - 1] = SnapPoint(result[result.Count - 1]);
            return result;
        }

        private Vector3 SnapPoint(Vector3 point)
        {
            var size = Mathf.Max(0.05f, gridSize);
            return new Vector3(
                Mathf.Round(point.x / size) * size,
                graphPlaneY,
                Mathf.Round(point.z / size) * size);
        }

        private static string BuildStrokeAssistLabel(bool straightened, bool snapped, bool cleaned)
        {
            var labels = new List<string>();
            if (straightened) labels.Add("straightened");
            if (snapped) labels.Add("snapped");
            if (cleaned && !straightened) labels.Add("cleaned");

            return labels.Count == 0 ? string.Empty : string.Join(", ", labels) + " ";
        }

        private void HandleEraseInput(Vector2 screenPosition, bool pressed)
        {
            if (!TryGetWorldPosition(screenPosition, out var worldPosition)) return;

            if (TryEraseStrokeAt(worldPosition))
            {
                return;
            }

            if (!pressed) return;

            if (!HasBakedGraph)
            {
                SetReport($"No drawn stroke found within {pathEraseRadius:0.00} world units.");
                return;
            }

            var nodeId = FindNearestNodeId(worldPosition, nodePickRadius);
            if (nodeId < 0)
            {
                SetReport(
                    $"No drawn stroke within {pathEraseRadius:0.00} units and no baked node within {nodePickRadius:0.00} units.");
                return;
            }

            EraseNodeOccupant(nodeId);
        }

        private bool TryEraseStrokeAt(Vector3 worldPosition)
        {
            var strokeIndex = FindNearestStrokeIndex(worldPosition, pathEraseRadius, out _);
            if (strokeIndex < 0) return false;

            strokes.RemoveAt(strokeIndex);
            graphDirty = HasBakedGraph;
            SetReport(
                $"Erased stroke {strokeIndex + 1}. " +
                (graphDirty ? "Press Bake Graph to update the baked graph." : "Draw or bake when you are ready."));
            RebuildPreview();
            return true;
        }

        private int FindNearestStrokeIndex(Vector3 worldPosition, float maxDistance, out float distance)
        {
            var bestIndex = -1;
            var bestDistanceSqr = maxDistance * maxDistance;

            for (int i = 0; i < strokes.Count; i++)
            {
                var points = strokes[i]?.points;
                if (points == null || points.Count == 0) continue;

                if (points.Count == 1)
                {
                    var pointDistanceSqr = DistanceSqrXZ(points[0], worldPosition);
                    if (pointDistanceSqr < bestDistanceSqr)
                    {
                        bestDistanceSqr = pointDistanceSqr;
                        bestIndex = i;
                    }

                    continue;
                }

                for (int p = 1; p < points.Count; p++)
                {
                    var segmentDistanceSqr = DistancePointToSegmentSqrXZ(worldPosition, points[p - 1], points[p]);
                    if (segmentDistanceSqr >= bestDistanceSqr) continue;

                    bestDistanceSqr = segmentDistanceSqr;
                    bestIndex = i;
                }
            }

            distance = bestIndex >= 0 ? Mathf.Sqrt(bestDistanceSqr) : 0f;
            return bestIndex;
        }

        private void HandleNodePaintInput(Vector2 screenPosition)
        {
            if (!HasBakedGraph)
            {
                SetReport("Paint modes need a baked graph. Draw paths, then press Bake Graph first.");
                return;
            }

            if (!TryGetWorldPosition(screenPosition, out var worldPosition)) return;

            var nodeId = FindNearestNodeId(worldPosition, nodePickRadius);
            if (nodeId < 0)
            {
                SetReport($"No node found within {nodePickRadius:0.00} world units.");
                return;
            }

            switch (mode)
            {
                case SlimyRuntimeLevelEditorMode.Hole:
                    PaintHole(nodeId);
                    break;
                case SlimyRuntimeLevelEditorMode.Rope:
                    PaintRopeNode(nodeId);
                    break;
                case SlimyRuntimeLevelEditorMode.Key:
                    ToggleKey(nodeId);
                    break;
                case SlimyRuntimeLevelEditorMode.Hidden:
                    ToggleHidden(nodeId);
                    break;
                case SlimyRuntimeLevelEditorMode.Lock:
                    ToggleLock(nodeId);
                    break;
                case SlimyRuntimeLevelEditorMode.Wall:
                    PaintWall(nodeId);
                    break;
                case SlimyRuntimeLevelEditorMode.DoubleRope:
                    SelectOuterRope(nodeId);
                    break;
                case SlimyRuntimeLevelEditorMode.Erase:
                    EraseNodeOccupant(nodeId);
                    break;
            }
        }

        private void PaintHole(int nodeId)
        {
            if (FindWallAtNode(nodeId) >= 0)
            {
                SetReport($"Node {nodeId} has a wall. Remove it before painting a hole there.");
                return;
            }

            if (FindRopeContainingNode(nodeId, out var ropeIndex))
            {
                SetReport($"Node {nodeId} is already occupied by rope {bakedData.ropes[ropeIndex].id}.");
                return;
            }

            if (currentRopeNodeIds.Contains(nodeId))
            {
                SetReport($"Node {nodeId} is part of the unfinished rope. Finish or cancel that rope first.");
                return;
            }

            var existingIndex = FindHoleAtNode(nodeId);
            if (existingIndex >= 0)
            {
                var existing = bakedData.holes[existingIndex];
                if (existing.color == paintColor)
                {
                    bakedData.holes.RemoveAt(existingIndex);
                    SetReport($"Removed hole {existing.id} from node {nodeId}.");
                }
                else
                {
                    existing.color = paintColor;
                    SetReport($"Recolored hole {existing.id} on node {nodeId} to {paintColor}.");
                }

                RebuildPreview();
                return;
            }

            var hole = new HoleData { id = NextHoleId(), color = paintColor, nodeId = nodeId };
            bakedData.holes.Add(hole);
            SetReport($"Added hole {hole.id} ({hole.color}) on node {nodeId}.");
            RebuildPreview();
        }

        private void PaintRopeNode(int nodeId)
        {
            if (FindHoleAtNode(nodeId) >= 0)
            {
                SetReport($"Node {nodeId} has a hole. Remove it before painting a rope there.");
                return;
            }

            if (FindWallAtNode(nodeId) >= 0)
            {
                SetReport($"Node {nodeId} has a wall. Remove it before painting a rope there.");
                return;
            }

            var containerIndex = GetSelectedOuterRopeIndex();
            var paintingContained = containerIndex >= 0;
            if (containedRopeOuterId > 0 && !paintingContained)
            {
                SetReport($"Outer rope {containedRopeOuterId} does not exist. Choose another outer rope.");
                return;
            }

            if (paintingContained && !bakedData.ropes[containerIndex].occupiedNodeIds.Contains(nodeId))
            {
                SetReport($"Inner rope nodes must be inside outer rope {containedRopeOuterId}.");
                return;
            }

            if (FindRopeContainingNode(nodeId, out var ropeIndex))
            {
                if (!paintingContained || ropeIndex != containerIndex)
                {
                    SetReport($"Node {nodeId} is already occupied by rope {bakedData.ropes[ropeIndex].id}.");
                    return;
                }
            }

            var existingSelectionIndex = currentRopeNodeIds.IndexOf(nodeId);
            if (existingSelectionIndex >= 0)
            {
                var removeCount = currentRopeNodeIds.Count - existingSelectionIndex - 1;
                if (removeCount > 0) currentRopeNodeIds.RemoveRange(existingSelectionIndex + 1, removeCount);
                SetReport($"Current rope undone back to node {nodeId}.");
                RebuildPreview();
                return;
            }

            if (currentRopeNodeIds.Count == 0)
            {
                if (paintingContained && HasInnerRope(containedRopeOuterId))
                {
                    SetReport($"Outer rope {containedRopeOuterId} already contains an inner rope.");
                    return;
                }

                currentRopeColor = paintColor;
                currentRopeNodeIds.Add(nodeId);
                SetReport(paintingContained
                    ? $"Started inner {currentRopeColor} rope inside rope {containedRopeOuterId} at node {nodeId}."
                    : $"Started {currentRopeColor} rope at node {nodeId}.");
                RebuildPreview();
                return;
            }

            var previousNodeId = currentRopeNodeIds[currentRopeNodeIds.Count - 1];
            if (!AreConnected(previousNodeId, nodeId))
            {
                SetReport($"Node {nodeId} is not connected to current rope end node {previousNodeId}.");
                return;
            }

            currentRopeNodeIds.Add(nodeId);
            SetReport($"Added node {nodeId} to current {currentRopeColor} rope.");
            RebuildPreview();
        }

        private void ToggleKey(int nodeId)
        {
            if (!FindRopeContainingNode(nodeId, out var ropeIndex, true))
            {
                SetReport($"Node {nodeId} has no rope to toggle a key on.");
                return;
            }

            var rope = bakedData.ropes[ropeIndex];
            rope.hasKey = !rope.hasKey;
            SetReport(rope.hasKey ? $"Rope {rope.id} now has a key." : $"Removed key from rope {rope.id}.");
            RebuildPreview();
        }

        private void ToggleHidden(int nodeId)
        {
            if (FindRopeContainingNode(nodeId, out var ropeIndex, true))
            {
                var rope = bakedData.ropes[ropeIndex];
                if (rope.hidden && rope.revealAfterCollections == hiddenRevealAfterCollections)
                {
                    rope.hidden = false;
                    rope.revealAfterCollections = 0;
                    SetReport($"Rope {rope.id} is no longer hidden.");
                }
                else
                {
                    rope.hidden = true;
                    rope.revealAfterCollections = hiddenRevealAfterCollections;
                    SetReport($"Rope {rope.id} will reveal after {hiddenRevealAfterCollections} collected rope(s).");
                }

                RebuildPreview();
                return;
            }

            var holeIndex = FindHoleAtNode(nodeId);
            if (holeIndex < 0)
            {
                SetReport($"Node {nodeId} has no rope or hole to toggle hidden state on.");
                return;
            }

            var hole = bakedData.holes[holeIndex];
            if (hole.hidden && hole.revealAfterCollections == hiddenRevealAfterCollections)
            {
                hole.hidden = false;
                hole.revealAfterCollections = 0;
                SetReport($"Hole {hole.id} is no longer hidden.");
            }
            else
            {
                hole.hidden = true;
                hole.revealAfterCollections = hiddenRevealAfterCollections;
                SetReport($"Hole {hole.id} will reveal after {hiddenRevealAfterCollections} collected rope(s).");
            }

            RebuildPreview();
        }

        private void ToggleLock(int nodeId)
        {
            var holeIndex = FindHoleAtNode(nodeId);
            if (holeIndex < 0)
            {
                SetReport($"Node {nodeId} has no hole to lock.");
                return;
            }

            var hole = bakedData.holes[holeIndex];
            if (hole.lockedKeyCount == lockedHoleKeyCount)
            {
                hole.lockedKeyCount = 0;
                SetReport($"Hole {hole.id} is no longer locked.");
            }
            else
            {
                hole.lockedKeyCount = lockedHoleKeyCount;
                SetReport($"Hole {hole.id} now requires {lockedHoleKeyCount} key(s).");
            }

            RebuildPreview();
        }

        private void PaintWall(int nodeId)
        {
            var existingIndex = FindWallAtNode(nodeId);
            if (existingIndex >= 0)
            {
                var wall = bakedData.walls[existingIndex];
                if (wall.ropeCollectionCount == wallRopeCollectionCount)
                {
                    bakedData.walls.RemoveAt(existingIndex);
                    SetReport($"Removed wall {wall.id} from node {nodeId}.");
                }
                else
                {
                    wall.ropeCollectionCount = wallRopeCollectionCount;
                    SetReport($"Wall {wall.id} now disappears after {wallRopeCollectionCount} collected rope(s).");
                }

                RebuildPreview();
                return;
            }

            if (FindHoleAtNode(nodeId) >= 0)
            {
                SetReport($"Node {nodeId} has a hole. Remove it before painting a wall there.");
                return;
            }

            if (currentRopeNodeIds.Contains(nodeId))
            {
                SetReport($"Node {nodeId} is part of the unfinished rope. Finish or cancel that rope first.");
                return;
            }

            if (FindRopeContainingNode(nodeId, out var ropeIndex, true))
            {
                SetReport($"Node {nodeId} is occupied by rope {bakedData.ropes[ropeIndex].id}.");
                return;
            }

            var wallData = new WallData
            {
                id = NextWallId(),
                nodeId = nodeId,
                ropeCollectionCount = wallRopeCollectionCount
            };
            bakedData.walls.Add(wallData);
            SetReport($"Added wall {wallData.id} on node {nodeId}.");
            RebuildPreview();
        }

        private void SelectOuterRope(int nodeId)
        {
            if (!FindRopeContainingNode(nodeId, out var ropeIndex))
            {
                SetReport($"Node {nodeId} has no rope to use as an outer rope.");
                return;
            }

            var rope = bakedData.ropes[ropeIndex];
            if (rope.containedByRopeId > 0)
            {
                SetReport($"Rope {rope.id} is already an inner rope. Choose an outer rope.");
                return;
            }

            if (currentRopeNodeIds.Count > 0)
            {
                SetReport("Finish or cancel the current rope before choosing an outer rope.");
                return;
            }

            containedRopeOuterId = containedRopeOuterId == rope.id ? 0 : rope.id;
            SetReport(containedRopeOuterId > 0
                ? $"Selected rope {containedRopeOuterId} as the outer rope. Switch to Rope mode and paint the inner rope inside it."
                : $"Cleared outer rope selection.");
            RebuildPreview();
        }

        private void EraseNodeOccupant(int nodeId)
        {
            var holeIndex = FindHoleAtNode(nodeId);
            if (holeIndex >= 0)
            {
                var id = bakedData.holes[holeIndex].id;
                bakedData.holes.RemoveAt(holeIndex);
                SetReport($"Removed hole {id} from node {nodeId}.");
                RebuildPreview();
                return;
            }

            if (FindRopeContainingNode(nodeId, out var ropeIndex, true))
            {
                var id = bakedData.ropes[ropeIndex].id;
                RemoveRopeAt(ropeIndex);
                SetReport($"Removed rope {id}.");
                RebuildPreview();
                return;
            }

            var wallIndex = FindWallAtNode(nodeId);
            if (wallIndex >= 0)
            {
                var id = bakedData.walls[wallIndex].id;
                bakedData.walls.RemoveAt(wallIndex);
                SetReport($"Removed wall {id} from node {nodeId}.");
                RebuildPreview();
                return;
            }

            SetReport($"Node {nodeId} has no painted rope, hole, or wall to erase.");
        }

        private List<WorkingSegment> BuildWorkingSegments(List<string> warnings)
        {
            var segments = new List<WorkingSegment>();

            for (int i = 0; i < strokes.Count; i++)
            {
                var sampled = BuildSampledStroke(strokes[i], i, warnings);
                if (sampled.Count < 2) continue;

                for (int p = 1; p < sampled.Count; p++)
                {
                    if (DistanceSqrXZ(sampled[p - 1], sampled[p]) <= IntersectionEpsilon) continue;

                    var segment = new WorkingSegment
                    {
                        StrokeIndex = i,
                        SegmentIndex = p - 1,
                        A = sampled[p - 1],
                        B = sampled[p]
                    };
                    segment.Splits.Add(new SegmentSplit(0f, segment.A));
                    segment.Splits.Add(new SegmentSplit(1f, segment.B));
                    segments.Add(segment);
                }
            }

            return segments;
        }

        private List<Vector3> BuildSampledStroke(StrokeData stroke, int strokeIndex, List<string> warnings)
        {
            var empty = new List<Vector3>();
            if (stroke?.points == null || stroke.points.Count < 2) return empty;

            var prepared = RemoveTinySegments(stroke.points, strokeSampleMinDistance * 0.5f);
            if (prepared.Count < 2)
            {
                warnings.Add($"Stroke {strokeIndex + 1} is too short after cleanup; skipped.");
                return empty;
            }

            if (strokeSimplifyTolerance > 0f)
            {
                prepared = SimplifyPolyline(prepared, strokeSimplifyTolerance);
            }

            for (int i = 0; i < smoothingIterations; i++)
            {
                prepared = SmoothPolyline(prepared);
            }

            var length = PolylineLength(prepared);
            if (length < unitDistance * 0.5f)
            {
                warnings.Add($"Stroke {strokeIndex + 1} is shorter than half a unit; skipped.");
                return empty;
            }

            var segmentCount = Mathf.Max(1, Mathf.RoundToInt(length / unitDistance));
            var actualSpacing = length / segmentCount;
            if (Mathf.Abs(actualSpacing - unitDistance) > segmentLengthWarningTolerance)
            {
                warnings.Add(
                    $"Stroke {strokeIndex + 1} length {length:0.00} normalizes to {segmentCount} segments of " +
                    $"{actualSpacing:0.00} units.");
            }

            return ResamplePolyline(prepared, segmentCount);
        }

        private void SplitSegmentsAtIntersections(List<WorkingSegment> segments, List<string> warnings)
        {
            var overlapWarnings = 0;

            for (int i = 0; i < segments.Count; i++)
            {
                for (int j = i + 1; j < segments.Count; j++)
                {
                    var a = segments[i];
                    var b = segments[j];

                    if (AreAdjacentSegmentsFromSameStroke(a, b)) continue;

                    if (!TrySegmentIntersectionXZ(a.A, a.B, b.A, b.B, out var t, out var u, out var point,
                            out var overlaps))
                    {
                        if (overlaps && overlapWarnings < 5)
                        {
                            warnings.Add(
                                $"Stroke segments overlap on the same line near {FormatPoint(a.A)}. " +
                                "Overlaps are welded by proximity but are harder to read than crossings.");
                            overlapWarnings++;
                        }

                        continue;
                    }

                    AddSplit(a, t, point);
                    AddSplit(b, u, point);
                }
            }

            if (overlapWarnings >= 5)
            {
                warnings.Add("Additional overlapping segment warnings were suppressed.");
            }
        }

        private void ConnectSplitSegment(WorkingSegment segment, NodeWelder welder,
            Dictionary<int, List<int>> connections, HashSet<long> edges, List<string> warnings)
        {
            segment.Splits.Sort((left, right) => left.T.CompareTo(right.T));

            for (int i = 1; i < segment.Splits.Count; i++)
            {
                var previous = segment.Splits[i - 1].Position;
                var current = segment.Splits[i].Position;
                if (DistanceSqrXZ(previous, current) <= IntersectionEpsilon) continue;

                var a = welder.GetOrCreate(previous);
                var b = welder.GetOrCreate(current);
                Connect(connections, edges, a, b, warnings);
            }
        }

        private void CaptureOccupants(List<HoleSnapshot> holeSnapshots, List<RopeSnapshot> ropeSnapshots,
            List<WallSnapshot> wallSnapshots)
        {
            if (bakedData == null || bakedData.groundNodes == null) return;

            var positions = BuildPositionLookup(bakedData);

            if (bakedData.holes != null)
            {
                for (int i = 0; i < bakedData.holes.Count; i++)
                {
                    var hole = bakedData.holes[i];
                    if (!positions.TryGetValue(hole.nodeId, out var position)) continue;
                    holeSnapshots.Add(new HoleSnapshot
                    {
                        Id = hole.id,
                        Color = hole.color,
                        Position = position,
                        Hidden = hole.hidden,
                        RevealAfterCollections = hole.revealAfterCollections,
                        LockedKeyCount = hole.lockedKeyCount
                    });
                }
            }

            if (bakedData.walls != null)
            {
                for (int i = 0; i < bakedData.walls.Count; i++)
                {
                    var wall = bakedData.walls[i];
                    if (!positions.TryGetValue(wall.nodeId, out var position)) continue;
                    wallSnapshots.Add(new WallSnapshot
                    {
                        Id = wall.id,
                        Position = position,
                        RopeCollectionCount = wall.ropeCollectionCount
                    });
                }
            }

            if (bakedData.ropes == null) return;

            for (int i = 0; i < bakedData.ropes.Count; i++)
            {
                var rope = bakedData.ropes[i];
                var snapshot = new RopeSnapshot
                {
                    Id = rope.id,
                    Color = rope.color,
                    HasKey = rope.hasKey,
                    Hidden = rope.hidden,
                    RevealAfterCollections = rope.revealAfterCollections,
                    ContainedByRopeId = rope.containedByRopeId
                };
                var valid = true;

                for (int p = 0; p < rope.occupiedNodeIds.Count; p++)
                {
                    if (!positions.TryGetValue(rope.occupiedNodeIds[p], out var position))
                    {
                        valid = false;
                        break;
                    }

                    snapshot.Positions.Add(position);
                }

                if (valid) ropeSnapshots.Add(snapshot);
            }
        }

        private void RestoreOccupants(List<HoleSnapshot> holeSnapshots, List<RopeSnapshot> ropeSnapshots,
            List<WallSnapshot> wallSnapshots, SlimyLevelData data, List<string> warnings)
        {
            for (int i = 0; i < holeSnapshots.Count; i++)
            {
                var snapshot = holeSnapshots[i];
                var nodeId = FindNearestNodeId(data, snapshot.Position, occupantSnapDistance);
                if (nodeId < 0)
                {
                    warnings.Add($"Hole {snapshot.Id} could not be snapped to the rebaked graph; skipped.");
                    continue;
                }

                data.holes.Add(new HoleData
                {
                    id = snapshot.Id,
                    color = snapshot.Color,
                    nodeId = nodeId,
                    hidden = snapshot.Hidden,
                    revealAfterCollections = snapshot.RevealAfterCollections,
                    lockedKeyCount = snapshot.LockedKeyCount
                });
            }

            for (int i = 0; i < wallSnapshots.Count; i++)
            {
                var snapshot = wallSnapshots[i];
                var nodeId = FindNearestNodeId(data, snapshot.Position, occupantSnapDistance);
                if (nodeId < 0)
                {
                    warnings.Add($"Wall {snapshot.Id} could not be snapped to the rebaked graph; skipped.");
                    continue;
                }

                data.walls.Add(new WallData
                {
                    id = snapshot.Id,
                    nodeId = nodeId,
                    ropeCollectionCount = snapshot.RopeCollectionCount
                });
            }

            for (int i = 0; i < ropeSnapshots.Count; i++)
            {
                var snapshot = ropeSnapshots[i];
                var rope = new RopeData
                {
                    id = snapshot.Id,
                    color = snapshot.Color,
                    hasKey = snapshot.HasKey,
                    hidden = snapshot.Hidden,
                    revealAfterCollections = snapshot.RevealAfterCollections,
                    containedByRopeId = snapshot.ContainedByRopeId
                };
                var valid = true;

                for (int p = 0; p < snapshot.Positions.Count; p++)
                {
                    var nodeId = FindNearestNodeId(data, snapshot.Positions[p], occupantSnapDistance);
                    if (nodeId < 0)
                    {
                        warnings.Add($"Rope {snapshot.Id} marker {p} could not be snapped to the rebaked graph; skipped.");
                        valid = false;
                        break;
                    }

                    rope.occupiedNodeIds.Add(nodeId);
                }

                if (valid) data.ropes.Add(rope);
            }
        }

        private void MergeShortJunctionEdges(SlimyLevelData data, List<string> warnings)
        {
            if (!mergeShortJunctionEdges || data?.groundNodes == null || data.groundNodes.Count < 2) return;

            var maxDistance = unitDistance * shortJunctionEdgeMergeUnitFactor;
            var maxDistanceSqr = maxDistance * maxDistance;
            var mergeCount = 0;
            var guard = data.groundNodes.Count * data.groundNodes.Count;

            while (TryFindShortJunctionEdge(data, maxDistanceSqr, out var keepId, out var removeId,
                       out var distance))
            {
                MergeGraphNodes(data, keepId, removeId);
                mergeCount++;

                if (mergeCount <= guard) continue;

                warnings.Add("Short junction edge merge stopped early to avoid an endless bake cleanup loop.");
                break;
            }

            if (mergeCount == 0) return;

            RenumberGroundNodes(data);
            warnings.Add(
                $"Merged {mergeCount} short junction edge(s) closer than {maxDistance:0.00} units.");
        }

        private static bool TryFindShortJunctionEdge(SlimyLevelData data, float maxDistanceSqr, out int keepId,
            out int removeId, out float distance)
        {
            keepId = -1;
            removeId = -1;
            distance = 0f;

            var lookup = BuildNodeLookup(data);
            var degrees = BuildDegreeLookup(data);
            var bestDistanceSqr = maxDistanceSqr;

            for (int i = 0; i < data.groundNodes.Count; i++)
            {
                var node = data.groundNodes[i];
                if (node.connectedNodeIds == null) continue;

                for (int c = 0; c < node.connectedNodeIds.Count; c++)
                {
                    var otherId = node.connectedNodeIds[c];
                    if (node.id >= otherId) continue;
                    if (!lookup.TryGetValue(otherId, out var other)) continue;

                    var degreeA = degrees.TryGetValue(node.id, out var a) ? a : 0;
                    var degreeB = degrees.TryGetValue(other.id, out var b) ? b : 0;
                    if (degreeA <= 2 && degreeB <= 2) continue;

                    var distanceSqr = DistanceSqrXZ(node.position, other.position);
                    if (distanceSqr <= IntersectionEpsilon || distanceSqr >= bestDistanceSqr) continue;

                    bestDistanceSqr = distanceSqr;
                    keepId = ChooseMergeKeepId(node.id, degreeA, other.id, degreeB);
                    removeId = keepId == node.id ? other.id : node.id;
                }
            }

            if (keepId < 0 || removeId < 0) return false;

            distance = Mathf.Sqrt(bestDistanceSqr);
            return true;
        }

        private static int ChooseMergeKeepId(int firstId, int firstDegree, int secondId, int secondDegree)
        {
            if (firstDegree > secondDegree) return firstId;
            if (secondDegree > firstDegree) return secondId;
            return Mathf.Min(firstId, secondId);
        }

        private static void MergeGraphNodes(SlimyLevelData data, int keepId, int removeId)
        {
            var lookup = BuildNodeLookup(data);
            if (!lookup.TryGetValue(keepId, out var keepNode)) return;
            if (!lookup.TryGetValue(removeId, out var removeNode)) return;

            if (keepNode.connectedNodeIds == null) keepNode.connectedNodeIds = new List<int>();

            if (removeNode.connectedNodeIds != null)
            {
                for (int i = 0; i < removeNode.connectedNodeIds.Count; i++)
                {
                    var neighbourId = removeNode.connectedNodeIds[i];
                    if (neighbourId == keepId || neighbourId == removeId) continue;
                    AddUniqueConnection(keepNode.connectedNodeIds, neighbourId);
                }
            }

            for (int i = 0; i < data.groundNodes.Count; i++)
            {
                var node = data.groundNodes[i];
                if (node.connectedNodeIds == null) continue;

                for (int c = 0; c < node.connectedNodeIds.Count; c++)
                {
                    if (node.connectedNodeIds[c] == removeId) node.connectedNodeIds[c] = keepId;
                }

                RemoveDuplicateAndSelfConnections(node.connectedNodeIds, node.id);
            }

            data.groundNodes.Remove(removeNode);
        }

        private static void RenumberGroundNodes(SlimyLevelData data)
        {
            if (data?.groundNodes == null) return;

            var idMap = new Dictionary<int, int>();
            for (int i = 0; i < data.groundNodes.Count; i++)
            {
                idMap[data.groundNodes[i].id] = FirstNodeId + i;
            }

            for (int i = 0; i < data.groundNodes.Count; i++)
            {
                var node = data.groundNodes[i];
                var oldId = node.id;
                node.id = idMap[oldId];

                if (node.connectedNodeIds == null)
                {
                    node.connectedNodeIds = new List<int>();
                    continue;
                }

                for (int c = 0; c < node.connectedNodeIds.Count; c++)
                {
                    if (idMap.TryGetValue(node.connectedNodeIds[c], out var mapped))
                    {
                        node.connectedNodeIds[c] = mapped;
                    }
                }

                RemoveDuplicateAndSelfConnections(node.connectedNodeIds, node.id);
            }

            if (data.holes != null)
            {
                for (int i = 0; i < data.holes.Count; i++)
                {
                    if (idMap.TryGetValue(data.holes[i].nodeId, out var mapped)) data.holes[i].nodeId = mapped;
                }
            }

            if (data.walls != null)
            {
                for (int i = 0; i < data.walls.Count; i++)
                {
                    if (idMap.TryGetValue(data.walls[i].nodeId, out var mapped)) data.walls[i].nodeId = mapped;
                }
            }

            if (data.ropes == null) return;

            for (int i = 0; i < data.ropes.Count; i++)
            {
                var nodes = data.ropes[i].occupiedNodeIds;
                if (nodes == null) continue;

                for (int n = 0; n < nodes.Count; n++)
                {
                    if (idMap.TryGetValue(nodes[n], out var mapped)) nodes[n] = mapped;
                }
            }
        }

        private static Dictionary<int, GroundNodeData> BuildNodeLookup(SlimyLevelData data)
        {
            var result = new Dictionary<int, GroundNodeData>();
            if (data?.groundNodes == null) return result;

            for (int i = 0; i < data.groundNodes.Count; i++)
            {
                result[data.groundNodes[i].id] = data.groundNodes[i];
            }

            return result;
        }

        private static Dictionary<int, int> BuildDegreeLookup(SlimyLevelData data)
        {
            var result = new Dictionary<int, int>();
            if (data?.groundNodes == null) return result;

            for (int i = 0; i < data.groundNodes.Count; i++)
            {
                var node = data.groundNodes[i];
                if (node.connectedNodeIds == null)
                {
                    result[node.id] = 0;
                    continue;
                }

                var unique = new HashSet<int>();
                for (int c = 0; c < node.connectedNodeIds.Count; c++)
                {
                    if (node.connectedNodeIds[c] != node.id) unique.Add(node.connectedNodeIds[c]);
                }

                result[node.id] = unique.Count;
            }

            return result;
        }

        private static void AddUniqueConnection(List<int> connections, int id)
        {
            if (!connections.Contains(id)) connections.Add(id);
        }

        private static void RemoveDuplicateAndSelfConnections(List<int> connections, int selfId)
        {
            var seen = new HashSet<int>();
            for (int i = connections.Count - 1; i >= 0; i--)
            {
                var id = connections[i];
                if (id == selfId || !seen.Add(id)) connections.RemoveAt(i);
            }
        }

        private void AddEditorValidation(SlimyLevelData data, List<string> errors, List<string> warnings)
        {
            for (int i = 0; i < data.ropes.Count; i++)
            {
                if (data.ropes[i].occupiedNodeIds == null || data.ropes[i].occupiedNodeIds.Count < 2)
                {
                    errors.Add($"Rope {data.ropes[i].id} must occupy at least 2 nodes.");
                }
            }

            AddConnectivityWarnings(data, warnings);
            AddSegmentLengthWarnings(data, warnings);
            AddColorPairWarnings(data, warnings);
        }

        private void AddConnectivityWarnings(SlimyLevelData data, List<string> warnings)
        {
            if (data.groundNodes.Count == 0) return;

            var nodesById = BuildPositionLookup(data);
            var connectionsById = BuildConnectionLookup(data);
            var visited = new HashSet<int>();
            var queue = new Queue<int>();

            queue.Enqueue(data.groundNodes[0].id);
            visited.Add(data.groundNodes[0].id);

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                if (!connectionsById.TryGetValue(current, out var neighbours)) continue;

                for (int i = 0; i < neighbours.Count; i++)
                {
                    if (!nodesById.ContainsKey(neighbours[i])) continue;
                    if (visited.Add(neighbours[i])) queue.Enqueue(neighbours[i]);
                }
            }

            if (visited.Count != data.groundNodes.Count)
            {
                warnings.Add($"Graph has {data.groundNodes.Count - visited.Count} node(s) outside the main connected island.");
            }
        }

        private void AddSegmentLengthWarnings(SlimyLevelData data, List<string> warnings)
        {
            var positions = BuildPositionLookup(data);
            var reported = 0;

            for (int i = 0; i < data.groundNodes.Count; i++)
            {
                var node = data.groundNodes[i];
                if (node.connectedNodeIds == null) continue;

                for (int c = 0; c < node.connectedNodeIds.Count; c++)
                {
                    var otherId = node.connectedNodeIds[c];
                    if (node.id > otherId) continue;
                    if (!positions.TryGetValue(node.id, out var a) || !positions.TryGetValue(otherId, out var b)) continue;

                    var length = Mathf.Sqrt(DistanceSqrXZ(a, b));
                    if (Mathf.Abs(length - unitDistance) <= segmentLengthWarningTolerance) continue;

                    if (reported < 8)
                    {
                        warnings.Add($"Segment {node.id}-{otherId} is {length:0.00} units; target is {unitDistance:0.00}.");
                    }

                    reported++;
                }
            }

            if (reported > 8)
            {
                warnings.Add($"{reported - 8} additional segment length warnings were suppressed.");
            }
        }

        private static void AddColorPairWarnings(SlimyLevelData data, List<string> warnings)
        {
            var ropeColors = new HashSet<RopeColor>();
            var holeColors = new HashSet<RopeColor>();

            for (int i = 0; i < data.ropes.Count; i++) ropeColors.Add(data.ropes[i].color);
            for (int i = 0; i < data.holes.Count; i++) holeColors.Add(data.holes[i].color);

            foreach (var color in ropeColors)
            {
                if (!holeColors.Contains(color)) warnings.Add($"Rope color {color} has no matching hole.");
            }

            foreach (var color in holeColors)
            {
                if (!ropeColors.Contains(color)) warnings.Add($"Hole color {color} has no matching rope.");
            }
        }

        private bool ValidateRopeChain(RopeData rope, out string error)
        {
            error = null;

            if (rope.occupiedNodeIds == null || rope.occupiedNodeIds.Count < 2)
            {
                error = "rope length must be at least 2 nodes.";
                return false;
            }

            var seen = new HashSet<int>();
            var outerIndex = FindRopeById(rope.containedByRopeId);
            var isContained = rope.containedByRopeId > 0;

            if (isContained && outerIndex < 0)
            {
                error = $"outer rope {rope.containedByRopeId} does not exist.";
                return false;
            }

            for (int i = 0; i < rope.occupiedNodeIds.Count; i++)
            {
                var nodeId = rope.occupiedNodeIds[i];
                if (!seen.Add(nodeId))
                {
                    error = $"node {nodeId} appears more than once in the rope.";
                    return false;
                }

                if (FindHoleAtNode(nodeId) >= 0)
                {
                    error = $"node {nodeId} contains a hole.";
                    return false;
                }

                if (FindWallAtNode(nodeId) >= 0)
                {
                    error = $"node {nodeId} contains a wall.";
                    return false;
                }

                if (isContained && !bakedData.ropes[outerIndex].occupiedNodeIds.Contains(nodeId))
                {
                    error = $"node {nodeId} is outside outer rope {rope.containedByRopeId}.";
                    return false;
                }

                if (FindRopeContainingNode(nodeId, out var ropeIndex) &&
                    (!isContained || ropeIndex != outerIndex))
                {
                    error = $"node {nodeId} is already occupied by rope {bakedData.ropes[ropeIndex].id}.";
                    return false;
                }

                if (i > 0 && !AreConnected(rope.occupiedNodeIds[i - 1], nodeId))
                {
                    error = $"nodes {rope.occupiedNodeIds[i - 1]} and {nodeId} are not connected.";
                    return false;
                }
            }

            return true;
        }

        private bool TryGetWorldPosition(Vector2 screenPosition, out Vector3 worldPosition)
        {
            worldPosition = Vector3.zero;
            var camera = Camera;
            if (camera == null)
            {
                SetReport("No camera found. Assign Target Camera or tag a scene camera as MainCamera.");
                return false;
            }

            var plane = new Plane(Vector3.up, new Vector3(0f, graphPlaneY, 0f));
            var ray = camera.ScreenPointToRay(screenPosition);
            if (!plane.Raycast(ray, out var distance))
            {
                SetReport(
                    $"Pointer ray from camera '{camera.name}' did not hit the XZ graph plane at Y={graphPlaneY:0.00}. " +
                    "Aim the camera down at the board plane or assign a camera that sees the XZ plane.");
                return false;
            }

            worldPosition = ProjectToGraphPlane(ray.GetPoint(distance));
            if (limitInputToBoard && !IsWithinBoard(worldPosition))
            {
                var halfSize = boardSize * 0.5f;
                SetReport(
                    $"Pointer is outside the board. Keep drawing within X/Z +/-{halfSize:0.00}.");
                return false;
            }

            return true;
        }

        private static bool TryReadPointer(out Vector2 screenPosition, out bool pressed, out bool held,
            out bool released)
        {
            screenPosition = Vector2.zero;
            pressed = false;
            held = false;
            released = false;

#if ENABLE_INPUT_SYSTEM
            var mouse = Mouse.current;
            if (mouse != null)
            {
                screenPosition = mouse.position.ReadValue();
                pressed = mouse.leftButton.wasPressedThisFrame;
                held = mouse.leftButton.isPressed;
                released = mouse.leftButton.wasReleasedThisFrame;
                if (pressed || held || released) return true;
            }

            var touch = Touchscreen.current;
            if (touch != null)
            {
                screenPosition = touch.primaryTouch.position.ReadValue();
                pressed = touch.primaryTouch.press.wasPressedThisFrame;
                held = touch.primaryTouch.press.isPressed;
                released = touch.primaryTouch.press.wasReleasedThisFrame;
                return pressed || held || released;
            }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
            try
            {
                screenPosition = UnityEngine.Input.mousePosition;
                pressed = UnityEngine.Input.GetMouseButtonDown(0);
                held = UnityEngine.Input.GetMouseButton(0);
                released = UnityEngine.Input.GetMouseButtonUp(0);
                return pressed || held || released;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
#else
            return false;
#endif
        }

        private bool IsStraightLineModifierActive()
        {
            if (!holdShiftForStraightLine) return false;

#if ENABLE_INPUT_SYSTEM
            var keyboard = Keyboard.current;
            if (keyboard != null && (keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed))
            {
                return true;
            }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
            try
            {
                return UnityEngine.Input.GetKey(KeyCode.LeftShift) ||
                       UnityEngine.Input.GetKey(KeyCode.RightShift);
            }
            catch (InvalidOperationException)
            {
                return false;
            }
#else
            return false;
#endif
        }

        private static bool WasKeyPressed(EditorKey key)
        {
#if ENABLE_INPUT_SYSTEM
            var keyboard = Keyboard.current;
            if (keyboard != null && TryGetInputSystemKey(key, out var primary, out var secondary))
            {
                if (primary != Key.None && keyboard[primary].wasPressedThisFrame) return true;
                if (secondary != Key.None && keyboard[secondary].wasPressedThisFrame) return true;
            }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
            try
            {
                return TryGetLegacyKeyCode(key, out var legacyPrimary, out var legacySecondary) &&
                       ((legacyPrimary != KeyCode.None && UnityEngine.Input.GetKeyDown(legacyPrimary)) ||
                        (legacySecondary != KeyCode.None && UnityEngine.Input.GetKeyDown(legacySecondary)));
            }
            catch (InvalidOperationException)
            {
                return false;
            }
#else
            return false;
#endif
        }

        private static bool WasNumberKeyPressed(int number)
        {
#if ENABLE_INPUT_SYSTEM
            var keyboard = Keyboard.current;
            if (keyboard != null && TryGetInputSystemNumberKey(number, out var primary, out var secondary))
            {
                if (primary != Key.None && keyboard[primary].wasPressedThisFrame) return true;
                if (secondary != Key.None && keyboard[secondary].wasPressedThisFrame) return true;
            }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
            try
            {
                return TryGetLegacyNumberKey(number, out var legacyPrimary, out var legacySecondary) &&
                       ((legacyPrimary != KeyCode.None && UnityEngine.Input.GetKeyDown(legacyPrimary)) ||
                        (legacySecondary != KeyCode.None && UnityEngine.Input.GetKeyDown(legacySecondary)));
            }
            catch (InvalidOperationException)
            {
                return false;
            }
#else
            return false;
#endif
        }

#if ENABLE_INPUT_SYSTEM
        private static bool TryGetInputSystemKey(EditorKey key, out Key primary, out Key secondary)
        {
            secondary = Key.None;
            switch (key)
            {
                case EditorKey.Escape:
                    primary = Key.Escape;
                    return true;
                case EditorKey.Enter:
                    primary = Key.Enter;
                    secondary = Key.NumpadEnter;
                    return true;
                case EditorKey.P:
                    primary = Key.P;
                    return true;
                case EditorKey.R:
                    primary = Key.R;
                    return true;
                case EditorKey.H:
                    primary = Key.H;
                    return true;
                case EditorKey.E:
                    primary = Key.E;
                    return true;
                case EditorKey.F:
                    primary = Key.F;
                    return true;
                case EditorKey.C:
                    primary = Key.C;
                    return true;
                case EditorKey.S:
                    primary = Key.S;
                    return true;
                case EditorKey.B:
                    primary = Key.B;
                    return true;
                case EditorKey.X:
                    primary = Key.X;
                    return true;
                case EditorKey.V:
                    primary = Key.V;
                    return true;
                default:
                    primary = Key.None;
                    return false;
            }
        }

        private static bool TryGetInputSystemNumberKey(int number, out Key primary, out Key secondary)
        {
            secondary = Key.None;
            switch (number)
            {
                case 1:
                    primary = Key.Digit1;
                    secondary = Key.Numpad1;
                    return true;
                case 2:
                    primary = Key.Digit2;
                    secondary = Key.Numpad2;
                    return true;
                case 3:
                    primary = Key.Digit3;
                    secondary = Key.Numpad3;
                    return true;
                case 4:
                    primary = Key.Digit4;
                    secondary = Key.Numpad4;
                    return true;
                case 5:
                    primary = Key.Digit5;
                    secondary = Key.Numpad5;
                    return true;
                case 6:
                    primary = Key.Digit6;
                    secondary = Key.Numpad6;
                    return true;
                case 7:
                    primary = Key.Digit7;
                    secondary = Key.Numpad7;
                    return true;
                case 8:
                    primary = Key.Digit8;
                    secondary = Key.Numpad8;
                    return true;
                case 9:
                    primary = Key.Digit9;
                    secondary = Key.Numpad9;
                    return true;
                default:
                    primary = Key.None;
                    return false;
            }
        }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        private static bool TryGetLegacyKeyCode(EditorKey key, out KeyCode primary, out KeyCode secondary)
        {
            secondary = KeyCode.None;
            switch (key)
            {
                case EditorKey.Escape:
                    primary = KeyCode.Escape;
                    return true;
                case EditorKey.Enter:
                    primary = KeyCode.Return;
                    secondary = KeyCode.KeypadEnter;
                    return true;
                case EditorKey.P:
                    primary = KeyCode.P;
                    return true;
                case EditorKey.R:
                    primary = KeyCode.R;
                    return true;
                case EditorKey.H:
                    primary = KeyCode.H;
                    return true;
                case EditorKey.E:
                    primary = KeyCode.E;
                    return true;
                case EditorKey.F:
                    primary = KeyCode.F;
                    return true;
                case EditorKey.C:
                    primary = KeyCode.C;
                    return true;
                case EditorKey.S:
                    primary = KeyCode.S;
                    return true;
                case EditorKey.B:
                    primary = KeyCode.B;
                    return true;
                case EditorKey.X:
                    primary = KeyCode.X;
                    return true;
                case EditorKey.V:
                    primary = KeyCode.V;
                    return true;
                default:
                    primary = KeyCode.None;
                    return false;
            }
        }

        private static bool TryGetLegacyNumberKey(int number, out KeyCode primary, out KeyCode secondary)
        {
            secondary = KeyCode.None;
            switch (number)
            {
                case 1:
                    primary = KeyCode.Alpha1;
                    secondary = KeyCode.Keypad1;
                    return true;
                case 2:
                    primary = KeyCode.Alpha2;
                    secondary = KeyCode.Keypad2;
                    return true;
                case 3:
                    primary = KeyCode.Alpha3;
                    secondary = KeyCode.Keypad3;
                    return true;
                case 4:
                    primary = KeyCode.Alpha4;
                    secondary = KeyCode.Keypad4;
                    return true;
                case 5:
                    primary = KeyCode.Alpha5;
                    secondary = KeyCode.Keypad5;
                    return true;
                case 6:
                    primary = KeyCode.Alpha6;
                    secondary = KeyCode.Keypad6;
                    return true;
                case 7:
                    primary = KeyCode.Alpha7;
                    secondary = KeyCode.Keypad7;
                    return true;
                case 8:
                    primary = KeyCode.Alpha8;
                    secondary = KeyCode.Keypad8;
                    return true;
                case 9:
                    primary = KeyCode.Alpha9;
                    secondary = KeyCode.Keypad9;
                    return true;
                default:
                    primary = KeyCode.None;
                    return false;
            }
        }
#endif

        private int FindNearestNodeId(Vector3 worldPosition, float maxDistance)
        {
            return FindNearestNodeId(bakedData, worldPosition, maxDistance);
        }

        private static int FindNearestNodeId(SlimyLevelData data, Vector3 worldPosition, float maxDistance)
        {
            var best = -1;
            var bestDistance = maxDistance * maxDistance;

            if (data?.groundNodes == null) return best;

            for (int i = 0; i < data.groundNodes.Count; i++)
            {
                var node = data.groundNodes[i];
                var distance = DistanceSqrXZ(node.position, worldPosition);
                if (distance >= bestDistance) continue;

                bestDistance = distance;
                best = node.id;
            }

            return best;
        }

        private int FindHoleAtNode(int nodeId)
        {
            for (int i = 0; i < bakedData.holes.Count; i++)
            {
                if (bakedData.holes[i].nodeId == nodeId) return i;
            }

            return -1;
        }

        private bool FindRopeContainingNode(int nodeId, out int ropeIndex, bool preferContained = false)
        {
            if (preferContained)
            {
                for (int i = 0; i < bakedData.ropes.Count; i++)
                {
                    var rope = bakedData.ropes[i];
                    if (rope.containedByRopeId <= 0 || rope.occupiedNodeIds == null) continue;
                    if (!rope.occupiedNodeIds.Contains(nodeId)) continue;

                    ropeIndex = i;
                    return true;
                }
            }

            for (int i = 0; i < bakedData.ropes.Count; i++)
            {
                var nodes = bakedData.ropes[i].occupiedNodeIds;
                if (nodes == null) continue;
                if (nodes.Contains(nodeId))
                {
                    ropeIndex = i;
                    return true;
                }
            }

            ropeIndex = -1;
            return false;
        }

        private int FindWallAtNode(int nodeId)
        {
            for (int i = 0; i < bakedData.walls.Count; i++)
            {
                if (bakedData.walls[i].nodeId == nodeId) return i;
            }

            return -1;
        }

        private int GetSelectedOuterRopeIndex()
        {
            if (containedRopeOuterId <= 0) return -1;

            return FindRopeById(containedRopeOuterId);
        }

        private int FindRopeById(int ropeId)
        {
            if (ropeId <= 0) return -1;

            for (int i = 0; i < bakedData.ropes.Count; i++)
            {
                if (bakedData.ropes[i].id == ropeId) return i;
            }

            return -1;
        }

        private bool HasInnerRope(int outerRopeId)
        {
            for (int i = 0; i < bakedData.ropes.Count; i++)
            {
                if (bakedData.ropes[i].containedByRopeId == outerRopeId) return true;
            }

            return false;
        }

        private void RemoveRopeAt(int ropeIndex)
        {
            if (ropeIndex < 0 || ropeIndex >= bakedData.ropes.Count) return;

            var removedId = bakedData.ropes[ropeIndex].id;
            bakedData.ropes.RemoveAt(ropeIndex);

            for (int i = bakedData.ropes.Count - 1; i >= 0; i--)
            {
                if (bakedData.ropes[i].containedByRopeId == removedId) bakedData.ropes.RemoveAt(i);
            }

            if (containedRopeOuterId == removedId) containedRopeOuterId = 0;
        }

        private bool AreConnected(int a, int b)
        {
            if (!TryGetGroundNode(a, out var node)) return false;
            return node.connectedNodeIds != null && node.connectedNodeIds.Contains(b);
        }

        private bool TryGetGroundNode(int nodeId, out GroundNodeData node)
        {
            for (int i = 0; i < bakedData.groundNodes.Count; i++)
            {
                if (bakedData.groundNodes[i].id != nodeId) continue;
                node = bakedData.groundNodes[i];
                return true;
            }

            node = null;
            return false;
        }

        private bool TryGetNodePosition(int nodeId, out Vector3 position)
        {
            if (TryGetGroundNode(nodeId, out var node))
            {
                position = node.position;
                return true;
            }

            position = Vector3.zero;
            return false;
        }

        private int NextRopeId()
        {
            var id = FirstRopeId;
            for (int i = 0; i < bakedData.ropes.Count; i++) id = Mathf.Max(id, bakedData.ropes[i].id + 1);
            return id;
        }

        private int NextHoleId()
        {
            var id = FirstHoleId;
            for (int i = 0; i < bakedData.holes.Count; i++) id = Mathf.Max(id, bakedData.holes[i].id + 1);
            return id;
        }

        private int NextWallId()
        {
            var id = FirstWallId;
            for (int i = 0; i < bakedData.walls.Count; i++) id = Mathf.Max(id, bakedData.walls[i].id + 1);
            return id;
        }

        private void DrawStrokePreview()
        {
            var color = graphDirty ? new Color(1f, 0.74f, 0.24f) : new Color(0.28f, 0.72f, 1f);
            for (int i = 0; i < strokes.Count; i++)
            {
                CreateLine($"Stroke_{i + 1}", strokes[i].points, color, strokePreviewWidth);
            }

            if (currentStroke != null)
            {
                CreateLine("Stroke_Current", currentStroke.points, Color.white, strokePreviewWidth * 1.4f);
            }
        }

        private void DrawBoardPreview()
        {
            if (!showBoard) return;

            var boardRoot = new GameObject("RuntimeLevelEditorBoard");
            boardRoot.transform.SetParent(previewRoot, false);

            var surface = GameObject.CreatePrimitive(PrimitiveType.Plane);
            surface.name = "Surface";
            surface.transform.SetParent(boardRoot.transform, false);
            surface.transform.position = new Vector3(0f, graphPlaneY - 0.01f, 0f);
            surface.transform.localScale = new Vector3(boardSize / 10f, 1f, boardSize / 10f);

            var collider = surface.GetComponent<Collider>();
            if (collider != null) Destroy(collider);

            var renderer = surface.GetComponent<Renderer>();
            if (renderer != null) renderer.sharedMaterial = SlimyPalette.GetMaterial(new Color(0.13f, 0.15f, 0.18f));

            var halfSize = boardSize * 0.5f;
            var lineCount = Mathf.FloorToInt(boardSize / boardGridSpacing);
            var start = -lineCount * boardGridSpacing * 0.5f;
            var gridColor = new Color(0.32f, 0.38f, 0.44f);
            var axisColor = new Color(0.72f, 0.78f, 0.84f);

            for (int i = 0; i <= lineCount; i++)
            {
                var offset = start + i * boardGridSpacing;
                var color = Mathf.Abs(offset) < boardGridSpacing * 0.25f ? axisColor : gridColor;
                var width = Mathf.Abs(offset) < boardGridSpacing * 0.25f ? boardGridWidth * 1.8f : boardGridWidth;

                CreateLine(boardRoot.transform, $"Grid_X_{i}", new[]
                {
                    new Vector3(-halfSize, graphPlaneY, offset),
                    new Vector3(halfSize, graphPlaneY, offset)
                }, color, width);

                CreateLine(boardRoot.transform, $"Grid_Z_{i}", new[]
                {
                    new Vector3(offset, graphPlaneY, -halfSize),
                    new Vector3(offset, graphPlaneY, halfSize)
                }, color, width);
            }
        }

        private void DrawGraphPreview()
        {
            var edgeColor = new Color(0.62f, 0.66f, 0.70f);
            for (int i = 0; i < bakedData.groundNodes.Count; i++)
            {
                var node = bakedData.groundNodes[i];
                if (node.connectedNodeIds == null) continue;

                for (int c = 0; c < node.connectedNodeIds.Count; c++)
                {
                    var otherId = node.connectedNodeIds[c];
                    if (node.id > otherId) continue;
                    if (!TryGetNodePosition(otherId, out var otherPosition)) continue;

                    CreateLine($"Edge_{node.id}_{otherId}", new[] { node.position, otherPosition }, edgeColor,
                        groundPreviewWidth);
                }
            }

            for (int i = 0; i < bakedData.groundNodes.Count; i++)
            {
                CreateMarker($"Node_{bakedData.groundNodes[i].id}", bakedData.groundNodes[i].position,
                    nodePreviewSize, new Color(0.92f, 0.94f, 0.96f));
            }
        }

        private void DrawHolePreview()
        {
            for (int i = 0; i < bakedData.holes.Count; i++)
            {
                var hole = bakedData.holes[i];
                if (!TryGetNodePosition(hole.nodeId, out var position)) continue;
                var color = hole.hidden ? SlimyPalette.Hidden : SlimyPalette.Get(hole.color);
                CreateMarker($"Hole_{hole.id}_{hole.color}", position + Vector3.up * 0.04f, holePreviewSize,
                    color);

                if (hole.lockedKeyCount > 0)
                {
                    CreateMarker($"Hole_{hole.id}_Lock", position + Vector3.up * 0.16f, holePreviewSize * 0.45f,
                        SlimyPalette.Lock);
                }
            }
        }

        private void DrawWallPreview()
        {
            for (int i = 0; i < bakedData.walls.Count; i++)
            {
                var wall = bakedData.walls[i];
                if (!TryGetNodePosition(wall.nodeId, out var position)) continue;
                CreateMarker($"Wall_{wall.id}", position + Vector3.up * 0.12f, nodePreviewSize * 1.5f,
                    SlimyPalette.Wall);
            }
        }

        private void DrawRopePreview()
        {
            for (int i = 0; i < bakedData.ropes.Count; i++)
            {
                var rope = bakedData.ropes[i];
                var points = GetRopePoints(rope.occupiedNodeIds,
                    rope.containedByRopeId > 0 ? ContainedRopePreviewLift : RopePreviewLift);
                var color = rope.hidden ? SlimyPalette.Hidden : SlimyPalette.Get(rope.color);
                var width = rope.containedByRopeId > 0 ? ropePreviewWidth * 0.55f : ropePreviewWidth;
                CreateLine($"Rope_{rope.id}_{rope.color}", points, color, width);
                if (points.Count > 0)
                {
                    CreateMarker($"Rope_{rope.id}_Head", points[0] + Vector3.up * 0.08f, nodePreviewSize * 1.2f,
                        rope.id == containedRopeOuterId ? SlimyPalette.Key : Color.white);
                }

                if (rope.hasKey && points.Count > 0)
                {
                    CreateMarker($"Rope_{rope.id}_Key", points[points.Count / 2] + Vector3.up * 0.2f,
                        nodePreviewSize * 1.2f, SlimyPalette.Key);
                }
            }

            if (currentRopeNodeIds.Count > 0)
            {
                var points = GetRopePoints(currentRopeNodeIds, RopePreviewLift);
                CreateLine("Rope_Current", points, SlimyPalette.Get(currentRopeColor), ropePreviewWidth * 0.85f);
            }
        }

        private List<Vector3> GetRopePoints(IReadOnlyList<int> nodeIds, float lift)
        {
            var points = new List<Vector3>();
            if (nodeIds == null) return points;

            for (int i = 0; i < nodeIds.Count; i++)
            {
                if (TryGetNodePosition(nodeIds[i], out var position)) points.Add(position + Vector3.up * lift);
            }

            return points;
        }

        private void CreateLine(string name, IReadOnlyList<Vector3> points, Color color, float width)
        {
            CreateLine(previewRoot, name, points, color, width);
        }

        private void CreateLine(Transform parent, string name, IReadOnlyList<Vector3> points, Color color, float width)
        {
            if (points == null || points.Count < 2) return;

            var go = new GameObject(name);
            go.transform.SetParent(parent, false);

            var line = go.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.positionCount = points.Count;
            line.widthMultiplier = width;
            line.numCapVertices = 4;
            line.numCornerVertices = 4;
            line.sharedMaterial = SlimyPalette.GetMaterial(color);

            for (int i = 0; i < points.Count; i++)
            {
                line.SetPosition(i, points[i] + Vector3.up * previewLift);
            }
        }

        private void CreateMarker(string name, Vector3 position, float size, Color color)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = name;
            go.transform.SetParent(previewRoot, false);
            go.transform.position = position + Vector3.up * previewLift;
            go.transform.localScale = Vector3.one * size;

            var collider = go.GetComponent<Collider>();
            if (collider != null) Destroy(collider);

            var renderer = go.GetComponent<Renderer>();
            if (renderer != null) renderer.sharedMaterial = SlimyPalette.GetMaterial(color);
        }

        private void EnsurePreviewRoot()
        {
            if (previewRoot != null) return;

            var existing = transform.Find("RuntimeLevelEditorPreview");
            if (existing != null)
            {
                previewRoot = existing;
                return;
            }

            var root = new GameObject("RuntimeLevelEditorPreview");
            root.transform.SetParent(transform, false);
            previewRoot = root.transform;
        }

        private void ClearPreviewChildren()
        {
            if (previewRoot == null) return;

            for (int i = previewRoot.childCount - 1; i >= 0; i--)
            {
                var child = previewRoot.GetChild(i).gameObject;
                if (Application.isPlaying) Destroy(child);
                else DestroyImmediate(child);
            }
        }

        private void DestroyPreview()
        {
            if (previewRoot == null) return;

            var root = previewRoot.gameObject;
            previewRoot = null;
            if (Application.isPlaying) Destroy(root);
            else DestroyImmediate(root);
        }

        private void EnsureCollections()
        {
            if (strokes == null) strokes = new List<StrokeData>();
            if (currentRopeNodeIds == null) currentRopeNodeIds = new List<int>();
            if (bakedData == null) bakedData = new SlimyLevelData();
            if (bakedData.groundNodes == null) bakedData.groundNodes = new List<GroundNodeData>();
            if (bakedData.ropes == null) bakedData.ropes = new List<RopeData>();
            if (bakedData.holes == null) bakedData.holes = new List<HoleData>();
            if (bakedData.walls == null) bakedData.walls = new List<WallData>();
        }

        private void SetReport(string report)
        {
            lastReport = report;
        }

        private string BuildBakeReport(List<string> warnings, string validationReport)
        {
            var builder = new StringBuilder();
            builder.AppendLine($"Baked graph for Level_{levelIndex}.");
            builder.AppendLine(
                $"nodes: {NodeCount}, edges: {EdgeCount}, ropes: {RopeCount}, holes: {HoleCount}, walls: {WallCount}");

            for (int i = 0; i < warnings.Count; i++)
            {
                builder.AppendLine($"WARNING: {warnings[i]}");
            }

            builder.AppendLine();
            builder.Append(validationReport);
            return builder.ToString().TrimEnd();
        }

        private string BuildValidationReport(SlimyLevelData data, List<string> errors, List<string> warnings)
        {
            var builder = new StringBuilder();
            builder.AppendLine(errors.Count == 0 ? "Level editor data is valid." : "Level editor data has errors.");
            builder.AppendLine(
                $"nodes: {data.groundNodes.Count}, edges: {CountEdges(data)}, ropes: {data.ropes.Count}, holes: {data.holes.Count}, walls: {data.walls?.Count ?? 0}");

            for (int i = 0; i < errors.Count; i++)
            {
                builder.AppendLine($"ERROR: {errors[i]}");
            }

            for (int i = 0; i < warnings.Count; i++)
            {
                builder.AppendLine($"WARNING: {warnings[i]}");
            }

            return builder.ToString().TrimEnd();
        }

        private static void Connect(Dictionary<int, List<int>> connections, HashSet<long> edges, int a, int b,
            List<string> warnings)
        {
            if (a == b)
            {
                warnings.Add($"Two consecutive split points welded into node {a}; weld tolerance may be too large.");
                return;
            }

            var key = EdgeKey(a, b);
            if (!edges.Add(key)) return;

            GetConnectionList(connections, a).Add(b);
            GetConnectionList(connections, b).Add(a);
        }

        private static List<int> GetConnectionList(Dictionary<int, List<int>> connections, int id)
        {
            if (connections.TryGetValue(id, out var list)) return list;

            list = new List<int>();
            connections[id] = list;
            return list;
        }

        private void AddSplit(WorkingSegment segment, float t, Vector3 position)
        {
            t = Mathf.Clamp01(t);
            position = ProjectToGraphPlane(position);

            for (int i = 0; i < segment.Splits.Count; i++)
            {
                if (Mathf.Abs(segment.Splits[i].T - t) < 0.0005f) return;
                if (DistanceSqrXZ(segment.Splits[i].Position, position) < 0.0001f) return;
            }

            segment.Splits.Add(new SegmentSplit(t, position));
        }

        private Vector3 ProjectToGraphPlane(Vector3 position)
        {
            return new Vector3(position.x, graphPlaneY, position.z);
        }

        private bool IsWithinBoard(Vector3 position)
        {
            var halfSize = boardSize * 0.5f;
            return Mathf.Abs(position.x) <= halfSize + 0.001f &&
                   Mathf.Abs(position.z) <= halfSize + 0.001f;
        }

        private void ClampPointsToBoard(List<Vector3> points)
        {
            if (!limitInputToBoard || points == null) return;

            for (int i = 0; i < points.Count; i++)
            {
                points[i] = ClampPointToBoard(points[i]);
            }
        }

        private Vector3 ClampPointToBoard(Vector3 position)
        {
            var halfSize = boardSize * 0.5f;
            return new Vector3(
                Mathf.Clamp(position.x, -halfSize, halfSize),
                graphPlaneY,
                Mathf.Clamp(position.z, -halfSize, halfSize));
        }

        private static bool AreAdjacentSegmentsFromSameStroke(WorkingSegment a, WorkingSegment b)
        {
            return a.StrokeIndex == b.StrokeIndex && Mathf.Abs(a.SegmentIndex - b.SegmentIndex) <= 1;
        }

        private static bool TrySegmentIntersectionXZ(Vector3 a, Vector3 b, Vector3 c, Vector3 d,
            out float t, out float u, out Vector3 point, out bool overlaps)
        {
            t = 0f;
            u = 0f;
            point = Vector3.zero;
            overlaps = false;

            var p = new Vector2(a.x, a.z);
            var r = new Vector2(b.x - a.x, b.z - a.z);
            var q = new Vector2(c.x, c.z);
            var s = new Vector2(d.x - c.x, d.z - c.z);
            var rxs = Cross(r, s);
            var qMinusP = q - p;
            var qpxr = Cross(qMinusP, r);

            if (Mathf.Abs(rxs) < 0.00001f)
            {
                if (Mathf.Abs(qpxr) > 0.00001f) return false;

                var rr = Vector2.Dot(r, r);
                if (rr <= 0.00001f) return false;

                var t0 = Vector2.Dot(qMinusP, r) / rr;
                var t1 = t0 + Vector2.Dot(s, r) / rr;
                if (t0 > t1)
                {
                    var temp = t0;
                    t0 = t1;
                    t1 = temp;
                }

                var overlapStart = Mathf.Max(0f, t0);
                var overlapEnd = Mathf.Min(1f, t1);
                overlaps = overlapEnd - overlapStart > 0.02f;
                return false;
            }

            t = Cross(qMinusP, s) / rxs;
            u = Cross(qMinusP, r) / rxs;

            if (t < -0.0001f || t > 1.0001f || u < -0.0001f || u > 1.0001f)
            {
                return false;
            }

            t = Mathf.Clamp01(t);
            u = Mathf.Clamp01(u);
            point = Vector3.Lerp(a, b, t);
            return true;
        }

        private static float Cross(Vector2 a, Vector2 b)
        {
            return a.x * b.y - a.y * b.x;
        }

        private static List<Vector3> RemoveTinySegments(IReadOnlyList<Vector3> source, float minDistance)
        {
            var result = new List<Vector3>();
            if (source == null || source.Count == 0) return result;

            result.Add(source[0]);
            var minDistanceSqr = minDistance * minDistance;
            for (int i = 1; i < source.Count; i++)
            {
                if (DistanceSqrXZ(result[result.Count - 1], source[i]) >= minDistanceSqr)
                {
                    result.Add(source[i]);
                }
            }

            if (result.Count == 1 && source.Count > 1) result.Add(source[source.Count - 1]);
            return result;
        }

        private static List<Vector3> SimplifyPolyline(IReadOnlyList<Vector3> source, float tolerance)
        {
            var result = new List<Vector3>();
            if (source == null || source.Count == 0) return result;
            if (source.Count <= 2 || tolerance <= 0f)
            {
                result.AddRange(source);
                return result;
            }

            var keep = new bool[source.Count];
            keep[0] = true;
            keep[source.Count - 1] = true;
            SimplifySection(source, 0, source.Count - 1, tolerance * tolerance, keep);

            for (int i = 0; i < source.Count; i++)
            {
                if (keep[i]) result.Add(source[i]);
            }

            return result;
        }

        private static void SimplifySection(IReadOnlyList<Vector3> source, int start, int end, float toleranceSqr,
            bool[] keep)
        {
            if (end <= start + 1) return;

            var bestIndex = -1;
            var bestDistance = 0f;

            for (int i = start + 1; i < end; i++)
            {
                var distance = DistancePointToSegmentSqrXZ(source[i], source[start], source[end]);
                if (distance <= bestDistance) continue;

                bestDistance = distance;
                bestIndex = i;
            }

            if (bestIndex < 0 || bestDistance <= toleranceSqr) return;

            keep[bestIndex] = true;
            SimplifySection(source, start, bestIndex, toleranceSqr, keep);
            SimplifySection(source, bestIndex, end, toleranceSqr, keep);
        }

        private static List<Vector3> SmoothPolyline(IReadOnlyList<Vector3> source)
        {
            var result = new List<Vector3>();
            if (source == null || source.Count == 0) return result;
            if (source.Count == 1)
            {
                result.Add(source[0]);
                return result;
            }

            result.Add(source[0]);
            for (int i = 1; i < source.Count; i++)
            {
                var a = source[i - 1];
                var b = source[i];
                result.Add(Vector3.Lerp(a, b, 0.25f));
                result.Add(Vector3.Lerp(a, b, 0.75f));
            }

            result.Add(source[source.Count - 1]);
            return result;
        }

        private static List<Vector3> ResamplePolyline(IReadOnlyList<Vector3> source, int segmentCount)
        {
            var result = new List<Vector3>();
            if (source == null || source.Count == 0 || segmentCount <= 0) return result;

            var length = PolylineLength(source);
            if (length <= 0f)
            {
                result.Add(source[0]);
                return result;
            }

            var step = length / segmentCount;
            for (int i = 0; i <= segmentCount; i++)
            {
                result.Add(EvaluatePolylineAtDistance(source, Mathf.Min(length, step * i)));
            }

            return result;
        }

        private static Vector3 EvaluatePolylineAtDistance(IReadOnlyList<Vector3> source, float distance)
        {
            if (source.Count == 0) return Vector3.zero;
            if (distance <= 0f) return source[0];

            var walked = 0f;
            for (int i = 1; i < source.Count; i++)
            {
                var segmentLength = Mathf.Sqrt(DistanceSqrXZ(source[i - 1], source[i]));
                if (walked + segmentLength >= distance)
                {
                    var t = segmentLength <= Mathf.Epsilon ? 0f : (distance - walked) / segmentLength;
                    return Vector3.Lerp(source[i - 1], source[i], t);
                }

                walked += segmentLength;
            }

            return source[source.Count - 1];
        }

        private static float PolylineLength(IReadOnlyList<Vector3> points)
        {
            if (points == null) return 0f;

            var length = 0f;
            for (int i = 1; i < points.Count; i++)
            {
                length += Mathf.Sqrt(DistanceSqrXZ(points[i - 1], points[i]));
            }

            return length;
        }

        private static float DistancePointToSegmentSqrXZ(Vector3 point, Vector3 a, Vector3 b)
        {
            var ab = new Vector2(b.x - a.x, b.z - a.z);
            var ap = new Vector2(point.x - a.x, point.z - a.z);
            var denom = Vector2.Dot(ab, ab);
            if (denom <= Mathf.Epsilon) return DistanceSqrXZ(point, a);

            var t = Mathf.Clamp01(Vector2.Dot(ap, ab) / denom);
            var closest = new Vector3(
                Mathf.Lerp(a.x, b.x, t),
                point.y,
                Mathf.Lerp(a.z, b.z, t));
            return DistanceSqrXZ(point, closest);
        }

        private static float DistanceSqrXZ(Vector3 a, Vector3 b)
        {
            var dx = a.x - b.x;
            var dz = a.z - b.z;
            return dx * dx + dz * dz;
        }

        private static long EdgeKey(int a, int b)
        {
            var low = Mathf.Min(a, b);
            var high = Mathf.Max(a, b);
            return ((long)low << 32) | (uint)high;
        }

        private static int CountEdges(SlimyLevelData data)
        {
            if (data?.groundNodes == null) return 0;

            var edges = new HashSet<long>();
            for (int i = 0; i < data.groundNodes.Count; i++)
            {
                var node = data.groundNodes[i];
                if (node.connectedNodeIds == null) continue;

                for (int c = 0; c < node.connectedNodeIds.Count; c++)
                {
                    if (node.id == node.connectedNodeIds[c]) continue;
                    edges.Add(EdgeKey(node.id, node.connectedNodeIds[c]));
                }
            }

            return edges.Count;
        }

        private static Dictionary<int, Vector3> BuildPositionLookup(SlimyLevelData data)
        {
            var result = new Dictionary<int, Vector3>();
            if (data?.groundNodes == null) return result;

            for (int i = 0; i < data.groundNodes.Count; i++)
            {
                result[data.groundNodes[i].id] = data.groundNodes[i].position;
            }

            return result;
        }

        private static Dictionary<int, List<int>> BuildConnectionLookup(SlimyLevelData data)
        {
            var result = new Dictionary<int, List<int>>();
            if (data?.groundNodes == null) return result;

            for (int i = 0; i < data.groundNodes.Count; i++)
            {
                result[data.groundNodes[i].id] = data.groundNodes[i].connectedNodeIds ?? new List<int>();
            }

            return result;
        }

        private static SlimyLevelData CloneData(SlimyLevelData source)
        {
            var clone = new SlimyLevelData();
            if (source == null) return clone;

            clone.levelIndex = source.levelIndex;

            if (source.groundNodes != null)
            {
                for (int i = 0; i < source.groundNodes.Count; i++)
                {
                    var node = source.groundNodes[i];
                    clone.groundNodes.Add(new GroundNodeData
                    {
                        id = node.id,
                        position = node.position,
                        connectedNodeIds = node.connectedNodeIds != null
                            ? new List<int>(node.connectedNodeIds)
                            : new List<int>()
                    });
                }
            }

            if (source.ropes != null)
            {
                for (int i = 0; i < source.ropes.Count; i++)
                {
                    var rope = source.ropes[i];
                    clone.ropes.Add(new RopeData
                    {
                        id = rope.id,
                        color = rope.color,
                        hasKey = rope.hasKey,
                        hidden = rope.hidden,
                        revealAfterCollections = rope.revealAfterCollections,
                        containedByRopeId = rope.containedByRopeId,
                        occupiedNodeIds = rope.occupiedNodeIds != null
                            ? new List<int>(rope.occupiedNodeIds)
                            : new List<int>()
                    });
                }
            }

            if (source.holes != null)
            {
                for (int i = 0; i < source.holes.Count; i++)
                {
                    var hole = source.holes[i];
                    clone.holes.Add(new HoleData
                    {
                        id = hole.id,
                        color = hole.color,
                        nodeId = hole.nodeId,
                        hidden = hole.hidden,
                        revealAfterCollections = hole.revealAfterCollections,
                        lockedKeyCount = hole.lockedKeyCount
                    });
                }
            }

            if (source.walls != null)
            {
                for (int i = 0; i < source.walls.Count; i++)
                {
                    var wall = source.walls[i];
                    clone.walls.Add(new WallData
                    {
                        id = wall.id,
                        nodeId = wall.nodeId,
                        ropeCollectionCount = wall.ropeCollectionCount
                    });
                }
            }

            return clone;
        }

        private static string FormatPoint(Vector3 point)
        {
            return $"({point.x:0.00}, {point.z:0.00})";
        }

        private sealed class NodeWelder
        {
            private readonly float tolerance;
            public readonly List<Vector3> Nodes = new List<Vector3>();

            public NodeWelder(float tolerance)
            {
                this.tolerance = Mathf.Max(0.01f, tolerance);
            }

            public int GetOrCreate(Vector3 position)
            {
                var existing = FindNearestIndex(position, tolerance);
                if (existing >= 0) return FirstNodeId + existing;

                var index = Nodes.Count;
                Nodes.Add(position);
                return FirstNodeId + index;
            }

            public int FindNearestId(Vector3 position, float maxDistance)
            {
                var index = FindNearestIndex(position, maxDistance);
                return index >= 0 ? FirstNodeId + index : -1;
            }

            private int FindNearestIndex(Vector3 position, float maxDistance)
            {
                var best = -1;
                var bestDistance = maxDistance * maxDistance;

                for (int i = 0; i < Nodes.Count; i++)
                {
                    var distance = DistanceSqrXZ(Nodes[i], position);
                    if (distance >= bestDistance) continue;

                    bestDistance = distance;
                    best = i;
                }

                return best;
            }
        }
    }
}

// -----------------------------------------------------------------------------
// UI Animation Utility
//
// AI-GENERATED. Authored by Claude (Anthropic) via Claude Code, September 2026,
// to a written design brief by the project author.
// See README.md in the folder above for usage.
// -----------------------------------------------------------------------------

using System.Collections.Generic;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace rmf_claude.DOTweenUI
{

    /// <summary>
    /// Scene-view editing for a step's movement path: the path drawn where the object will actually
    /// travel, with handles to drag its points, a + on every segment to add one, and Ctrl+click to
    /// remove one. Started from the step's "Edit Path in Scene" button, and ended by that button,
    /// Esc, the overlay's Done button, a selection change, a play-mode change or a recompile.
    ///
    /// It follows Unity's own Edit Collider pattern rather than drawing handles whenever a step
    /// happens to be expanded: while editing, the transform tool is hidden and clicks on empty space
    /// are swallowed, so a missed handle cannot move the object or select something else instead.
    ///
    /// Everything it writes goes through a SerializedObject, which is what gives each drag an Undo
    /// entry and marks prefab overrides, exactly as typing into the inspector would.
    ///
    /// Only position steps can be edited here. SizeDelta, OffsetMin/Max and Scale paths are real,
    /// but they are paths through a size or a scale, and there is no honest place to draw one.
    /// </summary>
    [InitializeOnLoad]
    internal static class UIAnimationPathEditor
    {
        private static readonly Color PathColor = new Color(1f, 0.62f, 0.15f, 1f);
        private static readonly Color InsertColor = new Color(1f, 0.62f, 0.15f, 0.55f);
        private static readonly Color StartColor = new Color(0.75f, 0.75f, 0.75f, 1f);
        private static readonly Color EndColor = new Color(0.35f, 0.85f, 0.45f, 1f);
        private static readonly Color DeleteColor = new Color(1f, 0.25f, 0.2f, 1f);

        private const int SamplesPerSegment = 16;

        private static readonly int PointHint = "UIAnimationPathPoint".GetHashCode();

        // The step being edited, by owner and property path. Static because only one path is
        // edited at a time, the same one-at-a-time rule the preview follows.
        private static UIAnimationPlayer player;
        private static string stepPath;
        private static SerializedObject serialized;

        private static bool hidTools;
        private static bool toolsWereHidden;

        // Scratch lists, reused so a repaint does not allocate more than it has to.
        private static readonly List<Vector3> values = new List<Vector3>();
        private static readonly List<Vector3> curve = new List<Vector3>();
        private static readonly List<int> curveIndex = new List<int>();
        private static readonly List<Vector3> samples = new List<Vector3>();

        private static GUIStyle labelStyle;

        static UIAnimationPathEditor()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
            SceneView.duringSceneGui += OnSceneGUI;

            Selection.selectionChanged -= OnSelectionChanged;
            Selection.selectionChanged += OnSelectionChanged;

            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;

            // Tools.hidden is Editor-wide state and outlives a domain reload, so it has to be put
            // back BEFORE the reload wipes the flag that says this class was the one that hid it.
            AssemblyReloadEvents.beforeAssemblyReload -= Stop;
            AssemblyReloadEvents.beforeAssemblyReload += Stop;
        }

        // ---------------------------------------------------------------- API for the drawer

        public static bool IsEditing(SerializedProperty step)
        {
            return player != null
                && step.serializedObject.targetObject == player
                && step.propertyPath == stepPath;
        }

        /// <summary>Whether this step can be edited in the Scene view, and if not, a reason to show.</summary>
        public static bool CanEdit(SerializedProperty step, UIAnimationStepType type, out string reason)
        {
            if (type != UIAnimationStepType.AnchoredPosition && type != UIAnimationStepType.LocalPosition)
            {
                reason = "Only position steps can be drawn in the Scene view. A path through a size or a " +
                         "scale has nowhere honest to be drawn - type its points in instead.";
                return false;
            }

            if (!(step.serializedObject.targetObject is UIAnimationPlayer))
            {
                reason = "A shared animation set has no object to draw the path on. Edit the path from a " +
                         "player that uses this set, or type the points in.";
                return false;
            }

            if (step.serializedObject.isEditingMultipleObjects)
            {
                reason = "Select a single player to edit its path in the Scene view.";
                return false;
            }

            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                reason = "Not available in play mode - changes made there are thrown away when it ends.";
                return false;
            }

            reason = null;
            return true;
        }

        public static void Begin(SerializedProperty step)
        {
            Stop();

            var owner = step.serializedObject.targetObject as UIAnimationPlayer;
            if (owner == null) return;

            player = owner;
            stepPath = step.propertyPath;
            serialized = new SerializedObject(owner);

            // The move tool's gizmo sits exactly where the path starts. Hidden while editing, and put
            // back to whatever it was rather than forced visible.
            toolsWereHidden = Tools.hidden;
            Tools.hidden = true;
            hidTools = true;

            SceneView.RepaintAll();
        }

        public static void Stop()
        {
            if (hidTools)
            {
                Tools.hidden = toolsWereHidden;
                hidTools = false;
            }

            bool wasEditing = player != null;

            player = null;
            stepPath = null;

            if (serialized != null)
            {
                serialized.Dispose();
                serialized = null;
            }

            if (wasEditing)
            {
                SceneView.RepaintAll();
                InternalEditorUtility.RepaintAllViews();
            }
        }

        /// <summary>
        /// Where the inspector's Add Point puts a new point: halfway between the last point and To,
        /// in To's space. With no points yet it is halfway along the straight line the step already
        /// takes, which needs to know where the step starts - see TryStartInToSpace.
        /// </summary>
        public static Vector3 SuggestNewPoint(SerializedProperty step, UIAnimationStepType type)
        {
            SerializedProperty points = step.FindPropertyRelative("Waypoints");
            Vector3 to = step.FindPropertyRelative("ToVector").vector3Value;

            Vector3 previous;
            if (points.arraySize > 0) previous = points.GetArrayElementAtIndex(points.arraySize - 1).vector3Value;
            else if (!TryStartInToSpace(step, type, out previous)) previous = to;

            return (previous + to) * 0.5f;
        }

        // ---------------------------------------------------------------- lifecycle

        private static void OnSelectionChanged()
        {
            if (player != null && Selection.activeGameObject != player.gameObject) Stop();
        }

        private static void OnPlayModeChanged(PlayModeStateChange change)
        {
            Stop();
        }

        // ---------------------------------------------------------------- scene GUI

        private static void OnSceneGUI(SceneView view)
        {
            if (stepPath == null) return;

            // Destroyed, or its scene was closed, since editing started.
            if (player == null || serialized == null || serialized.targetObject == null)
            {
                Stop();
                return;
            }

            serialized.Update();

            SerializedProperty step = serialized.FindProperty(stepPath);
            if (step == null)
            {
                Stop();
                return;
            }

            var type = (UIAnimationStepType)step.FindPropertyRelative("Type").enumValueIndex;
            bool positional = type == UIAnimationStepType.AnchoredPosition || type == UIAnimationStepType.LocalPosition;

            if (!positional || !step.FindPropertyRelative("UseCustomPath").boolValue)
            {
                Stop();
                return;
            }

            Event e = Event.current;

            if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape)
            {
                e.Use();
                Stop();
                return;
            }

            // Clicking empty space would otherwise select whatever is under the mouse, which would
            // end the edit. This is the same passive default control Unity's own edit modes register.
            int passive = GUIUtility.GetControlID(FocusType.Passive);
            if (e.type == EventType.Layout) HandleUtility.AddDefaultControl(passive);

            // Hover and the Ctrl-to-delete colour both depend on the mouse and modifiers, and the
            // Scene view does not repaint for those on its own.
            if (e.type == EventType.MouseMove || e.type == EventType.KeyDown || e.type == EventType.KeyUp)
            {
                view.Repaint();
            }

            string status = null;
            RectTransform rect = ResolveRect(step);

            if (UIAnimationPreview.IsPreviewing())
            {
                // The preview moves the object, and the path is drawn relative to where it sits.
                status = "Paused while an animation preview is running.";
            }
            else if (rect == null)
            {
                status = "This step has no RectTransform target, so there is nothing to draw the path for. " +
                         "Check its target slot and Target Path.";
            }
            else if (DrawAndEdit(step, type, rect))
            {
                Undo.SetCurrentGroupName("Edit Animation Path");
                serialized.ApplyModifiedProperties();
                InternalEditorUtility.RepaintAllViews();
            }

            if (DrawOverlay(view, step, status)) Stop();
        }

        /// <summary>
        /// Draws the path and its handles, and writes any edit back to the serialized step.
        /// Returns true when something changed.
        ///
        /// Values are computed exactly as playback resolves them, with one substitution: the
        /// Baseline a player captures at Awake is the value the property holds at rest, and in edit
        /// mode that is simply the value it holds now. A step without Use From starts wherever the
        /// object is when it runs, which the editor can only take to be where it is now - so a path
        /// after a step that moves the object is drawn from the object's resting place.
        /// </summary>
        private static bool DrawAndEdit(SerializedProperty step, UIAnimationStepType type, RectTransform rect)
        {
            var plane = new PlaneMapping(rect, type == UIAnimationStepType.AnchoredPosition);

            SerializedProperty points = step.FindPropertyRelative("Waypoints");
            SerializedProperty from = step.FindPropertyRelative("FromVector");
            SerializedProperty to = step.FindPropertyRelative("ToVector");

            var fromMode = (UIAnimationEndpointMode)step.FindPropertyRelative("FromMode").enumValueIndex;
            var toMode = (UIAnimationEndpointMode)step.FindPropertyRelative("ToMode").enumValueIndex;
            bool useFrom = HasAuthoredStart(step);
            bool curved = step.FindPropertyRelative("PathShape").enumValueIndex == (int)UIAnimationPathShape.Curved;

            Vector3 rest = plane.Flatten(ReadValue(rect, type));
            Vector3 fromOrigin = Origin(fromMode, rest);
            Vector3 toOrigin = Origin(toMode, rest);

            // Value space: start, each point, To. The same list PathPoints builds at runtime, with
            // the start the path plugin prepends at the front.
            values.Clear();
            values.Add(useFrom ? plane.Flatten(fromOrigin + from.vector3Value) : rest);

            for (int i = 0; i < points.arraySize; i++)
            {
                values.Add(plane.Flatten(toOrigin + points.GetArrayElementAtIndex(i).vector3Value));
            }

            values.Add(plane.Flatten(toOrigin + to.vector3Value));

            // Consecutive duplicates are dropped before the curve is built, as they are at runtime,
            // because a Catmull-Rom segment's shape depends on its neighbours. curveIndex maps each
            // value to the curve point it collapsed into.
            curve.Clear();
            curveIndex.Clear();

            for (int i = 0; i < values.Count; i++)
            {
                if (i == 0 || values[i] != values[i - 1]) curve.Add(plane.ToWorld(values[i]));
                curveIndex.Add(curve.Count - 1);
            }

            Event e = Event.current;

            if (e.type == EventType.Repaint && curve.Count > 1)
            {
                samples.Clear();

                for (int k = 0; k < curve.Count - 1; k++)
                {
                    int steps = curved ? SamplesPerSegment : 1;
                    for (int s = 0; s < steps; s++) samples.Add(Evaluate(curve, k, s / (float)steps, curved));
                }

                samples.Add(curve[curve.Count - 1]);

                Handles.color = PathColor;
                Handles.DrawAAPolyLine(3f, samples.ToArray());
            }

            bool changed = false;

            // Insert buttons first, so a point handle drawn on top of a short segment wins the click.
            for (int j = 0; j < values.Count - 1; j++)
            {
                if (values[j] == values[j + 1]) continue;

                Vector3 middle = Evaluate(curve, curveIndex[j], 0.5f, curved);
                float size = HandleUtility.GetHandleSize(middle) * 0.035f;

                Handles.color = InsertColor;
                if (Handles.Button(middle, plane.Rotation, size, size * 1.6f, Handles.DotHandleCap))
                {
                    // Segment j runs from value j to value j + 1, and value j + 1 is point j (or To),
                    // so the new point goes in at index j. Returns at once: every handle below is
                    // indexed against the list as it was before the insert.
                    InsertPoint(points, j, plane.ToAuthored(middle, toOrigin, Vector3.zero));
                    return true;
                }

                Label(middle, "+", 0f);
            }

            // Start: a handle when there is an authored From to move, a marker when there is not.
            Vector3 startWorld = plane.ToWorld(values[0]);

            if (useFrom)
            {
                Vector3 moved;
                bool unused;
                if (PointHandle(startWorld, plane, 0.09f, Handles.CircleHandleCap, StartColor, false, out moved, out unused))
                {
                    from.vector3Value = plane.ToAuthored(moved, fromOrigin, from.vector3Value);
                    changed = true;
                }

                Label(startWorld, "From", 18f);
            }
            else
            {
                if (e.type == EventType.Repaint)
                {
                    Handles.color = StartColor;
                    Handles.CircleHandleCap(0, startWorld, plane.Rotation, HandleUtility.GetHandleSize(startWorld) * 0.07f, EventType.Repaint);
                }

                Label(startWorld, "Start", 18f);
            }

            int remove = -1;

            for (int i = 0; i < points.arraySize; i++)
            {
                SerializedProperty point = points.GetArrayElementAtIndex(i);
                Vector3 world = plane.ToWorld(values[i + 1]);

                Vector3 moved;
                bool delete;
                if (PointHandle(world, plane, 0.05f, Handles.DotHandleCap, PathColor, true, out moved, out delete))
                {
                    point.vector3Value = plane.ToAuthored(moved, toOrigin, point.vector3Value);
                    changed = true;
                }

                if (delete) remove = i;

                Label(world, (i + 1).ToString(), 16f);
            }

            if (remove >= 0)
            {
                points.DeleteArrayElementAtIndex(remove);
                changed = true;
            }

            Vector3 endWorld = plane.ToWorld(values[values.Count - 1]);

            Vector3 movedEnd;
            bool noDelete;
            if (PointHandle(endWorld, plane, 0.09f, Handles.CircleHandleCap, EndColor, false, out movedEnd, out noDelete))
            {
                to.vector3Value = plane.ToAuthored(movedEnd, toOrigin, to.vector3Value);
                changed = true;
            }

            Label(endWorld, "To", 18f);

            return changed;
        }

        /// <summary>
        /// One draggable point, constrained to the canvas plane. Ctrl+click (Cmd on a Mac) on a
        /// deletable point reports delete instead of starting a drag, and the point turns red while
        /// that would happen, so the gesture is discoverable by hovering.
        /// </summary>
        private static bool PointHandle(Vector3 world, PlaneMapping plane, float sizeFactor, Handles.CapFunction cap,
            Color color, bool deletable, out Vector3 moved, out bool delete)
        {
            int id = GUIUtility.GetControlID(PointHint, FocusType.Passive);
            Event e = Event.current;
            float size = HandleUtility.GetHandleSize(world) * sizeFactor;

            bool deleteArmed = deletable
                && EditorGUI.actionKey
                && GUIUtility.hotControl == 0
                && HandleUtility.nearestControl == id;

            delete = false;
            if (deleteArmed && e.type == EventType.MouseDown && e.button == 0)
            {
                delete = true;
                e.Use();
            }

            Handles.color = deleteArmed ? DeleteColor : color;

            EditorGUI.BeginChangeCheck();
            moved = Handles.Slider2D(id, world, Vector3.zero, plane.Normal, plane.Right, plane.Up, size, cap, Vector2.zero, false);
            return EditorGUI.EndChangeCheck();
        }

        /// <summary>The instructions box. Returns true when Done was pressed.</summary>
        private static bool DrawOverlay(SceneView view, SerializedProperty step, string status)
        {
            const float width = 330f;
            const float height = 82f;

            float viewHeight = view.camera.pixelRect.height / EditorGUIUtility.pixelsPerPoint;
            var area = new Rect(10f, viewHeight - height - 10f, width, height);

            bool done;

            Handles.BeginGUI();
            GUILayout.BeginArea(area, EditorStyles.helpBox);

            GUILayout.Label("Editing path: " + Describe(step), EditorStyles.boldLabel);
            GUILayout.Label(status ?? "Drag a point to move it. Click a + to add a point. " +
                                      "Ctrl+click a numbered point to remove it.",
                EditorStyles.wordWrappedMiniLabel);

            done = GUILayout.Button("Done (Esc)", GUILayout.Width(90f));

            GUILayout.EndArea();
            Handles.EndGUI();

            return done;
        }

        /// <summary>
        /// A point's name, lifted clear of it by a fixed number of pixels. Drawn with a dark shadow
        /// because the start point sits on the object itself, and white text on a white Image is
        /// simply not there.
        /// </summary>
        private static void Label(Vector3 world, string text, float liftPixels)
        {
            if (Event.current.type != EventType.Repaint) return;

            if (labelStyle == null)
            {
                labelStyle = new GUIStyle(EditorStyles.whiteBoldLabel)
                {
                    alignment = TextAnchor.MiddleCenter,
                };
            }

            Vector2 at = HandleUtility.WorldToGUIPoint(world);
            var rect = new Rect(at.x - 40f, at.y - 9f - liftPixels, 80f, 18f);

            Handles.BeginGUI();

            Color previous = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.85f);
            GUI.Label(new Rect(rect.x + 1f, rect.y + 1f, rect.width, rect.height), text, labelStyle);
            GUI.color = previous;
            GUI.Label(rect, text, labelStyle);

            Handles.EndGUI();
        }

        /// <summary>"Show, step 2" - which animation and which step the overlay is editing.</summary>
        private static string Describe(SerializedProperty step)
        {
            string path = step.propertyPath;
            const string marker = ".Steps.Array.data[";

            int cut = path.LastIndexOf(marker);
            if (cut < 0) return step.displayName;

            SerializedProperty name = serialized.FindProperty(path.Substring(0, cut) + ".Name");
            string index = path.Substring(cut + marker.Length).TrimEnd(']');

            return (name != null ? name.stringValue : "?") + ", step " + index;
        }

        private static void InsertPoint(SerializedProperty points, int index, Vector3 value)
        {
            if (index >= points.arraySize)
            {
                points.arraySize++;
                index = points.arraySize - 1;
            }
            else
            {
                // Duplicates the element at index; overwritten straight after.
                points.InsertArrayElementAtIndex(index);
            }

            points.GetArrayElementAtIndex(index).vector3Value = value;
        }

        // ---------------------------------------------------------------- the path itself

        /// <summary>
        /// A point on segment k of the path, u from 0 to 1. Evaluated in world space, which is
        /// safe because both curve types are affine-invariant and value-to-world is an affine map.
        ///
        /// Curved reproduces DOTween's open Catmull-Rom exactly, including its end conventions,
        /// which are not the textbook ones: the control point before the first point is the SECOND
        /// point, and the one after the last is the last point mirrored through the one before it.
        /// Checked against DOTween 1.3.030 by sampling a real path tween. This matters because the
        /// drawn curve is the promise of where the object will go.
        /// </summary>
        internal static Vector3 Evaluate(List<Vector3> points, int k, float u, bool curved)
        {
            Vector3 b = points[k];
            Vector3 c = points[k + 1];

            if (!curved) return Vector3.LerpUnclamped(b, c, u);

            int last = points.Count - 1;
            Vector3 a = k == 0 ? points[1] : points[k - 1];
            Vector3 d = k + 2 > last ? c + (c - b) : points[k + 2];

            float u2 = u * u;
            float u3 = u2 * u;

            return 0.5f * ((-a + 3f * b - 3f * c + d) * u3
                         + (2f * a - 5f * b + 4f * c - d) * u2
                         + (-a + c) * u
                         + 2f * b);
        }

        // ---------------------------------------------------------------- resolving

        /// <summary>
        /// The RectTransform this step drives, by the same three tiers playback uses: the slot,
        /// then Target Path, then the player's own object. Null on an asset, which has no owner.
        /// </summary>
        private static RectTransform ResolveRect(SerializedProperty step)
        {
            var owner = step.serializedObject.targetObject as UIAnimationPlayer;
            if (owner == null) return null;

            var direct = step.FindPropertyRelative("RectTarget").objectReferenceValue as RectTransform;
            if (direct != null) return direct;

            GameObject host = UIAnimationStep.FindHost(owner.gameObject, step.FindPropertyRelative("TargetPath").stringValue);
            return host != null ? host.GetComponent<RectTransform>() : null;
        }

        private static Vector3 ReadValue(RectTransform rect, UIAnimationStepType type)
        {
            switch (type)
            {
                case UIAnimationStepType.AnchoredPosition: return rect.anchoredPosition;
                case UIAnimationStepType.LocalPosition: return rect.localPosition;
                case UIAnimationStepType.Scale: return rect.localScale;
                case UIAnimationStepType.SizeDelta: return rect.sizeDelta;
                case UIAnimationStepType.OffsetMin: return rect.offsetMin;
                case UIAnimationStepType.OffsetMax: return rect.offsetMax;
                default: return Vector3.zero;
            }
        }

        /// <summary>
        /// What an authored value is added to, for a given mode. Baseline and Current both come
        /// out as the resting value here: Baseline IS the resting value, and Current is relative to
        /// the start, which for a step with no From is where the object sits.
        /// </summary>
        private static Vector3 Origin(UIAnimationEndpointMode mode, Vector3 rest)
        {
            return mode == UIAnimationEndpointMode.Absolute ? Vector3.zero : rest;
        }

        /// <summary>Mirrors UIAnimationStep.HasAuthoredStart for a serialized step.</summary>
        private static bool HasAuthoredStart(SerializedProperty step)
        {
            return step.FindPropertyRelative("UseFrom").boolValue
                && step.FindPropertyRelative("FromMode").enumValueIndex != (int)UIAnimationEndpointMode.Current;
        }

        /// <summary>
        /// Where the step starts, expressed in To's space - the space a new point is authored in.
        /// False when that cannot be known, which is only when it depends on a target that cannot
        /// be resolved (a shared asset, or a missing target).
        /// </summary>
        private static bool TryStartInToSpace(SerializedProperty step, UIAnimationStepType type, out Vector3 start)
        {
            var fromMode = (UIAnimationEndpointMode)step.FindPropertyRelative("FromMode").enumValueIndex;
            var toMode = (UIAnimationEndpointMode)step.FindPropertyRelative("ToMode").enumValueIndex;
            Vector3 from = step.FindPropertyRelative("FromVector").vector3Value;
            bool useFrom = HasAuthoredStart(step);

            // Offsets from the start, so the start is the origin. Also true of a Baseline step with
            // no From, which starts at rest - the baseline itself.
            if (!useFrom && toMode != UIAnimationEndpointMode.Absolute)
            {
                start = Vector3.zero;
                return true;
            }

            if (useFrom && fromMode == toMode)
            {
                start = from;
                return true;
            }

            RectTransform rect = ResolveRect(step);
            if (rect == null)
            {
                start = Vector3.zero;
                return false;
            }

            Vector3 rest = ReadValue(rect, type);
            Vector3 startValue = useFrom ? Origin(fromMode, rest) + from : rest;

            start = startValue - Origin(toMode, rest);
            return true;
        }

        /// <summary>
        /// Converts between a position step's value space and world space.
        ///
        /// anchoredPosition is the pivot's offset from the anchor reference point, and localPosition
        /// is the pivot in the parent's space, so the two differ by a constant for as long as the
        /// anchors and the parent's rect do not change - which is why one subtraction taken now is
        /// enough to place every point of an anchoredPosition path.
        /// </summary>
        private struct PlaneMapping
        {
            private readonly Transform parent;
            private readonly bool anchored;
            private readonly Vector2 anchorOffset;
            private readonly float localZ;

            public readonly Quaternion Rotation;
            public readonly Vector3 Normal;
            public readonly Vector3 Right;
            public readonly Vector3 Up;

            public PlaneMapping(RectTransform rect, bool anchoredPosition)
            {
                parent = rect.parent;
                anchored = anchoredPosition;
                anchorOffset = (Vector2)rect.localPosition - rect.anchoredPosition;
                localZ = rect.localPosition.z;

                // Points move in the parent's XY plane - the plane the canvas lies in - so a drag in
                // a perspective Scene view cannot push one off the canvas in depth.
                Rotation = parent != null ? parent.rotation : Quaternion.identity;
                Normal = Rotation * Vector3.forward;
                Right = Rotation * Vector3.right;
                Up = Rotation * Vector3.up;
            }

            public Vector3 Flatten(Vector3 value)
            {
                if (anchored) value.z = 0f;
                return value;
            }

            public Vector3 ToWorld(Vector3 value)
            {
                Vector3 local = anchored
                    ? new Vector3(value.x + anchorOffset.x, value.y + anchorOffset.y, localZ)
                    : value;

                return parent != null ? parent.TransformPoint(local) : local;
            }

            /// <summary>
            /// A world position back to an authored value: into value space, minus the mode's
            /// origin. On an anchoredPosition step the authored Z is kept as it was, since it is
            /// never used and dragging should not be what changes it.
            /// </summary>
            public Vector3 ToAuthored(Vector3 world, Vector3 origin, Vector3 previous)
            {
                Vector3 local = parent != null ? parent.InverseTransformPoint(world) : world;

                if (!anchored) return local - origin;

                return new Vector3(local.x - anchorOffset.x - origin.x, local.y - anchorOffset.y - origin.y, previous.z);
            }
        }
    }
}

// -----------------------------------------------------------------------------
// UI Animation Utility
//
// AI-GENERATED. Authored by Claude (Anthropic) via Claude Code, September 2026,
// to a written design brief by the project author.
// See README.md in the folder above for usage.
// -----------------------------------------------------------------------------

using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace rmf_claude.DOTweenUI
{

    /// <summary>
    /// Scene-view drawings of what a player's animations do: a line with arrows for every position
    /// step, and a row of outlines from first to last moment for every size, offset or scale step.
    /// A position step's end dot - and its start ring, when it has a From - can be dragged to move
    /// that value, on the inspected object's own animations (not a player's Shared Animations).
    ///
    /// Drawn only for the player being inspected - or the Preview On player of a shared asset being
    /// inspected - and for an animation open in a pop-up window, which draws its own whatever is selected;
    /// only out of play mode. Where each step starts comes from UIAnimationSimulation,
    /// which replays the animation from rest the way the preview plays it, so the drawings hold still
    /// while a preview moves the real objects around underneath them.
    ///
    /// The settings are this user's editor preferences, not data on the player or the asset: they
    /// are about how one person likes to look at the Scene view, and should never show up as a
    /// change to a scene or prefab.
    /// </summary>
    internal static class UIAnimationGizmos
    {
        public enum ShowMode
        {
            Disabled = 0,
            AllAnimations = 1,
            ExpandedAnimations = 2,
            ExpandedSteps = 3,
        }

        public enum Palette
        {
            Distinct = 0,
            Rainbow = 1,
        }

        public static readonly string[] ShowNames = { "Disabled", "All Animations", "Expanded Animations", "Expanded Steps" };
        public static readonly string[] PaletteNames = { "Distinct", "Rainbow" };

        public const int MinOutlines = 2;
        public const int MaxOutlines = 12;

        private const string Prefix = "rmf_claude.DOTweenUI.Gizmos.";

        public static ShowMode Mode
        {
            get { return (ShowMode)EditorPrefs.GetInt(Prefix + "Mode", (int)ShowMode.ExpandedAnimations); }
            set { EditorPrefs.SetInt(Prefix + "Mode", (int)value); }
        }

        public static Palette Colors
        {
            get { return (Palette)EditorPrefs.GetInt(Prefix + "Palette", (int)Palette.Distinct); }
            set { EditorPrefs.SetInt(Prefix + "Palette", (int)value); }
        }

        /// <summary>
        /// The palette actually drawn with. Rainbow is only honoured in Expanded Steps: it is there to
        /// read the handful of steps being worked on in timeline order, and spread over whole animations
        /// it is just an arbitrary gradient, where distinct colours tell drawings apart far better. The
        /// stored choice is kept, so switching back to Expanded Steps brings Rainbow back.
        /// </summary>
        public static Palette EffectiveColors
        {
            get { return Mode == ShowMode.ExpandedSteps ? Colors : Palette.Distinct; }
        }

        public static int Seed
        {
            get { return EditorPrefs.GetInt(Prefix + "Seed", 0); }
            set { EditorPrefs.SetInt(Prefix + "Seed", value); }
        }

        public static int Outlines
        {
            get { return Mathf.Clamp(EditorPrefs.GetInt(Prefix + "Outlines", 4), MinOutlines, MaxOutlines); }
            set { EditorPrefs.SetInt(Prefix + "Outlines", Mathf.Clamp(value, MinOutlines, MaxOutlines)); }
        }

        // ---------------------------------------------------------------- inspector button

        /// <summary>The "Scene Gizmos" dropdown button the player and asset inspectors show under the preview.</summary>
        public static void DrawButton()
        {
            var content = new GUIContent("Scene Gizmos: " + ShowNames[(int)Mode],
                "What the Scene view draws for this player's animations: a line with arrows for each " +
                "position step, and outlines from start to finish for each size, offset or scale step.\n\n" +
                "Drag a position line's end dot to move its To, or its start ring to move its From " +
                "(when the step has one).\n\n" +
                "Expanded = open in the Inspector. Out of play mode only.");

            Rect rect = GUILayoutUtility.GetRect(content, EditorStyles.popup);

            if (EditorGUI.DropdownButton(rect, content, FocusType.Passive, EditorStyles.popup))
            {
                PopupWindow.Show(rect, new SettingsPopup());
            }
        }

        private sealed class SettingsPopup : PopupWindowContent
        {
            public override Vector2 GetWindowSize()
            {
                return new Vector2(300f, 160f);
            }

            public override void OnGUI(Rect rect)
            {
                EditorGUILayout.LabelField("Scene Gizmos", EditorStyles.boldLabel);

                float labelWidth = EditorGUIUtility.labelWidth;
                EditorGUIUtility.labelWidth = 90f;

                EditorGUI.BeginChangeCheck();

                var mode = (ShowMode)EditorGUILayout.Popup(new GUIContent("Show",
                    "Disabled = draw nothing.\n" +
                    "All Animations = every animation this player can play.\n" +
                    "Expanded Animations = the animations open in the Inspector.\n" +
                    "Expanded Steps = only the steps open in the Inspector, inside open animations."),
                    (int)Mode, ShowNames);

                // Rainbow only applies to Expanded Steps - see EffectiveColors. Anywhere else the row is
                // greyed out on Distinct, and what was chosen is left stored for when it applies again.
                bool rainbowApplies = mode == ShowMode.ExpandedSteps;
                Palette palette = Colors;

                using (new EditorGUI.DisabledScope(!rainbowApplies))
                {
                    var chosen = (Palette)EditorGUILayout.Popup(new GUIContent("Colors",
                        "Distinct = a different colour per drawing, spread as far apart as possible.\n" +
                        "Rainbow = red for the first drawing through to purple for the last, in timeline order. " +
                        "Expanded Steps only - across whole animations Distinct is always used."),
                        rainbowApplies ? (int)Colors : (int)Palette.Distinct, PaletteNames);

                    if (rainbowApplies) palette = chosen;
                }

                int outlines = EditorGUILayout.IntSlider(new GUIContent("Outlines",
                    "How many outlines a size, offset or scale step is drawn with, from its first moment to " +
                    "its last. They fade in from start to finish, so more outlines read as a denser trail."),
                    Outlines, MinOutlines, MaxOutlines);

                if (EditorGUI.EndChangeCheck())
                {
                    Mode = mode;
                    Colors = palette;
                    Outlines = outlines;
                    SceneView.RepaintAll();
                }

                using (new EditorGUI.DisabledScope(EffectiveColors != Palette.Distinct))
                {
                    if (GUILayout.Button(new GUIContent("Shuffle Colors", "Picks a new set of distinct colours.")))
                    {
                        Seed = Seed + 1;
                        SceneView.RepaintAll();
                    }
                }

                EditorGUIUtility.labelWidth = labelWidth;

                EditorGUILayout.LabelField("Edit mode only. Saved in your editor preferences.", EditorStyles.miniLabel);
            }
        }

        // ---------------------------------------------------------------- drawing

        private struct Drawing
        {
            public UIAnimationSimulation.Track Track;
            public bool Boxes;

            // A position step whose endpoints can be dragged: the serialized step to write to, its
            // two handles' control IDs, and what to call it while one is hovered. Step is null when
            // the drawing is look-only.
            public SerializedProperty Step;
            public int FromId;
            public int ToId;
            public string Name;
        }

        private static readonly List<Drawing> drawings = new List<Drawing>();
        private static readonly List<string> localNames = new List<string>();

        private static readonly int EndpointHint = "UIAnimationGizmoEndpoint".GetHashCode();

        /// <summary>
        /// Draws the gizmos for one player, with a draggable From and To on every position step the
        /// inspected object owns. serialized is the inspected object - the player, or a shared asset -
        /// says which animations and steps are expanded, and is what a drag writes through; owner is
        /// the player whose scene the steps resolve against.
        ///
        /// only is a pop-up animation window's animation: that one alone, open or not. Otherwise every
        /// animation the settings ask for, except those open in a window, which draw themselves.
        ///
        /// Runs for every event, not just Repaint, because the handles need the same control IDs in
        /// the Layout, mouse and Repaint passes. Only Repaint draws.
        /// </summary>
        public static void Draw(SerializedObject serialized, UIAnimationPlayer owner, int only = -1)
        {
            if (Mode == ShowMode.Disabled || owner == null || EditorApplication.isPlayingOrWillChangePlaymode) return;

            List<UIAnimation> animations = UIAnimationTargets.AnimationsOf(serialized);
            if (animations == null) return;

            serialized.Update();
            SerializedProperty list = serialized.FindProperty("Animations");

            bool repaint = Event.current.type == EventType.Repaint;
            drawings.Clear();
            litNow = 0;

            for (int a = 0; a < animations.Count; a++)
            {
                SerializedProperty animationProperty = list != null && a < list.arraySize ? list.GetArrayElementAtIndex(a) : null;

                if (only >= 0)
                {
                    if (a != only) continue;
                }
                else
                {
                    if (UIAnimationWindow.Find(serialized.targetObject, a) != null) continue;
                    if (Mode != ShowMode.AllAnimations && !IsOpen(list, animationProperty)) continue;
                }

                SerializedProperty steps = animationProperty != null ? animationProperty.FindPropertyRelative("Steps") : null;
                Collect(animations[a], owner, steps, Mode == ShowMode.ExpandedSteps, serialized.targetObject == owner ? a : -1,
                    true, repaint);
            }

            // A player also plays whatever its Shared asset adds, minus what its own animations override.
            // All Animations means all of it; otherwise what is expanded under Shared Animations says,
            // through the player's view of the set. Look-only: a drag here would change the set under
            // every player using it, and the section greys these fields out for the same reason.
            var player = serialized.targetObject as UIAnimationPlayer;
            SerializedObject view = player != null ? UIAnimationSharedSection.ViewOf(player) : null;

            if (only < 0 && player != null && player.EditorShared != null && (Mode == ShowMode.AllAnimations || view != null))
            {
                localNames.Clear();
                for (int a = 0; a < animations.Count; a++)
                {
                    if (animations[a] != null) localNames.Add(animations[a].Name);
                }

                SerializedProperty sharedList = view != null ? view.FindProperty("Animations") : null;
                List<UIAnimation> shared = player.EditorShared.Animations;

                for (int a = 0; a < shared.Count; a++)
                {
                    if (shared[a] == null || localNames.Contains(shared[a].Name)) continue;

                    SerializedProperty element = sharedList != null && a < sharedList.arraySize ? sharedList.GetArrayElementAtIndex(a) : null;
                    if (Mode != ShowMode.AllAnimations && !IsOpen(sharedList, element)) continue;

                    Collect(shared[a], owner, element != null ? element.FindPropertyRelative("Steps") : null,
                        Mode == ShowMode.ExpandedSteps, -1, false, repaint);
                }
            }

            bool changed = false;

            for (int i = 0; i < drawings.Count; i++)
            {
                Drawing drawing = drawings[i];
                Color color = ColorOf(i, drawings.Count);

                // Neighbouring lines put their arrows at different points along themselves. An out-and-
                // back pair draws the same line twice, and with the same spacing the return's arrows
                // would land exactly on top of the outward ones and hide them.
                if (drawing.Boxes)
                {
                    if (repaint) DrawOutlines(drawing.Track, color);
                }
                else
                {
                    if (repaint) DrawRoute(drawing.Track, color, i % 2 == 0 ? 0.5f : 0.25f);
                    changed |= DrawEndpoints(drawing, color);
                }
            }

            drawings.Clear();

            // The Scene view does not repaint for a mouse move on its own, and the white ring and the
            // name have to follow the mouse on and off an endpoint. Kept per caller: an Inspector and a
            // window both draw in one pass, and a shared value would make each look changed to the other.
            int lastLit;
            lastLitBy.TryGetValue(serialized, out lastLit);

            if (Event.current.type == EventType.MouseMove && litNow != lastLit) HandleUtility.Repaint();

            if (Event.current.type == EventType.MouseMove || repaint)
            {
                if (lastLitBy.Count > 32) lastLitBy.Clear();
                lastLitBy[serialized] = litNow;
            }

            if (changed)
            {
                Undo.SetCurrentGroupName("Move Animation Endpoint");
                serialized.ApplyModifiedProperties();
                UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
            }
        }

        private static bool IsOpen(SerializedProperty list, SerializedProperty element)
        {
            return list != null && list.isExpanded && element != null && element.isExpanded;
        }

        /// <summary>
        /// Works out one animation and queues its drawable steps. animationIndex is its place in the
        /// player's own list, for recognising the step the path editor has open, or -1 when it is not
        /// the player's own.
        /// </summary>
        private static void Collect(UIAnimation animation, UIAnimationPlayer owner, SerializedProperty steps,
            bool expandedStepsOnly, int animationIndex, bool editable, bool repaint)
        {
            // Each animation gets its own list: the drawings hold on to the tracks until everything is drawn.
            // Only Repaint draws outlines, so the other passes skip working them out.
            var results = new List<UIAnimationSimulation.Track>();
            UIAnimationSimulation.Run(animation, owner.gameObject, repaint ? Outlines : 0, results);

            // While a path is being edited, the Scene view belongs to it, as an Edit Collider mode would.
            editable &= !UIAnimationPathEditor.IsActive && steps != null;

            for (int i = 0; i < results.Count; i++)
            {
                UIAnimationSimulation.Track track = results[i];

                bool boxes;
                switch (track.Type)
                {
                    case UIAnimationStepType.AnchoredPosition:
                    case UIAnimationStepType.LocalPosition:
                        boxes = false;
                        break;

                    case UIAnimationStepType.SizeDelta:
                    case UIAnimationStepType.OffsetMin:
                    case UIAnimationStepType.OffsetMax:
                    case UIAnimationStepType.Scale:
                        boxes = true;
                        break;

                    default:
                        continue;
                }

                if (expandedStepsOnly)
                {
                    if (steps == null || !steps.isExpanded || track.StepIndex >= steps.arraySize) continue;
                    if (!steps.GetArrayElementAtIndex(track.StepIndex).isExpanded) continue;
                }

                // The path editor draws the step it is editing itself, with handles.
                if (animationIndex >= 0
                    && UIAnimationPathEditor.IsEditing(owner, "Animations.Array.data[" + animationIndex + "].Steps.Array.data[" + track.StepIndex + "]"))
                {
                    continue;
                }

                var drawing = new Drawing { Track = track, Boxes = boxes };

                // Taken before the filter below, and for every step that could have handles, so the
                // IDs line up between passes even when a drag makes a step stop moving.
                if (editable && !boxes && track.StepIndex < steps.arraySize && HasPlane(track.Rect.parent))
                {
                    drawing.Step = steps.GetArrayElementAtIndex(track.StepIndex);
                    drawing.FromId = GUIUtility.GetControlID(EndpointHint, FocusType.Passive);
                    drawing.ToId = GUIUtility.GetControlID(EndpointHint, FocusType.Passive);
                    drawing.Name = (string.IsNullOrEmpty(animation.Name) ? "?" : animation.Name) + ", step " + track.StepIndex;
                }

                // Nothing to show for a step that goes nowhere - typically a Close worked out from
                // rest, where it is already - and a dot or a stack of identical outlines is just noise.
                // Kept while one of its handles is held, or dragging To onto From would drop it mid-drag.
                if (!Moves(track) && !IsHeld(drawing.FromId) && !IsHeld(drawing.ToId)) continue;

                drawings.Add(drawing);
            }
        }

        /// <summary>
        /// Whether a point dragged in the world maps back to a position at all. Under a parent scaled
        /// to zero on X or Y - a panel that pops in from nothing, or a Canvas that has not sized itself
        /// yet - every value lands on one point, and a drag would write nonsense into the step.
        /// </summary>
        private static bool HasPlane(Transform parent)
        {
            if (parent == null) return true;
            return Vector3.Cross(parent.TransformVector(Vector3.right), parent.TransformVector(Vector3.up)).sqrMagnitude > 1e-20f;
        }

        private static bool IsHeld(int id)
        {
            return id != 0 && GUIUtility.hotControl == id;
        }

        /// <summary>
        /// Whether the step's own value changes at all. Judged on its route rather than its outlines,
        /// which also move with any other step running alongside it.
        /// </summary>
        private static bool Moves(UIAnimationSimulation.Track track)
        {
            for (int i = 1; i < track.Route.Count; i++)
            {
                if ((track.Route[i] - track.Route[0]).sqrMagnitude > 0.000001f) return true;
            }

            return false;
        }

        /// <summary>
        /// Distinct walks the hue circle by the golden ratio, which keeps any number of colours as far
        /// apart as it can and never lets neighbours in the timeline land on similar hues. Rainbow runs
        /// red to purple across the drawings in order, stopping short of wrapping back round to red.
        /// </summary>
        private static Color ColorOf(int index, int count)
        {
            if (EffectiveColors == Palette.Rainbow)
            {
                float hue = count > 1 ? 0.8f * index / (count - 1) : 0f;
                return Color.HSVToRGB(hue, 0.85f, 1f);
            }

            const float Golden = 0.61803398875f;
            float start = (Seed * 0.1372f) % 1f;
            return Color.HSVToRGB((start + index * Golden) % 1f, 0.8f, 1f);
        }

        private static readonly List<Vector3> world = new List<Vector3>();
        private static readonly List<float> worldLength = new List<float>();
        private static readonly Vector3[] outline = new Vector3[5];

        private static void DrawRoute(UIAnimationSimulation.Track track, Color color, float arrowPhase)
        {
            Transform parent = track.Rect.parent;

            world.Clear();
            for (int i = 0; i < track.Route.Count; i++)
            {
                world.Add(track.Frame.PositionToWorld(parent, track.Type, track.Route[i]));
            }

            if (world.Count < 2) return;

            Vector3 normal = parent != null ? parent.forward : Vector3.forward;

            Handles.color = color;
            Handles.DrawAAPolyLine(3f, world.ToArray());

            DrawArrows(normal, color, arrowPhase);
        }

        // ---------------------------------------------------------------- endpoint handles

        private const float StartSize = 0.05f;
        private const float EndSize = 0.045f;

        /// <summary>
        /// The ring at a position step's start and the dot at its end. Each is a handle when there is
        /// an authored value behind it - To always, From only when the step has one - and a plain
        /// marker otherwise: a step without From starts wherever the steps before it left the object,
        /// which is nothing stored on this step. Returns true when a drag changed a value.
        ///
        /// A drag converts the dropped point back exactly as the simulation resolved it - into the
        /// step's value space, minus whatever its mode adds - and leaves Z alone, as the path editor
        /// does: it moves the point within the canvas plane and nothing else.
        /// </summary>
        private static bool DrawEndpoints(Drawing drawing, Color color)
        {
            UIAnimationSimulation.Track track = drawing.Track;
            Transform parent = track.Rect.parent;
            bool editable = drawing.Step != null;
            bool changed = false;

            Vector3 start = track.Frame.PositionToWorld(parent, track.Type, track.Start);
            Vector3 end = track.Frame.PositionToWorld(parent, track.Type, track.End);

            Vector3 moved;

            bool released;

            if (editable && track.HasFrom)
            {
                if (Endpoint(drawing.FromId, start, parent, StartSize, WireCap, color, drawing.Name + ": From", out moved, out released))
                {
                    SerializedProperty from = drawing.Step.FindPropertyRelative("FromVector");
                    from.vector3Value = Authored(track, moved, track.FromOrigin, from.vector3Value);
                    changed = true;
                }

                if (released && track.Snaps) changed |= RoundOff(drawing.Step.FindPropertyRelative("FromVector"));
            }
            else if (Event.current.type == EventType.Repaint)
            {
                Handles.color = color;
                WireCap(0, start, parent != null ? parent.rotation : Quaternion.identity, HandleUtility.GetHandleSize(start) * StartSize, EventType.Repaint);
            }

            if (editable)
            {
                if (Endpoint(drawing.ToId, end, parent, EndSize, SolidCap, color, drawing.Name + ": To", out moved, out released))
                {
                    SerializedProperty to = drawing.Step.FindPropertyRelative("ToVector");
                    to.vector3Value = Authored(track, moved, track.ToOrigin, to.vector3Value);
                    changed = true;
                }

                if (released && track.Snaps) changed |= RoundOff(drawing.Step.FindPropertyRelative("ToVector"));
            }
            else if (Event.current.type == EventType.Repaint)
            {
                Handles.color = color;
                SolidCap(0, end, parent != null ? parent.rotation : Quaternion.identity, HandleUtility.GetHandleSize(end) * EndSize, EventType.Repaint);
            }

            return changed;
        }

        /// <summary>
        /// A snapping step's dragged value, rounded to whole numbers once it is let go - not during the
        /// drag, which would make the point jump from pixel to pixel under the mouse. Halves round away
        /// from zero (0.5 to 1, -0.5 to -1), not to even as Mathf.Round does. It is the typed number that
        /// is rounded, so the Inspector shows a whole number; playback rounds where it lands anyway.
        /// </summary>
        private static bool RoundOff(SerializedProperty property)
        {
            Vector3 value = property.vector3Value;
            var rounded = new Vector3(RoundHalfAway(value.x), RoundHalfAway(value.y), value.z);

            if (rounded == value) return false;

            property.vector3Value = rounded;
            return true;
        }

        private static float RoundHalfAway(float value)
        {
            return (float)System.Math.Round(value, System.MidpointRounding.AwayFromZero);
        }

        private static Vector3 Authored(UIAnimationSimulation.Track track, Vector3 world, Vector3 origin, Vector3 previous)
        {
            Vector3 value = track.Frame.WorldToPosition(track.Rect.parent, track.Type, world) - origin;
            value.z = previous.z;
            return value;
        }

        /// <summary>
        /// One endpoint, dragged within the canvas plane. Named in a label while hovered or held, which
        /// is how two endpoints sitting on top of each other - an Open's To on a Close's From - are told
        /// apart: the one named is the one a click takes.
        /// </summary>
        private static bool Endpoint(int id, Vector3 world, Transform parent, float sizeFactor, Handles.CapFunction cap,
            Color color, string name, out Vector3 moved, out bool released)
        {
            Quaternion rotation = parent != null ? parent.rotation : Quaternion.identity;
            float size = HandleUtility.GetHandleSize(world) * sizeFactor;

            Handles.color = color;

            bool wasHeld = IsHeld(id);

            EditorGUI.BeginChangeCheck();
            moved = Handles.Slider2D(id, world, Vector3.zero, rotation * Vector3.forward, rotation * Vector3.right,
                rotation * Vector3.up, size, cap, Vector2.zero, false);
            bool changed = EditorGUI.EndChangeCheck();

            released = wasHeld && !IsHeld(id);

            if (IsLit(id))
            {
                litNow = id;
                UIAnimationPathEditor.Label(world, name, 18f);
            }

            return changed;
        }

        // The endpoint lit in this pass, and in the last pass that drew or moved the mouse for each caller,
        // 0 for none.
        private static int litNow;
        private static readonly Dictionary<SerializedObject, int> lastLitBy = new Dictionary<SerializedObject, int>();

        /// <summary>Hovered with nothing held, or held.</summary>
        private static bool IsLit(int id)
        {
            return id != 0 && (GUIUtility.hotControl == id || (GUIUtility.hotControl == 0 && HandleUtility.nearestControl == id));
        }

        // Pickable a little beyond what is drawn, since the dots are small.
        private const float PickScale = 1.6f;

        private static void WireCap(int id, Vector3 position, Quaternion rotation, float size, EventType eventType)
        {
            if (eventType == EventType.Layout)
            {
                HandleUtility.AddControl(id, HandleUtility.DistanceToCircle(position, size * PickScale));
            }
            else if (eventType == EventType.Repaint)
            {
                Handles.DrawWireDisc(position, rotation * Vector3.forward, size, 2f);
                if (IsLit(id)) DrawLit(position, rotation, size);
            }
        }

        private static void SolidCap(int id, Vector3 position, Quaternion rotation, float size, EventType eventType)
        {
            if (eventType == EventType.Layout)
            {
                HandleUtility.AddControl(id, HandleUtility.DistanceToCircle(position, size * PickScale));
            }
            else if (eventType == EventType.Repaint)
            {
                Handles.DrawSolidDisc(position, rotation * Vector3.forward, size);
                if (IsLit(id)) DrawLit(position, rotation, size);
            }
        }

        /// <summary>A white ring round a hovered or held endpoint.</summary>
        private static void DrawLit(Vector3 position, Quaternion rotation, float size)
        {
            Color previous = Handles.color;
            Handles.color = Color.white;
            Handles.DrawWireDisc(position, rotation * Vector3.forward, size * 1.7f, 2f);
            Handles.color = previous;
        }

        /// <summary>
        /// Arrowheads spread evenly along the line, pointing the way the object travels. How many
        /// depends on how long the line is on screen, so a long move gets several and a nudge one.
        /// </summary>
        private static void DrawArrows(Vector3 normal, Color color, float phase)
        {
            worldLength.Clear();
            worldLength.Add(0f);

            float total = 0f;
            float screen = 0f;

            for (int i = 1; i < world.Count; i++)
            {
                total += Vector3.Distance(world[i - 1], world[i]);
                worldLength.Add(total);
                screen += Vector2.Distance(HandleUtility.WorldToGUIPoint(world[i - 1]), HandleUtility.WorldToGUIPoint(world[i]));
            }

            if (total <= 0f || screen < 24f) return;

            int count = Mathf.Clamp(Mathf.RoundToInt(screen / 110f), 1, 12);
            Handles.color = color;

            for (int k = 0; k < count; k++)
            {
                float along = (k + phase) / count * total;

                int i = 1;
                while (i < world.Count - 1 && worldLength[i] < along) i++;

                float segment = worldLength[i] - worldLength[i - 1];
                float u = segment > 0f ? (along - worldLength[i - 1]) / segment : 0f;

                Vector3 at = Vector3.Lerp(world[i - 1], world[i], u);
                Vector3 direction = (world[i] - world[i - 1]).normalized;
                if (direction == Vector3.zero) continue;

                float size = HandleUtility.GetHandleSize(at) * 0.09f;
                Vector3 side = Vector3.Cross(normal, direction).normalized * size * 0.55f;
                Vector3 tip = at + direction * size * 0.6f;
                Vector3 back = at - direction * size * 0.4f;

                Handles.DrawAAConvexPolygon(tip, back + side, back - side);
            }
        }

        private static void DrawOutlines(UIAnimationSimulation.Track track, Color color)
        {
            Transform parent = track.Rect.parent;
            int count = track.Moments.Count;

            for (int i = 0; i < count; i++)
            {
                float progress = count > 1 ? i / (float)(count - 1) : 1f;
                bool last = i == count - 1;

                track.Moments[i].Outline(parent, outline);

                Handles.color = new Color(color.r, color.g, color.b, Mathf.Lerp(0.3f, 1f, progress));
                Handles.DrawAAPolyLine(last ? 3f : 2f, outline);
            }
        }
    }
}

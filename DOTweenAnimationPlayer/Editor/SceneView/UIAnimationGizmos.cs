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
    /// that value; so can the corners and edges of a size, offset or scale step's last outline, and of
    /// its first when it has a From. Only on the inspected object's own animations (not a player's
    /// Shared Animations).
    ///
    /// An endpoint's line says its mode: solid for Absolute, dashed for Baseline, dotted for Current.
    /// A Baseline endpoint also shows a faint dashed ghost of the resting value it is measured from.
    ///
    /// The step last clicked in an Inspector or a pop-out window is the selected step. It is drawn
    /// whatever Show says (unless Disabled), on top of the rest, with a dark halo; Show's Selected Step
    /// draws it alone.
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
            SelectedStep = 4,
        }

        public enum Palette
        {
            Distinct = 0,
            Rainbow = 1,
        }

        public static readonly string[] ShowNames = { "Disabled", "All Animations", "Expanded Animations", "Expanded Steps", "Selected Step" };
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

        // ---------------------------------------------------------------- the selected step

        // The step last clicked, by its object's instance ID and its property path. Unity keeps a highlighted
        // row in every step list - one per animation - so there is no single selected step of its own to
        // read. Kept in SessionState, so a recompile doesn't lose it; instance IDs last as long as the session.
        private static bool selectionLoaded;
        private static int selectedId;
        private static string selectedPath;

        private static void LoadSelection()
        {
            if (selectionLoaded) return;

            selectedId = SessionState.GetInt(Prefix + "SelectedTarget", 0);
            selectedPath = SessionState.GetString(Prefix + "SelectedStep", string.Empty);
            selectionLoaded = true;
        }

        /// <summary>Makes a step the selected one. Called by the step drawer on any click inside the step.</summary>
        public static void Select(SerializedProperty step)
        {
            if (step == null || step.serializedObject.isEditingMultipleObjects) return;

            Object target = step.serializedObject.targetObject;
            if (target == null) return;

            LoadSelection();

            int id = target.GetInstanceID();
            if (id == selectedId && step.propertyPath == selectedPath) return;

            selectedId = id;
            selectedPath = step.propertyPath;
            SessionState.SetInt(Prefix + "SelectedTarget", selectedId);
            SessionState.SetString(Prefix + "SelectedStep", selectedPath);

            // Every view: the Scene view redraws, and an Inspector or window still marking the old step drops it.
            UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
        }

        /// <summary>Whether the Inspector should mark this step as the one the Scene view treats as selected.</summary>
        public static bool ShowsAsSelected(SerializedProperty step)
        {
            if (Mode == ShowMode.Disabled || EditorApplication.isPlayingOrWillChangePlaymode) return false;
            if (step.serializedObject.isEditingMultipleObjects || step.serializedObject.targetObject == null) return false;

            LoadSelection();
            return step.serializedObject.targetObject.GetInstanceID() == selectedId && step.propertyPath == selectedPath;
        }

        /// <summary>The index of the selected step in this step list, or -1 when it is not in it.</summary>
        private static int SelectedIndexIn(Object target, SerializedProperty steps)
        {
            LoadSelection();

            if (steps == null || target == null || target.GetInstanceID() != selectedId || string.IsNullOrEmpty(selectedPath)) return -1;

            string head = steps.propertyPath + ".Array.data[";
            if (!selectedPath.StartsWith(head, System.StringComparison.Ordinal) || !selectedPath.EndsWith("]", System.StringComparison.Ordinal)) return -1;

            int index;
            return int.TryParse(selectedPath.Substring(head.Length, selectedPath.Length - head.Length - 1), out index) ? index : -1;
        }

        // ---------------------------------------------------------------- inspector button

        /// <summary>The "Scene Gizmos" dropdown button the player and asset inspectors show under the preview.</summary>
        public static void DrawButton()
        {
            var content = new GUIContent("Scene Gizmos: " + ShowNames[(int)Mode],
                "What the Scene view draws for this player's animations: a line with arrows for each " +
                "position step, and outlines from start to finish for each size, offset or scale step.\n\n" +
                "Drag a line's end dot, or the corners and edges of a step's last outline, to change its To; " +
                "its start ring, or its first outline, to change its From (when the step has one). Shift keeps " +
                "the proportions.\n\n" +
                "Solid = Absolute, dashed = Baseline, dotted = Current. The step you last clicked is drawn on top, " +
                "with a dark outline.\n\n" +
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
                return new Vector2(300f, 178f);
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
                    "Expanded Steps = only the steps open in the Inspector, inside open animations.\n" +
                    "Selected Step = only the step you last clicked in the Inspector.\n\n" +
                    "Whatever is chosen (except Disabled), the step you last clicked is drawn too, on top."),
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
                    UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
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

                EditorGUILayout.LabelField("Click a step to select it: a bar marks it in the Inspector.", EditorStyles.miniLabel);
                EditorGUILayout.LabelField("Edit mode only. Saved in your editor preferences.", EditorStyles.miniLabel);
            }
        }

        // ---------------------------------------------------------------- drawing

        private struct Drawing
        {
            public UIAnimationSimulation.Track Track;
            public bool Boxes;
            public bool Selected;

            // How each end is drawn, by its mode: To dotted when it is an offset from where the step starts,
            // dashed when it is one from rest; From dashed when it is one from rest. A Baseline end also gets
            // a ghost of the rest it is measured from.
            public UIAnimationHandles.LineStyle FromStyle;
            public UIAnimationHandles.LineStyle ToStyle;
            public bool FromBaseline;
            public bool ToBaseline;

            // A step whose endpoints can be dragged: the serialized step to write to, its handles' control
            // IDs, and what to call it while one is hovered. Step is null when the drawing is look-only.
            // A position step has two handles, From then To; a box has eight per end - four corners, then
            // the edges - From's first.
            public SerializedProperty Step;
            public int[] Ids;
            public string Name;
        }

        private const int PositionHandles = 2;
        private const int BoxHandles = 16;

        private static readonly List<Drawing> drawings = new List<Drawing>();
        private static readonly List<string> localNames = new List<string>();

        private static readonly int EndpointHint = "UIAnimationGizmoEndpoint".GetHashCode();

        /// <summary>
        /// Draws the gizmos for one player, with draggable endpoints on every position, size, offset and
        /// scale step the inspected object owns. serialized is the inspected object - the player, or a
        /// shared asset - says which animations and steps are expanded, and is what a drag writes through;
        /// owner is the player whose scene the steps resolve against.
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
                else if (UIAnimationWindow.Find(serialized.targetObject, a) != null)
                {
                    continue;
                }

                SerializedProperty steps = animationProperty != null ? animationProperty.FindPropertyRelative("Steps") : null;

                // A window shows its animation whatever its foldout in the Inspector says.
                bool open = only >= 0 || IsOpen(list, animationProperty);
                int selected = SelectedIndexIn(serialized.targetObject, steps);

                if (!ShowsAny(open, selected)) continue;

                Collect(animations[a], owner, steps, open, selected, serialized.targetObject == owner ? a : -1, true, repaint);
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
                    SerializedProperty steps = element != null ? element.FindPropertyRelative("Steps") : null;

                    bool open = IsOpen(sharedList, element);
                    int selected = view != null ? SelectedIndexIn(view.targetObject, steps) : -1;

                    if (!ShowsAny(open, selected)) continue;

                    Collect(shared[a], owner, steps, open, selected, -1, false, repaint);
                }
            }

            bool changed = false;

            // The selected step last, so it sits on top. Colours still go by place in the list, so selecting
            // a step doesn't repaint every other one.
            try
            {
                for (int pass = 0; pass < 2; pass++)
                {
                    for (int i = 0; i < drawings.Count; i++)
                    {
                        Drawing drawing = drawings[i];
                        if (drawing.Selected != (pass == 1)) continue;

                        Color color = ColorOf(i, drawings.Count);
                        UIAnimationHandles.Highlighted = drawing.Selected;

                        // Neighbouring lines put their arrows at different points along themselves. An out-and-
                        // back pair draws the same line twice, and with the same spacing the return's arrows
                        // would land exactly on top of the outward ones and hide them.
                        if (drawing.Boxes)
                        {
                            changed |= DrawBoxes(drawing, color, repaint);
                        }
                        else
                        {
                            if (repaint) DrawRoute(drawing, color, i % 2 == 0 ? 0.5f : 0.25f);
                            changed |= DrawEndpoints(drawing, color);
                        }
                    }
                }
            }
            finally
            {
                UIAnimationHandles.Highlighted = false;
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

        /// <summary>Whether Show draws anything of an animation, open or not, holding the selected step or not.</summary>
        private static bool ShowsAny(bool open, int selected)
        {
            switch (Mode)
            {
                case ShowMode.AllAnimations: return true;
                case ShowMode.SelectedStep: return selected >= 0;
                default: return open || selected >= 0;
            }
        }

        /// <summary>Whether Show draws one step: the selected one always, the rest as the mode says.</summary>
        private static bool Shows(SerializedProperty steps, bool open, int selected, int index)
        {
            if (index == selected) return true;

            switch (Mode)
            {
                case ShowMode.AllAnimations:
                    return true;

                case ShowMode.ExpandedAnimations:
                    return open;

                case ShowMode.ExpandedSteps:
                    return open && steps != null && steps.isExpanded && index < steps.arraySize
                        && steps.GetArrayElementAtIndex(index).isExpanded;

                default:
                    return false;
            }
        }

        /// <summary>
        /// Works out one animation and queues its drawable steps. animationIndex is its place in the
        /// player's own list, for recognising the step the path editor has open, or -1 when it is not
        /// the player's own. selected is the selected step's index in it, or -1.
        /// </summary>
        private static void Collect(UIAnimation animation, UIAnimationPlayer owner, SerializedProperty steps,
            bool open, int selected, int animationIndex, bool editable, bool repaint)
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

                if (!Shows(steps, open, selected, track.StepIndex)) continue;

                // The path editor draws the step it is editing itself, with handles.
                if (animationIndex >= 0
                    && UIAnimationPathEditor.IsEditing(owner, "Animations.Array.data[" + animationIndex + "].Steps.Array.data[" + track.StepIndex + "]"))
                {
                    continue;
                }

                UIAnimationStep step = track.Step;
                bool relative = !step.UseFrom && step.ToMode == UIAnimationEndpointMode.Current;

                var drawing = new Drawing
                {
                    Track = track,
                    Boxes = boxes,
                    Selected = track.StepIndex == selected,

                    // A Current To with a From is read as the number typed (see UIAnimationSimulation.Begin),
                    // and the drawer turns it into Baseline the next time it is shown.
                    ToStyle = relative ? UIAnimationHandles.LineStyle.Dotted
                        : step.ToMode == UIAnimationEndpointMode.Baseline ? UIAnimationHandles.LineStyle.Dashed
                        : UIAnimationHandles.LineStyle.Solid,
                    ToBaseline = !relative && step.ToMode == UIAnimationEndpointMode.Baseline,
                    FromBaseline = track.HasFrom && step.FromMode == UIAnimationEndpointMode.Baseline,
                };

                drawing.FromStyle = drawing.FromBaseline ? UIAnimationHandles.LineStyle.Dashed : UIAnimationHandles.LineStyle.Solid;

                // Taken before the filter below, and for every step that could have handles, so the
                // IDs line up between passes even when a drag makes a step stop moving.
                if (editable && track.StepIndex < steps.arraySize && HasPlane(track.Rect.parent))
                {
                    drawing.Step = steps.GetArrayElementAtIndex(track.StepIndex);
                    drawing.Ids = new int[boxes ? BoxHandles : PositionHandles];
                    for (int k = 0; k < drawing.Ids.Length; k++) drawing.Ids[k] = GUIUtility.GetControlID(EndpointHint, FocusType.Passive);
                    drawing.Name = (string.IsNullOrEmpty(animation.Name) ? "?" : animation.Name) + ", step " + track.StepIndex;
                }

                // Nothing to show for a step that goes nowhere - typically a Close worked out from
                // rest, where it is already - and a dot or a stack of identical outlines is just noise.
                // Kept while one of its handles is held, or dragging To onto From would drop it mid-drag,
                // and kept when it is selected: that is the step being worked on, and a Close's To can only be
                // dragged out from rest if it is there to drag.
                if (!drawing.Selected && !Moves(track) && !AnyHeld(drawing.Ids)) continue;

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

        private static bool AnyHeld(int[] ids)
        {
            if (ids == null) return false;

            for (int i = 0; i < ids.Length; i++)
            {
                if (UIAnimationHandles.IsHeld(ids[i])) return true;
            }

            return false;
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

        /// <summary>A colour at the strength a ghost is drawn with.</summary>
        private static Color Faint(Color color)
        {
            return new Color(color.r, color.g, color.b, 0.55f);
        }

        // ---------------------------------------------------------------- position steps

        private static readonly List<Vector3> world = new List<Vector3>();
        private static readonly List<float> worldLength = new List<float>();

        private static void DrawRoute(Drawing drawing, Color color, float arrowPhase)
        {
            UIAnimationSimulation.Track track = drawing.Track;
            Transform parent = track.Rect.parent;

            world.Clear();
            for (int i = 0; i < track.Route.Count; i++)
            {
                world.Add(track.Frame.PositionToWorld(parent, track.Type, track.Route[i]));
            }

            if (world.Count < 2) return;

            DrawRestGhost(drawing, color);

            Vector3 normal = parent != null ? parent.forward : Vector3.forward;

            Handles.color = color;
            UIAnimationHandles.PolyLine(world, 3f, drawing.ToStyle);

            DrawArrows(normal, color, arrowPhase);
        }

        /// <summary>
        /// For a Baseline end, the resting position it is an offset from: a faint cross there, and a faint
        /// dashed line from it to the end. Nothing for an end sitting at rest, where the cross would only
        /// hide under the dot, nor for a To whose line already starts at rest, where the ghost would be
        /// drawn exactly over it.
        /// </summary>
        private static void DrawRestGhost(Drawing drawing, Color color)
        {
            if (!drawing.ToBaseline && !drawing.FromBaseline) return;

            UIAnimationSimulation.Track track = drawing.Track;
            Transform parent = track.Rect.parent;

            Vector3 rest = Flat(track, track.Rest);
            bool toAway = drawing.ToBaseline && (track.End - rest).sqrMagnitude > 0.000001f
                && (track.Start - rest).sqrMagnitude > 0.000001f;
            bool fromAway = drawing.FromBaseline && (track.Start - rest).sqrMagnitude > 0.000001f;
            if (!toAway && !fromAway) return;

            bool highlighted = UIAnimationHandles.Highlighted;
            UIAnimationHandles.Highlighted = false;

            Vector3 restWorld = track.Frame.PositionToWorld(parent, track.Type, rest);
            Handles.color = Faint(color);

            if (toAway)
            {
                UIAnimationHandles.PolyLine(new[] { restWorld, track.Frame.PositionToWorld(parent, track.Type, track.End) }, 1.5f,
                    UIAnimationHandles.LineStyle.Dashed);
            }

            if (fromAway)
            {
                UIAnimationHandles.PolyLine(new[] { restWorld, track.Frame.PositionToWorld(parent, track.Type, track.Start) }, 1.5f,
                    UIAnimationHandles.LineStyle.Dashed);
            }

            Quaternion rotation = parent != null ? parent.rotation : Quaternion.identity;
            float size = HandleUtility.GetHandleSize(restWorld) * 0.035f;
            Vector3 right = rotation * new Vector3(size, size, 0f);
            Vector3 up = rotation * new Vector3(-size, size, 0f);

            Handles.DrawAAPolyLine(2f, restWorld - right, restWorld + right);
            Handles.DrawAAPolyLine(2f, restWorld - up, restWorld + up);

            UIAnimationHandles.Highlighted = highlighted;
        }

        /// <summary>A value as a track holds its own: an anchored position carries no Z.</summary>
        private static Vector3 Flat(UIAnimationSimulation.Track track, Vector3 value)
        {
            if (UIAnimationStep.IsTwoDimensional(track.Type)) value.z = 0f;
            return value;
        }

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
            Quaternion rotation = parent != null ? parent.rotation : Quaternion.identity;

            Vector3 moved;

            bool released;

            if (editable && track.HasFrom)
            {
                if (Endpoint(drawing.Ids[0], start, parent, UIAnimationHandles.StartSize, UIAnimationHandles.WireCap, color,
                        drawing.Name + ": From", out moved, out released))
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
                UIAnimationHandles.WireCap(0, start, rotation, HandleUtility.GetHandleSize(start) * UIAnimationHandles.StartSize, EventType.Repaint);
            }

            if (editable)
            {
                if (Endpoint(drawing.Ids[1], end, parent, UIAnimationHandles.EndSize, UIAnimationHandles.SolidCap, color,
                        drawing.Name + ": To", out moved, out released))
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
                UIAnimationHandles.SolidCap(0, end, rotation, HandleUtility.GetHandleSize(end) * UIAnimationHandles.EndSize, EventType.Repaint);
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

            bool wasHeld = UIAnimationHandles.IsHeld(id);

            EditorGUI.BeginChangeCheck();
            moved = Handles.Slider2D(id, world, Vector3.zero, rotation * Vector3.forward, rotation * Vector3.right,
                rotation * Vector3.up, size, cap, Vector2.zero, false);
            bool changed = EditorGUI.EndChangeCheck();

            released = wasHeld && !UIAnimationHandles.IsHeld(id);

            if (UIAnimationHandles.IsLit(id))
            {
                litNow = id;
                UIAnimationHandles.Label(world, name, 18f);
            }

            return changed;
        }

        // The endpoint lit in this pass, and in the last pass that drew or moved the mouse for each caller,
        // 0 for none.
        private static int litNow;
        private static readonly Dictionary<SerializedObject, int> lastLitBy = new Dictionary<SerializedObject, int>();

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

                UIAnimationHandles.Triangle(tip, back + side, back - side);
            }
        }

        // ---------------------------------------------------------------- size, offset and scale steps

        private static readonly Vector3[] outline = new Vector3[5];
        private static readonly Vector3[] outlineX = new Vector3[5];
        private static readonly Vector3[] outlineY = new Vector3[5];

        /// <summary>
        /// A size, offset or scale step: the outline at its first moment, at its last, and a fading trail
        /// between, with a Baseline end's resting ghost underneath. The last outline - and the first, when
        /// the step has a From - has corners and edges to drag.
        /// </summary>
        private static bool DrawBoxes(Drawing drawing, Color color, bool repaint)
        {
            UIAnimationSimulation.Track track = drawing.Track;
            Transform parent = track.Rect.parent;
            bool editable = drawing.Step != null;

            if (repaint)
            {
                // The trail: every moment but the first and last, which are drawn below as the ends. Only
                // worked out on Repaint. A moment drawn within a few pixels of an end is left out: an ease
                // that settles slowly stacks its last moments on the To outline, and solid lines under a
                // dashed or dotted one fill its gaps in.
                OutlineAt(track.StartFrame, track.Type, track.Start, parent, startOutline);
                OutlineAt(track.EndFrame, track.Type, track.End, parent, endOutline);

                int count = track.Moments.Count;

                for (int i = 1; i < count - 1; i++)
                {
                    float progress = i / (float)(count - 1);

                    track.Moments[i].Outline(parent, outline);
                    if (ScreenGap(outline, startOutline) < TrailGap || ScreenGap(outline, endOutline) < TrailGap) continue;

                    Handles.color = new Color(color.r, color.g, color.b, Mathf.Lerp(0.3f, 1f, progress));
                    UIAnimationHandles.PolyLine(outline, 2f, UIAnimationHandles.LineStyle.Solid);
                }

                bool highlighted = UIAnimationHandles.Highlighted;
                UIAnimationHandles.Highlighted = false;
                Handles.color = Faint(color);

                if (drawing.FromBaseline) DrawGhost(track, track.StartFrame);
                if (drawing.ToBaseline) DrawGhost(track, track.EndFrame);

                UIAnimationHandles.Highlighted = highlighted;
            }

            // A From is an authored value, so its outline is stronger than the first moment of a step that
            // just starts wherever it is.
            Color fromLine = new Color(color.r, color.g, color.b, track.HasFrom ? 0.65f : 0.3f);

            bool changed = BoxEnd(drawing, false, track.StartFrame, track.Start, track.FromOrigin, "FromVector",
                editable && track.HasFrom, drawing.FromStyle, fromLine, 2f, color, 0);

            changed |= BoxEnd(drawing, true, track.EndFrame, track.End, track.ToOrigin, "ToVector",
                editable, drawing.ToStyle, color, 3f, color, BoxHandles / 2);

            return changed;
        }

        private static readonly Vector3[] startOutline = new Vector3[5];
        private static readonly Vector3[] endOutline = new Vector3[5];

        // Points on screen a trail outline must keep from an end to be drawn.
        private const float TrailGap = 4f;

        /// <summary>How far apart two outlines are on screen: the furthest any corner is from its counterpart.</summary>
        private static float ScreenGap(Vector3[] a, Vector3[] b)
        {
            float gap = 0f;

            for (int i = 0; i < 4; i++)
            {
                gap = Mathf.Max(gap, Vector2.Distance(HandleUtility.WorldToGUIPoint(a[i]), HandleUtility.WorldToGUIPoint(b[i])));
            }

            return gap;
        }

        /// <summary>The outline with the step's property back at rest, at one of its moments: what a Baseline end is measured from.</summary>
        private static void DrawGhost(UIAnimationSimulation.Track track, UIAnimationRectState frame)
        {
            OutlineAt(frame, track.Type, track.Rest, track.Rect.parent, outline);
            UIAnimationHandles.PolyLine(outline, 1.5f, UIAnimationHandles.LineStyle.Dashed);
        }

        /// <summary>The rect's outline at a moment, with the step's property set to a value.</summary>
        private static void OutlineAt(UIAnimationRectState frame, UIAnimationStepType type, Vector3 value, Transform parent, Vector3[] into)
        {
            frame.Write(type, value);
            frame.Outline(parent, into);
        }

        // Which way the corners and edges are numbered: corners as the outline lists them - bottom left, top
        // left, top right, bottom right - then the edges between them: left, top, right, bottom.
        private static Vector3 HandlePoint(Vector3[] corners, int handle)
        {
            if (handle < 4) return corners[handle];

            int edge = handle - 4;
            return (corners[edge] + corners[(edge + 1) % 4]) * 0.5f;
        }

        /// <summary>The left and right edges change the value's X; the top and bottom its Y.</summary>
        private static bool IsSideEdge(int handle)
        {
            return handle == 4 || handle == 6;
        }

        // The edge EdgeCap draws and picks, set just before the handle that uses it.
        private static Vector3 edgeA;
        private static Vector3 edgeB;

        // The value and moment a handle was pressed at, for Shift to keep the proportions of, and which
        // sides the box had no size along on screen then.
        private static int pressedId;
        private static Vector3 pressedValue;
        private static UIAnimationRectState pressedFrame;
        private static bool pressedFlatX;
        private static bool pressedFlatY;

        // Points on screen under which a box counts as having no size along a side.
        private const float FlatSize = 2f;

        /// <summary>
        /// One end of a box step: its outline in its mode's style and, when editable, a handle on each corner
        /// and edge that moves anything. Writes a drag back to the step and returns true when it did.
        ///
        /// Every rect view, and scale, moves the outline in proportion to the value, so how far each corner
        /// and edge moves per unit of X and of Y - measured once, by moving the value one unit each way -
        /// turns a dropped point straight back into a value. That keeps the handle exactly under the mouse
        /// and needs nothing about anchors, pivots or rotation beyond what Outline already knows. A handle
        /// that the value can't move is left out: an edge sitting on the pivot, or the far edges of an
        /// Offset Min or Max.
        /// </summary>
        private static bool BoxEnd(Drawing drawing, bool end, UIAnimationRectState frame, Vector3 value, Vector3 origin,
            string field, bool editable, UIAnimationHandles.LineStyle style, Color lineColor, float width, Color color, int firstId)
        {
            UIAnimationSimulation.Track track = drawing.Track;
            Transform parent = track.Rect.parent;
            UIAnimationStepType type = track.Type;

            OutlineAt(frame, type, value, parent, outline);

            if (Event.current.type == EventType.Repaint)
            {
                Handles.color = lineColor;
                UIAnimationHandles.PolyLine(outline, width, style);
            }

            if (!editable) return false;

            OutlineAt(frame, type, value + Vector3.right, parent, outlineX);
            OutlineAt(frame, type, value + Vector3.up, parent, outlineY);

            Quaternion rotation = parent != null ? parent.rotation : Quaternion.identity;
            Vector3 planeRight = rotation * Vector3.right;
            Vector3 planeUp = rotation * Vector3.up;
            Vector3 normal = rotation * Vector3.forward;

            bool changed = false;
            string name = drawing.Name + (end ? ": To" : ": From");

            // Edges first: where a corner sits on an edge, both are under the mouse, and EdgeCap gives way.
            for (int n = 0; n < 8; n++)
            {
                int handle = (n + 4) % 8;
                bool corner = handle < 4;

                Vector3 at = HandlePoint(outline, handle);
                Vector3 perX = HandlePoint(outlineX, handle) - at;
                Vector3 perY = HandlePoint(outlineY, handle) - at;

                bool movesX = MovesOnScreen(at, perX);
                bool movesY = MovesOnScreen(at, perY);

                if (corner ? !(movesX && movesY) : IsSideEdge(handle) ? !movesX : !movesY) continue;

                int id = drawing.Ids[firstId + handle];
                bool wasHeld = UIAnimationHandles.IsHeld(id);

                Handles.CapFunction cap;
                float size;

                if (corner)
                {
                    cap = end ? (Handles.CapFunction)UIAnimationHandles.SolidCap : UIAnimationHandles.WireCap;
                    size = HandleUtility.GetHandleSize(at) * (end ? UIAnimationHandles.EndSize : UIAnimationHandles.StartSize);
                }
                else
                {
                    edgeA = outline[handle - 4];
                    edgeB = outline[(handle - 3) % 4];
                    cap = EdgeCap;
                    size = HandleUtility.GetHandleSize(at) * 0.05f;
                }

                Handles.color = color;

                EditorGUI.BeginChangeCheck();
                Vector3 moved = Handles.Slider2D(id, at, Vector3.zero, normal, planeRight, planeUp, size, cap, Vector2.zero, false);
                bool dragged = EditorGUI.EndChangeCheck();

                bool held = UIAnimationHandles.IsHeld(id);

                if (held && !wasHeld)
                {
                    pressedId = id;
                    pressedValue = value;
                    pressedFrame = frame;
                    pressedFlatX = ScreenDistance(outline[1], outline[2]) < FlatSize;
                    pressedFlatY = ScreenDistance(outline[0], outline[1]) < FlatSize;
                }

                if (dragged)
                {
                    Vector3 delta = moved - at;
                    Vector2 step;

                    if (corner)
                    {
                        // Two unknowns, two directions in the canvas plane.
                        float a = Vector3.Dot(perX, planeRight);
                        float b = Vector3.Dot(perY, planeRight);
                        float c = Vector3.Dot(perX, planeUp);
                        float d = Vector3.Dot(perY, planeUp);
                        float det = a * d - b * c;

                        float px = Vector3.Dot(delta, planeRight);
                        float py = Vector3.Dot(delta, planeUp);

                        step = Mathf.Abs(det) > 1e-12f ? new Vector2((d * px - b * py) / det, (a * py - c * px) / det) : Vector2.zero;
                    }
                    else if (IsSideEdge(handle))
                    {
                        step = new Vector2(Vector3.Dot(delta, perX) / perX.sqrMagnitude, 0f);
                    }
                    else
                    {
                        step = new Vector2(0f, Vector3.Dot(delta, perY) / perY.sqrMagnitude);
                    }

                    var result = new Vector3(value.x + step.x, value.y + step.y, value.z);

                    if (pressedId == id)
                    {
                        result = Unfold(type, pressedFrame, pressedValue, result, pressedFlatX, pressedFlatY);

                        if (Event.current.shift)
                        {
                            result = KeepProportions(type, pressedFrame, pressedValue, result, corner ? -1 : IsSideEdge(handle) ? 0 : 1);
                        }
                    }

                    SerializedProperty property = drawing.Step.FindPropertyRelative(field);
                    Vector3 previous = property.vector3Value;
                    property.vector3Value = new Vector3(result.x - origin.x, result.y - origin.y, previous.z);
                    changed = true;
                }

                if (wasHeld && !held && track.Snaps) changed |= RoundOff(drawing.Step.FindPropertyRelative(field));

                if (UIAnimationHandles.IsLit(id))
                {
                    litNow = id;
                    UIAnimationHandles.Label(at, name, 18f);
                    UIAnimationHandles.ResizeCursor(id, at, corner ? perX + perY : IsSideEdge(handle) ? perX : perY);
                }
            }

            return changed;
        }

        private static float ScreenDistance(Vector3 a, Vector3 b)
        {
            return Vector2.Distance(HandleUtility.WorldToGUIPoint(a), HandleUtility.WorldToGUIPoint(b));
        }

        /// <summary>
        /// A box pressed while it had no size along a side - a pop-in from scale 0 - grows along that side
        /// whichever way it is pulled. Its corners all sit on one point then, which of them the mouse took is
        /// down to chance, and pulling the "wrong" one out would turn the box inside out (a negative scale).
        /// So the size along a flat side is taken as positive, by the same measure Shift keeps.
        /// </summary>
        private static Vector3 Unfold(UIAnimationStepType type, UIAnimationRectState frame, Vector3 pressed, Vector3 value,
            bool flatX, bool flatY)
        {
            if (!flatX && !flatY) return value;

            float sign = type == UIAnimationStepType.OffsetMin ? -1f : 1f;
            Vector2 constant = Proportions(type, frame, pressed) - sign * (Vector2)pressed;
            Vector2 size = sign * (Vector2)value + constant;

            if (flatX) size.x = Mathf.Abs(size.x);
            if (flatY) size.y = Mathf.Abs(size.y);

            Vector2 unfolded = sign * (size - constant);
            return new Vector3(unfolded.x, unfolded.y, value.z);
        }

        /// <summary>Whether a handle moves a visible amount on screen per unit of the value.</summary>
        private static bool MovesOnScreen(Vector3 at, Vector3 perUnit)
        {
            return (HandleUtility.WorldToGUIPoint(at + perUnit) - HandleUtility.WorldToGUIPoint(at)).sqrMagnitude > 1e-6f;
        }

        /// <summary>
        /// An edge: picked anywhere along its length, a little short of a corner's pull, and drawn white
        /// while hovered or held. The outline itself is what shows it otherwise.
        /// </summary>
        private static void EdgeCap(int id, Vector3 position, Quaternion rotation, float size, EventType eventType)
        {
            if (eventType == EventType.Layout)
            {
                HandleUtility.AddControl(id, Mathf.Max(0f, HandleUtility.DistanceToLine(edgeA, edgeB) - 2f) + 0.5f);
            }
            else if (eventType == EventType.Repaint && UIAnimationHandles.IsLit(id))
            {
                Color previous = Handles.color;
                Handles.color = Color.white;
                Handles.DrawAAPolyLine(3f, edgeA, edgeB);
                Handles.color = previous;
            }
        }

        /// <summary>
        /// Shift's version of a dragged value: the box's proportions as they were when the handle was
        /// pressed. A corner slides along the line that keeps them; an edge (axis 0 for X, 1 for Y) scales
        /// the other side by as much as it moved its own.
        ///
        /// Proportions are of the rect's real size for the rect views - what sizeDelta adds to the anchors'
        /// span, and Offset Min shrinks it as it grows - and of the scale itself for Scale. Each is the
        /// value times a sign plus a constant, so the value comes back by undoing that.
        /// </summary>
        private static Vector3 KeepProportions(UIAnimationStepType type, UIAnimationRectState frame, Vector3 pressed,
            Vector3 value, int axis)
        {
            float sign = type == UIAnimationStepType.OffsetMin ? -1f : 1f;

            Vector2 before = Proportions(type, frame, pressed);
            Vector2 constant = before - sign * (Vector2)pressed;
            Vector2 now = sign * (Vector2)value + constant;

            float factor;

            if (axis < 0)
            {
                float length = Vector2.Dot(before, before);
                if (length < 1e-8f) return value;

                factor = Vector2.Dot(now, before) / length;
            }
            else
            {
                if (Mathf.Abs(before[axis]) < 1e-6f) return value;

                factor = now[axis] / before[axis];
            }

            Vector2 kept = sign * (before * factor - constant);
            return new Vector3(kept.x, kept.y, value.z);
        }

        private static Vector2 Proportions(UIAnimationStepType type, UIAnimationRectState frame, Vector3 value)
        {
            if (type == UIAnimationStepType.Scale) return value;

            frame.Write(type, value);
            return Vector2.Scale(frame.ParentSize, frame.AnchorMax - frame.AnchorMin) + frame.SizeDelta;
        }
    }
}

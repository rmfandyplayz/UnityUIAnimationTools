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
    ///
    /// Drawn only for the player being inspected - or the Preview On player of a shared asset being
    /// inspected - and only out of play mode. Where each step starts comes from UIAnimationSimulation,
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
        }

        private static readonly List<Drawing> drawings = new List<Drawing>();
        private static readonly List<string> localNames = new List<string>();

        /// <summary>
        /// Draws the gizmos for one player. serialized is the inspected object - the player, or a shared
        /// asset - and says which animations and steps are expanded; owner is the player whose scene
        /// the steps resolve against.
        /// </summary>
        public static void Draw(SerializedObject serialized, UIAnimationPlayer owner)
        {
            if (Event.current.type != EventType.Repaint) return;
            if (Mode == ShowMode.Disabled || owner == null || EditorApplication.isPlayingOrWillChangePlaymode) return;

            List<UIAnimation> animations = UIAnimationTargets.AnimationsOf(serialized);
            if (animations == null) return;

            serialized.Update();
            SerializedProperty list = serialized.FindProperty("Animations");

            drawings.Clear();

            for (int a = 0; a < animations.Count; a++)
            {
                SerializedProperty animationProperty = list != null && a < list.arraySize ? list.GetArrayElementAtIndex(a) : null;
                if (Mode != ShowMode.AllAnimations && !IsOpen(list, animationProperty)) continue;

                SerializedProperty steps = animationProperty != null ? animationProperty.FindPropertyRelative("Steps") : null;
                Collect(animations[a], owner, steps, Mode == ShowMode.ExpandedSteps, serialized.targetObject == owner ? a : -1);
            }

            // A player also plays whatever its Shared asset adds, and All Animations means all of it.
            // Nothing in the player's inspector says whether those are expanded, so they are only
            // drawn in that mode.
            var player = serialized.targetObject as UIAnimationPlayer;
            if (Mode == ShowMode.AllAnimations && player != null && player.EditorShared != null)
            {
                localNames.Clear();
                for (int a = 0; a < animations.Count; a++)
                {
                    if (animations[a] != null) localNames.Add(animations[a].Name);
                }

                List<UIAnimation> shared = player.EditorShared.Animations;
                for (int a = 0; a < shared.Count; a++)
                {
                    if (shared[a] == null || localNames.Contains(shared[a].Name)) continue;
                    Collect(shared[a], owner, null, false, -1);
                }
            }

            for (int i = 0; i < drawings.Count; i++)
            {
                Color color = ColorOf(i, drawings.Count);

                // Neighbouring lines put their arrows at different points along themselves. An out-and-
                // back pair draws the same line twice, and with the same spacing the return's arrows
                // would land exactly on top of the outward ones and hide them.
                if (drawings[i].Boxes) DrawOutlines(drawings[i].Track, color);
                else DrawRoute(drawings[i].Track, color, i % 2 == 0 ? 0.5f : 0.25f);
            }

            drawings.Clear();
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
            bool expandedStepsOnly, int animationIndex)
        {
            // Each animation gets its own list: the drawings hold on to the tracks until everything is drawn.
            var results = new List<UIAnimationSimulation.Track>();
            UIAnimationSimulation.Run(animation, owner.gameObject, Outlines, results);

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

                // Nothing to show for a step that goes nowhere - typically a Close worked out from
                // rest, where it is already - and a dot or a stack of identical outlines is just noise.
                if (!Moves(track)) continue;

                drawings.Add(new Drawing { Track = track, Boxes = boxes });
            }
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

            Vector3 start = world[0];
            Vector3 end = world[world.Count - 1];

            Handles.DrawWireDisc(start, normal, HandleUtility.GetHandleSize(start) * 0.05f, 2f);
            Handles.DrawSolidDisc(end, normal, HandleUtility.GetHandleSize(end) * 0.045f);

            DrawArrows(normal, color, arrowPhase);
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

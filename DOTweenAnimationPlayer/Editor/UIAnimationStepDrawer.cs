// -----------------------------------------------------------------------------
// UI Animation Utility
//
// AI-GENERATED. Authored by Claude (Anthropic) via Claude Code, September 2026,
// to a written design brief by the project author.
// See README.md in the folder above for usage.
// -----------------------------------------------------------------------------

using System.Collections.Generic;
using DG.Tweening;
using UnityEditor;
using UnityEngine;

namespace rmf_claude.DOTweenUI
{

    /// <summary>
    /// Collapses a UIAnimationStep down to only the fields its selected Type actually uses.
    /// Without this the flat step class shows every value field at once and authoring is painful.
    /// </summary>
    [CustomPropertyDrawer(typeof(UIAnimationStep))]
    internal class UIAnimationStepDrawer : PropertyDrawer
    {
        private const float Pad = 2f;
        private const float LabelWidth = 74f;
        private const float ModeWidth = 78f;

        private const string AssetTargetReason =
            "Not available in a shared animation set: a ScriptableObject cannot hold a reference " +
            "to a scene object, so anything dropped here would be silently lost at the next save.\n\n" +
            "Leave it empty to animate whichever GameObject the player is on, or use Target Path " +
            "below to reach a named child of it.";

        private static GUIStyle boldFoldout;

        /// <summary>
        /// Bold version of the foldout style, so a collapsed step header stands out from the
        /// ordinary fields around it.
        ///
        /// Built as a COPY. Setting fontStyle on EditorStyles.foldout itself would bold every
        /// foldout in the whole Editor, and built-in styles are shared global state. Built lazily
        /// because EditorStyles is not available until there is a GUI skin, i.e. not at load.
        /// </summary>
        private static GUIStyle BoldFoldout
        {
            get
            {
                if (boldFoldout == null)
                {
                    boldFoldout = new GUIStyle(EditorStyles.foldout) { fontStyle = FontStyle.Bold };
                }

                return boldFoldout;
            }
        }

        private static GUIStyle dimFoldout;

        /// <summary>
        /// The bold header with its text faded, for a step that will do nothing as authored - so a
        /// collapsed list shows which rows are dead before anything is opened. Only the text fades;
        /// the arrow is a background image, and the warning icon beside it stays at full strength.
        /// </summary>
        private static GUIStyle DimFoldout
        {
            get
            {
                if (dimFoldout == null)
                {
                    dimFoldout = new GUIStyle(BoldFoldout);

                    foreach (GUIStyleState state in new[]
                             {
                                 dimFoldout.normal, dimFoldout.onNormal, dimFoldout.hover, dimFoldout.onHover,
                                 dimFoldout.focused, dimFoldout.onFocused, dimFoldout.active, dimFoldout.onActive,
                             })
                    {
                        Color color = state.textColor;
                        color.a *= 0.45f;
                        state.textColor = color;
                    }
                }

                return dimFoldout;
            }
        }

        /// <summary>Tracks vertical layout. Run once to measure, once to draw.</summary>
        private struct Layout
        {
            public Rect Area;
            public float Used;
            public bool Draw;

            public Rect Line()
            {
                Rect r = new Rect(Area.x, Area.y + Used, Area.width, EditorGUIUtility.singleLineHeight);
                Used += EditorGUIUtility.singleLineHeight + Pad;
                return r;
            }
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            Layout layout = new Layout { Area = new Rect(0f, 0f, 100f, 0f), Draw = false };
            Render(ref layout, property);
            return layout.Used;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            DrawBand(position, property);

            EditorGUI.BeginProperty(position, label, property);

            Layout layout = new Layout { Area = position, Draw = true };
            Render(ref layout, property);

            EditorGUI.EndProperty();
        }

        // Faint enough to leave the list's own selection highlight readable through it.
        private static readonly Color BandDark = new Color(1f, 1f, 1f, 0.06f);
        private static readonly Color BandLight = new Color(0f, 0f, 0f, 0.06f);

        /// <summary>
        /// Shades every other step, like spreadsheet rows, so where one expanded step ends and the
        /// next begins can be seen at a glance. Keyed off the element's index in its list, which is
        /// the last [n] of its property path, so it survives reordering and needs no state.
        /// </summary>
        private static void DrawBand(Rect position, SerializedProperty property)
        {
            if (Event.current.type != EventType.Repaint) return;

            string path = property.propertyPath;
            int open = path.LastIndexOf('[');
            int index;

            if (open < 0 || !int.TryParse(path.Substring(open + 1, path.Length - open - 2), out index)) return;
            if (index % 2 == 0) return;

            EditorGUI.DrawRect(
                new Rect(position.x - BandLeft, position.y - BandTop, position.width + BandLeft + BandRight, position.height),
                EditorGUIUtility.isProSkin ? BandDark : BandLight);
        }

        // How far the band reaches past the rect the list hands this drawer, so it fills exactly the
        // row the list's own selection highlight does. From ReorderableList's source (Unity 6000.3):
        // the list keeps its drag handle (20) and a foldout margin (8) to the left of the rect and its
        // padding (6) to the right, and it hands over the row's FULL height - the step's own height
        // plus its 2-point element padding - moved down by half that padding. So the band starts 1
        // point above the rect and is exactly as tall as it; any taller runs into the next row, which
        // an earlier guess of 2 above and 4 below visibly did.
        private const float BandLeft = 28f;
        private const float BandRight = 6f;
        private const float BandTop = 1f;

        private void Render(ref Layout layout, SerializedProperty property)
        {
            SerializedProperty type = property.FindPropertyRelative("Type");
            SerializedProperty start = property.FindPropertyRelative("Start");

            // intValue, not enumValueIndex: the index is a position in the declaration, and only the
            // number is the contract. They happen to agree today; nothing guarantees they keep to.
            var stepType = (UIAnimationStepType)type.intValue;
            var startMode = (UIAnimationStartMode)start.intValue;

            // Only looked for when it will be shown: always on a drawn header, and on the measuring
            // pass only when the step is open and the warning row takes up height.
            string problem = layout.Draw || property.isExpanded
                ? UIAnimationTargets.ProblemOf(property, stepType, UIAnimationTargets.OwnerOf(property.serializedObject))
                : null;

            // A step that will do nothing. Every problem is one, except a sound whose Target Path
            // misses - that still plays, on the shared source, so it is warned about but left alone.
            bool inert = problem != null
                && (stepType != UIAnimationStepType.PlaySound || property.FindPropertyRelative("Clip").objectReferenceValue == null);

            Rect header = layout.Line();
            if (layout.Draw)
            {
                float width = EditorGUI.IndentedRect(header).width - (problem != null ? IconWidth : 0f);
                GUIContent summary = FittedSummary(property, stepType, startMode, width);

                property.isExpanded = EditorGUI.Foldout(header, property.isExpanded, summary, true,
                    inert ? DimFoldout : BoldFoldout);

                // A collapsed step still says something is wrong with it: a warning icon at the end of
                // the row, where it lines up down the list. Drawn separately because the foldout style
                // does not draw a content image.
                if (problem != null)
                {
                    var iconRect = new Rect(header.xMax - IconWidth + 2f, header.y + 1f, 16f, 16f);
                    GUI.Label(iconRect, new GUIContent(WarningIcon, problem), GUIStyle.none);
                }
            }

            if (!property.isExpanded) return;

            layout.Area = new Rect(layout.Area.x + 12f, layout.Area.y, layout.Area.width - 12f, layout.Area.height);

            Rect typeRow = layout.Line();
            if (layout.Draw) DrawTypePopup(typeRow, type);

            Rect startRow = layout.Line();
            if (layout.Draw) DrawStartButton(startRow, start);

            DrawTarget(ref layout, property, stepType);

            if (problem != null) DrawProblem(ref layout, problem);

            // Everything that only matters once the step can reach what it drives is greyed out while
            // it cannot, so a dead step reads as dead rather than as a normal one with a warning on it.
            // What fixes it stays live: Type, Start, the target and Target Path above, and the Clip or
            // Shader Property below, which are drawn before the greyed part starts. DisabledScopes
            // nest by AND-ing, so nothing inside one could be switched back on.
            if (stepType == UIAnimationStepType.SetActive)
            {
                using (new EditorGUI.DisabledScope(inert))
                {
                    Field(ref layout, property.FindPropertyRelative("ActiveValue"), "Set Active To");
                    Field(ref layout, property.FindPropertyRelative("Delay"));
                }

                return;
            }

            if (stepType == UIAnimationStepType.PlaySound)
            {
                Field(ref layout, property.FindPropertyRelative("Clip"));

                using (new EditorGUI.DisabledScope(inert))
                {
                    Field(ref layout, property.FindPropertyRelative("Volume"));
                    Field(ref layout, property.FindPropertyRelative("Pitch"));
                    Field(ref layout, property.FindPropertyRelative("PitchVariation"), "Pitch Variation");
                    Field(ref layout, property.FindPropertyRelative("Delay"));
                }

                return;
            }

            if (stepType == UIAnimationStepType.MaterialFloat || stepType == UIAnimationStepType.MaterialColor)
            {
                Field(ref layout, property.FindPropertyRelative("ShaderProperty"));
            }

            using (new EditorGUI.DisabledScope(inert))
            {
                RenderTween(ref layout, property, stepType);
            }
        }

        /// <summary>Timing, ease, endpoints and the rest - everything a tweening step has below its target.</summary>
        private void RenderTween(ref Layout layout, SerializedProperty property, UIAnimationStepType stepType)
        {
            bool impulse = UIAnimationStep.IsImpulse(stepType);

            Field(ref layout, property.FindPropertyRelative("Duration"));
            Field(ref layout, property.FindPropertyRelative("Delay"));

            if (!impulse)
            {
                // Custom Curve is the first entry in the Ease dropdown rather than a checkbox of its
                // own, so a step on a preset is one row. The curve gets a row only once chosen.
                SerializedProperty useCurve = property.FindPropertyRelative("UseCustomCurve");

                Rect easeRow = layout.Line();
                if (layout.Draw) DrawEasePopup(easeRow, property.FindPropertyRelative("EaseType"), useCurve);

                if (useCurve.boolValue || useCurve.hasMultipleDifferentValues)
                {
                    Field(ref layout, property.FindPropertyRelative("Curve"));
                }
            }

            if (impulse)
            {
                DrawImpulseFields(ref layout, property, stepType);
                return;
            }

            // Use From is a FROM / TO button on the first endpoint row rather than a checkbox row of
            // its own - the way DOTween's own animation component does it - so a step without a
            // From is one row, not two. "Current" on the TO side means DOTween relative, which is
            // contradictory with an explicit FROM value, so it is only offered when there is none.
            // On the FROM side it never meant anything useful - see DrawEndpoint.
            UIAnimationValueKind kind = UIAnimationStep.ValueKindOf(stepType);
            SerializedProperty useFrom = property.FindPropertyRelative("UseFrom");

            if (useFrom.boolValue)
            {
                DrawEndpoint(ref layout, property, "From", "FromMode", kind, stepType, false, useFrom);
                DrawEndpoint(ref layout, property, "To", "ToMode", kind, stepType, false, null);
            }
            else
            {
                DrawEndpoint(ref layout, property, "To", "ToMode", kind, stepType, true, useFrom);
            }

            if (UIAnimationStep.SupportsPath(stepType)) DrawPath(ref layout, property, stepType);

            DrawStepSnapping(ref layout, property, stepType);
        }

        /// <summary>
        /// A step's own Snapping box. Snapping is normally set once on the animation, so the box only
        /// appears when that animation has Snap Per Step on - or when it is already ticked, because a
        /// ticked box always snaps and a setting that changes playback must never be invisible. That
        /// second case is also every step authored before the animation-wide box existed.
        /// </summary>
        private void DrawStepSnapping(ref Layout layout, SerializedProperty property, UIAnimationStepType stepType)
        {
            if (!UIAnimationStep.SupportsSnapping(stepType)) return;

            SerializedProperty snapping = property.FindPropertyRelative("Snapping");
            SerializedProperty perStep = AnimationField(property, "SnapPerStep");

            // Not inside an animation's step list, so there is no animation-wide box to defer to.
            bool shown = perStep == null || perStep.boolValue || snapping.boolValue || snapping.hasMultipleDifferentValues;
            if (shown) Field(ref layout, snapping);
        }

        /// <summary>
        /// A field on the UIAnimation this step belongs to, found by cutting the step's own index off
        /// its property path. Null when the step is not in an animation's Steps list.
        /// </summary>
        private static SerializedProperty AnimationField(SerializedProperty step, string fieldName)
        {
            const string marker = ".Steps.Array.data[";

            string path = step.propertyPath;
            int cut = path.LastIndexOf(marker, System.StringComparison.Ordinal);

            return cut < 0 ? null : step.serializedObject.FindProperty(path.Substring(0, cut) + "." + fieldName);
        }

        /// <summary>
        /// The movement path: one dropdown, and everything else only once it is on, because a path
        /// is the exception rather than the rule and should cost nothing to read past.
        ///
        /// Points are drawn as rows of their own rather than as Unity's list control, so they line
        /// up under From/To, show X/Y on the rect views, and can say which space they are in - the
        /// mode column shows To's mode, since that is the mode the points follow.
        /// </summary>
        private void DrawPath(ref Layout layout, SerializedProperty property, UIAnimationStepType stepType)
        {
            SerializedProperty usePath = property.FindPropertyRelative("UseCustomPath");

            Rect pathRow = layout.Line();
            if (layout.Draw) DrawPathPopup(pathRow, usePath, property.FindPropertyRelative("PathShape"));

            if (!usePath.boolValue) return;

            SerializedProperty points = property.FindPropertyRelative("Waypoints");
            SerializedProperty toMode = property.FindPropertyRelative("ToMode");
            bool twoD = UIAnimationStep.IsTwoDimensional(stepType);

            if (points.arraySize == 0)
            {
                Rect empty = layout.Line();
                if (layout.Draw)
                {
                    var noteRect = new Rect(empty.x + LabelWidth, empty.y, Mathf.Max(40f, empty.width - LabelWidth), empty.height);
                    EditorGUI.LabelField(noteRect, "No points yet - the step still moves in a straight line.",
                        EditorStyles.miniLabel);
                }
            }

            for (int i = 0; i < points.arraySize; i++)
            {
                Rect r = layout.Line();
                if (!layout.Draw) continue;

                var labelRect = new Rect(r.x, r.y, LabelWidth, r.height);
                var modeRect = new Rect(r.x + LabelWidth, r.y, ModeWidth, r.height);
                var removeRect = new Rect(r.xMax - RemoveWidth, r.y, RemoveWidth, r.height);
                var valueRect = new Rect(modeRect.xMax + 4f, r.y,
                    Mathf.Max(40f, removeRect.x - 4f - (modeRect.xMax + 4f)), r.height);

                EditorGUI.LabelField(labelRect, new GUIContent("Point " + (i + 1), points.tooltip));

                string mode = ModeNames[Mathf.Clamp(toMode.intValue, 0, ModeNames.Length - 1)];
                EditorGUI.LabelField(modeRect, new GUIContent(mode, "Points follow To's mode."), EditorStyles.miniLabel);

                SerializedProperty point = points.GetArrayElementAtIndex(i);
                DrawVector(valueRect, point, twoD);

                if (GUI.Button(removeRect, new GUIContent("-", "Remove this point."), EditorStyles.miniButton))
                {
                    points.DeleteArrayElementAtIndex(i);
                    break;
                }
            }

            Rect buttons = layout.Line();
            if (!layout.Draw) return;

            float x = buttons.x + LabelWidth;
            float width = buttons.xMax - x;
            var addRect = new Rect(x, buttons.y, Mathf.Floor(width * 0.4f) - 2f, buttons.height);
            var editRect = new Rect(addRect.xMax + 4f, buttons.y, buttons.xMax - addRect.xMax - 4f, buttons.height);

            if (GUI.Button(addRect, new GUIContent("Add Point",
                    "Adds a point halfway between the last one and To. Drag it into place in the Scene view, " +
                    "or type it in."), EditorStyles.miniButton))
            {
                int index = points.arraySize;
                Vector3 suggested = UIAnimationPathEditor.SuggestNewPoint(property, stepType);

                points.arraySize = index + 1;
                points.GetArrayElementAtIndex(index).vector3Value = suggested;
            }

            string reason;
            bool canEdit = UIAnimationPathEditor.CanEdit(property, stepType, out reason);
            bool editing = UIAnimationPathEditor.IsEditing(property);

            using (new EditorGUI.DisabledScope(!canEdit))
            {
                var content = new GUIContent(editing ? "Done Editing" : "Edit Path in Scene",
                    canEdit
                        ? "Shows this path in the Scene view with handles you can drag. Click a + to add a point, " +
                          "Ctrl+click a point to remove it, Esc to finish."
                        : reason);

                bool toggled = GUI.Toggle(editRect, editing, content, EditorStyles.miniButton);

                if (toggled != editing)
                {
                    if (toggled) UIAnimationPathEditor.Begin(property);
                    else UIAnimationPathEditor.Stop();
                }
            }
        }

        private const float RemoveWidth = 20f;

        private static readonly GUIContent[] PathOptions =
        {
            new GUIContent("Disabled"), new GUIContent("Curved"), new GUIContent("Linear"),
        };

        private const string PathTooltip =
            "Disabled = a straight line from where the step starts to To.\n" +
            "Curved = a smooth curve through the points below, then To.\n" +
            "Linear = straight lines between the points, with a sharp corner at each one.\n\n" +
            "The ease still applies - it controls how far along the path the step is, so an Out ease " +
            "decelerates into To along the curve.";

        /// <summary>
        /// The Custom Path row: Disabled, or the shape to follow. Two stored fields underneath, exactly
        /// as before - UseCustomPath and PathShape - so choosing Disabled leaves the shape and the
        /// points alone for when the path is turned back on.
        ///
        /// The row belongs to whichever field decides what it shows, the bool while the path is off or
        /// when that is what a prefab instance overrides, so bold and Revert follow what you can see.
        /// </summary>
        private static void DrawPathPopup(Rect rect, SerializedProperty usePath, SerializedProperty shape)
        {
            SerializedProperty bound = !usePath.boolValue || usePath.prefabOverride ? usePath : shape;

            GUIContent label = EditorGUI.BeginProperty(rect, new GUIContent("Custom Path", PathTooltip), bound);

            bool mixed = usePath.hasMultipleDifferentValues || (usePath.boolValue && shape.hasMultipleDifferentValues);
            int shown = usePath.boolValue ? 1 + Mathf.Clamp(shape.intValue, 0, PathOptions.Length - 2) : 0;

            EditorGUI.showMixedValue = mixed;
            EditorGUI.BeginChangeCheck();
            int chosen = EditorGUI.Popup(rect, label, shown, PathOptions);

            if (EditorGUI.EndChangeCheck())
            {
                usePath.boolValue = chosen > 0;
                if (chosen > 0) shape.intValue = chosen - 1;
            }

            EditorGUI.showMixedValue = false;
            EditorGUI.EndProperty();
        }

        private static void DrawVector(Rect rect, SerializedProperty value, bool twoDimensional)
        {
            if (twoDimensional)
            {
                Vector3 current = value.vector3Value;

                EditorGUI.BeginChangeCheck();
                Vector2 edited = EditorGUI.Vector2Field(rect, GUIContent.none, current);
                if (EditorGUI.EndChangeCheck()) value.vector3Value = new Vector3(edited.x, edited.y, current.z);
            }
            else
            {
                EditorGUI.PropertyField(rect, value, GUIContent.none);
            }
        }

        /// <summary>
        /// The target slot, and the Target Path beside it.
        ///
        /// On a UIAnimationAsset the slot is drawn DISABLED rather than hidden: a ScriptableObject
        /// cannot hold a scene reference, so a slot you could fill would serialize to null at the
        /// next save with nothing said about it. Showing it greyed out with the reason underneath
        /// says what the alternative is; hiding it would just look like a missing feature.
        /// </summary>
        private void DrawTarget(ref Layout layout, SerializedProperty property, UIAnimationStepType stepType)
        {
            string field;
            string label;

            switch (UIAnimationStep.TargetKindOf(stepType))
            {
                case UIAnimationTargetKind.CanvasGroup:
                    field = "CanvasGroupTarget";
                    label = "Canvas Group";
                    break;

                case UIAnimationTargetKind.Graphic:
                    field = "GraphicTarget";
                    label = "Graphic";
                    break;

                case UIAnimationTargetKind.Material:
                    field = "MaterialTarget";
                    label = "Material Inst.";
                    break;

                case UIAnimationTargetKind.GameObject:
                    field = "ActiveTarget";
                    label = "Game Object";
                    break;

                case UIAnimationTargetKind.Audio:
                    field = "AudioSourceTarget";
                    label = "Audio Source";
                    break;

                default:
                    field = "RectTarget";
                    label = "Rect Transform";
                    break;
            }

            SerializedProperty target = property.FindPropertyRelative(field);

            // Scoped by target object, not by type name: an asset is the only context where a
            // direct reference cannot survive being saved.
            bool inAsset = property.serializedObject.targetObject is UIAnimationAsset;

            Rect slot = layout.Line();
            if (layout.Draw)
            {
                using (new EditorGUI.DisabledScope(inAsset))
                {
                    EditorGUI.PropertyField(slot, target,
                        new GUIContent(label, inAsset ? AssetTargetReason : target.tooltip));
                }
            }

            if (inAsset)
            {
                Rect note = layout.Line();
                if (layout.Draw)
                {
                    float indent = EditorGUIUtility.labelWidth;
                    var noteRect = new Rect(note.x + indent, note.y, Mathf.Max(40f, note.width - indent), note.height);
                    EditorGUI.LabelField(noteRect, "Shared asset: no scene reference. Use Target Path.",
                        EditorStyles.miniLabel);
                }
            }

            // A filled slot wins outright and the path is not even looked up, so a path sitting
            // under one is dead text. Greyed out, with the tooltip saying why, rather than left
            // looking live - that is how it reads as "a direct reference beats a path".
            SerializedProperty path = property.FindPropertyRelative("TargetPath");
            bool overridden = !inAsset && target.objectReferenceValue != null;

            Rect pathRow = layout.Line();
            if (layout.Draw)
            {
                using (new EditorGUI.DisabledScope(overridden))
                {
                    EditorGUI.PropertyField(pathRow, path, new GUIContent("Target Path",
                        overridden
                            ? "IGNORED WHILE THE SLOT ABOVE IS FILLED - a direct reference always wins. " +
                              "Clear the slot to use a path instead.\n\n" + path.tooltip
                            : path.tooltip));
                }
            }
        }

        private const float IconWidth = 18f;

        private static readonly Color ProblemTint = new Color(1f, 0.72f, 0.1f, 0.13f);

        private static GUIStyle problemStyle;

        private static Texture WarningIcon
        {
            get { return EditorGUIUtility.FindTexture("console.warnicon.sml"); }
        }

        /// <summary>
        /// One warning row under the target, for a step that will do nothing as authored - the Type
        /// asks for a component the target does not have, a Target Path finds nothing, and so on. One
        /// line rather than a help box, because GetPropertyHeight is not told the width it would wrap
        /// to; the full text is the tooltip.
        /// </summary>
        private static void DrawProblem(ref Layout layout, string problem)
        {
            Rect r = layout.Line();
            if (!layout.Draw) return;

            if (problemStyle == null)
            {
                problemStyle = new GUIStyle(EditorStyles.label) { clipping = TextClipping.Clip, wordWrap = false };
            }

            EditorGUI.DrawRect(r, ProblemTint);

            // The icon gets a rect of its own: a label drops its image once the text overflows.
            GUI.Label(new Rect(r.x + 2f, r.y + 1f, 16f, 16f), new GUIContent(WarningIcon, problem), GUIStyle.none);
            EditorGUI.LabelField(new Rect(r.x + IconWidth + 2f, r.y, Mathf.Max(0f, r.width - IconWidth - 2f), r.height),
                new GUIContent(problem, problem), problemStyle);
        }

        private void DrawImpulseFields(ref Layout layout, SerializedProperty property, UIAnimationStepType stepType)
        {
            switch (stepType)
            {
                case UIAnimationStepType.ShakeAnchoredPosition:
                    Field(ref layout, property.FindPropertyRelative("ToFloat"), "Strength",
                        "How far the shake moves, in UI units.");
                    Field(ref layout, property.FindPropertyRelative("Vibrato"));
                    Field(ref layout, property.FindPropertyRelative("Randomness"));
                    break;

                case UIAnimationStepType.ShakeRotation:
                    Field(ref layout, property.FindPropertyRelative("ToVector"), "Strength",
                        "The most the shake turns on each axis, in degrees. Leave X and Y at 0 on UI - " +
                        "turning around those tips the element over in 3D. Z alone is the usual shake.");
                    Field(ref layout, property.FindPropertyRelative("Vibrato"));
                    Field(ref layout, property.FindPropertyRelative("Randomness"));
                    break;

                case UIAnimationStepType.ShakeScale:
                    Field(ref layout, property.FindPropertyRelative("ToVector"), "Strength",
                        "The most the shake changes the scale on each axis. 0.2 on X and Y is a clear wobble; " +
                        "Z does nothing visible on UI.");
                    Field(ref layout, property.FindPropertyRelative("Vibrato"));
                    Field(ref layout, property.FindPropertyRelative("Randomness"));
                    break;

                default:
                    Field(ref layout, property.FindPropertyRelative("ToVector"), "Punch", PunchTooltip(stepType));
                    Field(ref layout, property.FindPropertyRelative("Vibrato"));
                    Field(ref layout, property.FindPropertyRelative("Elasticity"));
                    break;
            }

            DrawStepSnapping(ref layout, property, stepType);
        }

        private static string PunchTooltip(UIAnimationStepType stepType)
        {
            switch (stepType)
            {
                case UIAnimationStepType.PunchRotation:
                    return "How far the punch turns on each axis, in degrees, before springing back. " +
                           "Use Z alone on UI - X and Y tip the element over in 3D.";

                case UIAnimationStepType.PunchScale:
                    return "How much scale the punch adds on each axis before springing back. 0.2 on X and Y " +
                           "is a firm press.";

                default:
                    return "How far the punch moves on each axis, in UI units, before springing back.";
            }
        }

        private const string ToTooltip =
            "Where this step ends. How the number is read depends on the dropdown beside it - hover that " +
            "for what each option means.";

        /// <summary>
        /// One endpoint row: label, mode, value. When fromToggle is passed, the label is the FROM / TO
        /// button that switches Use From, and it reads as whichever endpoint the row is showing.
        /// </summary>
        private void DrawEndpoint(ref Layout layout, SerializedProperty property, string label, string modeField,
            UIAnimationValueKind kind, UIAnimationStepType stepType, bool allowCurrent, SerializedProperty fromToggle)
        {
            Rect r = layout.Line();
            if (!layout.Draw) return;

            SerializedProperty mode = property.FindPropertyRelative(modeField);
            SerializedProperty value = property.FindPropertyRelative(ValueFieldName(label, kind));

            var labelRect = new Rect(r.x, r.y, LabelWidth, r.height);
            var modeRect = new Rect(r.x + LabelWidth, r.y, ModeWidth, r.height);
            var useRect = new Rect(r.xMax - UseWidth, r.y, UseWidth, r.height);
            var valueRect = new Rect(modeRect.xMax + 4f, r.y,
                Mathf.Max(40f, useRect.x - 2f - (modeRect.xMax + 4f)), r.height);

            if (fromToggle != null)
            {
                DrawFromToButton(new Rect(labelRect.x, labelRect.y, labelRect.width - 6f, labelRect.height), fromToggle);
            }
            else
            {
                EditorGUI.LabelField(labelRect, new GUIContent(label, ToTooltip));
            }

            // Current on a FROM row makes the step ignore its From value and start from wherever it
            // already is - the same as the button reading TO, and not the offset the word promises. It
            // is not offered, but a step that already stores it keeps seeing it, so nothing is rewritten.
            bool isFrom = label == "From";
            bool legacyCurrent = isFrom && !mode.hasMultipleDifferentValues
                && mode.intValue == (int)UIAnimationEndpointMode.Current;

            DrawModePopup(modeRect, mode, allowCurrent || legacyCurrent, ModeTooltip(isFrom, allowCurrent, legacyCurrent));

            if (kind == UIAnimationValueKind.Vector)
            {
                DrawVector(valueRect, value, UIAnimationStep.IsTwoDimensional(stepType));
            }
            else
            {
                EditorGUI.PropertyField(valueRect, value, GUIContent.none);
            }

            DrawUseCurrent(useRect, property, stepType, mode, value, isFrom);
        }

        // ---------------------------------------------------------------- Use Current

        private const float UseWidth = 20f;

        private static GUIStyle useStyle;

        private const string UseTooltip =
            "Use Current Value: copies what the target holds right now into this field.\n\n" +
            "To pose it: press Reset (or Play) above, then move, resize or recolour the object and click " +
            "this. Selecting the object to pose it keeps the preview running - come back here to click. " +
            "Stop and Restore puts the object back afterwards, and the value you copied stays.";

        private const string NeedsPreview =
            "\n\nOutside a preview, wherever the object sits IS its resting value, so this would always " +
            "store 0. Press Reset (or Play) above first, change the object - selecting it to do that keeps " +
            "the preview running - then come back and click. Stop and Restore puts it back.";

        /// <summary>
        /// The record button at the end of a From or To row. It reads the step's target and writes what it
        /// holds into the row, converted to the row's mode - as typed for Absolute, the difference from
        /// the resting value for Baseline, the difference from where the step starts for Current - so
        /// the step then lands exactly where the object was.
        ///
        /// Baseline and Current need to know the resting value, and out of a preview the resting value
        /// is simply where the object is, which would make every reading 0. They are enabled only while
        /// a preview runs, where the rest was captured before anything moved - which is also what lets
        /// Stop and Restore undo the posing afterwards.
        /// </summary>
        private static void DrawUseCurrent(Rect rect, SerializedProperty step, UIAnimationStepType type,
            SerializedProperty mode, SerializedProperty value, bool isFrom)
        {
            if (useStyle == null)
            {
                useStyle = new GUIStyle(EditorStyles.miniButton) { padding = new RectOffset(1, 1, 1, 1) };
            }

            Vector4 reading = Vector4.zero;
            string reason;
            bool available;

            if (mode.hasMultipleDifferentValues)
            {
                available = false;
                reason = "The selected steps use different modes.";
            }
            else
            {
                available = TryReadCurrent(step, type, (UIAnimationEndpointMode)mode.intValue, isFrom, out reading, out reason);
            }

            // Unity's own record dot: this captures a pose the way keying does in the Animation window,
            // and it cannot be mistaken for the screen-colour eyedropper a colour field already has.
            Texture icon = EditorGUIUtility.FindTexture(EditorGUIUtility.isProSkin ? "d_Animation.Record" : "Animation.Record");

            using (new EditorGUI.DisabledScope(!available))
            {
                if (GUI.Button(rect, new GUIContent(icon), useStyle)) WriteReading(value, type, reading);
            }

            // Over the button rather than on it, so the reason still shows while it is disabled.
            GUI.Label(rect, new GUIContent(string.Empty, available ? UseTooltip + ModeLine(mode) : reason), GUIStyle.none);
        }

        private static string ModeLine(SerializedProperty mode)
        {
            switch ((UIAnimationEndpointMode)mode.intValue)
            {
                case UIAnimationEndpointMode.Baseline: return "\n\nBaseline: stored as the difference from the resting value.";
                case UIAnimationEndpointMode.Current: return "\n\nCurrent: stored as the difference from where the step starts.";
                default: return "\n\nAbsolute: copied as it is.";
            }
        }

        /// <summary>What the button would write into this row, or false and why not.</summary>
        private static bool TryReadCurrent(SerializedProperty step, UIAnimationStepType type, UIAnimationEndpointMode mode,
            bool isFrom, out Vector4 reading, out string reason)
        {
            reading = Vector4.zero;
            SerializedObject serialized = step.serializedObject;

            UIAnimationPlayer owner = UIAnimationTargets.OwnerOf(serialized);
            if (owner == null)
            {
                reason = serialized.isEditingMultipleObjects
                    ? "Select a single object to copy values from its scene."
                    : "A shared animation set has no scene to read from. Set Preview On below, and this reads " +
                      "from that player's objects.";
                return false;
            }

            if (isFrom && mode == UIAnimationEndpointMode.Current)
            {
                reason = "This From is set to Current, which ignores the value here.";
                return false;
            }

            string problem;
            Object target = UIAnimationTargets.Resolve(type,
                step.FindPropertyRelative(UIAnimationTargets.SlotField(type)).objectReferenceValue,
                step.FindPropertyRelative("TargetPath").stringValue, owner.gameObject, out problem);

            Vector4 live;
            if (target == null || !ReadLive(type, target, step, out live))
            {
                reason = problem ?? "This step has nothing to read a value from.";
                return false;
            }

            // A Current To only means "an offset from the start" on a step with no From; with one,
            // playback reads it as the number typed, exactly like Absolute.
            bool relative = mode == UIAnimationEndpointMode.Current && !step.FindPropertyRelative("UseFrom").boolValue;

            if (mode == UIAnimationEndpointMode.Absolute || (mode == UIAnimationEndpointMode.Current && !relative))
            {
                reading = live;
                reason = null;
                return true;
            }

            if (!UIAnimationPreview.IsPreviewing(owner))
            {
                reason = (relative
                    ? "Current stores the difference from where the step starts."
                    : "Baseline stores the difference from the resting value.") + NeedsPreview;
                return false;
            }

            Vector4 origin;
            if (relative ? !TryReadStart(step, type, owner, out origin) : !TryReadRest(type, target, out origin))
            {
                reason = (relative ? "Current" : "Baseline") + " cannot be read back for this type of step. " +
                         "Switch the dropdown to Absolute to copy the value as it is.";
                return false;
            }

            reading = live - origin;
            if (type == UIAnimationStepType.Rotation) reading = WrapAngles(reading);

            reason = null;
            return true;
        }

        /// <summary>The value the target holds right now, in the step's own terms.</summary>
        private static bool ReadLive(UIAnimationStepType type, Object target, SerializedProperty step, out Vector4 value)
        {
            value = Vector4.zero;

            var rect = target as RectTransform;
            if (rect != null && UIAnimationRectState.Covers(type))
            {
                Vector3 read = UIAnimationRectState.Of(rect).Read(type);

                // Rotation reads back as 0 to 360, so -10 would come out as 350 - and a Rotation step
                // turns the long way round to 350. Nearest to zero is what was almost always meant.
                value = type == UIAnimationStepType.Rotation ? WrapAngles(read) : (Vector4)read;
                return true;
            }

            var group = target as CanvasGroup;
            if (group != null && type == UIAnimationStepType.CanvasGroupAlpha)
            {
                value = new Vector4(group.alpha, 0f, 0f, 0f);
                return true;
            }

            var graphic = target as UnityEngine.UI.Graphic;
            if (graphic != null && type == UIAnimationStepType.GraphicColor)
            {
                value = graphic.color;
                return true;
            }

            if (graphic != null && type == UIAnimationStepType.GraphicAlpha)
            {
                value = new Vector4(graphic.color.a, 0f, 0f, 0f);
                return true;
            }

            var instance = target as UIMaterialInstance;
            if (instance != null)
            {
                Material material = UIAnimationTargets.SourceMaterialOf(instance);
                string property = step.FindPropertyRelative("ShaderProperty").stringValue;
                if (material == null || string.IsNullOrEmpty(property) || !material.HasProperty(property)) return false;

                if (type == UIAnimationStepType.MaterialFloat)
                {
                    value = new Vector4(material.GetFloat(property), 0f, 0f, 0f);
                    return true;
                }

                if (type == UIAnimationStepType.MaterialColor)
                {
                    value = material.GetColor(property);
                    return true;
                }
            }

            return false;
        }

        /// <summary>The resting value a Baseline is measured from, as the preview captured it.</summary>
        private static bool TryReadRest(UIAnimationStepType type, Object target, out Vector4 rest)
        {
            rest = Vector4.zero;

            var rect = target as RectTransform;
            if (rect != null && UIAnimationRectState.Covers(type))
            {
                rest = UIAnimationSimulation.RestOf(rect).Read(type);
                return true;
            }

            var group = target as CanvasGroup;
            if (group != null)
            {
                rest = new Vector4(UIAnimationSimulation.RestAlphaOf(group), 0f, 0f, 0f);
                return true;
            }

            var graphic = target as UnityEngine.UI.Graphic;
            if (graphic != null)
            {
                Color color = UIAnimationSimulation.RestColorOf(graphic);
                rest = type == UIAnimationStepType.GraphicAlpha ? new Vector4(color.a, 0f, 0f, 0f) : (Vector4)color;
                return true;
            }

            return false;
        }

        /// <summary>Where the step starts, worked out by replaying its animation from rest.</summary>
        private static bool TryReadStart(SerializedProperty step, UIAnimationStepType type, UIAnimationPlayer owner, out Vector4 start)
        {
            start = Vector4.zero;
            if (!UIAnimationRectState.Covers(type)) return false;

            int animationIndex;
            int stepIndex;
            List<UIAnimation> animations = UIAnimationTargets.AnimationsOf(step.serializedObject);

            if (animations == null
                || !UIAnimationTargets.TryParseStepPath(step.propertyPath, out animationIndex, out stepIndex)
                || animationIndex >= animations.Count)
            {
                return false;
            }

            Vector3 value;
            Vector3 rest;
            if (!UIAnimationSimulation.TryStartOf(animations[animationIndex], owner.gameObject, stepIndex, out value, out rest)) return false;

            start = value;
            return true;
        }

        private static Vector4 WrapAngles(Vector4 angles)
        {
            return new Vector4(Mathf.DeltaAngle(0f, angles.x), Mathf.DeltaAngle(0f, angles.y), Mathf.DeltaAngle(0f, angles.z), 0f);
        }

        private static void WriteReading(SerializedProperty value, UIAnimationStepType type, Vector4 reading)
        {
            switch (value.propertyType)
            {
                case SerializedPropertyType.Vector3:
                {
                    // Z is never shown on the rect views, so it is left as it was rather than zeroed.
                    Vector3 current = value.vector3Value;
                    value.vector3Value = UIAnimationStep.IsTwoDimensional(type)
                        ? new Vector3(reading.x, reading.y, current.z)
                        : new Vector3(reading.x, reading.y, reading.z);
                    break;
                }

                case SerializedPropertyType.Float:
                    value.floatValue = reading.x;
                    break;

                case SerializedPropertyType.Color:
                    value.colorValue = reading;
                    break;
            }
        }

        private static readonly string[] ModeNames = { "Absolute", "Baseline", "Current" };

        private const string AbsoluteLine = "Absolute = the value as typed.";

        private const string BaselineLine =
            "Baseline = the resting value (captured at Awake), plus the value as an offset. Use this to " +
            "land on the authored state.";

        /// <summary>
        /// What the options in one mode dropdown mean - only the options that dropdown actually offers,
        /// so the tooltip never describes a choice that is not in the list under it.
        /// </summary>
        private static string ModeTooltip(bool isFrom, bool offersCurrent, bool legacyCurrent)
        {
            string text = AbsoluteLine + "\n" + BaselineLine;

            if (isFrom)
            {
                if (legacyCurrent)
                {
                    text += "\nCurrent = start from wherever the property already is. The value typed here is " +
                            "IGNORED - the same as switching the button to TO. Shown only because this step " +
                            "already uses it.";
                }

                return text;
            }

            if (offersCurrent)
            {
                return text + "\nCurrent = where the property is when the step starts, plus the value as an offset.";
            }

            return text + "\n\nCurrent is only offered while the button reads TO: an offset from wherever the " +
                   "step starts cannot be combined with a From.";
        }

        /// <summary>
        /// The Absolute / Baseline / Current dropdown. Its tooltip is attached to the dropdown itself -
        /// hovering the row's label says what the row is, hovering this says what the options mean.
        ///
        /// A row that cannot be relative offers only the first two. A stored mode it does not offer
        /// shows as Baseline and is written back as Baseline, as it always has been; the write only
        /// happens when the value actually differs.
        /// </summary>
        private static void DrawModePopup(Rect rect, SerializedProperty mode, bool allowCurrent, string tooltip)
        {
            int count = allowCurrent ? ModeNames.Length : 2;

            var options = new GUIContent[count];
            for (int i = 0; i < count; i++) options[i] = new GUIContent(ModeNames[i]);

            EditorGUI.BeginProperty(rect, GUIContent.none, mode);

            bool mixed = mode.hasMultipleDifferentValues;
            int shown = Mathf.Clamp(mode.intValue, 0, count - 1);

            EditorGUI.showMixedValue = mixed;
            EditorGUI.BeginChangeCheck();
            int chosen = EditorGUI.Popup(rect, shown, options);
            bool changed = EditorGUI.EndChangeCheck();
            EditorGUI.showMixedValue = false;

            if (changed || (!mixed && chosen != mode.intValue)) mode.intValue = chosen;

            EditorGUI.EndProperty();

            // A label over the popup is what carries the tooltip: it takes no clicks, so the popup
            // underneath still opens.
            GUI.Label(rect, new GUIContent(string.Empty, tooltip), GUIStyle.none);
        }

        /// <summary>
        /// The FROM / TO switch for Use From. Wrapped in BeginProperty so it still shows a prefab
        /// override in bold and offers Revert on right-click, exactly as the checkbox did.
        /// </summary>
        private static void DrawFromToButton(Rect rect, SerializedProperty useFrom)
        {
            var content = new GUIContent(useFrom.boolValue ? "FROM" : "TO", useFrom.tooltip);

            EditorGUI.BeginProperty(rect, content, useFrom);

            if (GUI.Button(rect, content, EditorStyles.miniButton))
            {
                useFrom.boolValue = !useFrom.boolValue;
            }

            EditorGUI.EndProperty();
        }

        private static float startWidth;

        /// <summary>
        /// The Start row: one button that switches between AFTER PREVIOUS and WITH PREVIOUS, in the
        /// style of the FROM / TO button - two choices do not need a dropdown. It writes the same enum
        /// the dropdown did. Anything but AfterPrevious reads as WITH PREVIOUS, because that is how
        /// playback treats it.
        /// </summary>
        private static void DrawStartButton(Rect rect, SerializedProperty start)
        {
            if (startWidth <= 0f)
            {
                startWidth = Mathf.Max(EditorStyles.miniButton.CalcSize(new GUIContent(StartAfter)).x,
                    EditorStyles.miniButton.CalcSize(new GUIContent(StartWith)).x) + 8f;
            }

            string tooltip = start.tooltip + "\n\nClick to switch.";

            GUIContent label = EditorGUI.BeginProperty(rect, new GUIContent("Start", tooltip), start);
            Rect field = EditorGUI.PrefixLabel(rect, label);
            field.width = Mathf.Min(field.width, startWidth);

            bool mixed = start.hasMultipleDifferentValues;
            bool with = start.intValue != (int)UIAnimationStartMode.AfterPrevious;

            if (GUI.Button(field, new GUIContent(mixed ? MixedValue : with ? StartWith : StartAfter, tooltip),
                    EditorStyles.miniButton))
            {
                start.intValue = (int)(mixed || with ? UIAnimationStartMode.AfterPrevious : UIAnimationStartMode.WithPrevious);
            }

            EditorGUI.EndProperty();
        }

        private const string StartAfter = "AFTER PREVIOUS";
        private const string StartWith = "WITH PREVIOUS";

        private static string ValueFieldName(string label, UIAnimationValueKind kind)
        {
            string prefix = label == "From" ? "From" : "To";

            switch (kind)
            {
                case UIAnimationValueKind.Float: return prefix + "Float";
                case UIAnimationValueKind.Color: return prefix + "Color";
                default: return prefix + "Vector";
            }
        }

        // ---------------------------------------------------------------- Type and Ease dropdowns

        // Menu order. Shown as the section headers of the Type dropdown.
        private static readonly string[] CategoryNames = { "TRANSFORM", "PUNCH & SHAKE", "COLOR & FADE", "MATERIAL", "OTHER" };

        /// <summary>
        /// Which section of the Type dropdown a step type is listed under. Derived from what the step
        /// drives rather than listed by hand, so a new type lands in a sensible section on its own.
        /// </summary>
        private static int CategoryOf(UIAnimationStepType type)
        {
            if (UIAnimationStep.IsImpulse(type)) return 1;

            switch (UIAnimationStep.TargetKindOf(type))
            {
                case UIAnimationTargetKind.Rect: return 0;
                case UIAnimationTargetKind.CanvasGroup:
                case UIAnimationTargetKind.Graphic: return 2;
                case UIAnimationTargetKind.Material: return 3;
                default: return 4;
            }
        }

        private static List<UIAnimationStepType> sortedTypes;

        /// <summary>
        /// Every step type, by section, alphabetical within one. Built from the enum itself, so a new
        /// type turns up in the menu in the right place without this list being touched - and the
        /// enum's numbers, which are the serialized contract, never have to move to make it read well.
        /// </summary>
        private static List<UIAnimationStepType> SortedTypes
        {
            get
            {
                if (sortedTypes == null)
                {
                    sortedTypes = new List<UIAnimationStepType>(
                        (UIAnimationStepType[])System.Enum.GetValues(typeof(UIAnimationStepType)));

                    sortedTypes.Sort((a, b) =>
                    {
                        int byCategory = CategoryOf(a).CompareTo(CategoryOf(b));
                        return byCategory != 0 ? byCategory : string.CompareOrdinal(TypeName(a), TypeName(b));
                    });
                }

                return sortedTypes;
            }
        }

        private static string TypeName(UIAnimationStepType type)
        {
            return ObjectNames.NicifyVariableName(type.ToString());
        }

        private const string MixedValue = "—";

        /// <summary>
        /// The Type field, as a dropdown split into sections with a header and a divider each.
        ///
        /// A GenericMenu rather than Unity's enum popup, which can only list the enum in declaration
        /// order. It writes back through a copy of the property, because the menu calls back after
        /// this repaint - and the property handed to the drawer - is long gone.
        /// </summary>
        private static void DrawTypePopup(Rect rect, SerializedProperty type)
        {
            GUIContent label = EditorGUI.BeginProperty(rect, new GUIContent("Type", type.tooltip), type);
            Rect field = EditorGUI.PrefixLabel(rect, label);

            bool mixed = type.hasMultipleDifferentValues;
            var current = (UIAnimationStepType)type.intValue;

            if (EditorGUI.DropdownButton(field, new GUIContent(mixed ? MixedValue : TypeName(current), type.tooltip),
                    FocusType.Keyboard, EditorStyles.popup))
            {
                SerializedProperty target = type.Copy();
                var menu = new GenericMenu();
                int section = -1;

                for (int i = 0; i < SortedTypes.Count; i++)
                {
                    UIAnimationStepType value = SortedTypes[i];
                    int category = CategoryOf(value);

                    if (category != section)
                    {
                        if (section >= 0) menu.AddSeparator(string.Empty);
                        menu.AddDisabledItem(new GUIContent(CategoryNames[category]));
                        section = category;
                    }

                    menu.AddItem(new GUIContent(TypeName(value)), !mixed && value == current,
                        () => SetInt(target, (int)value));
                }

                menu.DropDown(field);
            }

            EditorGUI.EndProperty();
        }

        private const string CustomCurveName = "Custom Curve";

        private static List<Ease> easeValues;

        /// <summary>
        /// DOTween's presets, in DOTween's own order, minus the three that are not choices: Unset
        /// (which a new step is filled in from anyway) and the two INTERNAL_ values.
        /// </summary>
        private static List<Ease> EaseValues
        {
            get
            {
                if (easeValues == null)
                {
                    easeValues = new List<Ease>();

                    foreach (Ease ease in System.Enum.GetValues(typeof(Ease)))
                    {
                        if (ease == Ease.Unset || ease.ToString().StartsWith("INTERNAL", System.StringComparison.Ordinal)) continue;
                        easeValues.Add(ease);
                    }
                }

                return easeValues;
            }
        }

        private static string EaseName(Ease ease)
        {
            return ObjectNames.NicifyVariableName(ease.ToString());
        }

        /// <summary>
        /// The Ease field, with Custom Curve as the first choice instead of a checkbox of its own.
        ///
        /// Nothing about the saved data changed to make this happen: picking Custom Curve ticks the
        /// same UseCustomCurve bool the checkbox did, and picking a preset clears it. The preset
        /// underneath is left alone while a curve is in use, as it always was.
        ///
        /// The row belongs to whichever of the two fields it is showing - the bool while a curve is in
        /// use or when that is what a prefab instance overrides, the preset otherwise - so the bold
        /// override marker and right-click Revert follow what you can see.
        /// </summary>
        private static void DrawEasePopup(Rect rect, SerializedProperty ease, SerializedProperty useCurve)
        {
            SerializedProperty bound = useCurve.boolValue || useCurve.prefabOverride ? useCurve : ease;

            GUIContent label = EditorGUI.BeginProperty(rect, new GUIContent("Ease", ease.tooltip), bound);
            Rect field = EditorGUI.PrefixLabel(rect, label);

            bool mixed = ease.hasMultipleDifferentValues || useCurve.hasMultipleDifferentValues;
            bool curve = useCurve.boolValue;
            var current = (Ease)ease.intValue;

            string shown = mixed ? MixedValue : curve ? CustomCurveName : EaseName(current);

            if (EditorGUI.DropdownButton(field, new GUIContent(shown, ease.tooltip), FocusType.Keyboard, EditorStyles.popup))
            {
                SerializedProperty easeTarget = ease.Copy();
                SerializedProperty curveTarget = useCurve.Copy();
                var menu = new GenericMenu();

                menu.AddItem(new GUIContent(CustomCurveName), !mixed && curve,
                    () => SetEase(curveTarget, easeTarget, true, current));
                menu.AddSeparator(string.Empty);

                for (int i = 0; i < EaseValues.Count; i++)
                {
                    Ease value = EaseValues[i];
                    menu.AddItem(new GUIContent(EaseName(value)), !mixed && !curve && value == current,
                        () => SetEase(curveTarget, easeTarget, false, value));
                }

                menu.DropDown(field);
            }

            EditorGUI.EndProperty();
        }

        private static void SetEase(SerializedProperty useCurve, SerializedProperty ease, bool customCurve, Ease preset)
        {
            useCurve.serializedObject.Update();

            useCurve.boolValue = customCurve;
            if (!customCurve) ease.intValue = (int)preset;

            useCurve.serializedObject.ApplyModifiedProperties();
        }

        private static void SetInt(SerializedProperty property, int value)
        {
            property.serializedObject.Update();
            property.intValue = value;
            property.serializedObject.ApplyModifiedProperties();
        }

        private void Field(ref Layout layout, SerializedProperty property, string label = null, string tooltip = null)
        {
            Rect r = layout.Line();
            if (!layout.Draw || property == null) return;

            // Keep the field's [Tooltip] even when the drawer overrides its display name.
            if (label == null) EditorGUI.PropertyField(r, property);
            else EditorGUI.PropertyField(r, property, new GUIContent(label, tooltip ?? property.tooltip));
        }

        private const string Ellipsis = "…";

        /// <summary>
        /// The header, shortened with an ellipsis to fit the row instead of running off the side of
        /// the Inspector. The full text becomes the tooltip whenever anything was cut.
        ///
        /// A Target Path gives way first, from its START: a long header is nearly always a deep
        /// path, and the end of a path is the part that names the object. It drops whole segments
        /// while it can, then characters. Only when the path is down to a stub - or the target is
        /// a plain name - does the end of the header get cut.
        /// </summary>
        private static GUIContent FittedSummary(SerializedProperty property, UIAnimationStepType type,
            UIAnimationStartMode start, float width)
        {
            string target = TargetName(property, type);
            string full = Summary(property, type, start, target);

            if (Fits(full, width)) return new GUIContent(full);

            // Whole path segments first, longest first, so a cut lands on a '/' where it can:
            // "…/HoverHighlight" says far more than "…overHighlight".
            for (int slash = target.IndexOf('/'); slash >= 0; slash = target.IndexOf('/', slash + 1))
            {
                string candidate = Summary(property, type, start, Ellipsis + target.Substring(slash));
                if (Fits(candidate, width)) return new GUIContent(candidate, full);
            }

            const int MinTarget = 4;

            // Only a path gives way from the front. A plain object name reads from its start like
            // everything else, so it is left whole and the end of the header is cut instead.
            if (target.IndexOf('/') >= 0 && target.Length > MinTarget)
            {
                // Longest tail of the target that fits. Fitting is monotonic in length, so a binary
                // search keeps this to a handful of measurements per repaint.
                int low = MinTarget;
                int high = target.Length - 1;
                string best = null;

                while (low <= high)
                {
                    int keep = (low + high) / 2;
                    string candidate = Summary(property, type, start, Ellipsis + target.Substring(target.Length - keep));

                    if (Fits(candidate, width))
                    {
                        best = candidate;
                        low = keep + 1;
                    }
                    else
                    {
                        high = keep - 1;
                    }
                }

                if (best != null) return new GUIContent(best, full);

                target = Ellipsis + target.Substring(target.Length - MinTarget);
            }

            string stubbed = Summary(property, type, start, target);

            int lo = 1;
            int hi = stubbed.Length - 1;
            int length = 1;

            while (lo <= hi)
            {
                int mid = (lo + hi) / 2;

                if (Fits(stubbed.Substring(0, mid).TrimEnd() + Ellipsis, width))
                {
                    length = mid;
                    lo = mid + 1;
                }
                else
                {
                    hi = mid - 1;
                }
            }

            return new GUIContent(stubbed.Substring(0, length).TrimEnd() + Ellipsis, full);
        }

        private static bool Fits(string text, float width)
        {
            return BoldFoldout.CalcSize(new GUIContent(text)).x <= width;
        }

        /// <summary>
        /// The one-line header shown when a step is collapsed, which is how the list is read most
        /// of the time. Reads: AFTER   ButtonContainer (Anchored Position)   0.6s   Out Quart
        ///
        /// The object name comes first because a long animation is scanned by "which element does
        /// this row move", and the property is named rather than the component type so that two
        /// Rect steps on the same object stay distinguishable while collapsed.
        /// </summary>
        private static string Summary(SerializedProperty property, UIAnimationStepType type, UIAnimationStartMode start,
            string target)
        {
            string prefix = start == UIAnimationStartMode.WithPrevious ? "WITH" : "AFTER";

            // The same prettified names the dropdowns below it use, so the header matches them.
            string typeName = TypeName(type);

            if (type == UIAnimationStepType.SetActive)
            {
                bool value = property.FindPropertyRelative("ActiveValue").boolValue;
                return prefix + "   " + target + " (" + typeName + " " + (value ? "on" : "off") + ")";
            }

            if (type == UIAnimationStepType.PlaySound)
            {
                Object clip = property.FindPropertyRelative("Clip").objectReferenceValue;
                return prefix + "   " + (clip != null ? clip.name : "(no clip)") + " (" + typeName + ")";
            }

            float duration = property.FindPropertyRelative("Duration").floatValue;
            string tail = duration.ToString("0.##") + "s";

            if (!UIAnimationStep.IsImpulse(type))
            {
                if (property.FindPropertyRelative("UseCustomCurve").boolValue)
                {
                    tail += "   Custom Curve";
                }
                else
                {
                    tail += "   " + EaseName((Ease)property.FindPropertyRelative("EaseType").intValue);
                }
            }

            // Only when the path actually takes effect, so the header never claims a path the
            // step is not following - an empty point list still moves in a straight line.
            if (UIAnimationStep.SupportsPath(type) && property.FindPropertyRelative("UseCustomPath").boolValue)
            {
                int count = property.FindPropertyRelative("Waypoints").arraySize;

                if (count > 0)
                {
                    SerializedProperty shape = property.FindPropertyRelative("PathShape");
                    string shapeName = ObjectNames.NicifyVariableName(((UIAnimationPathShape)shape.intValue).ToString());
                    tail += "   " + shapeName + " path, " + count + (count == 1 ? " point" : " points");
                }
            }

            return prefix + "   " + target + " (" + typeName + ")   " + tail;
        }

        private const string SelfName = "[self]";

        /// <summary>
        /// Name of the object this step drives, or [self] when that is the GameObject the player is
        /// on - an empty slot and path, or a slot pointing back at the player's own object. The
        /// player's name would only repeat what the Inspector already shows at the top, and [self] is
        /// spelled out rather than left blank so a self-targeting row does not read as a broken one.
        /// In an asset an empty slot means the same thing for whichever player uses it.
        /// </summary>
        private static string TargetName(SerializedProperty property, UIAnimationStepType type)
        {
            SerializedProperty target = property.FindPropertyRelative(UIAnimationTargets.SlotField(type));
            Object referenced = target != null ? target.objectReferenceValue : null;

            if (referenced != null)
            {
                var owner = property.serializedObject.targetObject as Component;
                var component = referenced as Component;
                GameObject referencedObject = component != null ? component.gameObject : referenced as GameObject;

                return owner != null && referencedObject == owner.gameObject ? SelfName : referenced.name;
            }

            // A path names the object just as well as a reference does, and reads better in a
            // header than the owner would - the whole point of the row is which object moves.
            SerializedProperty path = property.FindPropertyRelative("TargetPath");
            if (path != null && !string.IsNullOrEmpty(path.stringValue)) return path.stringValue;

            return SelfName;
        }
    }
}

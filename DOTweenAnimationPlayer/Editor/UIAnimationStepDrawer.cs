// -----------------------------------------------------------------------------
// UI Animation Utility
//
// AI-GENERATED. Authored by Claude (Anthropic) via Claude Code, September 2026,
// to a written design brief by the project author. Not hand-written by the
// Twindrill Goose team. See README.md in the folder above for usage.
// -----------------------------------------------------------------------------

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

        private static readonly string[] AbsoluteBaselineOnly = { "Absolute", "Baseline" };

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
            EditorGUI.BeginProperty(position, label, property);

            Layout layout = new Layout { Area = position, Draw = true };
            Render(ref layout, property);

            EditorGUI.EndProperty();
        }

        private void Render(ref Layout layout, SerializedProperty property)
        {
            SerializedProperty type = property.FindPropertyRelative("Type");
            SerializedProperty start = property.FindPropertyRelative("Start");

            var stepType = (UIAnimationStepType)type.enumValueIndex;
            var startMode = (UIAnimationStartMode)start.enumValueIndex;

            Rect header = layout.Line();
            if (layout.Draw)
            {
                property.isExpanded = EditorGUI.Foldout(header, property.isExpanded,
                    Summary(property, stepType, startMode), true, BoldFoldout);
            }

            if (!property.isExpanded) return;

            layout.Area = new Rect(layout.Area.x + 12f, layout.Area.y, layout.Area.width - 12f, layout.Area.height);

            Field(ref layout, type);
            Field(ref layout, start);

            DrawTarget(ref layout, property, stepType);

            bool impulse = UIAnimationStep.IsImpulse(stepType);

            if (stepType == UIAnimationStepType.SetActive)
            {
                Field(ref layout, property.FindPropertyRelative("ActiveValue"), "Set Active To");
                Field(ref layout, property.FindPropertyRelative("Delay"));
                return;
            }

            if (stepType == UIAnimationStepType.PlaySound)
            {
                Field(ref layout, property.FindPropertyRelative("Clip"));
                Field(ref layout, property.FindPropertyRelative("Volume"));
                Field(ref layout, property.FindPropertyRelative("Pitch"));
                Field(ref layout, property.FindPropertyRelative("PitchVariation"), "Pitch Variation");
                Field(ref layout, property.FindPropertyRelative("Delay"));
                return;
            }

            if (stepType == UIAnimationStepType.MaterialFloat || stepType == UIAnimationStepType.MaterialColor)
            {
                Field(ref layout, property.FindPropertyRelative("ShaderProperty"));
            }

            Field(ref layout, property.FindPropertyRelative("Duration"));
            Field(ref layout, property.FindPropertyRelative("Delay"));

            if (!impulse)
            {
                SerializedProperty useCurve = property.FindPropertyRelative("UseCustomCurve");
                Field(ref layout, useCurve, "Use Custom Curve");

                if (useCurve.boolValue) Field(ref layout, property.FindPropertyRelative("Curve"));
                else Field(ref layout, property.FindPropertyRelative("EaseType"), "Ease");
            }

            if (impulse)
            {
                DrawImpulseFields(ref layout, property, stepType);
                return;
            }

            UIAnimationValueKind kind = UIAnimationStep.ValueKindOf(stepType);
            SerializedProperty useFrom = property.FindPropertyRelative("UseFrom");
            Field(ref layout, useFrom);

            if (useFrom.boolValue)
            {
                DrawEndpoint(ref layout, property, "From", "FromMode", kind, stepType, true);
            }

            // "Current" on the TO side means DOTween relative, which is contradictory with an
            // explicit FROM value - so it is only offered when Use From is off.
            DrawEndpoint(ref layout, property, "To", "ToMode", kind, stepType, !useFrom.boolValue);

            if (UsesSnapping(stepType)) Field(ref layout, property.FindPropertyRelative("Snapping"));
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

            Field(ref layout, property.FindPropertyRelative("TargetPath"), "Target Path");
        }

        private void DrawImpulseFields(ref Layout layout, SerializedProperty property, UIAnimationStepType stepType)
        {
            if (stepType == UIAnimationStepType.ShakeAnchoredPosition)
            {
                Field(ref layout, property.FindPropertyRelative("ToFloat"), "Strength");
                Field(ref layout, property.FindPropertyRelative("Vibrato"));
                Field(ref layout, property.FindPropertyRelative("Randomness"));
            }
            else
            {
                Field(ref layout, property.FindPropertyRelative("ToVector"), "Punch");
                Field(ref layout, property.FindPropertyRelative("Vibrato"));
                Field(ref layout, property.FindPropertyRelative("Elasticity"));
            }

            if (UsesSnapping(stepType)) Field(ref layout, property.FindPropertyRelative("Snapping"));
        }

        private void DrawEndpoint(ref Layout layout, SerializedProperty property, string label, string modeField,
            UIAnimationValueKind kind, UIAnimationStepType stepType, bool allowCurrent)
        {
            Rect r = layout.Line();
            if (!layout.Draw) return;

            SerializedProperty mode = property.FindPropertyRelative(modeField);
            SerializedProperty value = property.FindPropertyRelative(ValueFieldName(label, kind));

            var labelRect = new Rect(r.x, r.y, LabelWidth, r.height);
            var modeRect = new Rect(r.x + LabelWidth, r.y, ModeWidth, r.height);
            var valueRect = new Rect(modeRect.xMax + 4f, r.y, Mathf.Max(40f, r.xMax - modeRect.xMax - 4f), r.height);

            EditorGUI.LabelField(labelRect, new GUIContent(label, mode.tooltip));

            if (allowCurrent)
            {
                EditorGUI.PropertyField(modeRect, mode, GUIContent.none);
            }
            else
            {
                int index = Mathf.Clamp(mode.enumValueIndex, 0, AbsoluteBaselineOnly.Length - 1);
                index = EditorGUI.Popup(modeRect, index, AbsoluteBaselineOnly);
                mode.enumValueIndex = index;
            }

            if (kind == UIAnimationValueKind.Vector && IsTwoDimensional(stepType))
            {
                Vector3 current = value.vector3Value;
                Vector2 edited = EditorGUI.Vector2Field(valueRect, GUIContent.none, current);
                value.vector3Value = new Vector3(edited.x, edited.y, current.z);
            }
            else
            {
                EditorGUI.PropertyField(valueRect, value, GUIContent.none);
            }
        }

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

        private static bool IsTwoDimensional(UIAnimationStepType type)
        {
            return type == UIAnimationStepType.AnchoredPosition
                || type == UIAnimationStepType.PunchAnchoredPosition
                || type == UIAnimationStepType.SizeDelta
                || type == UIAnimationStepType.OffsetMin
                || type == UIAnimationStepType.OffsetMax;
        }

        private static bool UsesSnapping(UIAnimationStepType type)
        {
            return type == UIAnimationStepType.AnchoredPosition
                || type == UIAnimationStepType.LocalPosition
                || type == UIAnimationStepType.PunchAnchoredPosition
                || type == UIAnimationStepType.ShakeAnchoredPosition
                || type == UIAnimationStepType.SizeDelta
                || type == UIAnimationStepType.OffsetMin
                || type == UIAnimationStepType.OffsetMax;
        }

        private void Field(ref Layout layout, SerializedProperty property, string label = null)
        {
            Rect r = layout.Line();
            if (!layout.Draw || property == null) return;

            // Keep the field's [Tooltip] even when the drawer overrides its display name.
            if (label == null) EditorGUI.PropertyField(r, property);
            else EditorGUI.PropertyField(r, property, new GUIContent(label, property.tooltip));
        }

        /// <summary>
        /// The one-line header shown when a step is collapsed, which is how the list is read most
        /// of the time. Reads: AFTER   ButtonContainer (Anchored Position)   0.6s   Out Quart
        ///
        /// The object name comes first because a long animation is scanned by "which element does
        /// this row move", and the property is named rather than the component type so that two
        /// Rect steps on the same object stay distinguishable while collapsed.
        /// </summary>
        private static string Summary(SerializedProperty property, UIAnimationStepType type, UIAnimationStartMode start)
        {
            string prefix = start == UIAnimationStartMode.WithPrevious ? "WITH" : "AFTER";
            SerializedProperty typeProperty = property.FindPropertyRelative("Type");

            // Use Unity's prettified enum names so the header matches the dropdowns below it.
            string typeName = typeProperty.enumValueIndex >= 0
                ? typeProperty.enumDisplayNames[typeProperty.enumValueIndex]
                : type.ToString();

            string target = TargetName(property, type);

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
                    SerializedProperty ease = property.FindPropertyRelative("EaseType");
                    if (ease.enumValueIndex >= 0) tail += "   " + ease.enumDisplayNames[ease.enumValueIndex];
                }
            }

            return prefix + "   " + target + " (" + typeName + ")   " + tail;
        }

        /// <summary>
        /// Name of the object this step drives. An empty target slot means "the GameObject this
        /// player is on", which is spelled out rather than left blank so a self-targeting row does
        /// not read as a broken one.
        /// </summary>
        private static string TargetName(SerializedProperty property, UIAnimationStepType type)
        {
            string field;

            switch (UIAnimationStep.TargetKindOf(type))
            {
                case UIAnimationTargetKind.CanvasGroup: field = "CanvasGroupTarget"; break;
                case UIAnimationTargetKind.Graphic: field = "GraphicTarget"; break;
                case UIAnimationTargetKind.Material: field = "MaterialTarget"; break;
                case UIAnimationTargetKind.GameObject: field = "ActiveTarget"; break;
                case UIAnimationTargetKind.Audio: field = "AudioSourceTarget"; break;
                default: field = "RectTarget"; break;
            }

            SerializedProperty target = property.FindPropertyRelative(field);
            if (target != null && target.objectReferenceValue != null) return target.objectReferenceValue.name;

            // A path names the object just as well as a reference does, and reads better in a
            // header than the owner would - the whole point of the row is which object moves.
            SerializedProperty path = property.FindPropertyRelative("TargetPath");
            if (path != null && !string.IsNullOrEmpty(path.stringValue)) return path.stringValue;

            Object owner = property.serializedObject.targetObject;
            var component = owner as Component;

            // An asset has no owner to name, so it says what an empty slot means there instead.
            return component != null ? component.gameObject.name + " (self)" : "(owner)";
        }
    }
}

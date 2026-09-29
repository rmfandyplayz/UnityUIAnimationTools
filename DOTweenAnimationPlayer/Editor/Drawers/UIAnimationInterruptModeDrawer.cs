// -----------------------------------------------------------------------------
// UI Animation Utility
//
// AI-GENERATED. Authored by Claude (Anthropic) via Claude Code, September 2026,
// to a written design brief by the project author.
// See README.md in the folder above for usage.
// -----------------------------------------------------------------------------

using UnityEditor;
using UnityEngine;

namespace rmf_claude.DOTweenUI
{

    /// <summary>
    /// The Interrupt Others row: its checkbox, then - while it is ticked - a STOP / COMPLETE button and a
    /// REPORT / SILENT button on the same row, the way Play At Custom FPS shows its frame rate.
    ///
    /// Each of the three is its own property as far as prefab overrides go: each goes bold on its own
    /// and offers its own Revert, which is why the box's BeginProperty rect stops where the first
    /// button starts. The second button is greyed out while the first reads STOP, since only a
    /// completion has anything to report.
    /// </summary>
    [CustomPropertyDrawer(typeof(UIAnimationInterruptModeAttribute))]
    internal class UIAnimationInterruptModeDrawer : PropertyDrawer
    {
        private const float ToggleWidth = 18f;

        private const string Complete = "COMPLETE";
        private const string Stop = "STOP";
        private const string Silent = "SILENT";
        private const string Report = "REPORT";

        // Measured from the words rather than fixed, as the Start button is: a fixed 72 clipped
        // COMPLETE at 125% scaling. Both words of a button fit, so it never changes width on a click.
        private static float completeWidth;
        private static float silentWidth;

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var mode = (UIAnimationInterruptModeAttribute)attribute;
            SerializedProperty complete = Sibling(property, mode.CompleteField);
            SerializedProperty silent = Sibling(property, mode.SilentField);

            // A renamed sibling shows the plain box rather than hiding the settings for good, which would
            // be impossible to diagnose from the Inspector.
            if (complete == null || silent == null || property.propertyType != SerializedPropertyType.Boolean)
            {
                EditorGUI.PropertyField(position, property, label);
                return;
            }

            // The label and the box, up to where the first button starts. PrefixLabel always puts the
            // field at labelWidth from the row's left edge, whatever the indent.
            float fieldX = position.x + EditorGUIUtility.labelWidth;
            var boxRect = new Rect(position.x, position.y, fieldX + ToggleWidth - position.x, position.height);

            GUIContent content = EditorGUI.BeginProperty(boxRect, label, property);
            Rect field = EditorGUI.PrefixLabel(position, content);
            var toggleRect = new Rect(field.x, field.y, ToggleWidth, field.height);

            // Indent is already spent on the label; the controls after it sit where PrefixLabel says.
            int indent = EditorGUI.indentLevel;
            EditorGUI.indentLevel = 0;

            EditorGUI.showMixedValue = property.hasMultipleDifferentValues;
            EditorGUI.BeginChangeCheck();

            bool on = EditorGUI.Toggle(toggleRect, property.boolValue);
            if (EditorGUI.EndChangeCheck()) property.boolValue = on;

            EditorGUI.showMixedValue = false;
            EditorGUI.EndProperty();

            if (property.boolValue || property.hasMultipleDifferentValues)
            {
                if (completeWidth <= 0f)
                {
                    completeWidth = ButtonWidthFor(Complete, Stop);
                    silentWidth = ButtonWidthFor(Silent, Report);
                }

                float x = toggleRect.xMax + 2f;
                Rect completeRect = ButtonRect(ref x, field, completeWidth);
                Rect silentRect = ButtonRect(ref x, field, silentWidth);

                DrawButton(completeRect, complete, Complete, Stop);

                // Greyed rather than hidden: it keeps its place, and what it is set to stays readable.
                using (new EditorGUI.DisabledScope(!complete.boolValue && !complete.hasMultipleDifferentValues))
                {
                    DrawButton(silentRect, silent, Silent, Report);
                }
            }

            EditorGUI.indentLevel = indent;
        }

        private static float ButtonWidthFor(string first, string second)
        {
            return Mathf.Max(EditorStyles.miniButton.CalcSize(new GUIContent(first)).x,
                EditorStyles.miniButton.CalcSize(new GUIContent(second)).x) + 8f;
        }

        /// <summary>The next button along the row, narrowed to whatever room is left.</summary>
        private static Rect ButtonRect(ref float x, Rect field, float width)
        {
            var rect = new Rect(x, field.y, Mathf.Max(0f, Mathf.Min(width, field.xMax - x)), field.height);
            x = rect.xMax + 2f;
            return rect;
        }

        /// <summary>
        /// A button that flips a bool, reading onText while it is true. On a multi-selection that
        /// disagrees, the first click sets them all to false, as the Snapping button's does.
        /// </summary>
        private static void DrawButton(Rect rect, SerializedProperty flag, string onText, string offText)
        {
            if (rect.width <= 0f) return;

            bool mixed = flag.hasMultipleDifferentValues;

            EditorGUI.BeginProperty(rect, GUIContent.none, flag);

            var content = new GUIContent(mixed ? "—" : flag.boolValue ? onText : offText, flag.tooltip);
            if (GUI.Button(rect, content, EditorStyles.miniButton)) flag.boolValue = !mixed && !flag.boolValue;

            EditorGUI.EndProperty();
        }

        /// <summary>The named field on the same object, found by swapping the last element of the path.</summary>
        private static SerializedProperty Sibling(SerializedProperty property, string fieldName)
        {
            string path = property.propertyPath;
            int cut = path.LastIndexOf('.');

            return property.serializedObject.FindProperty(
                cut < 0 ? fieldName : path.Substring(0, cut + 1) + fieldName);
        }
    }
}

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
    /// A [UIAnimationInlineValue] bool: its checkbox, then - while it is ticked - the value it turns
    /// on, in a short field on the same row.
    ///
    /// Each half is its own property as far as prefab overrides go: the box and the field each go
    /// bold on their own and each offer their own Revert, which is why the box's BeginProperty rect
    /// stops where the field starts.
    /// </summary>
    [CustomPropertyDrawer(typeof(UIAnimationInlineValueAttribute))]
    internal class UIAnimationInlineValueDrawer : PropertyDrawer
    {
        private const float ToggleWidth = 18f;
        private const float ValueWidth = 50f;

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var inline = (UIAnimationInlineValueAttribute)attribute;
            SerializedProperty value = Sibling(property, inline.ValueField);

            // A renamed sibling shows the plain box rather than hiding the value for good, which would
            // be impossible to diagnose from the Inspector.
            if (value == null || property.propertyType != SerializedPropertyType.Boolean)
            {
                EditorGUI.PropertyField(position, property, label);
                return;
            }

            // The label and the box, up to where the value field starts. PrefixLabel always puts the
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
                float x = toggleRect.xMax + 2f;
                var valueRect = new Rect(x, field.y, Mathf.Max(0f, Mathf.Min(ValueWidth, field.xMax - x)), field.height);

                EditorGUI.PropertyField(valueRect, value, GUIContent.none);

                // A label over the field carries its tooltip: a field drawn with no label shows none,
                // and a label takes no clicks, so the field under it still edits.
                GUI.Label(valueRect, new GUIContent(string.Empty, value.tooltip), GUIStyle.none);
            }

            EditorGUI.indentLevel = indent;
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

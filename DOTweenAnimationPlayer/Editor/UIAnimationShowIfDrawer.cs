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
    /// Draws a [UIAnimationShowIf] field only when the bool it names is ticked.
    ///
    /// This is deliberately an attribute drawer rather than a drawer for UIAnimation itself:
    /// taking over the whole animation would mean re-implementing its list UI, and would sit in
    /// front of the reorder handles and the right-click copy/paste/mirror menu.
    /// </summary>
    [CustomPropertyDrawer(typeof(UIAnimationShowIfAttribute))]
    internal class UIAnimationShowIfDrawer : PropertyDrawer
    {
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            if (Visible(property)) return EditorGUI.GetPropertyHeight(property, label, true);

            // Unity adds standardVerticalSpacing after every property it draws, so a height of 0
            // still leaves a stray gap where the field used to be. Cancel it out.
            return -EditorGUIUtility.standardVerticalSpacing;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (!Visible(property)) return;

            EditorGUI.PropertyField(position, property, label, true);
        }

        private bool Visible(SerializedProperty property)
        {
            var showIf = (UIAnimationShowIfAttribute)attribute;
            SerializedProperty condition = Sibling(property, showIf.Condition);

            // A renamed or misspelled condition shows the field rather than hiding it forever,
            // which is the failure that would be impossible to diagnose from the Inspector.
            if (condition == null || condition.propertyType != SerializedPropertyType.Boolean) return true;

            return condition.boolValue;
        }

        /// <summary>
        /// The named field on the same object, found by swapping the last element of this
        /// property's path. Works inside list elements, where the path is
        /// "Animations.Array.data[0].FrameRate".
        /// </summary>
        private static SerializedProperty Sibling(SerializedProperty property, string fieldName)
        {
            string path = property.propertyPath;
            int cut = path.LastIndexOf('.');

            return property.serializedObject.FindProperty(
                cut < 0 ? fieldName : path.Substring(0, cut + 1) + fieldName);
        }
    }
}

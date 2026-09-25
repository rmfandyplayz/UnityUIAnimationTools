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
    /// The animation's Snapping row: one button that cycles DISABLED, ALL STEPS, PER STEP.
    ///
    /// Underneath it is still two bools, and every combination they can hold maps to one of the three
    /// - Snap Per Step wins over Snapping, as it does at runtime - so older data shows as whatever it
    /// actually does and nothing is rewritten until the button is clicked.
    /// </summary>
    [CustomPropertyDrawer(typeof(UIAnimationSnapModeAttribute))]
    internal class UIAnimationSnapModeDrawer : PropertyDrawer
    {
        private static readonly string[] Names = { "DISABLED", "ALL STEPS", "PER STEP" };

        private const float ButtonWidth = 84f;

        private const string Tooltip =
            "Rounds positions and sizes to whole units every frame - pixel-perfect movement for pixel art. " +
            "Causes visible stepping on a slow move otherwise.\n\n" +
            "DISABLED = nothing snaps, except a step whose own Snapping box is ticked.\n" +
            "ALL STEPS = every step that moves or resizes something snaps.\n" +
            "PER STEP = each position or size step shows its own Snapping box, and you choose there.\n\n" +
            "Click to switch. Scale and rotation never snap - DOTween has no option for them.";

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var snapMode = (UIAnimationSnapModeAttribute)attribute;
            SerializedProperty perStep = Sibling(property, snapMode.PerStepField);

            // A renamed sibling shows the plain box rather than nothing, which would be impossible to
            // diagnose from the Inspector.
            if (perStep == null || perStep.propertyType != SerializedPropertyType.Boolean)
            {
                EditorGUI.PropertyField(position, property, label);
                return;
            }

            int mode = perStep.boolValue ? 2 : property.boolValue ? 1 : 0;
            bool mixed = property.hasMultipleDifferentValues || perStep.hasMultipleDifferentValues;

            // The row belongs to whichever bool is deciding what you see, so the bold prefab-override
            // marker and right-click Revert follow it - the same rule as the step's Ease row.
            SerializedProperty bound = perStep.boolValue || perStep.prefabOverride ? perStep : property;

            GUIContent content = EditorGUI.BeginProperty(position, new GUIContent(label.text, Tooltip), bound);
            Rect field = EditorGUI.PrefixLabel(position, content);
            field.width = Mathf.Min(field.width, ButtonWidth);

            if (GUI.Button(field, new GUIContent(mixed ? "—" : Names[mode], Tooltip), EditorStyles.miniButton))
            {
                int next = mixed ? 0 : (mode + 1) % Names.Length;

                property.boolValue = next == 1;
                perStep.boolValue = next == 2;
            }

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

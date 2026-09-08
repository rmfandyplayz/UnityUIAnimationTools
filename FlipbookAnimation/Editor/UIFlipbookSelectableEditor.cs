// -----------------------------------------------------------------------------
// Flipnote Style Animation Utility
//
// AI-GENERATED. Authored by Claude (Anthropic) via Claude Code, September 2026,
// to a written design brief by the project author. Not hand-written by the
// Twindrill Goose team. See the README.md in the folder above for usage.
// -----------------------------------------------------------------------------

using UnityEditor;
using UnityEngine;

namespace rmf_claude.FlipbookAnimation
{
    /// <summary>
    /// Forces each interaction state so a button's five clips can be checked without entering play
    /// mode and hovering it. The clip shown is the one the state would REALLY play, fallback chain
    /// included, so an empty Highlighted correctly shows you Normal rather than nothing.
    /// </summary>
    [CustomEditor(typeof(UIFlipbookSelectable))]
    [CanEditMultipleObjects]
    internal class UIFlipbookSelectableEditor : Editor
    {
        private static readonly UIFlipbookState[] States =
        {
            UIFlipbookState.Normal,
            UIFlipbookState.Highlighted,
            UIFlipbookState.Pressed,
            UIFlipbookState.Selected,
            UIFlipbookState.Disabled,
        };

        private void OnDisable()
        {
            var selectable = target as UIFlipbookSelectable;
            if (selectable == null) return;

            var flipbook = selectable.EditorFlipbook;

            if (flipbook != null && UIFlipbookPreview.IsPreviewing(flipbook))
            {
                UIFlipbookPreview.End();
            }
        }

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            if (targets.Length > 1) return;

            var selectable = (UIFlipbookSelectable)target;

            EditorGUILayout.Space();

            if (Application.isPlaying)
            {
                DrawPlayModePreview(selectable);
                return;
            }

            DrawEditModePreview(selectable);
        }

        private void DrawPlayModePreview(UIFlipbookSelectable selectable)
        {
            EditorGUILayout.LabelField("Preview", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("State: " + selectable.CurrentState, EditorStyles.miniLabel);

            Repaint();
        }

        private void DrawEditModePreview(UIFlipbookSelectable selectable)
        {
            var flipbook = selectable.EditorFlipbook;

            UIFlipbookPreviewGUI.DrawHeader("Preview (edit mode)", UIFlipbookPreview.IsPreviewing(flipbook));

            if (flipbook == null)
            {
                EditorGUILayout.HelpBox(
                    "No UISpriteFlipbook found on this object or its children, so there is nothing to preview.",
                    MessageType.Warning);

                return;
            }

            EditorGUILayout.BeginHorizontal();

            for (int i = 0; i < States.Length; i++)
            {
                var state = States[i];
                var clip = selectable.EditorClipFor(state);

                using (new EditorGUI.DisabledScope(clip == null))
                {
                    // A state with no frames anywhere in its fallback chain is greyed out rather
                    // than hidden, so it is obvious the slot is empty on purpose.
                    if (GUILayout.Button(state.ToString()))
                    {
                        UIFlipbookPreview.Begin(flipbook, clip);
                    }
                }
            }

            EditorGUILayout.EndHorizontal();

            UIFlipbookPreviewGUI.DrawStopButton(flipbook);
            UIFlipbookPreviewGUI.DrawScrubber(flipbook);
        }
    }
}

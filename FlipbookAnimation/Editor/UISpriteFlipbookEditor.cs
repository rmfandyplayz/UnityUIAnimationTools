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
    /// Play / Stop / scrub for a flipbook, without entering play mode.
    ///
    /// In play mode the buttons just call the ordinary runtime API. Out of play mode they hand off
    /// to UIFlipbookPreview, which animates the real Image and puts it back afterwards.
    /// </summary>
    [CustomEditor(typeof(UISpriteFlipbook))]
    [CanEditMultipleObjects]
    internal class UISpriteFlipbookEditor : Editor
    {
        private void OnDisable()
        {
            // Selecting something else abandons the preview, so put the sprite back first.
            var flipbook = target as UISpriteFlipbook;

            if (flipbook != null && UIFlipbookPreview.IsPreviewing(flipbook))
            {
                UIFlipbookPreview.End();
            }
        }

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            if (targets.Length > 1) return;

            var flipbook = (UISpriteFlipbook)target;

            EditorGUILayout.Space();

            if (Application.isPlaying) DrawPlayModePreview(flipbook);
            else UIFlipbookPreviewGUI.Draw(flipbook, flipbook.EditorClip, "Preview (edit mode)");
        }

        private void DrawPlayModePreview(UISpriteFlipbook flipbook)
        {
            EditorGUILayout.LabelField("Preview", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Play")) flipbook.Play();
            if (GUILayout.Button("Restart")) flipbook.Restart();
            if (GUILayout.Button("Stop")) flipbook.Stop();

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.LabelField(
                "Frame " + flipbook.CurrentFrame + (flipbook.IsPlaying ? "  (playing)" : "  (stopped)"),
                EditorStyles.miniLabel);

            Repaint();
        }
    }
}

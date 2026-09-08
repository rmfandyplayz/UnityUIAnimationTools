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
    /// The bits of preview UI that both inspectors draw, so the warning text and the transport
    /// controls cannot drift apart between them.
    /// </summary>
    internal static class UIFlipbookPreviewGUI
    {
        private const string WarningText =
            "Edit-mode preview animates the real Image in your scene.\n\n" +
            "The sprite is put back when the preview stops, and the whole thing is one Undo step " +
            "(Ctrl+Z) if it goes wrong. Stop it before you save.\n\n" +
            "On Completed events do not fire, so nothing in the animation can run game code while " +
            "you are not in play mode.";

        /// <summary>Title, then the standing warning - amber while a preview is actually running.</summary>
        internal static void DrawHeader(string title, bool active)
        {
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(WarningText, active ? MessageType.Warning : MessageType.Info);
        }

        /// <summary>The whole panel for a plain flipbook: warning, Play/Stop, scrubber.</summary>
        internal static void Draw(UISpriteFlipbook flipbook, UIFlipbookClip clip, string title)
        {
            DrawHeader(title, UIFlipbookPreview.IsPreviewing(flipbook));

            if (flipbook == null)
            {
                EditorGUILayout.HelpBox("No UISpriteFlipbook to drive.", MessageType.None);
                return;
            }

            var playable = clip != null && clip.Resolved().HasFrames;

            EditorGUILayout.BeginHorizontal();

            using (new EditorGUI.DisabledScope(!playable))
            {
                if (GUILayout.Button(playable ? "Play" : "Play (no frames)"))
                {
                    UIFlipbookPreview.Begin(flipbook, clip);
                }
            }

            DrawStopButton(flipbook);

            EditorGUILayout.EndHorizontal();

            DrawScrubber(flipbook);
        }

        internal static void DrawStopButton(UISpriteFlipbook flipbook)
        {
            using (new EditorGUI.DisabledScope(!UIFlipbookPreview.IsPreviewing(flipbook)))
            {
                if (GUILayout.Button("Stop and Restore"))
                {
                    UIFlipbookPreview.End();
                }
            }
        }

        /// <summary>
        /// Frame slider. Dragging it pauses auto-advance, which is the only way to actually look at
        /// a single drawing; Resume picks up from wherever it was left.
        /// </summary>
        internal static void DrawScrubber(UISpriteFlipbook flipbook)
        {
            if (!UIFlipbookPreview.IsPreviewing(flipbook)) return;

            var count = flipbook.EditorFrameCount;
            if (count <= 0) return;

            EditorGUILayout.BeginHorizontal();

            EditorGUI.BeginChangeCheck();

            var frame = EditorGUILayout.IntSlider(
                "Frame",
                Mathf.Clamp(flipbook.EditorCurrentFrame, 0, count - 1),
                0,
                count - 1);

            if (EditorGUI.EndChangeCheck())
            {
                UIFlipbookPreview.SetFrame(frame);
            }

            using (new EditorGUI.DisabledScope(flipbook.EditorIsPlaying))
            {
                if (GUILayout.Button("Resume", GUILayout.Width(64f)))
                {
                    UIFlipbookPreview.Resume();
                }
            }

            EditorGUILayout.EndHorizontal();
        }
    }
}

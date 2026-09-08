// -----------------------------------------------------------------------------
// UI Animation Utility
//
// AI-GENERATED. Authored by Claude (Anthropic) via Claude Code, September 2026,
// to a written design brief by the project author. Not hand-written by the
// Twindrill Goose team. See README.md in the folder above for usage.
// -----------------------------------------------------------------------------

using System.Collections.Generic;
using DG.Tweening;
using DG.DOTweenEditor;
using UnityEditor;
using UnityEngine;

namespace rmf_claude.DOTweenUI
{

    /// <summary>
    /// Play / Stop buttons for tuning animation timing without writing test code.
    ///
    /// In play mode this just calls the ordinary runtime API. Out of play mode it drives the
    /// tween off the Editor's own update loop via DOTweenEditorPreview, which means the tween
    /// writes to REAL scene objects - so the preview is wrapped in a capture/restore pair and
    /// an Undo record. See BeginPreview / EndPreview for what that costs and what it cannot
    /// protect you from.
    /// </summary>
    [CustomEditor(typeof(UIAnimationPlayer))]
    [CanEditMultipleObjects]
    internal class UIAnimationPlayerEditor : Editor
    {
        // Which player is previewing, so the buttons can only ever start one at a time and a
        // second inspector cannot silently steal the restore state out from under the first.
        private static UIAnimationPlayer previewing;
        private static readonly List<Object> previewTargets = new List<Object>();

        private void OnDisable()
        {
            // Selecting something else abandons the preview, so put the scene back first.
            if (previewing != null && previewing == target) EndPreview();
        }

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            if (targets.Length > 1) return;

            var player = (UIAnimationPlayer)target;

            EditorGUILayout.Space();

            if (Application.isPlaying) DrawPlayModePreview(player);
            else DrawEditModePreview(player);
        }

        private void DrawPlayModePreview(UIAnimationPlayer player)
        {
            EditorGUILayout.LabelField("Preview", EditorStyles.boldLabel);

            List<UIAnimation> animations = player.EditorAnimations;
            for (int i = 0; i < animations.Count; i++)
            {
                string animationName = animations[i].Name;
                if (string.IsNullOrEmpty(animationName)) continue;

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(animationName, GUILayout.MinWidth(60f));

                if (GUILayout.Button("Play", GUILayout.Width(44f))) player.Play(animationName);
                if (GUILayout.Button("Start", GUILayout.Width(44f))) player.ApplyFromState(animationName);
                if (GUILayout.Button("Stop", GUILayout.Width(44f))) player.Stop(animationName);

                EditorGUILayout.EndHorizontal();
            }

            if (GUILayout.Button("Stop All")) player.StopAll();

            Repaint();
        }

        private void DrawEditModePreview(UIAnimationPlayer player)
        {
            EditorGUILayout.LabelField("Preview (edit mode)", EditorStyles.boldLabel);

            bool active = previewing == player;

            EditorGUILayout.HelpBox(
                "Edit-mode preview animates the real objects in your scene.\n\n" +
                "Values are put back when the preview stops, and the whole thing is one Undo step " +
                "(Ctrl+Z) if it goes wrong. Stop it before you save.\n\n" +
                "Sound steps are skipped, and On Complete events do not fire, so nothing in the " +
                "animation can run game code while you are not in play mode.",
                active ? MessageType.Warning : MessageType.Info);

            List<UIAnimation> animations = player.EditorAnimations;

            for (int i = 0; i < animations.Count; i++)
            {
                string animationName = animations[i].Name;
                if (string.IsNullOrEmpty(animationName)) continue;

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(animationName, GUILayout.MinWidth(60f));

                if (GUILayout.Button("Play", GUILayout.Width(44f))) PlayPreview(player, animationName, false);
                if (GUILayout.Button("Start", GUILayout.Width(44f))) PlayPreview(player, animationName, true);

                EditorGUILayout.EndHorizontal();
            }

            using (new EditorGUI.DisabledScope(!active))
            {
                if (GUILayout.Button("Stop and Restore")) EndPreview();
            }

            if (active) Repaint();
        }

        // ---------------------------------------------------------------- preview lifecycle

        /// <summary>
        /// Plays one animation, or - when fromStateOnly - just snaps its FROM values on so a
        /// starting pose can be eyeballed without watching the whole thing.
        /// </summary>
        private void PlayPreview(UIAnimationPlayer player, string animationName, bool fromStateOnly)
        {
            // Restarting from a clean, restored scene every time is what stops repeated previews
            // compounding: the second Play must measure its baselines from rest, not from wherever
            // the first one happened to leave things.
            if (previewing != null) EndPreview();

            BeginPreview(player);

            if (fromStateOnly)
            {
                player.ApplyFromState(animationName);
                return;
            }

            UIAnimationStep.EditorSuppressSound = true;

            Sequence sequence;
            try
            {
                sequence = player.Play(animationName);
            }
            finally
            {
                UIAnimationStep.EditorSuppressSound = false;
            }

            if (sequence == null)
            {
                EndPreview();
                return;
            }

            // clearCallbacks: an animation's On Complete is a UnityEvent wired to arbitrary game
            // code, and firing that outside play mode is not something a preview should do.
            DOTweenEditorPreview.PrepareTweenForPreview(sequence, true, true, false);
            DOTweenEditorPreview.Start(OnPreviewUpdate);
        }

        private void BeginPreview(UIAnimationPlayer player)
        {
            // Resolve targets and read the resting values BEFORE anything moves - the restore and
            // every Baseline endpoint are both measured from this moment.
            player.EditorPrepareForPreview();

            previewTargets.Clear();
            player.EditorCollectTargets(previewTargets);

            if (previewTargets.Count > 0)
            {
                Undo.RecordObjects(previewTargets.ToArray(), "UI Animation Preview");
            }

            previewing = player;
        }

        private static void EndPreview()
        {
            if (previewing == null) return;

            DOTweenEditorPreview.Stop();
            previewing.StopAll();
            previewing.EditorRestoreBaselines();

            // The restore wrote through plain property setters, which do not mark a prefab
            // instance's overrides or a scene as needing a resave on their own.
            for (int i = 0; i < previewTargets.Count; i++)
            {
                if (previewTargets[i] != null) EditorUtility.SetDirty(previewTargets[i]);
            }

            previewTargets.Clear();
            previewing = null;

            SceneView.RepaintAll();
        }

        private static void OnPreviewUpdate()
        {
            SceneView.RepaintAll();
        }
    }
}

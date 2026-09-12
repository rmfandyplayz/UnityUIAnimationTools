// -----------------------------------------------------------------------------
// UI Animation Utility
//
// AI-GENERATED. Authored by Claude (Anthropic) via Claude Code, September 2026,
// to a written design brief by the project author. Not hand-written by the
// Twindrill Goose team. See README.md in the folder above for usage.
// -----------------------------------------------------------------------------

using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace rmf_claude.DOTweenUI
{

    /// <summary>
    /// Play / Stop buttons for tuning animation timing without writing test code.
    ///
    /// In play mode this just calls the ordinary runtime API. Out of play mode it hands off to
    /// UIAnimationPreview, which drives the tween off the Editor's own update loop - so the tween
    /// writes to REAL scene objects. See that class for what makes it safe and what it cannot
    /// protect you from.
    ///
    /// The list includes animations from the Shared asset as well as local ones, because those are
    /// exactly as playable; a local animation shadowing a shared name appears once, and it is the
    /// local one that plays.
    /// </summary>
    [CustomEditor(typeof(UIAnimationPlayer))]
    [CanEditMultipleObjects]
    internal class UIAnimationPlayerEditor : Editor
    {
        private static readonly List<string> names = new List<string>();

        private void OnDisable()
        {
            // Selecting something else abandons the preview, so put the scene back first.
            UIAnimationPreview.EndIfOwnedBy(this);
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

            CollectNames(player);

            for (int i = 0; i < names.Count; i++)
            {
                string animationName = names[i];

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

            bool active = UIAnimationPreview.IsPreviewing(player);

            EditorGUILayout.HelpBox(
                "Edit-mode preview animates the real objects in your scene.\n\n" +
                "Values are put back when the preview stops, and the whole thing is one Undo step " +
                "(Ctrl+Z) if it goes wrong. Stop it before you save.\n\n" +
                "Sound steps are skipped, and On Complete events do not fire, so nothing in the " +
                "animation can run game code while you are not in play mode.",
                active ? MessageType.Warning : MessageType.Info);

            CollectNames(player);

            for (int i = 0; i < names.Count; i++)
            {
                string animationName = names[i];

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(animationName, GUILayout.MinWidth(60f));

                if (GUILayout.Button("Play", GUILayout.Width(44f)))
                {
                    UIAnimationPreview.Play(this, player, animationName, false);
                }

                if (GUILayout.Button("Start", GUILayout.Width(44f)))
                {
                    UIAnimationPreview.Play(this, player, animationName, true);
                }

                EditorGUILayout.EndHorizontal();
            }

            using (new EditorGUI.DisabledScope(!active))
            {
                if (GUILayout.Button("Stop and Restore")) UIAnimationPreview.End();
            }

            if (active) Repaint();
        }

        private static void CollectNames(UIAnimationPlayer player)
        {
            names.Clear();
            player.EditorCollectAnimationNames(names);
        }
    }
}

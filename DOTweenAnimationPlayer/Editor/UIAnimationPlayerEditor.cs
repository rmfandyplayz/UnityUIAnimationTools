// -----------------------------------------------------------------------------
// UI Animation Utility
//
// AI-GENERATED. Authored by Claude (Anthropic) via Claude Code, September 2026,
// to a written design brief by the project author.
// See README.md in the folder above for usage.
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

            NoteShadowedNames(player);

            EditorGUILayout.Space();

            if (Application.isPlaying)
            {
                EditorGUILayout.LabelField("Preview", EditorStyles.boldLabel);
            }
            else
            {
                EditorGUILayout.LabelField("Preview (edit mode)", EditorStyles.boldLabel);

                EditorGUILayout.HelpBox(
                    "Edit-mode preview animates the real objects in your scene.\n\n" + UIAnimationPreview.EditModeNote,
                    UIAnimationPreview.IsPreviewing(player) ? MessageType.Warning : MessageType.Info);
            }

            CollectNames(player);

            for (int i = 0; i < names.Count; i++)
            {
                UIAnimationPreview.DrawRow(this, player, names[i]);
            }

            UIAnimationPreview.DrawFooter(player);

            if (Application.isPlaying || UIAnimationPreview.IsPreviewing(player)) Repaint();
        }

        /// <summary>
        /// The player-side half of the shadowing warning the asset inspector gives. Local winning is
        /// the override feature working, so this is information rather than a warning - but it is the
        /// only place a player's own inspector says that the shared version of a name never plays here.
        /// </summary>
        private static void NoteShadowedNames(UIAnimationPlayer player)
        {
            if (player.EditorShared == null) return;

            string list = UIAnimationAssetEditor.ShadowedNames(player.EditorShared, player);
            if (list == null) return;

            EditorGUILayout.HelpBox(
                "Local animations override the ones of the same name in '" + player.EditorShared.name +
                "': " + list + ". Only the local versions play on this player.",
                MessageType.Info);
        }

        private static void CollectNames(UIAnimationPlayer player)
        {
            names.Clear();
            player.EditorCollectAnimationNames(names);
        }
    }
}

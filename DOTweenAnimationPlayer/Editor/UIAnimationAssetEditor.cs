// -----------------------------------------------------------------------------
// UI Animation Utility
//
// AI-GENERATED. Authored by Claude (Anthropic) via Claude Code, September 2026,
// to a written design brief by the project author.
// See README.md in the folder above for usage.
// -----------------------------------------------------------------------------

using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace rmf_claude.DOTweenUI
{

    /// <summary>
    /// Inspector for a shared animation set, and the answer to the obvious problem with previewing
    /// one: an animation is a description of how something moves, and an asset has nothing to move.
    ///
    /// So the preview here borrows a scene player. Drop one in Preview On and the buttons run the
    /// ordinary player preview - same capture, same restore, same Undo entry, same one-at-a-time
    /// rule as previewing from the player's own inspector, because it is literally that code.
    ///
    /// The player has to actually reference this asset, since it plays what its own merge produced
    /// rather than what any asset happens to contain. When it does not - or the slot is empty - no
    /// buttons are drawn and a message says why. Refusing out loud beats a Play button that
    /// silently does nothing.
    ///
    /// The Preview On slot lives on this Editor and is deliberately NOT a serialized field on the
    /// asset: a scene reference is the one thing an asset cannot keep, which is the whole reason
    /// this class exists.
    /// </summary>
    [CustomEditor(typeof(UIAnimationAsset))]
    internal class UIAnimationAssetEditor : Editor
    {
        private UIAnimationPlayer previewPlayer;

        private static readonly List<string> shadowed = new List<string>();

        private void OnDisable()
        {
            UIAnimationPreview.EndIfOwnedBy(this);
        }

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            EditorGUILayout.Space();

            EditorGUILayout.HelpBox(
                "Animations in a shared set cannot point at a target directly - an asset has no " +
                "scene to point into, so those slots are disabled.\n\n" +
                "Leave a target empty to animate whichever GameObject the player is on, or set " +
                "Target Path to reach a named child of it, e.g. \"Panel/Icon\".",
                MessageType.Info);

            EditorGUILayout.Space();
            DrawPreview();
        }

        private void DrawPreview()
        {
            var asset = (UIAnimationAsset)target;

            EditorGUILayout.LabelField(
                Application.isPlaying ? "Preview" : "Preview (edit mode)", EditorStyles.boldLabel);

            previewPlayer = (UIAnimationPlayer)EditorGUILayout.ObjectField(
                new GUIContent("Preview On",
                    "A UI Animation Player in the open scene to preview these animations on. " +
                    "An animation set has no object of its own to animate.\n\n" +
                    "Not saved into the asset - an asset cannot hold a scene reference."),
                previewPlayer, typeof(UIAnimationPlayer), true);

            if (previewPlayer == null)
            {
                EditorGUILayout.HelpBox(
                    "Drop in a scene UI Animation Player that uses this asset to preview these " +
                    "animations. They describe how something moves; the player is what supplies " +
                    "the something.",
                    MessageType.Info);
                return;
            }

            if (previewPlayer.EditorShared != asset)
            {
                EditorGUILayout.HelpBox(
                    "'" + previewPlayer.name + "' does not use this asset - its Shared slot is " +
                    (previewPlayer.EditorShared == null ? "empty" : "'" + previewPlayer.EditorShared.name + "'") +
                    ". Assign this asset to it, or pick a player that already uses it.",
                    MessageType.Warning);
                return;
            }

            WarnAboutShadowedNames(asset, previewPlayer);

            bool active = UIAnimationPreview.IsPreviewing(previewPlayer);

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox(
                    "Edit-mode preview animates the real objects in '" + previewPlayer.name + "'.\n\n" +
                    UIAnimationPreview.EditModeNote,
                    active ? MessageType.Warning : MessageType.Info);
            }

            for (int i = 0; i < asset.Animations.Count; i++)
            {
                UIAnimation animation = asset.Animations[i];
                if (animation == null || string.IsNullOrEmpty(animation.Name)) continue;

                UIAnimationPreview.DrawRow(this, previewPlayer, animation.Name);
            }

            UIAnimationPreview.DrawFooter(previewPlayer);

            if (Application.isPlaying || active) Repaint();
        }

        /// <summary>
        /// Warns about names this asset defines that the chosen player also defines locally. The
        /// local one wins - that is the override feature working as intended - but it does mean
        /// pressing Play on that row previews the player's version rather than this one, which is
        /// worth saying before someone spends ten minutes tuning a curve that never plays.
        /// </summary>
        private static void WarnAboutShadowedNames(UIAnimationAsset asset, UIAnimationPlayer player)
        {
            string list = ShadowedNames(asset, player);
            if (list == null) return;

            EditorGUILayout.HelpBox(
                "'" + player.name + "' overrides " + (shadowed.Count == 1 ? "this animation" : "these animations") +
                " locally: " + list + ". Previewing on this player plays its version, not the one in this asset.",
                MessageType.Warning);
        }

        /// <summary>
        /// The names both the asset and the player's local list define, comma-separated, or null when
        /// there are none. Shared with the player's inspector, which says the same thing from its side.
        /// </summary>
        public static string ShadowedNames(UIAnimationAsset asset, UIAnimationPlayer player)
        {
            shadowed.Clear();

            List<UIAnimation> local = player.EditorAnimations;

            for (int i = 0; i < asset.Animations.Count; i++)
            {
                UIAnimation animation = asset.Animations[i];
                if (animation == null || string.IsNullOrEmpty(animation.Name)) continue;

                for (int j = 0; j < local.Count; j++)
                {
                    if (local[j] == null || local[j].Name != animation.Name) continue;

                    if (!shadowed.Contains(animation.Name)) shadowed.Add(animation.Name);
                    break;
                }
            }

            if (shadowed.Count == 0) return null;

            var text = new StringBuilder();

            for (int i = 0; i < shadowed.Count; i++)
            {
                if (i > 0) text.Append(", ");
                text.Append(shadowed[i]);
            }

            return text.ToString();
        }
    }
}

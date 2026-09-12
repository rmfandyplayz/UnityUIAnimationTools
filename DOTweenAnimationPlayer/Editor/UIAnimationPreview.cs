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
    /// The edit-mode preview, and the three things that make it safe to run one.
    ///
    /// Out of play mode there is no game loop, so the tween is driven off the Editor's own update
    /// via DOTweenEditorPreview - which means it writes to REAL objects in the open scene. What
    /// keeps that from leaving damage behind:
    ///
    ///   1. Capture before, restore after, PER PROPERTY. Never a blanket EditorJsonUtility
    ///      round-trip of the component, which would also rewrite object references and blank
    ///      things like an Image's sprite.
    ///   2. Baselines re-captured before EVERY preview, so relative endpoints do not drift a
    ///      little further each time Play is pressed.
    ///   3. Callbacks cleared, because an On Complete is a UnityEvent wired to arbitrary game
    ///      code that has no business running outside play mode.
    ///
    /// Plus one preview at a time - which is what guarantees the captured state belongs to the
    /// object being restored - a single Undo entry as a backstop, and SetDirty on everything
    /// touched, since writing through plain property setters does not mark a scene or a prefab
    /// instance's overrides on its own.
    ///
    /// It lives here rather than in an Editor class because two inspectors drive it: a player's,
    /// and a shared asset's by way of a player. A second copy of the above is exactly the kind of
    /// thing that ends up with only one of the three mechanisms.
    /// </summary>
    internal static class UIAnimationPreview
    {
        // The player being previewed, and whoever asked for it. Static because only one preview
        // may exist; the owner is tracked so an inspector closing can end the preview IT started
        // without ending one another inspector started since.
        private static UIAnimationPlayer previewing;
        private static object owner;

        private static readonly List<Object> previewTargets = new List<Object>();

        public static bool IsPreviewing(UIAnimationPlayer player)
        {
            return previewing != null && previewing == player;
        }

        public static bool IsPreviewing()
        {
            return previewing != null;
        }

        /// <summary>
        /// Plays one animation on real scene objects, or - when fromStateOnly - just snaps its FROM
        /// values on so a starting pose can be eyeballed without watching the whole thing.
        /// </summary>
        public static void Play(object requester, UIAnimationPlayer player, string animationName, bool fromStateOnly)
        {
            if (player == null) return;

            // Restarting from a clean, restored scene every time is what stops repeated previews
            // compounding: the second Play must measure its baselines from rest, not from wherever
            // the first one happened to leave things.
            End();

            Begin(requester, player);

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
                End();
                return;
            }

            // clearCallbacks: an animation's On Complete is a UnityEvent wired to arbitrary game
            // code, and firing that outside play mode is not something a preview should do.
            DOTweenEditorPreview.PrepareTweenForPreview(sequence, true, true, false);
            DOTweenEditorPreview.Start(OnPreviewUpdate);
        }

        private static void Begin(object requester, UIAnimationPlayer player)
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
            owner = requester;
        }

        /// <summary>Ends the preview and puts every value back. Safe to call when none is running.</summary>
        public static void End()
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
            owner = null;

            SceneView.RepaintAll();
        }

        /// <summary>
        /// Ends the preview only if this requester is the one that started it, so an inspector
        /// being closed cannot restore a scene out from under a preview someone else started since.
        /// </summary>
        public static void EndIfOwnedBy(object requester)
        {
            if (previewing != null && ReferenceEquals(owner, requester)) End();
        }

        private static void OnPreviewUpdate()
        {
            SceneView.RepaintAll();
        }
    }
}

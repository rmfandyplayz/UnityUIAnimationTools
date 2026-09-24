// -----------------------------------------------------------------------------
// UI Animation Utility
//
// AI-GENERATED. Authored by Claude (Anthropic) via Claude Code, September 2026,
// to a written design brief by the project author.
// See README.md in the folder above for usage.
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
    ///   2. Baselines re-captured from rest before EVERY Play, so relative endpoints do not drift
    ///      a little further each time Play is pressed.
    ///   3. Callbacks cleared, because an On Complete is a UnityEvent wired to arbitrary game
    ///      code that has no business running outside play mode. Sound steps are silenced for
    ///      the whole preview for the same reason.
    ///
    /// Plus one player previewing at a time - which is what guarantees the captured state belongs
    /// to the objects being restored - a single Undo entry as a backstop, SetDirty on everything
    /// touched, since writing through plain property setters does not mark a scene or a prefab
    /// instance's overrides on its own, and an unwind on selection change, entering play mode and
    /// assembly reload.
    ///
    /// Within one player a preview runs like play mode does: a second Play carries on from where
    /// the first left things, so a Close can be previewed straight after its Open. Play on the same
    /// animation again starts it over from where it started last time. Stop and Restore - or any of
    /// the unwinds above - goes all the way back to rest.
    ///
    /// It lives here rather than in an Editor class because two inspectors drive it: a player's,
    /// and a shared asset's by way of a player. A second copy of the above is exactly the kind of
    /// thing that ends up with only one of the three mechanisms.
    /// </summary>
    [InitializeOnLoad]
    internal static class UIAnimationPreview
    {
        // DOTween never initialises out of play mode, and Tween.Kill() returns without doing
        // anything when it has not - measured against 1.3.030, the sequence stays active and
        // playing. DOTweenEditorPreview.Stop() does not kill either; it only stops calling
        // DOTween.ManualUpdate. So a stopped preview's sequence would sit in DOTween's list,
        // still playing, and resume the moment ANY later preview started updating again -
        // running alongside the new one and replaying its old callbacks. DOTween.Kill by id is
        // the one kill that does work out of play mode, so every preview sequence carries this.
        private static readonly object PreviewId = new object();

        // The player being previewed, and whoever asked for it. Static because only one preview
        // may exist; the owner is tracked so an inspector closing can end the preview IT started
        // without ending one another inspector started since.
        private static UIAnimationPlayer previewing;
        private static object owner;

        private static Sequence running;
        private static string lastPlayed;

        private static readonly List<Object> previewTargets = new List<Object>();
        private static readonly List<Object> scratch = new List<Object>();

        static UIAnimationPreview()
        {
            // Static state does not survive a domain reload, and entering play mode backs the scene
            // up as it stands - so either one without an unwind first would keep the preview's
            // mid-animation values as if they had been authored.
            AssemblyReloadEvents.beforeAssemblyReload += End;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

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
        ///
        /// A different player ends the current preview first. The same player carries on: the
        /// running sequence stops where it is, and the new one starts from there.
        /// </summary>
        public static void Play(object requester, UIAnimationPlayer player, string animationName, bool fromStateOnly)
        {
            if (player == null || EditorApplication.isPlayingOrWillChangePlaymode) return;

            bool fresh = !IsPreviewing(player);

            if (fresh)
            {
                End();
                Begin(requester, player);
            }
            else
            {
                Halt();
                player.StopAll();

                // Same animation again = start it over from where it started, not from its end.
                player.EditorContinuePreview(animationName == lastPlayed);
                RecordNewTargets(player);

                owner = requester;
            }

            lastPlayed = animationName;

            if (fromStateOnly)
            {
                player.ApplyFromState(animationName);
                SceneView.RepaintAll();
                return;
            }

            Sequence sequence = player.Play(animationName);

            if (sequence == null)
            {
                // Nothing to play. A preview that never moved anything has nothing worth keeping
                // open; one already under way keeps the state the last Play left.
                if (fresh) End();
                return;
            }

            sequence.SetId(PreviewId);

            // clearCallbacks: an animation's On Complete is a UnityEvent wired to arbitrary game
            // code, and firing that outside play mode is not something a preview should do.
            DOTweenEditorPreview.PrepareTweenForPreview(sequence, true, true, false);
            DOTweenEditorPreview.Start(OnPreviewUpdate);

            running = sequence;
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

            // For the whole preview rather than just while the sequence is built: sounds fire from
            // sequence callbacks as it plays, and the shared fallback source cannot exist out of
            // play mode.
            UIAnimationStep.EditorSuppressSound = true;

            previewing = player;
            owner = requester;
        }

        /// <summary>
        /// A target that appeared since the preview began - a slot filled in mid-preview - is not in
        /// the Undo record. It is still at rest at this point, so recording it now is still a
        /// record of its resting state.
        /// </summary>
        private static void RecordNewTargets(UIAnimationPlayer player)
        {
            scratch.Clear();
            player.EditorCollectTargets(scratch);

            for (int i = 0; i < scratch.Count; i++)
            {
                if (previewTargets.Contains(scratch[i])) continue;

                Undo.RecordObject(scratch[i], "UI Animation Preview");
                previewTargets.Add(scratch[i]);
            }

            scratch.Clear();
        }

        /// <summary>Stops the running sequence where it stands, and makes sure it stays stopped.</summary>
        private static void Halt()
        {
            DOTweenEditorPreview.Stop();

            // Paused first so nothing can advance it even if the kill below ever stops working.
            if (running != null && running.IsActive()) running.Pause();
            running = null;

            DOTween.Kill(PreviewId);
        }

        /// <summary>Ends the preview and puts every value back. Safe to call when none is running.</summary>
        public static void End()
        {
            // ReferenceEquals rather than ==: a player deleted mid-preview compares equal to null,
            // but its targets can live elsewhere in the scene and still need putting back.
            if (ReferenceEquals(previewing, null)) return;

            Halt();

            previewing.StopAll();
            previewing.EditorRestoreBaselines();

            UIAnimationStep.EditorSuppressSound = false;

            // The restore wrote through plain property setters, which do not mark a prefab
            // instance's overrides or a scene as needing a resave on their own.
            for (int i = 0; i < previewTargets.Count; i++)
            {
                if (previewTargets[i] != null) EditorUtility.SetDirty(previewTargets[i]);
            }

            previewTargets.Clear();
            previewing = null;
            owner = null;
            lastPlayed = null;

            SceneView.RepaintAll();
        }

        /// <summary>
        /// Ends the preview only if this requester is the one that started it, so an inspector
        /// being closed cannot restore a scene out from under a preview someone else started since.
        /// </summary>
        public static void EndIfOwnedBy(object requester)
        {
            if (!ReferenceEquals(previewing, null) && ReferenceEquals(owner, requester)) End();
        }

        // ---------------------------------------------------------------- shared inspector rows

        private const float ButtonWidth = 48f;

        private static readonly GUIContent PreviewPlayLabel = new GUIContent("Play",
            "Plays this animation on the real scene objects.\n\n" +
            "Carries on from wherever the last preview left things, the way play mode would - so a " +
            "Close can be previewed straight after its Open. Play on the same animation again starts " +
            "it over from where it started.");

        private static readonly GUIContent PreviewResetLabel = new GUIContent("Reset",
            "Jumps to this animation's first frame without playing it: each step with a From snaps " +
            "to it, and steps without one stay where they are.\n\n" +
            "On the animation you just played, rewinds to where that play started first.");

        private static readonly GUIContent PlayLabel = new GUIContent("Play",
            "Plays this animation - the same as calling Play(name) from code.");

        private static readonly GUIContent ResetLabel = new GUIContent("Reset",
            "Snaps each step's From value onto its target without playing anything - the same as " +
            "ApplyFromState(name). Steps without a From stay where they are.");

        private static readonly GUIContent StopLabel = new GUIContent("Stop",
            "Stops this animation where it stands. On Complete does not fire.");

        private static readonly GUIContent StopAllLabel = new GUIContent("Stop All",
            "Stops every animation on this player where it stands. On Complete does not fire.");

        private static readonly GUIContent StopAndRestoreLabel = new GUIContent("Stop and Restore",
            "Ends the preview and puts every value it touched back exactly as it was before the first Play.");

        /// <summary>
        /// One animation's row of buttons: the runtime API in play mode, this preview out of it.
        /// Shared by the player's inspector and the asset's, so the two can never drift apart.
        /// </summary>
        public static void DrawRow(object requester, UIAnimationPlayer player, string animationName)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(animationName, GUILayout.MinWidth(60f));

            if (Application.isPlaying)
            {
                if (GUILayout.Button(PlayLabel, GUILayout.Width(ButtonWidth))) player.Play(animationName);
                if (GUILayout.Button(ResetLabel, GUILayout.Width(ButtonWidth))) player.ApplyFromState(animationName);
                if (GUILayout.Button(StopLabel, GUILayout.Width(ButtonWidth))) player.Stop(animationName);
            }
            else
            {
                if (GUILayout.Button(PreviewPlayLabel, GUILayout.Width(ButtonWidth)))
                {
                    Play(requester, player, animationName, false);
                }

                if (GUILayout.Button(PreviewResetLabel, GUILayout.Width(ButtonWidth)))
                {
                    Play(requester, player, animationName, true);
                }
            }

            EditorGUILayout.EndHorizontal();
        }

        /// <summary>Stop All in play mode; Stop and Restore, enabled only while previewing, out of it.</summary>
        public static void DrawFooter(UIAnimationPlayer player)
        {
            if (Application.isPlaying)
            {
                if (GUILayout.Button(StopAllLabel)) player.StopAll();
                return;
            }

            using (new EditorGUI.DisabledScope(!IsPreviewing(player)))
            {
                if (GUILayout.Button(StopAndRestoreLabel)) End();
            }
        }

        public const string EditModeNote =
            "Play carries on from wherever the last preview left things, as play mode would. Stop and " +
            "Restore puts every value back as it was before the first Play, and the whole preview is " +
            "one Undo step (Ctrl+Z) if it goes wrong. Selecting something else or entering play mode " +
            "stops and restores it too.\n\n" +
            "Sound steps are skipped, and On Complete events do not fire, so nothing in the " +
            "animation can run game code while you are not in play mode.";

        private static void OnPlayModeChanged(PlayModeStateChange change)
        {
            // ExitingEditMode runs before the scene is backed up for play mode, so the restore
            // lands in what play mode starts from and in what it returns to afterwards.
            if (change == PlayModeStateChange.ExitingEditMode) End();
        }

        private static void OnPreviewUpdate()
        {
            // The player was deleted while previewing.
            if (!ReferenceEquals(previewing, null) && previewing == null)
            {
                End();
                return;
            }

            SceneView.RepaintAll();
        }
    }
}

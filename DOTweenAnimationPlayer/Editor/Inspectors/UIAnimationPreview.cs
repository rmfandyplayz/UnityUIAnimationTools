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
using UnityEditor.SceneManagement;
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
    /// instance's overrides on its own, and an unwind on entering play mode, assembly reload and
    /// selection change. The last one waits until the selection leaves the preview altogether:
    /// selecting one of the objects it animates keeps it running, because that is how an object gets
    /// posed. A preview a pop-up animation window started belongs to the window, and a recording
    /// session holds its preview open whatever is selected (Hold).
    ///
    /// Within one player a preview chains: a second Play carries on from where the first ends, so
    /// a Close can be previewed straight after its Open. One still running is finished first, so
    /// where the second starts never depends on when it was clicked. Every Play starts from the
    /// animation's first frame, the one Reset shows - each From applied at once, whatever Apply From
    /// Values Immediately says - and Play on the same animation again starts over from that frame.
    /// Stop and Restore - or any of the unwinds above - goes all the way back to rest.
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

        // Whatever is holding the preview open - a record session - so that no other owner, no selection
        // change and no Stop and Restore button ends it. Only End itself, which play mode and a recompile
        // still call, gets past it.
        private static object holder;

        // The Undo group the resting state was last recorded into: by Begin, or by Hold joining a preview
        // already running - so a record session can fold everything since it started into one step.
        private static int undoGroup = -1;

        /// <summary>
        /// Raised at the end of End, once everything is back at rest - whoever called it. A record session
        /// listens, since a preview ended under it has nothing left to measure from.
        /// </summary>
        public static event System.Action Ended;

        private static readonly List<Object> previewTargets = new List<Object>();
        private static readonly List<Object> scratch = new List<Object>();

        static UIAnimationPreview()
        {
            // Static state does not survive a domain reload, and entering play mode backs the scene
            // up as it stands - so either one without an unwind first would keep the preview's
            // mid-animation values as if they had been authored.
            AssemblyReloadEvents.beforeAssemblyReload += End;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            Selection.selectionChanged += OnSelectionChanged;
            EditorSceneManager.sceneClosing += OnSceneClosing;

            // A scene saved mid-preview would otherwise keep whatever frame the preview was showing - or the
            // pose being recorded - as if it had been authored, and a material instance with it.
            EditorSceneManager.sceneSaving += OnSceneSaving;
            EditorSceneManager.sceneSaved += OnSceneSaved;
            PrefabStage.prefabSaving += OnPrefabSaving;
            PrefabStage.prefabSaved += OnPrefabSaved;
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
        /// The Undo group the running preview last recorded the resting state into, or -1: where it began, or
        /// where Hold joined it. Everything from there is the preview's - or the recording's.
        /// </summary>
        public static int UndoGroup
        {
            get { return ReferenceEquals(previewing, null) ? -1 : undoGroup; }
        }

        /// <summary>
        /// Starts a preview on this player without playing or moving anything - or keeps the one already
        /// running on it, wherever it has got to, refreshed to cover anything added since - and holds it
        /// open for holder: no other owner, selection change or Stop and Restore button ends it until
        /// Release. A preview on another player is ended first. For the record session, which needs the
        /// resting values captured before the user poses anything, and needs them put back afterwards.
        /// False when no preview can run.
        /// </summary>
        public static bool Hold(object holder, UIAnimationPlayer player)
        {
            if (player == null || EditorApplication.isPlayingOrWillChangePlaymode) return false;

            if (IsPreviewing(player))
            {
                // Joined where it stands. Its resting state is recorded again, in a group of its own, so the
                // recording can be one Undo step without taking everything since the first Play with it.
                Refresh(player, true);
            }
            else
            {
                End();
                Begin(holder, player);
            }

            owner = holder;
            UIAnimationPreview.holder = holder;
            return true;
        }

        /// <summary>Lets go of a preview Hold kept open. It keeps running, still owned by holder.</summary>
        public static void Release(object holder)
        {
            if (ReferenceEquals(UIAnimationPreview.holder, holder)) UIAnimationPreview.holder = null;
        }

        /// <summary>
        /// Makes the running preview cover whatever was added since it began - a step, or a target filled
        /// in - by doing what a second Play does before it plays: back to rest, re-initialise, put the
        /// current state back. A running animation is finished first, as Play would.
        /// </summary>
        public static void Refresh(UIAnimationPlayer player)
        {
            Refresh(player, false);
        }

        private static void Refresh(UIAnimationPlayer player, bool recordRest)
        {
            if (!IsPreviewing(player)) return;

            FinishRunning();
            Halt();
            player.StopAll();

            // Undo keeps the state an object is in when it is recorded, and this is the one moment the
            // targets are back at rest.
            System.Action atRest = null;
            if (recordRest)
            {
                atRest = () =>
                {
                    Undo.IncrementCurrentGroup();
                    undoGroup = Undo.GetCurrentGroup();

                    if (previewTargets.Count > 0) Undo.RecordObjects(previewTargets.ToArray(), "UI Animation Preview");
                };
            }

            player.EditorContinuePreview(false, atRest);
            RecordNewTargets(player);

            SceneView.RepaintAll();
        }

        /// <summary>
        /// Plays one animation on real scene objects, or - when fromStateOnly - just snaps its FROM
        /// values on so a starting pose can be eyeballed without watching the whole thing.
        ///
        /// A different player ends the current preview first. The same player carries on: a
        /// different animation starts from where the running one ends (finishing it first if it is
        /// still going), and the same one again starts over from where it last started.
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
                // Same animation again = start it over from where it started, not from its end.
                bool startOver = animationName == lastPlayed;

                // A different one carries on from where the running one was HEADING, not from
                // wherever it happened to be when the button was pressed. Chaining is for judging
                // one animation straight after another, and a Close has to be seen starting from
                // the whole Open - otherwise what it starts from depends on when you clicked.
                if (!startOver) FinishRunning();

                Halt();
                player.StopAll();

                player.EditorContinuePreview(startOver);
                RecordNewTargets(player);

                // A held preview stays with its holder, or an inspector that played on it last could
                // end it from under a recording just by being closed.
                if (holder == null) owner = requester;
            }

            lastPlayed = animationName;

            // Every Play starts from the animation's first frame - exactly what Reset shows - and that
            // frame is then where Play-again starts over from. Without it, an animation with Apply From
            // Values Immediately off played from rest holds each delayed step at rest until the step
            // begins, then jumps: correct play-mode behaviour, but it makes a Close previewed on its
            // own unreadable. In play mode that animation still waits; the README says so.
            player.ApplyFromState(animationName);
            player.EditorMarkPreviewStart();

            if (fromStateOnly)
            {
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

            // The gizmos and the record session measure against rest, which from here on is not what
            // the objects hold.
            UIAnimationSimulation.NoteRest(previewTargets);
            UIAnimationSimulation.NoteRest(player);

            // A group of its own, so a record session can fold everything from here into one Undo step.
            Undo.IncrementCurrentGroup();
            undoGroup = Undo.GetCurrentGroup();

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

            RebuildInspectorsShowingMaterials();
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

            UIAnimationSimulation.NoteRest(scratch);
            UIAnimationSimulation.NoteRest(player);

            for (int i = 0; i < scratch.Count; i++)
            {
                if (previewTargets.Contains(scratch[i])) continue;

                Undo.RecordObject(scratch[i], "UI Animation Preview");
                previewTargets.Add(scratch[i]);
            }

            scratch.Clear();
        }

        /// <summary>
        /// Jumps the running sequence to its end, firing the SetActive callbacks it passes on the way
        /// (sound is suppressed for the whole preview). An endless loop has no end to jump to, so it
        /// is left where it stands. Complete works out of play mode, unlike Kill - see the table in
        /// CLAUDE.md.
        /// </summary>
        private static void FinishRunning()
        {
            if (running == null || !running.IsActive() || running.Loops() == -1) return;

            running.Complete(true);
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

            // Out of play mode a material instance sits on the scene object like any edit, and saving the
            // scene would keep a copy of it, cut off from the material asset.
            previewing.EditorReleaseMaterials();
            RebuildInspectorsShowingMaterials();

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
            holder = null;
            lastPlayed = null;
            undoGroup = -1;

            UIAnimationSimulation.ForgetRest();

            SceneView.RepaintAll();

            if (Ended != null) Ended();
        }

        /// <summary>
        /// Ends the preview only if this requester is the one that started it, so an inspector
        /// being closed cannot restore a scene out from under a preview someone else started since.
        ///
        /// Unless the selection is still on the preview: the player, its shared asset, or one of the
        /// objects it animates. Selecting the ball to drag it into a pose, then coming back to click
        /// Use Current Value, is the workflow, and ending the preview there restored the ball and left
        /// Baseline and Current nothing to measure against. The preview then belongs to nobody, and
        /// OnSelectionChanged ends it once the selection leaves.
        /// </summary>
        public static void EndIfOwnedBy(object requester)
        {
            EndIfOwnedBy(requester, false);
        }

        /// <summary>
        /// As above, but evenIfSelected ends it wherever the selection is - for a pop-up animation window
        /// being closed, which is a deliberate act, where a selection change is a side effect.
        /// </summary>
        public static void EndIfOwnedBy(object requester, bool evenIfSelected)
        {
            if (ReferenceEquals(previewing, null) || !ReferenceEquals(owner, requester)) return;

            // Held open by someone else - a recording - which ends it itself.
            if (holder != null && !ReferenceEquals(holder, requester))
            {
                owner = holder;
                return;
            }

            if (!evenIfSelected && SelectionIsOnPreview())
            {
                owner = null;
                return;
            }

            End();
        }

        /// <summary>
        /// Ends a preview nobody owns once the selection leaves it. One with an owner is left to that
        /// owner: an inspector that still exists - a locked one, say - still shows the player, and
        /// ends the preview itself when it goes. A plain object as the owner (a test harness) is never
        /// disabled, so it keeps its preview through any selection change, as it always did.
        /// </summary>
        private static void OnSelectionChanged()
        {
            if (ReferenceEquals(previewing, null) || owner != null || holder != null) return;

            if (!SelectionIsOnPreview()) End();
        }

        private static bool SelectionIsOnPreview()
        {
            if (previewing == null) return false;

            Object[] selected = Selection.objects;

            for (int i = 0; i < selected.Length; i++)
            {
                Object item = selected[i];
                if (item == null) continue;

                if (item == previewing.EditorShared) return true;

                var component = item as Component;
                GameObject selectedObject = component != null ? component.gameObject : item as GameObject;
                if (selectedObject == null) continue;

                if (selectedObject == previewing.gameObject) return true;

                for (int t = 0; t < previewTargets.Count; t++)
                {
                    Object animated = previewTargets[t];
                    if (animated == null) continue;

                    var animatedComponent = animated as Component;
                    GameObject animatedObject = animatedComponent != null ? animatedComponent.gameObject : animated as GameObject;
                    if (animatedObject == selectedObject) return true;
                }
            }

            return false;
        }

        // ---------------------------------------------------------------- shared inspector rows

        private const float ButtonWidth = 48f;

        private static readonly GUIContent PreviewPlayLabel = new GUIContent("Play",
            "Plays this animation on the real scene objects, from its first frame - the one Reset shows.\n\n" +
            "Carries on from where the last animation ends - so a Close can be previewed straight after " +
            "its Open. One still playing is finished first. Play on the same animation again starts it " +
            "over.");

        private static readonly GUIContent PreviewResetLabel = new GUIContent("Reset",
            "Jumps to this animation's first frame without playing it: each step with a From snaps " +
            "to it, and steps without one stay where they are. Play then runs it from that frame.\n\n" +
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

        private static readonly GUIContent PlayModeRestoreLabel = new GUIContent("Stop and Restore",
            "Stops every animation on this player and puts back every value its steps animate as it was when the " +
            "player started up - the values Baseline measures from. A play-mode testing aid: there is no code " +
            "equivalent, and it is not undone when play mode ends, since nothing in play mode is kept anyway.");

        private const string PlayModeNote =
            "In play mode these buttons call the player itself, as your code would. An animation with no From plays " +
            "from wherever things are, so once it has played, Play again has nowhere left to go, and Reset only " +
            "moves steps that have a From. Stop and Restore puts everything back to replay it.";

        private static readonly GUIContent StopAndRestoreLabel = new GUIContent("Stop and Restore",
            "Ends the preview and puts every value it touched back exactly as it was before the first Play.");

        private static readonly GUIContent HeldLabel = new GUIContent("Recording - Finish or Cancel in its window",
            "A pop-up animation window is recording values from this preview. Its Finish button keeps them and its " +
            "Cancel button drops them; either one ends the preview and puts every value back.");

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
                EditorGUILayout.BeginHorizontal();

                if (GUILayout.Button(StopAllLabel)) player.StopAll();

                if (GUILayout.Button(PlayModeRestoreLabel))
                {
                    player.StopAll();
                    player.EditorRestoreBaselines();
                }

                EditorGUILayout.EndHorizontal();
                EditorGUILayout.HelpBox(PlayModeNote, MessageType.None);
                return;
            }

            if (holder != null && IsPreviewing(player))
            {
                // The recording's Finish and Cancel end it, and say which of the two happens to the pose.
                using (new EditorGUI.DisabledScope(true))
                {
                    GUILayout.Button(HeldLabel);
                }

                return;
            }

            using (new EditorGUI.DisabledScope(!IsPreviewing(player)))
            {
                if (GUILayout.Button(StopAndRestoreLabel)) End();
            }
        }

        public const string EditModeNote =
            "Play starts from the animation's first frame (what Reset shows) and otherwise carries on " +
            "from where the last animation ends, finishing it first if it is still playing. Play on the " +
            "same animation again starts it over. Stop and " +
            "Restore puts every value back as it was before the first Play, and the whole preview is " +
            "one Undo step (Ctrl+Z) if it goes wrong. Selecting something the preview does not animate, " +
            "or entering play mode, stops and restores it too - selecting one of the objects it animates " +
            "keeps it running, and so does a pop-up animation window that started it, until it is closed.\n\n" +
            "Sound steps are skipped, and On Complete events do not fire, so nothing in the " +
            "animation can run game code while you are not in play mode.";

        /// <summary>
        /// A scene closing with the previewed player in it takes the objects with it, so the preview has to
        /// end first - or a recording would be left holding a player that is gone.
        /// </summary>
        private static void OnSceneClosing(UnityEngine.SceneManagement.Scene scene, bool removingScene)
        {
            if (previewing != null && previewing.gameObject.scene == scene) End();
        }

        // The player parked for a save in progress, so the same one is put back afterwards.
        private static UIAnimationPlayer parked;

        /// <summary>
        /// A scene being saved with the preview running in it is written with every animated value at rest and
        /// every Graphic's own material, then put back exactly as it was (OnSceneSaved): the preview, and any
        /// recording, carries on as if nothing happened. Through plain property setters, so nothing is recorded
        /// for Undo and the scene is not marked as changed again once saved.
        /// </summary>
        private static void OnSceneSaving(UnityEngine.SceneManagement.Scene scene, string path)
        {
            if (previewing != null && previewing.gameObject.scene == scene) Park();
        }

        private static void OnSceneSaved(UnityEngine.SceneManagement.Scene scene)
        {
            Unpark();
        }

        private static void OnPrefabSaving(GameObject root)
        {
            if (previewing != null && root != null && previewing.gameObject.scene == root.scene) Park();
        }

        private static void OnPrefabSaved(GameObject root)
        {
            Unpark();
        }

        private static void Park()
        {
            if (parked != null) return;

            parked = previewing;
            parked.EditorPark();

            // In case the save fails before saying it has saved.
            EditorApplication.delayCall -= Unpark;
            EditorApplication.delayCall += Unpark;
        }

        private static void Unpark()
        {
            if (parked == null) return;

            UIAnimationPlayer player = parked;
            parked = null;

            if (player == previewing) player.EditorUnpark();
        }

        /// <summary>
        /// The Inspector shows a Graphic's material at the bottom, built for whichever material it had when the
        /// Inspector was built - so once the preview has given it an instance, or taken one back, an Inspector
        /// showing it still edits the old one, and editing the asset there while the instance is on screen
        /// looks like it does nothing. Rebuilt after the change, and only when a selected object is affected,
        /// so the player's own Inspector is not rebuilt from under a click.
        /// </summary>
        private static void RebuildInspectorsShowingMaterials()
        {
            bool shown = false;
            GameObject[] selected = Selection.gameObjects;

            for (int i = 0; i < previewTargets.Count && !shown; i++)
            {
                var instance = previewTargets[i] as UIMaterialInstance;
                if (instance == null) continue;

                for (int s = 0; s < selected.Length; s++)
                {
                    if (selected[s] == instance.gameObject) shown = true;
                }
            }

            if (!shown) return;

            EditorApplication.delayCall -= RebuildInspectors;
            EditorApplication.delayCall += RebuildInspectors;
        }

        private static void RebuildInspectors()
        {
            ActiveEditorTracker.sharedTracker.ForceRebuild();
        }

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

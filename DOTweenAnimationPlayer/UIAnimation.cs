// -----------------------------------------------------------------------------
// UI Animation Utility
//
// AI-GENERATED. Authored by Claude (Anthropic) via Claude Code, September 2026,
// to a written design brief by the project author.
// See the README.md beside this file for usage.
// -----------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.Events;

namespace rmf_claude.DOTweenUI
{

    /// <summary>
    /// A named animation: an ordered list of steps plus playback options.
    /// The framework attaches no meaning to the name - "Show" and "Wobble" are treated identically.
    /// </summary>
    [Serializable]
    public class UIAnimation
    {
        [Tooltip("The name you pass to Play(). Must be unique on this player.")]
        public string Name = "New Animation";

        [Tooltip("Free-text note for yourself. What this animates, where it is played from, " +
                 "anything that would otherwise be lost. Never read by the framework.")]
        [TextArea(2, 6)]
        public string Notes;

        [Tooltip("Runs top to bottom. Each step is appended after, or joined alongside, the one above it.")]
        public List<UIAnimationStep> Steps = new List<UIAnimationStep>();

        [Tooltip("How many times the whole animation plays. 1 = play once (the normal case). " +
                 "2 = play twice. -1 = loop forever until stopped. 0 is not valid and is treated as 1.")]
        public int Loops = 1;

        [Tooltip("Only has an effect when Loops is not 1.\n\n" +
                 "Restart = jump back to the start each loop.\n" +
                 "Yoyo = play forwards, then backwards, then forwards again.\n" +
                 "Incremental = each loop starts where the last one ended and repeats the same " +
                 "CHANGE rather than the same values, so a +50 move keeps climbing: 50, 100, 150. " +
                 "It reads the step's From/To as a difference, so it only makes sense on steps " +
                 "that move by an amount, and does nothing useful on one that fades to a fixed 1.")]
        public LoopType LoopType = LoopType.Restart;

        // Drawn with FPS beside it on the same row, shown while this is ticked.
        [UIAnimationInlineValue("FPS")]
        [Tooltip("Play in discrete frames instead of smoothly, for a stop-motion or flipbook look. " +
                 "Every step still starts and lands on exactly the same values - only the movement " +
                 "in between is stepped. Tick it and type the frame rate in the box beside it.")]
        public bool PlayAtCustomFPS;

        // Shown beside Play At Custom FPS rather than as a row of its own.
        [HideInInspector]
        [Tooltip("Frames per second to step at. 12 is the classic hand-drawn look, 24 is film, " +
                 "6 to 8 is very chunky. Setting it above the display refresh rate does nothing.\n" +
                 "Every step shares one frame grid, so staggered steps tick together.\n" +
                 "Punch and shake steps are never stepped - they drive their own oscillation.")]
        public float FPS = 12f;

        // Drawn together with SnapPerStep as one DISABLED / ALL STEPS / PER STEP button.
        [UIAnimationSnapMode("SnapPerStep")]
        [Tooltip("Round positions and sizes to whole units every frame, on every step that moves or " +
                 "resizes something. Useful for pixel art; causes visible stepping on a slow move otherwise.\n\n" +
                 "Scale and rotation are never snapped - DOTween has no option for them.")]
        public bool Snapping;

        // Shown through Snapping's button rather than as a box of its own.
        [HideInInspector]
        [Tooltip("Choose snapping step by step instead. Each position or size step then shows its own " +
                 "Snapping box, and the animation-wide Snapping is ignored.\n\n" +
                 "A step whose own box is ticked always snaps, so its box stays visible either way.")]
        public bool SnapPerStep;

        [Tooltip("Snap every FROM value to its target as soon as the animation starts, rather than " +
                 "when each individual step begins. Prevents a visible flash on delayed steps.")]
        public bool ApplyFromValuesImmediately = true;

        [Tooltip("Kill every other animation on this player before starting. Turn off for " +
                 "layered animations such as a looping pulse or a hover tint.")]
        public bool InterruptOthers = true;

        [Tooltip("Fires when the animation finishes naturally. Does NOT fire if it is interrupted, " +
                 "stopped, or killed because the GameObject was disabled.")]
        public UnityEvent OnComplete;

        /// <summary>Effective loop count. Guards the meaningless 0 that a zero-initialised list element produces.</summary>
        public int EffectiveLoops
        {
            get { return Loops == 0 ? 1 : Loops; }
        }

        /// <summary>
        /// Frames per second to quantise playback to, or 0 for smooth playback.
        /// The frame rate is resolved here and nowhere else - steps already take one as a build
        /// parameter, so a per-step override would only have to change this single lookup.
        /// </summary>
        public float EffectiveFrameRate
        {
            get { return PlayAtCustomFPS && FPS > 0f ? FPS : 0f; }
        }

        /// <summary>
        /// Whether the animation-wide Snapping applies. A step also snaps when its own box is ticked,
        /// whatever this says: steps carried Snapping before animations did, so an animation that
        /// predates the animation-wide box has it off and keeps snapping exactly the steps it did.
        /// </summary>
        public bool SnapsEveryStep
        {
            get { return Snapping && !SnapPerStep; }
        }

        // Runtime state. One live Sequence per animation, owned here.
        [NonSerialized] public Sequence RuntimeSequence;
        [NonSerialized] public Action RuntimeCallback;

        /// <summary>
        /// The current play's end callback, already wrapped in its own fire-once guard by
        /// UIAnimationPlayer. Null when nothing is armed.
        ///
        /// This holds the WRAPPER rather than the caller's delegate on purpose: the guard has to
        /// belong to one play, not to the animation. A callback that starts the same animation
        /// again replaces everything here mid-teardown, and a guard living on the animation would
        /// then let the old play's kill fire the new play's callback.
        /// </summary>
        [NonSerialized] public Action<UIAnimationEndReason> RuntimeEndCallback;

        /// <summary>
        /// Why the current play is ending, read by the sequence's OnKill backstop.
        ///
        /// Set to Stopped when a play is armed, so a kill from outside the player - DOTween.KillAll,
        /// DOTween.Clear - never reports itself as a natural finish. UIAnimationPlayer.Kill
        /// overwrites it with something more specific before killing.
        /// </summary>
        [NonSerialized] public UIAnimationEndReason PendingEndReason;

        public bool IsPlaying
        {
            get { return RuntimeSequence != null && RuntimeSequence.IsActive() && RuntimeSequence.IsPlaying(); }
        }

        public bool HasLiveSequence
        {
            get { return RuntimeSequence != null && RuntimeSequence.IsActive(); }
        }

        /// <summary>
        /// An independent copy for one player to own and mutate.
        ///
        /// UIAnimationPlayer clones every animation it takes from a shared UIAnimationAsset, and
        /// that is a correctness requirement rather than an optimisation: RuntimeSequence,
        /// RuntimeCallback and each step's resolved targets are ordinary instance fields, so two
        /// players reading the same asset would overwrite each other's live sequences and animate
        /// each other's objects.
        ///
        /// JsonUtility, not EditorJsonUtility - see UIAnimationContextMenu for why that matters,
        /// and note this one has to work in a build too. OnComplete is re-pointed at the original
        /// UnityEvent rather than round-tripped: its persistent call list is Unity's own
        /// serialization detail, and a listener list is read-only at runtime, so sharing the one
        /// instance is both safer and cheaper than copying it.
        /// </summary>
        public UIAnimation CloneForRuntime()
        {
            var clone = new UIAnimation();
            JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(this), clone);
            clone.OnComplete = OnComplete;
            return clone;
        }

        /// <summary>
        /// Fills in fields still at their zero value. Unity does not run C# field initialisers when
        /// you press + on a serialized list, so a fresh animation arrives with Loops 0 and no name.
        /// Only ever fills blanks. Editor-only, driven from UIAnimationPlayer.OnValidate.
        /// </summary>
        public void FillUnsetDefaults()
        {
            if (Loops == 0) Loops = 1;
            if (FPS == 0f) FPS = 12f;
            if (string.IsNullOrEmpty(Name)) Name = "New Animation";

            for (int i = 0; i < Steps.Count; i++)
            {
                if (Steps[i] != null) Steps[i].FillUnsetDefaults();
            }
        }
    }
}

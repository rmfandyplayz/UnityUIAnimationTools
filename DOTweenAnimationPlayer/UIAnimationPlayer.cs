// -----------------------------------------------------------------------------
// UI Animation Utility
//
// AI-GENERATED. Authored by Claude (Anthropic) via Claude Code, September 2026,
// to a written design brief by the project author. Not hand-written by the
// Twindrill Goose team. See the README.md beside this file for usage.
// -----------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

namespace rmf_claude.DOTweenUI
{

    /// <summary>
    /// How a play of an animation ended. Reported by the Play overload that takes an
    /// Action&lt;UIAnimationEndReason&gt;, which always fires exactly once whatever happens.
    ///
    /// Serialized as an integer wherever anyone stores one - see the note on UIAnimationStepType.
    /// The numbers are the contract; new reasons take the next free number.
    /// </summary>
    public enum UIAnimationEndReason
    {
        /// <summary>Ran to its natural end. The only reason that also fires On Complete.</summary>
        Completed = 0,

        /// <summary>Another Play cut it short - the same animation restarting, or one with Interrupt Others.</summary>
        Interrupted = 1,

        /// <summary>Stop, StopAll, or a kill issued from outside this player.</summary>
        Stopped = 2,

        /// <summary>The GameObject or the player component was disabled while it was running.</summary>
        Disabled = 3,

        /// <summary>The player was destroyed while it was still running.</summary>
        Destroyed = 4,

        /// <summary>
        /// No animation of that name exists on this player or its shared asset, so nothing played.
        /// A typo is a runtime failure like any other, and a caller waiting on a callback that
        /// never comes is exactly what this overload exists to prevent.
        /// </summary>
        NotFound = 5,
    }

    /// <summary>
    /// Plays named, inspector-authored DOTween animations on UI elements.
    ///
    /// The framework has no idea what any animation name means - it just builds and plays
    /// whatever steps you authored. Drive it from your own scripts:
    ///
    ///     player.Play("Show");
    ///     player.Play("Hide", () => gameObject.SetActive(false));
    ///
    /// See README.md in this folder for the authoring workflow.
    /// </summary>
    [DisallowMultipleComponent]
    public class UIAnimationPlayer : MonoBehaviour
    {
        [Tooltip("Run animations on unscaled time so UI still animates while Time.timeScale is 0 (pause menus).")]
        [SerializeField] private bool UseUnscaledTime = true;

        [Tooltip("Kill running animations when this GameObject is disabled. Completion callbacks do not fire.")]
        [SerializeField] private bool KillOnDisable = true;

        [Tooltip("Optional shared animation library. Animations from the asset are added to the ones " +
                 "below, and a local animation of the same name wins - that is how one object overrides " +
                 "a single animation out of a shared set. Leave it empty and nothing changes.")]
        [SerializeField] private UIAnimationAsset Shared;

        [SerializeField] private List<UIAnimation> Animations = new List<UIAnimation>();

        private readonly Dictionary<string, int> lookup = new Dictionary<string, int>();

        // What actually plays: the local animations, then any from Shared whose name is not
        // already taken. Rebuilt by Initialize. With no Shared asset this is the local list,
        // same objects in the same order, so nothing about an existing player changes.
        private readonly List<UIAnimation> runtime = new List<UIAnimation>();

        // Scratch space for the timeline pass in BuildSequence. Reused so playing an animation
        // does not allocate.
        private readonly List<float> stepStarts = new List<float>();

        private bool initialized;

        /// <summary>True while any animation on this player is running.</summary>
        public bool IsAnyPlaying
        {
            get
            {
                for (int i = 0; i < runtime.Count; i++)
                {
                    if (runtime[i].IsPlaying) return true;
                }

                return false;
            }
        }

        private void Awake()
        {
            Initialize();
        }

        private void OnDisable()
        {
            if (KillOnDisable) StopAll(false, UIAnimationEndReason.Disabled);
        }

        /// <summary>
        /// Anything still live here was not killed on disable, because Kill On Disable is off or
        /// the object was already inactive. Note that destroying an ENABLED object reports
        /// Disabled rather than Destroyed: Unity runs OnDisable first and gives no way to know a
        /// destroy is coming, so that is reported honestly rather than guessed at.
        /// </summary>
        private void OnDestroy()
        {
            StopAll(false, UIAnimationEndReason.Destroyed);
        }

    #if UNITY_EDITOR
        /// <summary>
        /// Unity does not run C# field initialisers when you press + on a serialized list, so a new
        /// animation arrives with Loops 0 and a new step with Duration 0 / Ease Unset. This fills
        /// those blanks in. It only ever writes to fields still at their zero value, so it can never
        /// overwrite something you authored.
        /// </summary>
        private void OnValidate()
        {
            for (int i = 0; i < Animations.Count; i++)
            {
                if (Animations[i] != null) Animations[i].FillUnsetDefaults();
            }
        }
    #endif

        // ---------------------------------------------------------------- public API

        /// <summary>Plays a named animation. Returns the live Sequence, or null if the name is unknown.</summary>
        public Sequence Play(string animationName)
        {
            return PlayInternal(animationName, null, null);
        }

        /// <summary>
        /// Plays a named animation and invokes onComplete when it finishes naturally.
        /// The callback does NOT fire if the animation is interrupted, stopped, or killed on disable.
        /// Use the Action&lt;UIAnimationEndReason&gt; overload when you need one that always fires.
        /// </summary>
        public Sequence Play(string animationName, Action onComplete)
        {
            return PlayInternal(animationName, onComplete, null);
        }

        /// <summary>
        /// Plays a named animation and invokes onEnd EXACTLY ONCE however it ends - completed,
        /// interrupted, stopped, disabled, destroyed, or never started because the name is unknown.
        ///
        /// This is the overload to sequence game logic against:
        ///
        ///     player.Play("Hide", reason =&gt; Destroy(gameObject));
        ///
        /// leaks nothing when something interrupts the hide, which the Action overload above does.
        /// Check the reason when it matters - only Completed means the animation actually finished.
        ///
        /// The authored On Complete UnityEvent and the plain Action overload are untouched by this
        /// and still fire on a natural finish only, because that is what "finished" means to
        /// someone who authored an event in the Inspector. onEnd fires last, after both of them,
        /// so an authored On Complete still runs before a caller's Destroy.
        ///
        /// Note that Play(name, null) is now ambiguous and needs a cast to pick an overload.
        /// </summary>
        public Sequence Play(string animationName, Action<UIAnimationEndReason> onEnd)
        {
            return PlayInternal(animationName, null, onEnd);
        }

        private Sequence PlayInternal(string animationName, Action onComplete, Action<UIAnimationEndReason> onEnd)
        {
            Initialize();

            UIAnimation animation = Find(animationName);
            if (animation == null)
            {
                if (onEnd != null) onEnd.Invoke(UIAnimationEndReason.NotFound);
                return null;
            }

            Kill(animation, false, UIAnimationEndReason.Interrupted);

            if (animation.InterruptOthers)
            {
                for (int i = 0; i < runtime.Count; i++)
                {
                    if (runtime[i] != animation) Kill(runtime[i], false, UIAnimationEndReason.Interrupted);
                }
            }

            Sequence sequence = BuildSequence(animation);
            if (sequence == null)
            {
                // Nothing to play. Fire the callbacks anyway so caller logic never hangs.
                if (onComplete != null) onComplete.Invoke();
                if (animation.OnComplete != null) animation.OnComplete.Invoke();
                if (onEnd != null) onEnd.Invoke(UIAnimationEndReason.Completed);
                return null;
            }

            animation.RuntimeSequence = sequence;
            animation.RuntimeCallback = onComplete;

            // The reason for a kill nobody here issued - DOTween.KillAll, DOTween.Clear. Every
            // path that knows better overwrites it before killing.
            animation.PendingEndReason = UIAnimationEndReason.Stopped;

            UIAnimation captured = animation;

            // The fire-once flag is a local captured by THIS play's closure, not a field on the
            // animation. An onEnd that plays the same animation again re-arms the animation while
            // this play is still tearing down, and a flag living on the animation would then let
            // this play's teardown fire the next play's callback.
            bool ended = false;
            Action<UIAnimationEndReason> fire = reason =>
            {
                if (ended) return;
                ended = true;

                if (onEnd != null) onEnd.Invoke(reason);
            };

            animation.RuntimeEndCallback = fire;

            sequence.OnComplete(() =>
            {
                captured.RuntimeSequence = null;
                Action callback = captured.RuntimeCallback;
                captured.RuntimeCallback = null;

                // Disarmed before the callbacks run, so a callback that starts a new play of this
                // animation arms its own and keeps it.
                if (ReferenceEquals(captured.RuntimeEndCallback, fire)) captured.RuntimeEndCallback = null;

                if (callback != null) callback.Invoke();
                if (captured.OnComplete != null) captured.OnComplete.Invoke();

                fire(UIAnimationEndReason.Completed);
            });

            // The backstop for a kill this player did not issue. Measured against DOTween 1.3.030
            // in play mode, OnKill fires synchronously for tween.Kill(), DOTween.KillAll() and
            // DOTween.Clear() alike, so this covers all of them; Kill() below sets the reason
            // before killing, and the fire-once flag makes whichever of the two arrives second a
            // no-op. Out of play mode DOTween's update loop never runs and a kill never despawns,
            // so this never fires there - which is fine, because the preview strips callbacks and
            // passes no onEnd anyway.
            sequence.OnKill(() => fire(captured.PendingEndReason));

            sequence.SetUpdate(UseUnscaledTime);

            // Must be applied to the outer Sequence - SetLink is a no-op on tweens inside one.
            sequence.SetLink(gameObject, LinkBehaviour.KillOnDestroy);

            if (animation.EffectiveLoops != 1) sequence.SetLoops(animation.EffectiveLoops, animation.LoopType);

            sequence.Play();
            return sequence;
        }

        /// <summary>
        /// Plays a named animation, discarding the Sequence so that this returns void.
        ///
        /// This exists for Inspector UnityEvents. Unity only lists methods that return void and
        /// take at most one argument, which rules out both Play overloads - and a void Play(string)
        /// cannot be an overload, because C# will not overload on return type alone.
        /// From code, call Play instead: it hands back the Sequence.
        /// </summary>
        public void PlayAnimation(string animationName)
        {
            PlayInternal(animationName, null, null);
        }

        /// <summary>Stops one animation. complete=true jumps to the end state and fires its callbacks.</summary>
        public void Stop(string animationName, bool complete = false)
        {
            UIAnimation animation = Find(animationName);
            if (animation != null) Kill(animation, complete, UIAnimationEndReason.Stopped);
        }

        /// <summary>
        /// Stops a named animation where it stands, without firing its callbacks.
        ///
        /// The UnityEvent-friendly form of Stop. An optional parameter is still a parameter, so
        /// Stop(string, bool) reads as two arguments and Unity does not offer it in the Inspector.
        /// </summary>
        public void StopAnimation(string animationName)
        {
            Stop(animationName);
        }

        /// <summary>Stops every animation on this player.</summary>
        public void StopAll(bool complete = false)
        {
            StopAll(complete, UIAnimationEndReason.Stopped);
        }

        private void StopAll(bool complete, UIAnimationEndReason reason)
        {
            for (int i = 0; i < runtime.Count; i++)
            {
                Kill(runtime[i], complete, reason);
            }
        }

        public bool IsPlaying(string animationName)
        {
            UIAnimation animation = FindQuiet(animationName);
            return animation != null && animation.IsPlaying;
        }

        public bool Has(string animationName)
        {
            Initialize();
            return lookup.ContainsKey(animationName);
        }

        /// <summary>
        /// Snaps every FROM value of an animation onto its targets without playing anything.
        /// ApplyFromState("Show") in Awake is how you start a panel hidden without authoring a
        /// separate state for it.
        /// </summary>
        public void ApplyFromState(string animationName)
        {
            Initialize();

            UIAnimation animation = Find(animationName);
            if (animation == null) return;

            for (int i = 0; i < animation.Steps.Count; i++)
            {
                animation.Steps[i].ApplyFromValue();
            }
        }

        /// <summary>
        /// Re-reads the resting values that Baseline endpoints are relative to.
        /// Call after deliberately moving an element at runtime. Do not call mid-animation.
        /// </summary>
        public void CaptureBaseline()
        {
            Initialize();

            for (int i = 0; i < runtime.Count; i++)
            {
                List<UIAnimationStep> steps = runtime[i].Steps;
                for (int s = 0; s < steps.Count; s++)
                {
                    steps[s].CaptureBaseline();
                }
            }
        }

        // ---------------------------------------------------------------- internals

        private void Initialize()
        {
            if (initialized) return;
            initialized = true;

            lookup.Clear();
            BuildRuntimeList();

            for (int i = 0; i < runtime.Count; i++)
            {
                UIAnimation animation = runtime[i];

                if (string.IsNullOrEmpty(animation.Name))
                {
                    Debug.LogWarning("UIAnimationPlayer on '" + name + "': animation " + i + " has no name.", this);
                }
                else if (lookup.ContainsKey(animation.Name))
                {
                    Debug.LogWarning(
                        "UIAnimationPlayer on '" + name + "': duplicate animation name '" + animation.Name +
                        "'. The first one wins.", this);
                }
                else
                {
                    lookup.Add(animation.Name, i);
                }

                List<UIAnimationStep> steps = animation.Steps;
                for (int s = 0; s < steps.Count; s++)
                {
                    steps[s].Resolve(gameObject);
                }
            }

            // Baselines are captured after every target is resolved and before anything animates,
            // so all steps sharing a target agree on the same resting value.
            for (int i = 0; i < runtime.Count; i++)
            {
                List<UIAnimationStep> steps = runtime[i].Steps;
                for (int s = 0; s < steps.Count; s++)
                {
                    steps[s].CaptureBaseline();
                }
            }
        }

        /// <summary>
        /// Merges the local animations with the shared asset's. Local first and local wins on a
        /// name collision, which is what makes the asset a library you can override one animation
        /// out of rather than an all-or-nothing swap.
        ///
        /// Shared animations are CLONED. They have to be: the resolved targets and the live
        /// sequence are instance fields, so two players sharing one asset would otherwise fight
        /// over both. The cost is that editing the asset at runtime does not reach players that
        /// have already initialised.
        /// </summary>
        private void BuildRuntimeList()
        {
            runtime.Clear();

            for (int i = 0; i < Animations.Count; i++)
            {
                if (Animations[i] != null) runtime.Add(Animations[i]);
            }

            if (Shared == null) return;

            List<UIAnimation> sharedAnimations = Shared.Animations;

            for (int i = 0; i < sharedAnimations.Count; i++)
            {
                UIAnimation source = sharedAnimations[i];
                if (source == null || string.IsNullOrEmpty(source.Name)) continue;

                if (HasLocal(source.Name)) continue;

                runtime.Add(source.CloneForRuntime());
            }
        }

        private bool HasLocal(string animationName)
        {
            for (int i = 0; i < Animations.Count; i++)
            {
                if (Animations[i] != null && Animations[i].Name == animationName) return true;
            }

            return false;
        }

        /// <summary>
        /// Lays every step out on an explicit timeline, then Inserts it at a position, rather than
        /// using Append/Join. The positions are the ones Append/Join would produce anyway, but doing
        /// the arithmetic here keeps a step's Delay applied in exactly one place.
        ///
        /// That matters: verified against the loaded DOTween, a tween's own SetDelay is added ON TOP
        /// of its insert position rather than instead of it. So BuildTween deliberately never calls
        /// SetDelay, and the delay is folded into the position here.
        /// </summary>
        private Sequence BuildSequence(UIAnimation animation)
        {
            List<UIAnimationStep> steps = animation.Steps;
            if (steps.Count == 0) return null;

            stepStarts.Clear();

            float total = 0f;
            float groupStart = 0f;

            for (int i = 0; i < steps.Count; i++)
            {
                UIAnimationStep step = steps[i];

                // A step group starts with an AfterPrevious step and gathers the WithPrevious
                // steps below it. Joined steps offset from the group start, not from each other.
                if (i > 0 && step.Start == UIAnimationStartMode.AfterPrevious) groupStart = total;

                stepStarts.Add(groupStart + step.Delay);
                total = Mathf.Max(total, groupStart + step.TotalDuration);
            }

            Sequence sequence = DOTween.Sequence();
            sequence.SetAutoKill(true);

            // Resolved once here, so a future per-step override changes only this line. Steps that
            // happen at a point in time rather than over one keep their exact authored position -
            // snapping those to the grid would silently collapse a sub-frame stagger.
            float frameRate = animation.EffectiveFrameRate;

            bool anyContent = false;

            for (int i = 0; i < steps.Count; i++)
            {
                UIAnimationStep step = steps[i];
                float at = stepStarts[i];

                if (UIAnimationStep.IsInstant(step.Type))
                {
                    // SetActive and PlaySound happen at a point in the timeline rather than over
                    // a span of it, so they are callbacks rather than tweens.
                    UIAnimationStep captured = step;

                    if (step.Type == UIAnimationStepType.SetActive)
                    {
                        sequence.InsertCallback(at, () => captured.ApplyActiveValue());
                    }
                    else
                    {
                        sequence.InsertCallback(at, () => captured.PlaySound());
                    }

                    anyContent = true;
                }
                else
                {
                    string context = "UIAnimationPlayer on '" + name + "' animation '" + animation.Name + "' step " + i;
                    Tween tween = step.BuildTween(animation.ApplyFromValuesImmediately, frameRate, at, context);
                    if (tween == null) continue;

                    sequence.Insert(at, tween);
                    anyContent = true;
                }
            }

            if (!anyContent)
            {
                sequence.Kill();
                return null;
            }

            return sequence;
        }

        private void Kill(UIAnimation animation, bool complete, UIAnimationEndReason reason)
        {
            if (animation.RuntimeSequence == null) return;

            Sequence sequence = animation.RuntimeSequence;
            Action<UIAnimationEndReason> armed = animation.RuntimeEndCallback;

            // Read by the sequence's OnKill, which DOTween runs from inside the Kill below, so
            // that path reports this kill's reason rather than the armed default.
            animation.PendingEndReason = reason;

            if (!complete)
            {
                // Drop the callbacks first so an interrupted animation never reports completion.
                animation.RuntimeSequence = null;
                animation.RuntimeCallback = null;
            }

            if (sequence.IsActive()) sequence.Kill(complete);

            // Killing runs the sequence's own callbacks synchronously, and those can start a new
            // play of this same animation. Only clear what still belongs to the play being killed.
            if (ReferenceEquals(animation.RuntimeSequence, sequence))
            {
                animation.RuntimeSequence = null;
                animation.RuntimeCallback = null;
            }

            if (armed == null) return;

            if (ReferenceEquals(animation.RuntimeEndCallback, armed)) animation.RuntimeEndCallback = null;

            // In play mode the sequence has already resolved this itself - through OnComplete for
            // complete = true, through OnKill otherwise - and the delegate's fire-once flag makes
            // this a no-op. It does the work out of play mode, where DOTween runs no update loop
            // and a kill never despawns, and for any sequence whose callbacks were stripped.
            armed.Invoke(complete ? UIAnimationEndReason.Completed : reason);
        }

        private UIAnimation Find(string animationName)
        {
            UIAnimation animation = FindQuiet(animationName);
            if (animation == null)
            {
                Debug.LogWarning("UIAnimationPlayer on '" + name + "': no animation named '" + animationName + "'.", this);
            }

            return animation;
        }

        private UIAnimation FindQuiet(string animationName)
        {
            Initialize();

            int index;
            if (animationName == null || !lookup.TryGetValue(animationName, out index)) return null;
            return runtime[index];
        }

    #if UNITY_EDITOR
        /// <summary>Editor-only accessor for the inspector play buttons. Local animations only.</summary>
        public List<UIAnimation> EditorAnimations
        {
            get { return Animations; }
        }

        /// <summary>Editor-only accessor, so the asset inspector can tell which players use it.</summary>
        public UIAnimationAsset EditorShared
        {
            get { return Shared; }
        }

        /// <summary>
        /// Every animation name this player can play, local first, then the shared asset's minus
        /// anything a local animation shadows. Editor-only, for the inspector preview list, which
        /// needs the names before a preview has initialised anything.
        /// </summary>
        public void EditorCollectAnimationNames(List<string> into)
        {
            for (int i = 0; i < Animations.Count; i++)
            {
                if (Animations[i] == null || string.IsNullOrEmpty(Animations[i].Name)) continue;
                if (!into.Contains(Animations[i].Name)) into.Add(Animations[i].Name);
            }

            if (Shared == null) return;

            for (int i = 0; i < Shared.Animations.Count; i++)
            {
                UIAnimation animation = Shared.Animations[i];
                if (animation == null || string.IsNullOrEmpty(animation.Name)) continue;
                if (!into.Contains(animation.Name)) into.Add(animation.Name);
            }
        }

        /// <summary>
        /// Re-resolves every target and re-captures every baseline from whatever the targets hold
        /// right now. Editor-only, for the inspector preview.
        ///
        /// Awake never runs in edit mode, so without this the first preview would resolve nothing.
        /// It has to run again before EVERY preview rather than once: the caller restores the
        /// resting values after each one, and re-capturing from that restored state is what stops
        /// Baseline endpoints drifting a little further every time you press Play.
        /// </summary>
        public void EditorPrepareForPreview()
        {
            initialized = false;
            Initialize();
        }

        /// <summary>
        /// Puts every target back to the resting value captured by the last
        /// EditorPrepareForPreview. Editor-only, for the inspector preview.
        /// </summary>
        public void EditorRestoreBaselines()
        {
            for (int i = 0; i < runtime.Count; i++)
            {
                List<UIAnimationStep> steps = runtime[i].Steps;
                for (int s = 0; s < steps.Count; s++)
                {
                    steps[s].RestoreBaseline();
                }
            }
        }

        /// <summary>
        /// Every distinct scene object the animations write to, for the preview's Undo record.
        /// Editor-only. Call after EditorPrepareForPreview, since targets resolve there.
        /// </summary>
        public void EditorCollectTargets(List<UnityEngine.Object> into)
        {
            for (int i = 0; i < runtime.Count; i++)
            {
                List<UIAnimationStep> steps = runtime[i].Steps;
                for (int s = 0; s < steps.Count; s++)
                {
                    UnityEngine.Object target = steps[s].ResolvedTarget();
                    if (target != null && !into.Contains(target)) into.Add(target);
                }
            }
        }
    #endif
    }
}

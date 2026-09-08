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

        [SerializeField] private List<UIAnimation> Animations = new List<UIAnimation>();

        private readonly Dictionary<string, int> lookup = new Dictionary<string, int>();

        // Scratch space for the timeline pass in BuildSequence. Reused so playing an animation
        // does not allocate.
        private readonly List<float> stepStarts = new List<float>();

        private bool initialized;

        /// <summary>True while any animation on this player is running.</summary>
        public bool IsAnyPlaying
        {
            get
            {
                for (int i = 0; i < Animations.Count; i++)
                {
                    if (Animations[i].IsPlaying) return true;
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
            if (KillOnDisable) StopAll();
        }

        private void OnDestroy()
        {
            StopAll();
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
            return Play(animationName, null);
        }

        /// <summary>
        /// Plays a named animation and invokes onComplete when it finishes naturally.
        /// The callback does NOT fire if the animation is interrupted, stopped, or killed on disable.
        /// </summary>
        public Sequence Play(string animationName, Action onComplete)
        {
            Initialize();

            UIAnimation animation = Find(animationName);
            if (animation == null) return null;

            Kill(animation, false);

            if (animation.InterruptOthers)
            {
                for (int i = 0; i < Animations.Count; i++)
                {
                    if (Animations[i] != animation) Kill(Animations[i], false);
                }
            }

            Sequence sequence = BuildSequence(animation);
            if (sequence == null)
            {
                // Nothing to play. Fire the callbacks anyway so caller logic never hangs.
                if (onComplete != null) onComplete.Invoke();
                if (animation.OnComplete != null) animation.OnComplete.Invoke();
                return null;
            }

            animation.RuntimeSequence = sequence;
            animation.RuntimeCallback = onComplete;

            UIAnimation captured = animation;
            sequence.OnComplete(() =>
            {
                captured.RuntimeSequence = null;
                Action callback = captured.RuntimeCallback;
                captured.RuntimeCallback = null;

                if (callback != null) callback.Invoke();
                if (captured.OnComplete != null) captured.OnComplete.Invoke();
            });

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
            Play(animationName, null);
        }

        /// <summary>Stops one animation. complete=true jumps to the end state and fires its callbacks.</summary>
        public void Stop(string animationName, bool complete = false)
        {
            UIAnimation animation = Find(animationName);
            if (animation != null) Kill(animation, complete);
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
            for (int i = 0; i < Animations.Count; i++)
            {
                Kill(Animations[i], complete);
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

            for (int i = 0; i < Animations.Count; i++)
            {
                List<UIAnimationStep> steps = Animations[i].Steps;
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

            for (int i = 0; i < Animations.Count; i++)
            {
                UIAnimation animation = Animations[i];

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
            for (int i = 0; i < Animations.Count; i++)
            {
                List<UIAnimationStep> steps = Animations[i].Steps;
                for (int s = 0; s < steps.Count; s++)
                {
                    steps[s].CaptureBaseline();
                }
            }
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

        private void Kill(UIAnimation animation, bool complete)
        {
            if (animation.RuntimeSequence == null) return;

            Sequence sequence = animation.RuntimeSequence;

            if (!complete)
            {
                // Drop the callbacks first so an interrupted animation never reports completion.
                animation.RuntimeSequence = null;
                animation.RuntimeCallback = null;
            }

            if (sequence.IsActive()) sequence.Kill(complete);

            animation.RuntimeSequence = null;
            animation.RuntimeCallback = null;
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
            return Animations[index];
        }

    #if UNITY_EDITOR
        /// <summary>Editor-only accessor for the inspector play buttons.</summary>
        public List<UIAnimation> EditorAnimations
        {
            get { return Animations; }
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
            for (int i = 0; i < Animations.Count; i++)
            {
                List<UIAnimationStep> steps = Animations[i].Steps;
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
            for (int i = 0; i < Animations.Count; i++)
            {
                List<UIAnimationStep> steps = Animations[i].Steps;
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

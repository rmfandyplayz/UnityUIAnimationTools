// -----------------------------------------------------------------------------
// Flipnote Style Animation Utility
//
// AI-GENERATED. Authored by Claude (Anthropic) via Claude Code, September 2026,
// to a written design brief by the project author. Not hand-written by the
// Twindrill Goose team. See the README.md beside this file for usage.
// -----------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace rmf_claude.FlipbookAnimation
{
    /// <summary>
    /// Cycles a UI Image through hand-drawn sprite frames. No Animator, no Animation Clips.
    ///
    ///     flipbook.Play();                // the clip authored on this component
    ///     flipbook.Play(someOtherClip);   // any clip, e.g. from UIFlipbookSelectable
    ///     flipbook.Stop();
    ///     flipbook.Restart();
    ///
    /// Only sprite frames are this component's business. Let DOTween (or anything else)
    /// animate the same object's position, scale and colour independently.
    ///
    /// All playback state lives here rather than on the clip, which is what lets a single
    /// UIFlipbookClipAsset drive any number of objects without them marching in lockstep.
    /// </summary>
    [DisallowMultipleComponent]
    public class UISpriteFlipbook : MonoBehaviour
    {
        // Never step more than this many frames in a single tick. Stops a huge frame hitch,
        // or an absurd FPS value, from spinning the catch-up loop.
        private const int MaxCatchUpSteps = 64;

        [Tooltip("The Image whose sprite is swapped. Left empty, an Image on this GameObject is used.")]
        [SerializeField] private Image TargetImage;

        [Tooltip("The clip played by Play() with no arguments.")]
        [SerializeField] private UIFlipbookClip Clip = new UIFlipbookClip();

        [Tooltip("Start the clip above automatically. Turn this OFF when something else drives this " +
                 "flipbook, such as a UIFlipbookSelectable.")]
        [SerializeField] private bool PlayOnEnable = true;

        [Tooltip("Begin on a random frame instead of frame 0. Put this on a shared idle so several " +
                 "copies of the same drawing do not flip in lockstep, which is what makes hand-drawn " +
                 "UI read as machine-made. Lives here rather than on the clip precisely so objects " +
                 "sharing one clip asset can still be scattered.")]
        [SerializeField] private bool RandomStartFrame;

        [Tooltip("Advance on unscaled time so the drawing keeps flipping while Time.timeScale is 0 " +
                 "(pause menus, hit-stop).")]
        [SerializeField] private bool UseUnscaledTime = true;

        [Tooltip("Fires when a non-looping clip runs off its last frame. Same moment as the C# " +
                 "Completed event; this one is here so it can be wired up in the Inspector.")]
        [SerializeField] private UnityEvent OnCompleted = new UnityEvent();

        /// <summary>
        /// Fires when a non-looping clip runs off its last frame. Never fires for Loop or Ping Pong,
        /// and never fires when playback is replaced by another Play() or cut short by Stop().
        /// </summary>
        public event Action Completed;

        private UIFlipbookClip current;
        private int frameIndex = -1;
        private float frameTimer;
        private bool playing;
        private int direction = 1;

        // Set by Stop(), cleared by Play(). A clip that merely RAN OUT does not set it, which is
        // what lets OnEnable tell "the user stopped this" from "it finished". See OnEnable.
        private bool stoppedExplicitly;

        // One pass worth of randomised frame times, re-rolled whenever the pass restarts. Rolling
        // up front rather than per frame is what makes a pass reproducible enough to scrub in the
        // editor preview, and keeping the buffer here is what stops objects sharing a clip asset
        // from drawing identical numbers.
        private readonly List<float> rolledDurations = new List<float>();

        /// <summary>True while frames are advancing.</summary>
        public bool IsPlaying
        {
            get { return playing; }
        }

        /// <summary>
        /// The clip currently playing, or the last one played. Null until something plays.
        /// This is the RESOLVED clip, so for a shared clip it is the asset's, not the local stub.
        /// </summary>
        public UIFlipbookClip CurrentClip
        {
            get { return current; }
        }

        /// <summary>Index of the frame on screen, or -1 if nothing has been shown yet.</summary>
        public int CurrentFrame
        {
            get { return frameIndex; }
        }

        /// <summary>Play the clip authored on this component.</summary>
        public void Play()
        {
            Play(Clip, true);
        }

        /// <summary>Play a clip. If it is already the clip running, playback is left alone.</summary>
        public void Play(UIFlipbookClip clip)
        {
            Play(clip, false);
        }

        /// <summary>
        /// Play a clip, always from the start when <paramref name="restartIfAlreadyPlaying"/> is true.
        /// A null or empty clip is the same as <see cref="Stop"/>.
        /// </summary>
        public void Play(UIFlipbookClip clip, bool restartIfAlreadyPlaying)
        {
            // Resolve up front, so a shared clip and the stub pointing at it are the same thing
            // as far as everything below is concerned.
            var resolved = clip != null ? clip.Resolved() : null;

            if (resolved == null || !resolved.HasFrames)
            {
                // Nothing to show is not the same as the user asking for a stop, so this must not
                // arm stoppedExplicitly - otherwise filling the clip in later would never start.
                StopInternal(false);
                return;
            }

            if (!restartIfAlreadyPlaying && playing && ReferenceEquals(resolved, current))
            {
                return;
            }

            // Full reset, so switching clips can never leave a stale timer, index or direction behind.
            current = resolved;
            frameTimer = 0f;
            frameIndex = -1;
            direction = 1;
            playing = true;
            stoppedExplicitly = false;

            RollPass();

            var start = RandomStartFrame && resolved.FrameCount > 1
                ? UnityEngine.Random.Range(0, resolved.FrameCount)
                : 0;

            ShowFrame(start);
        }

        /// <summary>Stop advancing and leave the current drawing on screen.</summary>
        public void Stop()
        {
            StopInternal(true);
        }

        /// <summary>Restart the current clip - or the authored one, if nothing has played yet - from the start.</summary>
        public void Restart()
        {
            Play(current != null ? current : Clip, true);
        }

        private void StopInternal(bool explicitStop)
        {
            playing = false;
            frameTimer = 0f;

            if (explicitStop)
            {
                stoppedExplicitly = true;
            }
        }

        private void Awake()
        {
            ResolveTarget();
        }

        private void OnEnable()
        {
            ResolveTarget();

            // Something was mid-play when we were switched off: restart it rather than resuming
            // mid-frame, so re-enabling is always predictable.
            if (playing && current != null)
            {
                Play(current, true);
                return;
            }

            if (!PlayOnEnable) return;

            // An explicit Stop() is a decision and it survives being switched off and on again.
            // A clip that simply RAN OUT is not: re-enabling replays it, which is what a one-shot
            // flourish on a panel you show, hide, and show again has to do. The old code tested
            // "current == null" here, which cannot tell those two apart, so a finished one-shot
            // played exactly once per scene load and then sat on its last frame forever.
            if (stoppedExplicitly) return;

            Play(current != null ? current : Clip, true);
        }

        // OnDisable needs no work: Unity stops calling Update, and `playing` staying true is
        // exactly the flag OnEnable reads to decide whether to resume.

        private void Update()
        {
            Tick(UseUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime);
        }

        /// <summary>
        /// One step of playback. Split out from Update so the editor preview drives the exact same
        /// code off EditorApplication.update and cannot drift from what the game does.
        /// </summary>
        private void Tick(float delta)
        {
            if (!playing)
            {
                return;
            }

            if (current == null || !current.HasFrames || TargetImage == null)
            {
                // Clip emptied in the inspector, or the Image was destroyed out from under us.
                StopInternal(false);
                return;
            }

            frameTimer += delta;

            var duration = DurationOfCurrentFrame();
            var steps = 0;

            while (playing && frameTimer >= duration && steps++ < MaxCatchUpSteps)
            {
                frameTimer -= duration;
                Advance();

                if (!playing)
                {
                    break;
                }

                duration = DurationOfCurrentFrame();
            }
        }

        private void Advance()
        {
            var count = current.FrameCount;
            var next = frameIndex + direction;

            if (next >= count)
            {
                switch (current.LoopMode)
                {
                    case UIFlipbookLoopMode.Once:
                        Complete();
                        return;

                    case UIFlipbookLoopMode.PingPong:
                        // count - 2 rather than count - 1, so the last drawing is not held for two
                        // frame times at the turn. Max() keeps a one-frame clip from going negative.
                        direction = -1;
                        next = Mathf.Max(0, count - 2);
                        RollPass();
                        break;

                    default:
                        next = 0;
                        RollPass();
                        break;
                }
            }
            else if (next < 0)
            {
                // Only reachable while ping-ponging back down. Same no-double-hold reasoning.
                direction = 1;
                next = count > 1 ? 1 : 0;
                RollPass();
            }

            ShowFrame(next);
        }

        private void Complete()
        {
            // Hold the last frame. Clear state BEFORE the callbacks, which are free to Play() again.
            StopInternal(false);

    #if UNITY_EDITOR
            // An OnCompleted UnityEvent is wired to arbitrary game code. Running that from an
            // edit-mode preview is not something a preview should ever do.
            if (EditorSuppressEvents) return;
    #endif

            if (OnCompleted != null)
            {
                OnCompleted.Invoke();
            }

            var completed = Completed;
            if (completed != null)
            {
                completed();
            }
        }

        /// <summary>
        /// Fills <see cref="rolledDurations"/> with one pass of random frame times, or empties it
        /// when this clip is not randomly timed. Called on Play and at every wrap or bounce, so
        /// each pass through the drawing is timed differently.
        /// </summary>
        private void RollPass()
        {
            rolledDurations.Clear();

            if (current == null || current.Timing != UIFlipbookTiming.RandomOffset) return;

            for (int i = 0; i < current.FrameCount; i++)
            {
                rolledDurations.Add(current.RollDuration());
            }
        }

        private float DurationOfCurrentFrame()
        {
            if (frameIndex >= 0 && frameIndex < rolledDurations.Count)
            {
                return rolledDurations[frameIndex];
            }

            return current.DurationOf(frameIndex);
        }

        private void ShowFrame(int index)
        {
            frameIndex = index;

            if (TargetImage == null || current == null || index < 0 || index >= current.FrameCount)
            {
                return;
            }

            var sprite = current.Frames[index];

            // Writing Image.sprite dirties the canvas, so only write on a real change.
            // Repeated nulls in a frame list are legitimate (a blank beat in the drawing).
            if (!ReferenceEquals(TargetImage.sprite, sprite))
            {
                TargetImage.sprite = sprite;
            }
        }

        private void ResolveTarget()
        {
            if (TargetImage == null)
            {
                TargetImage = GetComponent<Image>();
            }
        }

        private void Reset()
        {
            TargetImage = GetComponent<Image>();

            if (Clip != null)
            {
                Clip.FillUnsetDefaults();
            }
        }

        private void OnValidate()
        {
            if (Clip != null)
            {
                Clip.FillUnsetDefaults();
            }
        }

    #if UNITY_EDITOR

        /// <summary>Set while an edit-mode preview is running. See UIFlipbookPreview.</summary>
        public static bool EditorSuppressEvents;

        /// <summary>The clip authored on this component, for the inspector to preview.</summary>
        public UIFlipbookClip EditorClip
        {
            get { return Clip; }
        }

        /// <summary>The Image a preview writes to, resolved the same way playback resolves it.</summary>
        public Image EditorImage
        {
            get
            {
                ResolveTarget();
                return TargetImage;
            }
        }

        public int EditorFrameCount
        {
            get { return current != null ? current.FrameCount : 0; }
        }

        public int EditorCurrentFrame
        {
            get { return frameIndex; }
        }

        public bool EditorIsPlaying
        {
            get { return playing; }
        }

        public void EditorBeginPreview(UIFlipbookClip clip)
        {
            ResolveTarget();
            Play(clip, true);
        }

        public void EditorTick(float delta)
        {
            Tick(delta);
        }

        /// <summary>Jump to one frame and hold there. Used by the inspector scrubber.</summary>
        public void EditorSetFrame(int index)
        {
            if (current == null || !current.HasFrames) return;

            playing = false;
            frameTimer = 0f;
            ShowFrame(Mathf.Clamp(index, 0, current.FrameCount - 1));
        }

        /// <summary>Resume auto-advance from wherever the scrubber left off.</summary>
        public void EditorResume()
        {
            if (current == null || !current.HasFrames) return;

            playing = true;
            frameTimer = 0f;
        }

        /// <summary>Drop all preview playback state. The sprite itself is restored by the caller.</summary>
        public void EditorEndPreview()
        {
            StopInternal(false);

            current = null;
            frameIndex = -1;
            direction = 1;
            rolledDurations.Clear();
        }

    #endif
    }
}

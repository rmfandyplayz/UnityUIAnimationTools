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

namespace rmf_claude.FlipbookAnimation
{
    /// <summary>
    /// What a clip does when it runs off its last frame.
    ///
    /// Serialized, so THESE NUMBERS ARE THE CONTRACT. Adding a value is safe; changing or
    /// reusing one silently repoints every clip already authored against it.
    /// </summary>
    public enum UIFlipbookLoopMode
    {
        Once = 0,
        Loop = 1,
        PingPong = 2,
    }

    /// <summary>
    /// How long each frame is held. Serialized - see the note on <see cref="UIFlipbookLoopMode"/>
    /// about these numbers being the contract.
    /// </summary>
    public enum UIFlipbookTiming
    {
        /// <summary>Every frame holds for 1/FPS. The default, and what you want most of the time.</summary>
        ConstantFPS = 0,

        /// <summary>1/FPS plus a fresh random offset per frame, re-rolled every pass.</summary>
        RandomOffset = 1,

        /// <summary>Hand-typed seconds per frame, from <see cref="UIFlipbookClip.FrameDurations"/>.</summary>
        PerFrameDurations = 2,
    }

    /// <summary>
    /// One hand-drawn flipbook: an ordered list of sprites plus timing.
    ///
    /// This is plain serialized data with no runtime state of its own, so the same clip can be
    /// handed to <see cref="UISpriteFlipbook.Play(UIFlipbookClip)"/> as often as you like, and
    /// a single <see cref="UIFlipbookClipAsset"/> can drive any number of objects at once.
    /// Playback position - and the random timing roll - live on the player, not here.
    /// </summary>
    [Serializable]
    public class UIFlipbookClip
    {
        /// <summary>Frames per second used when a frame has no other timing.</summary>
        public const float DefaultFPS = 12f;

        /// <summary>Floor for FPS. Stops a divide by zero and a frame that never advances.</summary>
        public const float MinFPS = 0.01f;

        /// <summary>Floor for a randomised frame time, so a negative offset cannot stall a frame.</summary>
        public const float MinFrameDuration = 1f / 240f;

        /// <summary>Upper end of the FPS slider. Typing a larger number into the field still works.</summary>
        public const float SliderMaxFPS = 12f;

        private const float DefaultRandomSpread = 0.03f;

        [Tooltip("OPTIONAL. Point at a shared clip asset and this clip's own settings below are " +
                 "ignored - every object using that asset animates from one place. Leave empty " +
                 "to author the frames right here, which is the normal case.")]
        public UIFlipbookClipAsset Shared;

        [Tooltip("Frames in playback order, one sprite per drawing.")]
        public List<Sprite> Frames = new List<Sprite>();

        [Tooltip("Frames per second. 4-8 reads as hand-drawn; 24 is smooth. The slider stops at 12, " +
                 "but you can type a bigger number into the field.")]
        public float FPS = DefaultFPS;

        [Tooltip("What happens after the last frame. Ping Pong runs forever and never reports Completed.")]
        public UIFlipbookLoopMode LoopMode = UIFlipbookLoopMode.Loop;

        [Tooltip("How each frame's hold time is decided.")]
        public UIFlipbookTiming Timing = UIFlipbookTiming.ConstantFPS;

        [Tooltip("Shortest random offset, in seconds, added to the FPS time. Negative is fine - " +
                 "the result is floored so a frame can never take zero time.")]
        public float RandomOffsetMin = -DefaultRandomSpread;

        [Tooltip("Longest random offset, in seconds, added to the FPS time.")]
        public float RandomOffsetMax = DefaultRandomSpread;

        [Tooltip("Hold time in seconds for each frame, matched to Frames by index. Any entry left at " +
                 "0 - and any frame past the end of this list - falls back to the FPS time, so you " +
                 "only fill in the frames you actually want to hold longer.")]
        public List<float> FrameDurations = new List<float>();

        // Set the first time defaults are filled. Without it, FillUnsetDefaults could not tell
        // "this was never authored" from "the user deliberately set everything to zero".
        [SerializeField, HideInInspector] private bool defaultsFilled;

        // ---- Legacy, read once and then never again. See MigrateLoop. ----

        [SerializeField, HideInInspector] private bool Loop;
        [SerializeField, HideInInspector] private bool loopMigrated;

        public int FrameCount
        {
            get { return Frames == null ? 0 : Frames.Count; }
        }

        public bool HasFrames
        {
            get { return FrameCount > 0; }
        }

        /// <summary>
        /// The clip that actually plays: the shared asset's clip when one is assigned, otherwise this
        /// one. Resolution is deliberately ONE level deep - a clip asset is not allowed to point at
        /// another clip asset (UIFlipbookClipAsset.OnValidate enforces that), so this cannot cycle.
        /// </summary>
        public UIFlipbookClip Resolved()
        {
            if (Shared == null) return this;

            var inner = Shared.Clip;
            return inner != null ? inner : this;
        }

        /// <summary>Seconds per frame at the authored FPS, ignoring any per-frame or random timing.</summary>
        public float BaseDuration()
        {
            return 1f / (FPS >= MinFPS ? FPS : DefaultFPS);
        }

        /// <summary>
        /// How long frame <paramref name="index"/> stays on screen, in seconds. Always &gt; 0.
        ///
        /// Random timing is NOT handled here, because a roll has to be per-player: several objects
        /// sharing one clip asset must not draw the same numbers, or the whole point of randomising
        /// them is lost. UISpriteFlipbook rolls a pass up front and reads from that instead.
        /// </summary>
        public float DurationOf(int index)
        {
            if (Timing == UIFlipbookTiming.PerFrameDurations &&
                FrameDurations != null && index >= 0 && index < FrameDurations.Count &&
                FrameDurations[index] > 0f)
            {
                return FrameDurations[index];
            }

            return BaseDuration();
        }

        /// <summary>One random frame time: the FPS time plus an offset from the authored range.</summary>
        public float RollDuration()
        {
            var low = Mathf.Min(RandomOffsetMin, RandomOffsetMax);
            var high = Mathf.Max(RandomOffsetMin, RandomOffsetMax);

            return Mathf.Max(MinFrameDuration, BaseDuration() + UnityEngine.Random.Range(low, high));
        }

        /// <summary>Run time of one pass, for inspector summaries. Random timing gives the average.</summary>
        public float TotalDuration()
        {
            if (Timing == UIFlipbookTiming.RandomOffset)
            {
                var average = Mathf.Max(MinFrameDuration,
                    BaseDuration() + (RandomOffsetMin + RandomOffsetMax) * 0.5f);

                return average * FrameCount;
            }

            var total = 0f;
            for (int i = 0; i < FrameCount; i++)
            {
                total += DurationOf(i);
            }

            return total;
        }

        /// <summary>
        /// Unity does not run C# field initialisers for elements added with "+" on a serialized list,
        /// so a new clip arrives zero-filled (FPS 0, LoopMode Once). Call this from the owner's
        /// OnValidate. It only ever writes fields that are still unauthored, so it never clobbers
        /// real data.
        /// </summary>
        public void FillUnsetDefaults()
        {
            if (Frames == null)
            {
                Frames = new List<Sprite>();
            }

            if (FrameDurations == null)
            {
                FrameDurations = new List<float>();
            }

            if (FPS < MinFPS)
            {
                FPS = DefaultFPS;
            }

            MigrateLoop();

            if (!defaultsFilled)
            {
                defaultsFilled = true;
                LoopMode = UIFlipbookLoopMode.Loop;

                // A brand new clip lands on constant FPS. Random offsets are the default choice
                // once you go LOOKING for hand timing, not something every clip silently gets.
                Timing = UIFlipbookTiming.ConstantFPS;
                RandomOffsetMin = -DefaultRandomSpread;
                RandomOffsetMax = DefaultRandomSpread;
            }
        }

        /// <summary>
        /// LoopMode replaced a plain bool named Loop. Unity cannot convert one to the other, so a
        /// clip authored under the old schema would silently come back as Once.
        ///
        /// defaultsFilled is what tells the two cases apart: the OLD FillUnsetDefaults set it too,
        /// so anything carrying it was authored before this change and its Loop bool means
        /// something. A brand new zero-filled element has it false and gets the ordinary default.
        /// </summary>
        private void MigrateLoop()
        {
            if (loopMigrated) return;

            loopMigrated = true;

            if (defaultsFilled)
            {
                LoopMode = Loop ? UIFlipbookLoopMode.Loop : UIFlipbookLoopMode.Once;
            }
        }
    }
}

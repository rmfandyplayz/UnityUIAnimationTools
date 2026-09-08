// -----------------------------------------------------------------------------
// UI Animation Utility
//
// AI-GENERATED. Authored by Claude (Anthropic) via Claude Code, September 2026,
// to a written design brief by the project author. Not hand-written by the
// Twindrill Goose team. See the README.md beside this file for usage.
// -----------------------------------------------------------------------------

using DG.Tweening;
using DG.Tweening.Core.Easing;
using UnityEngine;

namespace rmf_claude.DOTweenUI
{

    /// <summary>
    /// Makes a tween advance in discrete frames instead of smoothly, for a stop-motion look.
    ///
    /// DOTween has no frame rate setting, but it does let a tween supply its own easing function.
    /// So rather than throttling anything, this wraps the step's normal ease and quantises the
    /// TIME handed to it. The tween still updates every frame - it just keeps returning the same
    /// value until the next frame boundary, which is what stepped playback actually is.
    ///
    /// One instance is cached per step and reconfigured on each build, so replaying an animation
    /// allocates nothing.
    /// </summary>
    public class UIAnimationSteppedEase
    {
        // Cached so handing this to DOTween does not allocate a delegate on every play.
        private readonly EaseFunction function;

        // The ease being stepped: a preset resolved to a function, or a curve. Never both.
        private EaseFunction inner;
        private AnimationCurve curve;

        private float frameRate;
        private float offset;

        public UIAnimationSteppedEase()
        {
            function = Evaluate;
        }

        /// <summary>
        /// Points this at an ease and a frame grid, and returns the function to hand to SetEase.
        /// Pass a curve to step a custom curve, or null to step the Ease preset.
        ///
        /// timelineOffset is where the tween sits in its sequence. Quantising against the whole
        /// animation's grid rather than each tween's own start is what keeps steps that begin at
        /// different times ticking together - without it a staggered group reads as jitter
        /// rather than as stop-motion.
        /// </summary>
        public EaseFunction Configure(Ease ease, AnimationCurve customCurve, float framesPerSecond, float timelineOffset)
        {
            curve = customCurve;
            inner = customCurve != null ? null : EaseManager.ToEaseFunction(ease);
            frameRate = framesPerSecond;
            offset = timelineOffset;

            return function;
        }

        private float Evaluate(float time, float duration, float overshootOrAmplitude, float period)
        {
            float stepped = Quantize(time, duration);

            if (curve != null) return curve.Evaluate(duration > 0f ? stepped / duration : 1f);
            return inner(stepped, duration, overshootOrAmplitude, period);
        }

        private float Quantize(float time, float duration)
        {
            if (frameRate <= 0f) return time;

            // The end is always sampled exactly, so a step lands on its authored To value even
            // when its Duration is not a whole number of frames. The last frame is simply short.
            if (time >= duration) return duration;

            float stepped = Mathf.Floor((time + offset) * frameRate) / frameRate - offset;

            // A tween starting part way through a frame gets a negative first sample off the
            // shared grid - it is still on its From value until the grid catches up.
            return Mathf.Clamp(stepped, 0f, duration);
        }
    }
}

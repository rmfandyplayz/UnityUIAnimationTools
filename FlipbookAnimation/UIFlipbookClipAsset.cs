// -----------------------------------------------------------------------------
// Flipnote Style Animation Utility
//
// AI-GENERATED. Authored by Claude (Anthropic) via Claude Code, September 2026,
// to a written design brief by the project author. Not hand-written by the
// Twindrill Goose team. See the README.md beside this file for usage.
// -----------------------------------------------------------------------------

using UnityEngine;

namespace rmf_claude.FlipbookAnimation
{
    /// <summary>
    /// A flipbook clip saved as a project asset, so many objects can share one set of frames
    /// and one set of timings.
    ///
    /// This is the OPT-IN path and it is not the default: authoring frames directly on the
    /// component is fewer clicks and is what you want for a one-off. Reach for an asset when
    /// the same drawing is on enough objects that retiming it by hand stops being reasonable -
    /// a shared idle across a screenful of buttons, say.
    ///
    /// Assign it to a clip's Shared slot. Playback state - including the random timing roll -
    /// still lives on each individual UISpriteFlipbook, so sharing frames does NOT make every
    /// object animate in lockstep. Turn on Random Start Frame if you want them scattered.
    /// </summary>
    [CreateAssetMenu(fileName = "Flipbook Clip", menuName = "Flipbook/Clip", order = 200)]
    public class UIFlipbookClipAsset : ScriptableObject
    {
        [Tooltip("The shared frames and timing. Everything pointing at this asset animates from here.")]
        public UIFlipbookClip Clip = new UIFlipbookClip();

        private void OnValidate()
        {
            if (Clip == null) return;

            Clip.FillUnsetDefaults();

            // A clip asset pointing at another clip asset is the one shape that could cycle, and
            // UIFlipbookClip.Resolved deliberately only looks one level deep. Break it here, at the
            // point of authoring, rather than defending against it on every frame of playback.
            if (Clip.Shared != null)
            {
                Debug.LogWarning("[UIFlipbookClipAsset] " + name + ": a clip asset cannot point at " +
                                 "another clip asset. The Shared slot has been cleared.", this);

                Clip.Shared = null;
            }
        }
    }
}

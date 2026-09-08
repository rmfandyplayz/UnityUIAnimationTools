// -----------------------------------------------------------------------------
// UI Animation Utility
//
// AI-GENERATED. Authored by Claude (Anthropic) via Claude Code, September 2026,
// to a written design brief by the project author. Not hand-written by the
// Twindrill Goose team. See the README.md beside this file for usage.
// -----------------------------------------------------------------------------

using UnityEngine;

namespace rmf_claude.DOTweenUI
{

    /// <summary>
    /// Fallback AudioSource for PlaySound steps that have no explicit source.
    ///
    /// A Play Sound step uses, in order: its own Audio Source field, then an AudioSource on
    /// the player's own GameObject, then this shared one. The shared source exists purely so
    /// that adding a click sound to forty buttons does not mean adding forty AudioSources.
    ///
    /// It is a 2D source, so it is unaffected by where the AudioListener is, and it survives
    /// scene loads so a sound started by a "leaving this menu" animation is not cut off.
    ///
    /// If you want UI sounds routed through an AudioMixer (for a volume slider), call
    /// SetShared once from your own bootstrap with a source you configured, or assign that
    /// source directly on the steps that need it.
    /// </summary>
    public static class UIAnimationAudio
    {
        private static AudioSource shared;

        /// <summary>
        /// The shared 2D UI source, created on first use. Never null at runtime.
        /// </summary>
        public static AudioSource Shared
        {
            get
            {
                // A destroyed AudioSource compares equal to null, so this also recovers from
                // the object being destroyed or from a domain reload wiping the static.
                if (shared != null) return shared;

                var host = new GameObject("UI Animation Audio");
                Object.DontDestroyOnLoad(host);

                shared = host.AddComponent<AudioSource>();
                shared.playOnAwake = false;
                shared.spatialBlend = 0f;
                shared.ignoreListenerPause = true;

                return shared;
            }
        }

        /// <summary>
        /// Replaces the shared source, e.g. with one routed to a UI AudioMixer group.
        /// Call it once before anything plays. Passing null restores the auto-created source.
        /// </summary>
        public static void SetShared(AudioSource source)
        {
            shared = source;
        }
    }
}

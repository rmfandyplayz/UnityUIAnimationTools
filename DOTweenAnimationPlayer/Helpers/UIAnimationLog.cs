// -----------------------------------------------------------------------------
// UI Animation Utility
//
// AI-GENERATED. Authored by Claude (Anthropic) via Claude Code, September 2026,
// to a written design brief by the project author.
// See the README.md beside this file for usage.
// -----------------------------------------------------------------------------

using UnityEngine;

namespace rmf_claude.DOTweenUI
{
    /// <summary>
    /// How every warning in this folder reads: who is warning and what is going to happen, in bold
    /// with the consequence in capitals, so it can be picked out of a busy Console at a glance - then
    /// the details. The Console renders the bold; the log file shows the tags.
    ///
    /// Public because the editor code, in its own assembly, warns the same way. The headline arrives
    /// already in capitals rather than being upper-cased here, so a name quoted in it keeps its own
    /// spelling.
    /// </summary>
    public static class UIAnimationLog
    {
        public static void Warn(string who, string headline, string details, Object context)
        {
            Debug.LogWarning("<b>" + who + ": " + headline + "</b> " + details, context);
        }

        /// <summary>"UIAnimationPlayer on 'Name'" - who most warnings here come from.</summary>
        public static string Player(Object owner)
        {
            return "UIAnimationPlayer on '" + (owner != null ? owner.name : "?") + "'";
        }
    }
}

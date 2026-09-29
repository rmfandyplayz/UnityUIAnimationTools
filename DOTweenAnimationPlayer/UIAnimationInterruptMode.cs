// -----------------------------------------------------------------------------
// UI Animation Utility
//
// AI-GENERATED. Authored by Claude (Anthropic) via Claude Code, September 2026,
// to a written design brief by the project author.
// See the README.md beside this file for usage.
// -----------------------------------------------------------------------------

using System;
using UnityEngine;

namespace rmf_claude.DOTweenUI
{

    /// <summary>
    /// Draws Interrupt Others with its two settings beside it on the same row, shown only while the box
    /// is ticked: "Interrupt Others [x] [COMPLETE] [REPORT]". Each button toggles one bool.
    ///
    /// All three fields stay exactly as they were stored; the two bools just need [HideInInspector] so
    /// they are not drawn a second time. The drawer is Editor/UIAnimationInterruptModeDrawer.cs.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    public class UIAnimationInterruptModeAttribute : PropertyAttribute
    {
        /// <summary>Name of the bool that completes interrupted animations rather than stopping them.</summary>
        public readonly string CompleteField;

        /// <summary>Name of the bool that keeps a completion from counting as a finish.</summary>
        public readonly string SilentField;

        public UIAnimationInterruptModeAttribute(string completeFieldName, string silentFieldName)
        {
            CompleteField = completeFieldName;
            SilentField = silentFieldName;
        }
    }
}

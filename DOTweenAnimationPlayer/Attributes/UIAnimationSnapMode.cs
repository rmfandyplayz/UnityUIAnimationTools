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
    /// Draws an animation's Snapping bool and its Snap Per Step sibling as one three-way button -
    /// DISABLED, ALL STEPS, PER STEP - in the style of a step's FROM / TO button.
    ///
    /// Both bools stay exactly as they were stored, so no saved data changes: the button only
    /// chooses which combination to write. The drawer is Editor/UIAnimationSnapModeDrawer.cs.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    public class UIAnimationSnapModeAttribute : PropertyAttribute
    {
        /// <summary>Name of the Snap Per Step bool, on the same object.</summary>
        public readonly string PerStepField;

        public UIAnimationSnapModeAttribute(string perStepFieldName)
        {
            PerStepField = perStepFieldName;
        }
    }
}

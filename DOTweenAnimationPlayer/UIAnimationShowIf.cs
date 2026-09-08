// -----------------------------------------------------------------------------
// UI Animation Utility
//
// AI-GENERATED. Authored by Claude (Anthropic) via Claude Code, September 2026,
// to a written design brief by the project author. Not hand-written by the
// Twindrill Goose team. See the README.md beside this file for usage.
// -----------------------------------------------------------------------------

using System;
using UnityEngine;

namespace rmf_claude.DOTweenUI
{

    /// <summary>
    /// Hides a field in the Inspector unless a sibling bool on the same object is true, so an
    /// option and the value it configures read as one control instead of two.
    ///
    /// Lives here rather than in a shared utilities folder because it exists for this framework's
    /// inspector; the drawer is Editor/UIAnimationShowIfDrawer.cs.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    public class UIAnimationShowIfAttribute : PropertyAttribute
    {
        /// <summary>Name of the bool field, on the same object, that gates this one.</summary>
        public readonly string Condition;

        public UIAnimationShowIfAttribute(string conditionFieldName)
        {
            Condition = conditionFieldName;
        }
    }
}

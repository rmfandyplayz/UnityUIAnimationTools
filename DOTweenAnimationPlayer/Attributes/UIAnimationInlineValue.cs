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
    /// Draws a bool with the value it switches on beside it, on the same row, shown only while the
    /// box is ticked - "Play At Custom FPS [x] [12]" rather than a checkbox row and a second row
    /// that comes and goes under it.
    ///
    /// Both fields stay exactly as they were stored; the value field just needs [HideInInspector] so
    /// it is not drawn a second time. The drawer is Editor/UIAnimationInlineValueDrawer.cs.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    public class UIAnimationInlineValueAttribute : PropertyAttribute
    {
        /// <summary>Name of the value field, on the same object.</summary>
        public readonly string ValueField;

        public UIAnimationInlineValueAttribute(string valueFieldName)
        {
            ValueField = valueFieldName;
        }
    }
}

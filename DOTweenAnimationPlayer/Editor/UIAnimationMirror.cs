// -----------------------------------------------------------------------------
// UI Animation Utility
//
// AI-GENERATED. Authored by Claude (Anthropic) via Claude Code, September 2026,
// to a written design brief by the project author. Not hand-written by the
// Twindrill Goose team. See README.md in the folder above for usage.
// -----------------------------------------------------------------------------

using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace rmf_claude.DOTweenUI
{

    /// <summary>
    /// Turns an authored animation into the animation that undoes it, by rewriting the data
    /// rather than by playing anything backwards. A mirrored Show is a real, editable Hide.
    ///
    /// This is an authoring operation: it runs once, from the right-click menu, and what it
    /// produces is ordinary authored data you can then tune. Nothing at runtime knows about
    /// mirroring, which is why it all lives here rather than on UIAnimationStep.
    ///
    /// Easing is deliberately left alone - see MirrorStep.
    /// </summary>
    internal static class UIAnimationMirror
    {
        /// <summary>
        /// Mirrors an animation in place: groups run in the opposite order, staggers inside a
        /// group run the other way, and every step's endpoints swap. Easing is left as authored.
        /// Warns about any step whose forward start was undefined, since those cannot be
        /// mirrored exactly and are the ones worth looking at afterwards.
        /// </summary>
        public static void Mirror(UIAnimation animation, Object context)
        {
            List<UIAnimationStep> steps = animation.Steps;
            if (steps == null || steps.Count == 0) return;

            List<List<UIAnimationStep>> groups = BuildGroups(steps);

            for (int g = 0; g < groups.Count; g++)
            {
                MirrorGroupDelays(groups[g]);
            }

            var needsReview = new List<UIAnimationStep>();
            for (int i = 0; i < steps.Count; i++)
            {
                if (!MirrorStep(steps[i])) needsReview.Add(steps[i]);
            }

            // Reverse the order of the GROUPS, not of the raw list. Start is relative to the
            // step above, so reversing the list naively would scramble which steps are joined.
            groups.Reverse();
            steps.Clear();

            for (int g = 0; g < groups.Count; g++)
            {
                List<UIAnimationStep> group = groups[g];

                // Reverse inside the group too, so the whole list reads last-to-first. This is
                // purely how it reads: joined steps all offset from the same group start, so their
                // order in the list never affected timing, and MirrorGroupDelays has already
                // flipped the stagger. Without this an animation authored as one joined group -
                // the common case - appears not to mirror at all.
                group.Reverse();

                for (int s = 0; s < group.Count; s++)
                {
                    group[s].Start = s == 0
                        ? UIAnimationStartMode.AfterPrevious
                        : UIAnimationStartMode.WithPrevious;

                    steps.Add(group[s]);
                }
            }

            if (needsReview.Count > 0) Report(animation, needsReview, context);
        }

        /// <summary>
        /// Mirrors one step's endpoints. Returns false when the step had no authored FROM, because
        /// then its forward starting value was whatever the property happened to hold and there is
        /// nothing exact to mirror it onto.
        ///
        /// The Ease preset and any custom Curve are passed through untouched, on purpose.
        /// </summary>
        public static bool MirrorStep(UIAnimationStep step)
        {
            if (step.Type == UIAnimationStepType.SetActive)
            {
                // A Show that switches something on becomes a Hide that switches it off. This is
                // the only reading under which a mirrored animation actually undoes the original.
                step.ActiveValue = !step.ActiveValue;
                return true;
            }

            // A clip has no reverse, and the sound still belongs at this point in the timeline.
            if (step.Type == UIAnimationStepType.PlaySound) return true;

            // Punch and shake return to where they started, so there is nothing to invert.
            if (UIAnimationStep.IsImpulse(step.Type)) return true;

            // Easing is deliberately NOT mirrored - neither the Ease preset nor a custom curve.
            // A strict time-reversal would turn OutQuad into InQuad, but the house style here is
            // that things decelerate into place in both directions, so a mirrored Hide should keep
            // the Out ease its Show was authored with. Endpoints and timing are what mirror; the
            // feel of the motion is a separate authored choice and stays put.

            if (step.UseFrom)
            {
                // Both ends are declared, so the mirror is exact: swap them.
                Swap(ref step.FromMode, ref step.ToMode);
                Swap(ref step.FromVector, ref step.ToVector);
                Swap(ref step.FromFloat, ref step.ToFloat);
                Swap(ref step.FromColor, ref step.ToColor);
                return true;
            }

            if (step.ToMode == UIAnimationEndpointMode.Current)
            {
                // A relative nudge mirrors exactly by negating the offset.
                step.ToVector = -step.ToVector;
                step.ToFloat = -step.ToFloat;
                step.ToColor = Negate(step.ToColor);
                return true;
            }

            // No authored FROM. The one thing we do know is where the forward step ended, so the
            // mirror starts there; the resting value is the best available guess for where it
            // should land. Flagged, because that guess is the part worth checking.
            step.UseFrom = true;
            step.FromMode = step.ToMode;
            step.FromVector = step.ToVector;
            step.FromFloat = step.ToFloat;
            step.FromColor = step.ToColor;

            step.ToMode = UIAnimationEndpointMode.Baseline;
            step.ToVector = Vector3.zero;
            step.ToFloat = 0f;
            step.ToColor = new Color(0f, 0f, 0f, 0f);

            return false;
        }

        /// <summary>
        /// Splits the step list into join-groups: each group is one step that starts after the
        /// previous group, plus every step joined alongside it.
        /// </summary>
        private static List<List<UIAnimationStep>> BuildGroups(List<UIAnimationStep> steps)
        {
            var groups = new List<List<UIAnimationStep>>();

            for (int i = 0; i < steps.Count; i++)
            {
                // The first step always opens a group, whatever its Start says - that is how
                // UIAnimationPlayer lays the sequence out too.
                bool opensGroup = i == 0 || steps[i].Start == UIAnimationStartMode.AfterPrevious;

                if (opensGroup) groups.Add(new List<UIAnimationStep>());
                groups[groups.Count - 1].Add(steps[i]);
            }

            return groups;
        }

        /// <summary>
        /// Flips delays within a joined group so a stagger runs the other way: the step that
        /// arrived last is the first to leave. A group of one keeps its delay, because its gap
        /// belongs between groups and cannot be expressed on the step itself.
        /// </summary>
        private static void MirrorGroupDelays(List<UIAnimationStep> group)
        {
            if (group.Count < 2) return;

            float span = 0f;
            for (int i = 0; i < group.Count; i++)
            {
                span = Mathf.Max(span, group[i].TotalDuration);
            }

            for (int i = 0; i < group.Count; i++)
            {
                group[i].Delay = Mathf.Max(0f, span - group[i].TotalDuration);
            }
        }

        private static void Report(UIAnimation animation, List<UIAnimationStep> needsReview, Object context)
        {
            var text = new StringBuilder();

            text.Append("Mirrored animation '").Append(animation.Name).Append("'. ");
            text.Append(needsReview.Count == 1 ? "1 step had" : needsReview.Count + " steps had");
            text.Append(" Use From switched off, so there was no authored start to mirror onto. ");
            text.Append("Their To is now the resting value, which is the part worth checking: ");

            for (int i = 0; i < needsReview.Count; i++)
            {
                if (i > 0) text.Append(", ");

                text.Append("step ").Append(animation.Steps.IndexOf(needsReview[i]));
                text.Append(" (").Append(needsReview[i].Type).Append(")");
            }

            Debug.LogWarning(text.ToString(), context);
        }

        private static Color Negate(Color c)
        {
            // Color has no unary minus.
            return new Color(-c.r, -c.g, -c.b, -c.a);
        }

        private static void Swap<T>(ref T a, ref T b)
        {
            T temp = a;
            a = b;
            b = temp;
        }
    }
}

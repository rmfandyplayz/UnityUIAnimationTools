// -----------------------------------------------------------------------------
// UI Animation Utility
//
// AI-GENERATED. Authored by Claude (Anthropic) via Claude Code, September 2026,
// to a written design brief by the project author.
// See README.md in the folder above for usage.
// -----------------------------------------------------------------------------

using System.Collections.Generic;
using DG.Tweening;
using DG.Tweening.Core.Easing;
using UnityEngine;

namespace rmf_claude.DOTweenUI
{

    /// <summary>
    /// Everything about a RectTransform that position, size, scale and rotation steps change, plus
    /// the parts that decide where it is drawn and that no step changes.
    ///
    /// The rect views are kept as anchoredPosition and sizeDelta, and offsetMin / offsetMax are
    /// derived from them exactly as Unity derives them - Unity stores one rect and every view writes
    /// through it, so this is the only representation in which an OffsetMin step and a SizeDelta step
    /// on the same object interact the way they really do.
    /// </summary>
    internal struct UIAnimationRectState
    {
        public Vector2 AnchoredPosition;
        public Vector2 SizeDelta;
        public float LocalZ;
        public Vector3 LocalScale;
        public Vector3 Euler;

        // Fixed for as long as the anchors, pivot and parent do not change, which no step does.
        public Vector2 AnchorReference;
        public Vector2 AnchorMin;
        public Vector2 AnchorMax;
        public Vector2 Pivot;
        public Vector2 ParentSize;

        public static UIAnimationRectState Of(RectTransform rect)
        {
            var parent = rect.parent as RectTransform;

            return new UIAnimationRectState
            {
                AnchoredPosition = rect.anchoredPosition,
                SizeDelta = rect.sizeDelta,
                LocalZ = rect.localPosition.z,
                LocalScale = rect.localScale,
                Euler = rect.localEulerAngles,

                // anchoredPosition is the pivot's offset from a reference point that depends only on
                // the parent's rect, the anchors and the pivot, so it is one subtraction taken now.
                AnchorReference = (Vector2)rect.localPosition - rect.anchoredPosition,
                AnchorMin = rect.anchorMin,
                AnchorMax = rect.anchorMax,
                Pivot = rect.pivot,
                ParentSize = parent != null ? parent.rect.size : Vector2.zero,
            };
        }

        public Vector3 LocalPosition
        {
            get { return new Vector3(AnchorReference.x + AnchoredPosition.x, AnchorReference.y + AnchoredPosition.y, LocalZ); }
            set
            {
                AnchoredPosition = (Vector2)value - AnchorReference;
                LocalZ = value.z;
            }
        }

        // Unity's own RectTransform.offsetMin / offsetMax setters, which move the one edge and keep
        // the other where it is.
        public Vector2 OffsetMin
        {
            get { return AnchoredPosition - Vector2.Scale(SizeDelta, Pivot); }
            set
            {
                Vector2 offset = value - OffsetMin;
                SizeDelta -= offset;
                AnchoredPosition += Vector2.Scale(offset, Vector2.one - Pivot);
            }
        }

        public Vector2 OffsetMax
        {
            get { return AnchoredPosition + Vector2.Scale(SizeDelta, Vector2.one - Pivot); }
            set
            {
                Vector2 offset = value - OffsetMax;
                SizeDelta += offset;
                AnchoredPosition += Vector2.Scale(offset, Pivot);
            }
        }

        /// <summary>True for the step types this struct can play.</summary>
        public static bool Covers(UIAnimationStepType type)
        {
            switch (type)
            {
                case UIAnimationStepType.AnchoredPosition:
                case UIAnimationStepType.LocalPosition:
                case UIAnimationStepType.Scale:
                case UIAnimationStepType.Rotation:
                case UIAnimationStepType.SizeDelta:
                case UIAnimationStepType.OffsetMin:
                case UIAnimationStepType.OffsetMax:
                    return true;

                default:
                    return false;
            }
        }

        public Vector3 Read(UIAnimationStepType type)
        {
            switch (type)
            {
                case UIAnimationStepType.AnchoredPosition: return AnchoredPosition;
                case UIAnimationStepType.LocalPosition: return LocalPosition;
                case UIAnimationStepType.Scale: return LocalScale;
                case UIAnimationStepType.Rotation: return Euler;
                case UIAnimationStepType.SizeDelta: return SizeDelta;
                case UIAnimationStepType.OffsetMin: return OffsetMin;
                case UIAnimationStepType.OffsetMax: return OffsetMax;
                default: return Vector3.zero;
            }
        }

        public void Write(UIAnimationStepType type, Vector3 value)
        {
            switch (type)
            {
                case UIAnimationStepType.AnchoredPosition: AnchoredPosition = value; break;
                case UIAnimationStepType.LocalPosition: LocalPosition = value; break;
                case UIAnimationStepType.Scale: LocalScale = value; break;
                case UIAnimationStepType.Rotation: Euler = value; break;
                case UIAnimationStepType.SizeDelta: SizeDelta = value; break;
                case UIAnimationStepType.OffsetMin: OffsetMin = value; break;
                case UIAnimationStepType.OffsetMax: OffsetMax = value; break;
            }
        }

        /// <summary>A position step's value as a world position: the pivot, where the object is drawn from.</summary>
        public Vector3 PositionToWorld(Transform parent, UIAnimationStepType type, Vector3 value)
        {
            Vector3 local = type == UIAnimationStepType.AnchoredPosition
                ? new Vector3(AnchorReference.x + value.x, AnchorReference.y + value.y, LocalZ)
                : value;

            return parent != null ? parent.TransformPoint(local) : local;
        }

        /// <summary>
        /// PositionToWorld backwards: a world position as a position step's value. An anchored
        /// position's Z is 0, as the rect views carry none.
        /// </summary>
        public Vector3 WorldToPosition(Transform parent, UIAnimationStepType type, Vector3 world)
        {
            Vector3 local = parent != null ? parent.InverseTransformPoint(world) : world;

            return type == UIAnimationStepType.AnchoredPosition
                ? new Vector3(local.x - AnchorReference.x, local.y - AnchorReference.y, 0f)
                : local;
        }

        /// <summary>
        /// The rect's outline in world space, as five points closing the loop. Worked out from this
        /// state rather than read off the object, so it can be drawn for a moment of the animation the
        /// object is not at.
        /// </summary>
        public void Outline(Transform parent, Vector3[] into)
        {
            Vector2 size = Vector2.Scale(ParentSize, AnchorMax - AnchorMin) + SizeDelta;
            Vector2 min = -Vector2.Scale(Pivot, size);
            Vector2 max = min + size;

            Matrix4x4 world = (parent != null ? parent.localToWorldMatrix : Matrix4x4.identity)
                * Matrix4x4.TRS(LocalPosition, Quaternion.Euler(Euler), LocalScale);

            into[0] = world.MultiplyPoint3x4(new Vector3(min.x, min.y, 0f));
            into[1] = world.MultiplyPoint3x4(new Vector3(min.x, max.y, 0f));
            into[2] = world.MultiplyPoint3x4(new Vector3(max.x, max.y, 0f));
            into[3] = world.MultiplyPoint3x4(new Vector3(max.x, min.y, 0f));
            into[4] = into[0];
        }
    }

    /// <summary>
    /// A value an alpha, colour, material or Custom Property step drives - anything outside a rect: the
    /// object it lives on, and the shader property or member within it. Member is null for a Canvas
    /// Group's alpha and a Graphic's colour, which is one value per object; a Graphic's alpha steps write
    /// part of its colour.
    /// </summary>
    internal struct UIAnimationChannel : System.IEquatable<UIAnimationChannel>
    {
        public readonly Object Target;
        public readonly string Member;

        public UIAnimationChannel(Object target, string member)
        {
            Target = target;
            Member = member;
        }

        public bool Equals(UIAnimationChannel other)
        {
            return IdOf(Target) == IdOf(other.Target) && Member == other.Member;
        }

        public override bool Equals(object other)
        {
            return other is UIAnimationChannel && Equals((UIAnimationChannel)other);
        }

        public override int GetHashCode()
        {
            return (IdOf(Target) * 397) ^ (Member != null ? Member.GetHashCode() : 0);
        }

        // By instance ID, which a destroyed object still has, rather than by Unity's ==.
        private static int IdOf(Object target)
        {
            return ReferenceEquals(target, null) ? 0 : target.GetInstanceID();
        }
    }

    /// <summary>
    /// Plays an animation forward on paper, without touching the scene: its position, size, scale and
    /// rotation steps (Run), to find where each one starts and ends and what the object looks like at
    /// any moment of it, and its alpha, colour, material and Custom Property steps (RunValues), to find
    /// where each one starts. That is what the Scene-view gizmos draw, what the path editor draws a path
    /// from, and what the record session measures a Current offset against.
    ///
    /// It follows the preview's rules rather than inventing its own: every animation starts from
    /// rest with its From values already applied (what Reset shows), steps are laid out by the
    /// player's own UIAnimationPlayer.LayOutSteps, and a step without a From starts from whatever the
    /// steps before it left - which is the part the old "draw from where the object sits now" rule
    /// got wrong for any step after the first.
    ///
    /// What it does not know: where another animation left things. A TO-only step that relies on
    /// that - a Close played after its Open - is worked out as if played from rest. Punch and shake
    /// are skipped, since they end where they began.
    /// </summary>
    internal static class UIAnimationSimulation
    {
        /// <summary>One position, size, scale or rotation step, worked out.</summary>
        public sealed class Track
        {
            public int StepIndex;
            public UIAnimationStepType Type;
            public RectTransform Rect;

            /// <summary>Where the step starts and ends, in its own value space.</summary>
            public Vector3 Start;
            public Vector3 End;

            /// <summary>The way it travels, in value space: start, then any path, then End.</summary>
            public readonly List<Vector3> Route = new List<Vector3>();

            /// <summary>The whole rect at evenly spaced moments of the step, first to last.</summary>
            public readonly List<UIAnimationRectState> Moments = new List<UIAnimationRectState>();

            /// <summary>The rect when the step starts, for turning its values into world positions.</summary>
            public UIAnimationRectState Frame;

            /// <summary>
            /// Whether Start is the step's own authored From, rather than wherever the steps before it
            /// left the object - only then is there a stored value behind it to change.
            /// </summary>
            public bool HasFrom;

            /// <summary>
            /// What the authored From and To are added to, in value space: Start = FromOrigin + From
            /// (when HasFrom) and End = ToOrigin + To. Nothing, the rest value, or the step's start,
            /// by mode - so an edited endpoint converts back by one subtraction.
            /// </summary>
            public Vector3 FromOrigin;
            public Vector3 ToOrigin;

            // Working state for the run.
            internal UIAnimationStep Step;
            internal float Begins;
            internal float Ends;
            internal bool Started;
            internal bool Finished;
            internal Vector3 Rest;
            internal bool Snaps;
            internal readonly List<float> RouteLength = new List<float>();
        }

        /// <summary>
        /// One alpha, colour, material or Custom Property step, worked out. Values are in the step's own
        /// terms: a number in x, a vector in xyz, a colour as rgba - and for an alpha step, the alpha alone.
        /// </summary>
        public sealed class ValueTrack
        {
            public int StepIndex;
            public UIAnimationStepType Type;
            public UIAnimationChannel Channel;

            public Vector4 Start;
            public Vector4 End;

            /// <summary>The resting value a Baseline endpoint is added to.</summary>
            public Vector4 Rest;

            // Working state for the run.
            internal UIAnimationStep Step;
            internal UIAnimationValueKind Kind;
            internal bool TwoDimensional;
            internal bool Counted;
            internal float Begins;
            internal float Ends;
            internal bool Started;
            internal bool Finished;
        }

        /// <summary>
        /// A channel's value as a run goes: a number, or - for a counted number, which lives in a text -
        /// the text, read back the way playback reads it.
        /// </summary>
        private struct ChannelValue
        {
            public Vector4 Number;
            public string Text;
        }

        // Resting state of every RectTransform the running preview touches, captured before anything
        // moved. Empty when no preview runs, and then the scene as it stands IS the rest. Without it
        // the gizmos would be drawn from wherever the preview has moved things to.
        private static readonly Dictionary<RectTransform, UIAnimationRectState> previewRest =
            new Dictionary<RectTransform, UIAnimationRectState>();

        private static readonly Dictionary<Object, Color> previewColors = new Dictionary<Object, Color>();
        private static readonly Dictionary<Object, float> previewAlphas = new Dictionary<Object, float>();

        // Material and Custom Property values at rest, as the preview's baselines captured them. Unlike
        // the rest, a member cannot be read off the object outside a preview: that would mean calling its
        // getter on a repaint.
        private static readonly Dictionary<UIAnimationChannel, ChannelValue> previewValues =
            new Dictionary<UIAnimationChannel, ChannelValue>();

        private static readonly List<UIAnimationStep> stepScratch = new List<UIAnimationStep>();

        /// <summary>Called by the preview before anything moves, for every object it is about to record.</summary>
        public static void NoteRest(List<Object> targets)
        {
            for (int i = 0; i < targets.Count; i++)
            {
                var rect = targets[i] as RectTransform;
                if (rect != null && !previewRest.ContainsKey(rect)) previewRest.Add(rect, UIAnimationRectState.Of(rect));

                var group = targets[i] as CanvasGroup;
                if (group != null && !previewAlphas.ContainsKey(group)) previewAlphas.Add(group, group.alpha);

                var graphic = targets[i] as UnityEngine.UI.Graphic;
                if (graphic != null && !previewColors.ContainsKey(graphic)) previewColors.Add(graphic, graphic.color);
            }
        }

        /// <summary>
        /// Called by the preview once the player has captured its baselines at rest: the material and Custom
        /// Property values among them. Read from the baselines rather than the objects, since a member is
        /// only read by calling its getter - which the baseline capture, run on a click, already did.
        /// </summary>
        public static void NoteRest(UIAnimationPlayer player)
        {
            stepScratch.Clear();
            player.EditorCollectSteps(stepScratch);

            for (int i = 0; i < stepScratch.Count; i++)
            {
                Object target;
                string member;
                Vector4 number;
                string text;

                if (stepScratch[i] == null || !stepScratch[i].EditorTryGetBaseline(out target, out member, out number, out text)) continue;

                var channel = new UIAnimationChannel(target, member);
                if (!previewValues.ContainsKey(channel)) previewValues.Add(channel, new ChannelValue { Number = number, Text = text });
            }

            stepScratch.Clear();
        }

        /// <summary>Called by the preview once everything is back at rest.</summary>
        public static void ForgetRest()
        {
            previewRest.Clear();
            previewColors.Clear();
            previewAlphas.Clear();
            previewValues.Clear();
        }

        /// <summary>
        /// Whether the running preview captured the resting value of whatever this step drives, so it
        /// both knows where Baseline measures from and will put the value back when it ends.
        /// </summary>
        public static bool IsNoted(UIAnimationStep step, GameObject owner)
        {
            if (step == null) return false;

            if (UIAnimationRectState.Covers(step.Type))
            {
                RectTransform rect = UIAnimationTargets.RectOf(step, owner);
                return rect != null && previewRest.ContainsKey(rect);
            }

            UIAnimationChannel channel;
            if (!TryChannelOf(step, owner, out channel)) return false;

            switch (UIAnimationStep.TargetKindOf(step.Type))
            {
                case UIAnimationTargetKind.CanvasGroup: return previewAlphas.ContainsKey(channel.Target);
                case UIAnimationTargetKind.Graphic: return previewColors.ContainsKey(channel.Target);
                default: return previewValues.ContainsKey(channel);
            }
        }

        public static UIAnimationRectState RestOf(RectTransform rect)
        {
            UIAnimationRectState state;
            return previewRest.TryGetValue(rect, out state) ? state : UIAnimationRectState.Of(rect);
        }

        public static float RestAlphaOf(CanvasGroup group)
        {
            float alpha;
            return previewAlphas.TryGetValue(group, out alpha) ? alpha : group.alpha;
        }

        public static Color RestColorOf(UnityEngine.UI.Graphic graphic)
        {
            Color color;
            return previewColors.TryGetValue(graphic, out color) ? color : graphic.color;
        }

        private static readonly List<float> starts = new List<float>();
        private static readonly List<float> times = new List<float>();
        private static readonly Dictionary<RectTransform, UIAnimationRectState> states =
            new Dictionary<RectTransform, UIAnimationRectState>();

        private const float Epsilon = 0.00001f;
        private const int SamplesPerSegment = 16;

        /// <summary>
        /// Works out every position, size, scale and rotation step of an animation, in step order.
        /// moments is how many evenly spaced snapshots of the whole rect to keep per step, first and
        /// last included; 0 keeps none.
        /// </summary>
        public static void Run(UIAnimation animation, GameObject owner, int moments, List<Track> into)
        {
            into.Clear();
            states.Clear();

            if (animation == null || owner == null) return;

            List<UIAnimationStep> steps = animation.Steps;
            UIAnimationPlayer.LayOutSteps(steps, starts);
            frameRate = animation.EffectiveFrameRate;

            for (int i = 0; i < steps.Count; i++)
            {
                UIAnimationStep step = steps[i];
                if (step == null || !UIAnimationRectState.Covers(step.Type)) continue;

                RectTransform rect = UIAnimationTargets.RectOf(step, owner);
                if (rect == null) continue;

                if (!states.ContainsKey(rect)) states.Add(rect, RestOf(rect));

                var track = new Track
                {
                    StepIndex = i,
                    Type = step.Type,
                    Rect = rect,
                    Step = step,
                    Begins = starts[i],
                    Ends = starts[i] + Mathf.Max(0f, step.Duration),
                    Rest = RestOf(rect).Read(step.Type),

                    // The same rule BuildTween applies. Scale never snaps - DOTween has no option for it.
                    Snaps = (step.Snapping || animation.SnapsEveryStep) && UIAnimationStep.SupportsSnapping(step.Type),
                };

                into.Add(track);
            }

            // The animation's first frame, as Reset shows it: every From applied at once, in step
            // order, so a later step's From wins on a property two of them set.
            for (int i = 0; i < into.Count; i++)
            {
                Track track = into[i];
                if (!HasAuthoredStart(track.Step)) continue;

                UIAnimationRectState state = states[track.Rect];
                state.Write(track.Type, Flatten(track.Type, Resolve(track.Step.FromMode, track.Step.FromVector, track.Rest)));
                states[track.Rect] = state;
            }

            // Every moment anything happens: each step's start and end, and each snapshot. Values are
            // pure functions of time once a step has started, so nothing in between needs visiting.
            times.Clear();

            for (int i = 0; i < into.Count; i++)
            {
                Track track = into[i];
                times.Add(track.Begins);
                times.Add(track.Ends);

                for (int m = 0; m < moments; m++) times.Add(MomentTime(track, m, moments));
            }

            times.Sort();

            // Steps that start earlier act first at any one moment, and among steps starting together
            // the one lower in the list writes last and wins, as it does in the Sequence.
            into.Sort((a, b) => a.Begins != b.Begins ? a.Begins.CompareTo(b.Begins) : a.StepIndex.CompareTo(b.StepIndex));

            float previous = float.NegativeInfinity;

            for (int t = 0; t < times.Count; t++)
            {
                float now = times[t];
                if (now - previous < Epsilon) continue;
                previous = now;

                for (int i = 0; i < into.Count; i++) Advance(into[i], now);

                for (int i = 0; i < into.Count; i++)
                {
                    Track track = into[i];

                    for (int m = 0; m < moments; m++)
                    {
                        if (Mathf.Abs(MomentTime(track, m, moments) - now) < Epsilon) track.Moments.Add(states[track.Rect]);
                    }
                }
            }

            into.Sort((a, b) => a.StepIndex.CompareTo(b.StepIndex));
        }

        /// <summary>
        /// Where one step of an animation starts, in its own value space, and the rest value its
        /// Baseline is measured from. False when the step is not one this can play or has no target.
        /// </summary>
        public static bool TryStartOf(UIAnimation animation, GameObject owner, int stepIndex, out Vector3 start, out Vector3 rest)
        {
            Run(animation, owner, 0, scratch);

            for (int i = 0; i < scratch.Count; i++)
            {
                if (scratch[i].StepIndex != stepIndex) continue;

                start = scratch[i].Start;
                rest = scratch[i].Rest;
                scratch.Clear();
                return true;
            }

            scratch.Clear();
            start = Vector3.zero;
            rest = Vector3.zero;
            return false;
        }

        private static readonly List<Track> scratch = new List<Track>();

        /// <summary>
        /// Where one step of an animation starts, in its own terms - a vector step's value in xyz, a number
        /// in x, a colour as rgba - worked out by replaying the animation from rest. False when it cannot be:
        /// no target, a step with no value, a Text step, or a Custom Property whose resting value is not
        /// known, which only a preview reads.
        /// </summary>
        public static bool TryStartOf(UIAnimation animation, GameObject owner, int stepIndex, out Vector4 start)
        {
            start = Vector4.zero;
            if (animation == null || stepIndex < 0 || stepIndex >= animation.Steps.Count || animation.Steps[stepIndex] == null) return false;

            if (UIAnimationRectState.Covers(animation.Steps[stepIndex].Type))
            {
                Vector3 value;
                Vector3 rest;
                if (!TryStartOf(animation, owner, stepIndex, out value, out rest)) return false;

                start = value;
                return true;
            }

            RunValues(animation, owner, valueScratch);

            for (int i = 0; i < valueScratch.Count; i++)
            {
                if (valueScratch[i].StepIndex != stepIndex) continue;

                start = valueScratch[i].Start;
                valueScratch.Clear();
                return true;
            }

            valueScratch.Clear();
            return false;
        }

        private static readonly List<ValueTrack> valueScratch = new List<ValueTrack>();

        /// <summary>
        /// The resting value a step's Baseline adds to, in its own terms as TryStartOf gives them, and - for
        /// a Custom Property whose member is a string - the resting text. False when it is not known: no
        /// target, or a Custom Property outside a preview.
        /// </summary>
        public static bool TryRestOf(UIAnimationStep step, GameObject owner, out Vector4 rest, out string restText)
        {
            rest = Vector4.zero;
            restText = null;
            if (step == null) return false;

            if (UIAnimationRectState.Covers(step.Type))
            {
                RectTransform rect = UIAnimationTargets.RectOf(step, owner);
                if (rect == null) return false;

                rest = RestOf(rect).Read(step.Type);
                return true;
            }

            UIAnimationChannel channel;
            ChannelValue value;
            if (!TryChannelOf(step, owner, out channel) || !TryChannelRest(step, channel, out value)) return false;

            rest = ReadAs(NewTrack(step, channel, -1, 0f), value);
            restText = value.Text;
            return true;
        }

        /// <summary>
        /// The value a non-rect step drives, found by playback's rules and without side effects: no getter
        /// is called, and no material is instanced. False for the rect types, punch and shake, the steps
        /// with no value, and a step with nothing to drive.
        /// </summary>
        public static bool TryChannelOf(UIAnimationStep step, GameObject owner, out UIAnimationChannel channel)
        {
            channel = default(UIAnimationChannel);

            string problem;
            Object target = UIAnimationTargets.Resolve(step.Type, UIAnimationTargets.SlotOf(step, step.Type), step.TargetPath, owner, out problem);
            if (target == null) return false;

            switch (step.Type)
            {
                case UIAnimationStepType.CanvasGroupAlpha:
                case UIAnimationStepType.GraphicColor:
                case UIAnimationStepType.GraphicAlpha:
                    channel = new UIAnimationChannel(target, null);
                    return true;

                case UIAnimationStepType.MaterialFloat:
                case UIAnimationStepType.MaterialColor:
                    if (string.IsNullOrEmpty(step.ShaderProperty)) return false;

                    channel = new UIAnimationChannel(target, step.ShaderProperty);
                    return true;

                case UIAnimationStepType.CustomProperty:
                {
                    Component component;
                    System.Reflection.MemberInfo member;

                    if (!UIAnimationProperties.TryLocate(target as GameObject, step.PropertyComponent, step.PropertyMember,
                            step.PropertyKind, out component, out member, out problem))
                    {
                        return false;
                    }

                    channel = new UIAnimationChannel(component, step.PropertyMember);
                    return true;
                }

                default:
                    return false;
            }
        }

        /// <summary>
        /// A channel's resting value: what the running preview captured before anything moved, or - with no
        /// preview, when the scene IS at rest - what the object holds now. A material is read off the material
        /// it would be cloned from, so nothing is instanced; a Custom Property is only known from a preview.
        /// </summary>
        private static bool TryChannelRest(UIAnimationStep step, UIAnimationChannel channel, out ChannelValue rest)
        {
            rest = default(ChannelValue);

            switch (UIAnimationStep.TargetKindOf(step.Type))
            {
                case UIAnimationTargetKind.CanvasGroup:
                {
                    var group = channel.Target as CanvasGroup;
                    if (group == null) return false;

                    rest.Number = new Vector4(RestAlphaOf(group), 0f, 0f, 0f);
                    return true;
                }

                case UIAnimationTargetKind.Graphic:
                {
                    var graphic = channel.Target as UnityEngine.UI.Graphic;
                    if (graphic == null) return false;

                    rest.Number = RestColorOf(graphic);
                    return true;
                }

                case UIAnimationTargetKind.Material:
                {
                    if (previewValues.TryGetValue(channel, out rest)) return true;

                    Material material = UIAnimationTargets.SourceMaterialOf(channel.Target as UIMaterialInstance);
                    if (material == null || !material.HasProperty(channel.Member)) return false;

                    rest.Number = step.Type == UIAnimationStepType.MaterialColor
                        ? (Vector4)material.GetColor(channel.Member)
                        : new Vector4(material.GetFloat(channel.Member), 0f, 0f, 0f);
                    return true;
                }

                default:
                    return previewValues.TryGetValue(channel, out rest);
            }
        }

        // Every channel's value as a RunValues goes.
        private static readonly Dictionary<UIAnimationChannel, ChannelValue> channels =
            new Dictionary<UIAnimationChannel, ChannelValue>();

        /// <summary>
        /// Works out every alpha, colour, material and Custom Property step of an animation, in step order,
        /// the way Run works out the rect steps and by the same rules: from rest, every From applied first
        /// (what Reset shows), each step's start read at its start time, steps acting in (start time, index)
        /// order. Values outside a rect never touch one, so this runs on its own. A Text step is left out -
        /// Current is never offered for one - and so is a step whose resting value is not known.
        /// </summary>
        public static void RunValues(UIAnimation animation, GameObject owner, List<ValueTrack> into)
        {
            into.Clear();
            channels.Clear();

            if (animation == null || owner == null) return;

            List<UIAnimationStep> steps = animation.Steps;
            UIAnimationPlayer.LayOutSteps(steps, starts);
            frameRate = animation.EffectiveFrameRate;

            for (int i = 0; i < steps.Count; i++)
            {
                UIAnimationStep step = steps[i];
                if (step == null || UIAnimationStep.IsImpulse(step.Type)) continue;
                if (step.Type == UIAnimationStepType.CustomProperty && step.PropertyKind == UIAnimationPropertyKind.Text) continue;

                UIAnimationChannel channel;
                ChannelValue rest;
                if (!TryChannelOf(step, owner, out channel) || !TryChannelRest(step, channel, out rest)) continue;

                if (!channels.ContainsKey(channel)) channels.Add(channel, rest);

                ValueTrack track = NewTrack(step, channel, i, starts[i]);
                track.Rest = ReadAs(track, rest);
                into.Add(track);
            }

            // The animation's first frame, as Reset shows it: every From applied at once, in step order.
            for (int i = 0; i < into.Count; i++)
            {
                if (HasAuthoredStart(into[i].Step)) WriteValue(into[i], FromOf(into[i]));
            }

            times.Clear();

            for (int i = 0; i < into.Count; i++)
            {
                times.Add(into[i].Begins);
                times.Add(into[i].Ends);
            }

            times.Sort();
            into.Sort((a, b) => a.Begins != b.Begins ? a.Begins.CompareTo(b.Begins) : a.StepIndex.CompareTo(b.StepIndex));

            float previous = float.NegativeInfinity;

            for (int t = 0; t < times.Count; t++)
            {
                float now = times[t];
                if (now - previous < Epsilon) continue;
                previous = now;

                for (int i = 0; i < into.Count; i++) AdvanceValue(into[i], now);
            }

            into.Sort((a, b) => a.StepIndex.CompareTo(b.StepIndex));
        }

        private static ValueTrack NewTrack(UIAnimationStep step, UIAnimationChannel channel, int index, float begins)
        {
            var track = new ValueTrack
            {
                StepIndex = index,
                Type = step.Type,
                Channel = channel,
                Step = step,
                Begins = begins,
                Ends = begins + Mathf.Max(0f, step.Duration),
            };

            if (step.Type == UIAnimationStepType.CustomProperty)
            {
                track.Kind = UIAnimationStep.ValueKindOf(step.PropertyKind);
                track.TwoDimensional = step.PropertyKind == UIAnimationPropertyKind.Vector2;
                track.Counted = step.PropertyKind == UIAnimationPropertyKind.NumberText;
            }
            else
            {
                track.Kind = UIAnimationStep.ValueKindOf(step.Type);
            }

            return track;
        }

        private static void AdvanceValue(ValueTrack track, float now)
        {
            if (track.Finished || now < track.Begins - Epsilon) return;

            if (!track.Started)
            {
                BeginValue(track);
                track.Started = true;
            }

            float duration = track.Ends - track.Begins;
            float linear = duration > 0f ? Mathf.Clamp01((now - track.Begins) / duration) : 1f;

            WriteValue(track, Vector4.LerpUnclamped(track.Start, track.End, Eased(track.Step, track.Begins, track.Ends, linear)));

            if (now >= track.Ends - Epsilon) track.Finished = true;
        }

        /// <summary>
        /// Resolves a step at the moment it starts, the way BuildTween does: From if it has one, the value as
        /// it stands if not, and a Current To as an offset from that start. An int member's endpoints are
        /// whole numbers, as the tween is built with them.
        /// </summary>
        private static void BeginValue(ValueTrack track)
        {
            UIAnimationStep step = track.Step;

            track.Start = HasAuthoredStart(step) ? FromOf(track) : ReadAs(track, channels[track.Channel]);

            Vector4 to = Masked(track, track.Kind == UIAnimationValueKind.Color ? (Vector4)step.ToColor
                : track.Kind == UIAnimationValueKind.Vector ? (Vector4)step.ToVector
                : new Vector4(step.ToFloat, 0f, 0f, 0f));

            if (track.Kind == UIAnimationValueKind.Int) to.x = Mathf.RoundToInt(to.x);

            bool relative = !step.UseFrom && step.ToMode == UIAnimationEndpointMode.Current;

            if (relative)
            {
                track.End = track.Start + to;
            }
            else
            {
                Vector4 end = step.ToMode == UIAnimationEndpointMode.Baseline ? track.Rest + to : to;
                if (track.Kind == UIAnimationValueKind.Int) end.x = Mathf.RoundToInt(end.x);
                track.End = Masked(track, end);
            }
        }

        /// <summary>A step's resolved From, in its own terms.</summary>
        private static Vector4 FromOf(ValueTrack track)
        {
            UIAnimationStep step = track.Step;

            Vector4 from = Masked(track, track.Kind == UIAnimationValueKind.Color ? (Vector4)step.FromColor
                : track.Kind == UIAnimationValueKind.Vector ? (Vector4)step.FromVector
                : new Vector4(step.FromFloat, 0f, 0f, 0f));

            if (step.FromMode == UIAnimationEndpointMode.Baseline) from += track.Rest;
            if (track.Kind == UIAnimationValueKind.Int) from.x = Mathf.RoundToInt(from.x);

            return from;
        }

        /// <summary>A channel's value as this step reads it: an alpha step sees the alpha, a counted number the number its text shows.</summary>
        private static Vector4 ReadAs(ValueTrack track, ChannelValue value)
        {
            if (track.Type == UIAnimationStepType.GraphicAlpha) return new Vector4(value.Number.w, 0f, 0f, 0f);
            if (track.Counted) return new Vector4(UIAnimationProperties.ParseNumber(value.Text), 0f, 0f, 0f);

            return Masked(track, value.Number);
        }

        /// <summary>
        /// Writes a step's value into its channel the way its tween does: an alpha step only the alpha, an
        /// int rounded, a counted number written out in its format.
        /// </summary>
        private static void WriteValue(ValueTrack track, Vector4 value)
        {
            ChannelValue channel = channels[track.Channel];

            if (track.Type == UIAnimationStepType.GraphicAlpha) channel.Number.w = value.x;
            else if (track.Counted) channel.Text = UIAnimationProperties.FormatNumber(value.x, track.Step.NumberFormat);
            else if (track.Kind == UIAnimationValueKind.Int) channel.Number = new Vector4(Mathf.RoundToInt(value.x), 0f, 0f, 0f);
            else channel.Number = Masked(track, value);

            channels[track.Channel] = channel;
        }

        /// <summary>Only the parts of a value its kind has.</summary>
        private static Vector4 Masked(ValueTrack track, Vector4 value)
        {
            switch (track.Kind)
            {
                case UIAnimationValueKind.Vector:
                    return new Vector4(value.x, value.y, track.TwoDimensional ? 0f : value.z, 0f);

                case UIAnimationValueKind.Color:
                    return value;

                default:
                    return new Vector4(value.x, 0f, 0f, 0f);
            }
        }

        private static float MomentTime(Track track, int index, int count)
        {
            if (count <= 1) return track.Ends;
            return Mathf.Lerp(track.Begins, track.Ends, index / (float)(count - 1));
        }

        private static void Advance(Track track, float now)
        {
            if (track.Finished || now < track.Begins - Epsilon) return;

            UIAnimationRectState state = states[track.Rect];

            if (!track.Started)
            {
                Begin(track, state);
                track.Started = true;
            }

            float duration = track.Ends - track.Begins;
            float linear = duration > 0f ? Mathf.Clamp01((now - track.Begins) / duration) : 1f;

            Vector3 value = ValueAt(track, Eased(track.Step, track.Begins, track.Ends, linear));
            if (track.Snaps) value = new Vector3(Mathf.Round(value.x), Mathf.Round(value.y), Mathf.Round(value.z));

            state.Write(track.Type, value);
            states[track.Rect] = state;

            if (now >= track.Ends - Epsilon) track.Finished = true;
        }

        /// <summary>
        /// Resolves a step at the moment it starts, the way BuildTween does: From if it has one, where
        /// the property is now if not, and a Current To as an offset from that start.
        /// </summary>
        private static void Begin(Track track, UIAnimationRectState state)
        {
            UIAnimationStep step = track.Step;
            UIAnimationStepType type = track.Type;

            track.Frame = state;
            track.HasFrom = HasAuthoredStart(step);
            track.FromOrigin = step.FromMode == UIAnimationEndpointMode.Baseline ? track.Rest : Vector3.zero;
            track.Start = track.HasFrom
                ? Flatten(type, Resolve(step.FromMode, step.FromVector, track.Rest))
                : state.Read(type);

            bool relative = !step.UseFrom && step.ToMode == UIAnimationEndpointMode.Current;
            Vector3 origin = relative ? track.Start : step.ToMode == UIAnimationEndpointMode.Baseline ? track.Rest : Vector3.zero;

            track.ToOrigin = origin;
            track.End = Flatten(type, origin + step.ToVector);

            track.Route.Clear();
            track.Route.Add(track.Start);

            if (step.HasPath)
            {
                // Waypoints follow To's mode, and consecutive duplicates are dropped - the same list
                // UIAnimationStep.PathPoints builds, with the start the path plugin puts in front.
                for (int i = 0; i <= step.Waypoints.Count; i++)
                {
                    Vector3 point = i < step.Waypoints.Count ? Flatten(type, origin + step.Waypoints[i]) : track.End;
                    if (point != track.Route[track.Route.Count - 1]) track.Route.Add(point);
                }

                if (step.PathShape == UIAnimationPathShape.Curved && track.Route.Count > 2) Sample(track.Route);
            }
            else
            {
                track.Route.Add(track.End);
            }

            track.RouteLength.Clear();
            float length = 0f;
            track.RouteLength.Add(0f);

            for (int i = 1; i < track.Route.Count; i++)
            {
                length += Vector3.Distance(track.Route[i - 1], track.Route[i]);
                track.RouteLength.Add(length);
            }
        }

        private static readonly List<Vector3> corners = new List<Vector3>();

        /// <summary>Replaces a curved route's corner points with the curve itself, finely sampled.</summary>
        private static void Sample(List<Vector3> route)
        {
            corners.Clear();
            corners.AddRange(route);
            route.Clear();

            for (int k = 0; k < corners.Count - 1; k++)
            {
                for (int s = 0; s < SamplesPerSegment; s++)
                {
                    route.Add(UIAnimationPathEditor.Evaluate(corners, k, s / (float)SamplesPerSegment, true));
                }
            }

            route.Add(corners[corners.Count - 1]);
        }

        /// <summary>
        /// The value a fraction of the way through the step. A straight step is an unclamped lerp, so
        /// an ease that overshoots overshoots here too. A path is followed at constant speed, as
        /// DOTween's path plugin does, so the fraction is a fraction of its length.
        /// </summary>
        private static Vector3 ValueAt(Track track, float fraction)
        {
            List<Vector3> route = track.Route;
            if (route.Count == 2) return Vector3.LerpUnclamped(route[0], route[1], fraction);

            float total = track.RouteLength[track.RouteLength.Count - 1];
            if (total <= 0f) return route[route.Count - 1];

            float along = Mathf.Clamp01(fraction) * total;

            for (int i = 1; i < route.Count; i++)
            {
                if (track.RouteLength[i] < along) continue;

                float segment = track.RouteLength[i] - track.RouteLength[i - 1];
                float u = segment > 0f ? (along - track.RouteLength[i - 1]) / segment : 0f;
                return Vector3.Lerp(route[i - 1], route[i], u);
            }

            return route[route.Count - 1];
        }

        // The animation being run's Play At Custom FPS, or 0 for smooth.
        private static float frameRate;

        private static readonly UIAnimationSteppedEase stepped = new UIAnimationSteppedEase();

        /// <summary>
        /// The step's ease at a fraction of its duration. A custom curve is read across its own length,
        /// as DOTween's EaseCurve does, so a curve whose last key is not at 1 plays the same here. A
        /// stepped animation goes through the player's own UIAnimationSteppedEase, on the same shared
        /// frame grid, rather than a copy of it.
        /// </summary>
        private static float Eased(UIAnimationStep step, float begins, float ends, float linear)
        {
            bool useCurve = step.UseCustomCurve && step.Curve != null && step.Curve.length > 0;

            if (frameRate > 0f)
            {
                float duration = ends - begins;
                if (duration <= 0f) return 1f;

                EaseFunction function = stepped.Configure(step.EaseType, useCurve ? step.Curve : null, frameRate, begins);
                return function(linear * duration, duration, DOTween.defaultEaseOvershootOrAmplitude, DOTween.defaultEasePeriod);
            }

            if (useCurve)
            {
                float length = step.Curve[step.Curve.length - 1].time;
                return step.Curve.Evaluate(linear * length);
            }

            return EaseManager.Evaluate(step.EaseType, null, linear, 1f,
                DOTween.defaultEaseOvershootOrAmplitude, DOTween.defaultEasePeriod);
        }

        /// <summary>Mirrors UIAnimationStep.HasAuthoredStart; punch and shake never reach here.</summary>
        private static bool HasAuthoredStart(UIAnimationStep step)
        {
            return step.UseFrom && step.FromMode != UIAnimationEndpointMode.Current;
        }

        private static Vector3 Resolve(UIAnimationEndpointMode mode, Vector3 value, Vector3 rest)
        {
            return mode == UIAnimationEndpointMode.Baseline ? rest + value : value;
        }

        /// <summary>Z means nothing on the rect views, as in UIAnimationStep.Flatten.</summary>
        private static Vector3 Flatten(UIAnimationStepType type, Vector3 value)
        {
            if (UIAnimationStep.IsTwoDimensional(type)) value.z = 0f;
            return value;
        }
    }
}

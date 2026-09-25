// -----------------------------------------------------------------------------
// UI Animation Utility
//
// AI-GENERATED. Authored by Claude (Anthropic) via Claude Code, September 2026,
// to a written design brief by the project author.
// See the README.md beside this file for usage.
// -----------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using DG.Tweening;
using DG.Tweening.Core;
using DG.Tweening.Plugins;
using UnityEngine;
using UnityEngine.UI;

namespace rmf_claude.DOTweenUI
{

    /// <summary>
    /// What a single animation step drives. The framework never interprets these
    /// semantically - it only knows how to build a DOTween tween for each one.
    ///
    /// Unity serializes an enum field as its integer value, not its name, so what is stored in
    /// a scene or prefab is the NUMBER. The numbers below are therefore written out explicitly
    /// and are the real contract - each one is permanently spoken for by authored data.
    ///
    /// Reordering these lines is now harmless, because a value carries its number with it.
    /// CHANGING a number, or reusing a retired one, silently repoints every step already
    /// authored against it - with no error, no warning, and no import log. This list was
    /// alphabetised once while the numbers were still implicit, which turned 16 authored Scale
    /// steps on the main menu buttons into GraphicAlpha steps that faded them to invisible.
    /// New types take the next free number.
    ///
    /// The order the Inspector lists them in is not this order: the step drawer groups them into
    /// categories and sorts each one alphabetically by itself, so the numbers never have to be
    /// shuffled to make the dropdown read well.
    /// </summary>
    public enum UIAnimationStepType
    {
        AnchoredPosition = 0,
        CanvasGroupAlpha = 1,
        GraphicAlpha = 2,
        GraphicColor = 3,
        LocalPosition = 4,
        MaterialColor = 5,
        MaterialFloat = 6,
        OffsetMin = 7,
        OffsetMax = 8,
        PunchAnchoredPosition = 9,
        PunchScale = 10,
        Rotation = 11,
        Scale = 12,
        SetActive = 13,
        ShakeAnchoredPosition = 14,
        SizeDelta = 15,
        PlaySound = 16,
        PunchRotation = 17,
        ShakeRotation = 18,
        ShakeScale = 19,
    }

    /// <summary>
    /// How a FROM/TO endpoint value is resolved at build time.
    /// Serialized as an integer - see the note on UIAnimationStepType. Numbers are the contract.
    /// </summary>
    public enum UIAnimationEndpointMode
    {
        /// <summary>Use the authored value exactly as typed.</summary>
        Absolute = 0,

        /// <summary>Resting value captured at Awake, plus the authored value as an offset.</summary>
        Baseline = 1,

        /// <summary>Value when the tween starts, plus the authored value as an offset (DOTween relative).</summary>
        Current = 2,
    }

    /// <summary>
    /// Whether a step is appended after the previous one or joined alongside it.
    /// Serialized as an integer - see the note on UIAnimationStepType. Numbers are the contract.
    /// </summary>
    public enum UIAnimationStartMode
    {
        AfterPrevious = 0,
        WithPrevious = 1,
    }

    /// <summary>
    /// How a movement path joins its points.
    /// Serialized as an integer - see the note on UIAnimationStepType. Numbers are the contract.
    ///
    /// Curved is 0 on purpose: a step added with + on a serialized list arrives zero-filled, and
    /// a smooth curve is what someone ticking "Custom Path" almost always wants.
    /// </summary>
    public enum UIAnimationPathShape
    {
        /// <summary>A smooth curve through every point (DOTween's Catmull-Rom path).</summary>
        Curved = 0,

        /// <summary>Straight lines between the points, with a sharp corner at each one.</summary>
        Linear = 1,
    }

    /// <summary>Which value fields a step type actually uses. Drives the inspector drawer.</summary>
    public enum UIAnimationValueKind
    {
        None,
        Float,
        Vector,
        Color,
    }

    /// <summary>Which target slot a step type needs. Drives the inspector drawer.</summary>
    public enum UIAnimationTargetKind
    {
        Rect,
        CanvasGroup,
        Graphic,
        Material,
        GameObject,
        Audio,
    }

    /// <summary>
    /// One tween inside a named animation. Flat and serializable on purpose - a custom
    /// PropertyDrawer hides whichever fields the selected Type does not use.
    /// </summary>
    [Serializable]
    public class UIAnimationStep
    {
        [Tooltip("Which property this step tweens. Changing it swaps the fields shown below.")]
        public UIAnimationStepType Type = UIAnimationStepType.CanvasGroupAlpha;

        [Tooltip("After Previous = runs once the step above has finished (Append).\n" +
                 "With Previous = runs at the same time as the step above (Join).")]
        public UIAnimationStartMode Start = UIAnimationStartMode.AfterPrevious;

        [Tooltip("What to animate. Leave empty to use the GameObject this UIAnimationPlayer is on.")]
        public RectTransform RectTarget;

        [Tooltip("What to animate. Leave empty to use the GameObject this UIAnimationPlayer is on.")]
        public CanvasGroup CanvasGroupTarget;

        [Tooltip("What to animate. Accepts Image, RawImage, Text and TextMeshProUGUI.\n" +
                 "Leave empty to use the GameObject this UIAnimationPlayer is on.")]
        public Graphic GraphicTarget;

        [Tooltip("The UIMaterialInstance component holding the per-element material clone.\n" +
                 "Leave empty to use the GameObject this UIAnimationPlayer is on.")]
        public UIMaterialInstance MaterialTarget;

        [Tooltip("The GameObject to enable or disable. Leave empty to use this one.")]
        public GameObject ActiveTarget;

        [Tooltip("Which AudioSource plays the clip. Leave empty to use an AudioSource on this GameObject, " +
                 "or - if there isn't one - a shared 2D source the framework creates on first use.")]
        public AudioSource AudioSourceTarget;

        [Tooltip("Optional path from the GameObject this player is on to the one to animate, e.g. \"Panel/Icon\".\n\n" +
                 "Only used when the slot above is empty - a direct reference always wins, and the path is " +
                 "then ignored. With both empty, the player's own GameObject is animated.\n\n" +
                 "Uses Transform.Find, so names must match exactly. \"..\" steps up to the parent, so " +
                 "\"../Icon\" is a sibling. Inactive objects are found. A path that matches nothing warns " +
                 "once and the step is skipped.")]
        public string TargetPath;

        [Tooltip("How long the tween runs, in seconds. Does not include Delay.")]
        public float Duration = 0.25f;

        [Tooltip("Seconds to wait before this step starts, measured from wherever Start places it.")]
        public float Delay;

        [Tooltip("Easing curve preset. Out* eases decelerate into the end value and suit most UI.\n\n" +
                 "Custom Curve, at the top of the list, swaps the preset for a curve you draw yourself.")]
        public Ease EaseType = Ease.OutQuad;

        [Tooltip("Use a hand-drawn AnimationCurve instead of the Ease preset. Set by picking Custom Curve " +
                 "in the Ease dropdown; the preset underneath is kept for when you pick one again.")]
        public bool UseCustomCurve;

        [Tooltip("Custom easing. Time runs 0 to 1 left to right; value 0 = the FROM value, 1 = the TO value.\n" +
                 "Going above 1 or below 0 overshoots, which is how you build a bounce.")]
        public AnimationCurve Curve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Tooltip("FROM = the step starts from an authored value and travels to To.\n" +
                 "TO = no starting value: the step travels to To from wherever the property already is.\n" +
                 "Click to switch.")]
        public bool UseFrom;

        [Tooltip("Absolute = the value as typed.\nBaseline = resting value captured at Awake, plus the value as an offset.")]
        public UIAnimationEndpointMode FromMode = UIAnimationEndpointMode.Absolute;

        [Tooltip("Absolute = the value as typed.\n" +
                 "Baseline = resting value captured at Awake, plus the value as an offset. Use this to land on the authored state.\n" +
                 "Current = relative to the value when the tween starts. Only offered while the button reads TO (no From).")]
        public UIAnimationEndpointMode ToMode = UIAnimationEndpointMode.Absolute;

        [Tooltip("Starting value for this step.")]
        public Vector3 FromVector;

        [Tooltip("Target value for this step.")]
        public Vector3 ToVector;

        [Tooltip("Starting value for this step.")]
        public float FromFloat;

        [Tooltip("Target value for this step.")]
        public float ToFloat = 1f;

        [Tooltip("Starting value for this step.")]
        public Color FromColor = Color.white;

        [Tooltip("Target value for this step.")]
        public Color ToColor = Color.white;

        [Tooltip("Shader property name to drive, e.g. _Progress. Must exist on the material's shader.")]
        public string ShaderProperty = "_Progress";

        [Tooltip("The active state to apply when this step is reached.")]
        public bool ActiveValue = true;

        [Tooltip("The sound to play when this step is reached. Played as a one-shot, so it is never " +
                 "cut off by the next sound and keeps playing if the animation is stopped.")]
        public AudioClip Clip;

        [Tooltip("Volume multiplier. 1 = the clip's own volume. Above 1 boosts it.")]
        public float Volume = 1f;

        [Tooltip("Playback speed and therefore pitch. 1 = normal. This is written to the AudioSource, " +
                 "so it also affects other sounds still playing on that same source.")]
        public float Pitch = 1f;

        [Tooltip("Random pitch spread, applied as Pitch +/- this amount. 0 = every play sounds identical. " +
                 "0.05 to 0.15 stops a repeated click sounding like a machine gun.")]
        public float PitchVariation;

        [Tooltip("How many times the punch or shake oscillates over its Duration. Higher = busier.")]
        public int Vibrato = 10;

        [Tooltip("How far the punch is allowed to overshoot past its starting value. 0 = no overshoot, 1 = full.")]
        public float Elasticity = 1f;

        [Tooltip("How random the shake direction is, in degrees. 0 = shakes along one axis only.")]
        public float Randomness = 90f;

        [Tooltip("Round this step's positions or sizes to whole units each frame. Useful for pixel art, " +
                 "causes stepping otherwise.\n\n" +
                 "Usually set once for the whole animation with its own Snapping box instead. This one is " +
                 "shown when the animation's Snap Per Step is on, and always snaps when ticked.")]
        public bool Snapping;

        [Tooltip("Travel to To along a path through the points below, instead of in a straight line.\n\n" +
                 "The ease still applies - it controls how far along the path the step is, so an Out ease " +
                 "decelerates into To along the curve.")]
        public bool UseCustomPath;

        [Tooltip("Curved = a smooth curve through every point.\n" +
                 "Linear = straight lines between the points, with a sharp corner at each one.")]
        public UIAnimationPathShape PathShape = UIAnimationPathShape.Curved;

        [Tooltip("Points the step passes through, in order, between where it starts and To.\n\n" +
                 "They are in the same space as To and follow To's mode: Absolute = as typed, Baseline = an " +
                 "offset from the resting value, Current = an offset from wherever the step starts.")]
        public List<Vector3> Waypoints = new List<Vector3>();

        // Runtime only. Never serialized, so authored data is never mutated by play mode.
        [NonSerialized] private RectTransform rect;
        [NonSerialized] private CanvasGroup canvasGroup;
        [NonSerialized] private Graphic graphic;
        [NonSerialized] private UIMaterialInstance materialInstance;
        [NonSerialized] private GameObject activeObject;
        [NonSerialized] private AudioSource audioSource;

        [NonSerialized] private Vector3 baselineVector;
        [NonSerialized] private float baselineFloat;
        [NonSerialized] private Color baselineColor;
        [NonSerialized] private bool baselineActive;

        // The Type the baseline was captured under, so RestoreBaseline writes back the property that
        // was actually read even if Type has been changed in the Inspector since.
        [NonSerialized] private UIAnimationStepType baselineType;

        [NonSerialized] private int shaderPropertyId;
        [NonSerialized] private bool shaderPropertyValid;
        [NonSerialized] private bool targetPathMissed;

    #if UNITY_EDITOR
        /// <summary>
        /// Set by the inspector's edit-mode preview, for as long as a preview is running, so PlaySound
        /// steps do nothing. Static because preview is a single, editor-only, one-at-a-time operation;
        /// there is nothing to scope it to.
        /// </summary>
        public static bool EditorSuppressSound;

        // The edit-mode preview's second capture: where this step's property stood when the most
        // recent preview Play started, as opposed to the baseline, which is where it rests. It is
        // what lets Play carry on from where the last preview left things and still start the same
        // animation over from the same place. Same shape as the baseline, and captured by the same
        // switch, so no step type can be covered by one and missed by the other.
        [NonSerialized] private bool hasSnapshot;
        [NonSerialized] private UnityEngine.Object snapshotTarget;
        [NonSerialized] private UIAnimationStepType snapshotType;
        [NonSerialized] private Vector3 snapshotVector;
        [NonSerialized] private float snapshotFloat;
        [NonSerialized] private Color snapshotColor;
        [NonSerialized] private bool snapshotActive;
    #endif

        // Built on first use and reconfigured per build, so replaying a stepped animation does
        // not allocate. Only one sequence per animation is ever live, so it is never shared.
        [NonSerialized] private UIAnimationSteppedEase steppedEase;

        /// <summary>How long the tween itself runs. Zero for instant steps.</summary>
        public float TweenDuration
        {
            get { return IsInstant(Type) ? 0f : Mathf.Max(0f, Duration); }
        }

        /// <summary>Time this step occupies from its group's start, delay included.</summary>
        public float TotalDuration
        {
            get { return Delay + TweenDuration; }
        }

        /// <summary>
        /// True when the step is a DOTween relative tween - no FROM, and a TO that is an offset
        /// from wherever the value happens to be.
        /// </summary>
        private bool IsRelative
        {
            get { return !UseFrom && ToMode == UIAnimationEndpointMode.Current; }
        }

        /// <summary>
        /// True when the step declares where it starts, i.e. when it has a usable FROM.
        /// </summary>
        private bool HasAuthoredStart
        {
            get { return UseFrom && !IsImpulse(Type) && FromMode != UIAnimationEndpointMode.Current; }
        }

        /// <summary>
        /// True for steps that happen at a point in time rather than over one. These become a
        /// sequence callback instead of a tween, and have no Duration or easing.
        /// </summary>
        public static bool IsInstant(UIAnimationStepType type)
        {
            return type == UIAnimationStepType.SetActive
                || type == UIAnimationStepType.PlaySound;
        }

        public static UIAnimationValueKind ValueKindOf(UIAnimationStepType type)
        {
            switch (type)
            {
                case UIAnimationStepType.CanvasGroupAlpha:
                case UIAnimationStepType.GraphicAlpha:
                case UIAnimationStepType.MaterialFloat:
                case UIAnimationStepType.ShakeAnchoredPosition:
                    return UIAnimationValueKind.Float;

                case UIAnimationStepType.GraphicColor:
                case UIAnimationStepType.MaterialColor:
                    return UIAnimationValueKind.Color;

                case UIAnimationStepType.SetActive:
                case UIAnimationStepType.PlaySound:
                    return UIAnimationValueKind.None;

                default:
                    return UIAnimationValueKind.Vector;
            }
        }

        public static UIAnimationTargetKind TargetKindOf(UIAnimationStepType type)
        {
            switch (type)
            {
                case UIAnimationStepType.CanvasGroupAlpha:
                    return UIAnimationTargetKind.CanvasGroup;

                case UIAnimationStepType.GraphicColor:
                case UIAnimationStepType.GraphicAlpha:
                    return UIAnimationTargetKind.Graphic;

                case UIAnimationStepType.MaterialFloat:
                case UIAnimationStepType.MaterialColor:
                    return UIAnimationTargetKind.Material;

                case UIAnimationStepType.SetActive:
                    return UIAnimationTargetKind.GameObject;

                case UIAnimationStepType.PlaySound:
                    return UIAnimationTargetKind.Audio;

                default:
                    return UIAnimationTargetKind.Rect;
            }
        }

        /// <summary>True for punch/shake, which have no meaningful FROM/TO pair.</summary>
        public static bool IsImpulse(UIAnimationStepType type)
        {
            switch (type)
            {
                case UIAnimationStepType.PunchAnchoredPosition:
                case UIAnimationStepType.PunchRotation:
                case UIAnimationStepType.PunchScale:
                case UIAnimationStepType.ShakeAnchoredPosition:
                case UIAnimationStepType.ShakeRotation:
                case UIAnimationStepType.ShakeScale:
                    return true;

                default:
                    return false;
            }
        }

        /// <summary>
        /// True for the step types DOTween can round to whole units as they play: the ones that move
        /// or resize a rect. Scale and rotation have no snapping option in DOTween.
        /// </summary>
        public static bool SupportsSnapping(UIAnimationStepType type)
        {
            switch (type)
            {
                case UIAnimationStepType.AnchoredPosition:
                case UIAnimationStepType.LocalPosition:
                case UIAnimationStepType.PunchAnchoredPosition:
                case UIAnimationStepType.ShakeAnchoredPosition:
                case UIAnimationStepType.SizeDelta:
                case UIAnimationStepType.OffsetMin:
                case UIAnimationStepType.OffsetMax:
                    return true;

                default:
                    return false;
            }
        }

        /// <summary>
        /// Points this step at the thing it drives. Call before CaptureBaseline.
        ///
        /// Three tiers, in order: the direct reference in the step's own target slot, then
        /// TargetPath resolved against the owner, then the owner's own component. An empty slot
        /// and an empty path are the ordinary case and mean "the GameObject the player is on",
        /// which is what makes an animation with no targets portable to any object.
        /// </summary>
        public void Resolve(GameObject owner)
        {
            // A filled slot wins outright and the path is never looked up. It is ignored, so it must
            // not be able to warn about - let alone skip - a step that has a perfectly good target.
            targetPathMissed = false;
            GameObject host = DirectTarget() != null ? null : ResolveHost(owner);

            switch (TargetKindOf(Type))
            {
                case UIAnimationTargetKind.Rect:
                    rect = RectTarget != null ? RectTarget : ComponentOn<RectTransform>(host);
                    break;

                case UIAnimationTargetKind.CanvasGroup:
                    canvasGroup = CanvasGroupTarget != null ? CanvasGroupTarget : ComponentOn<CanvasGroup>(host);
                    break;

                case UIAnimationTargetKind.Graphic:
                    graphic = GraphicTarget != null ? GraphicTarget : ComponentOn<Graphic>(host);
                    break;

                case UIAnimationTargetKind.Material:
                    materialInstance = MaterialTarget != null ? MaterialTarget : ComponentOn<UIMaterialInstance>(host);
                    ResolveShaderProperty(owner);
                    break;

                case UIAnimationTargetKind.GameObject:
                    activeObject = ActiveTarget != null ? ActiveTarget : host;
                    break;

                case UIAnimationTargetKind.Audio:
                    // May stay null. The shared fallback source is resolved lazily at play time,
                    // so a player that never actually fires a sound never creates one. A missed
                    // path therefore lands on the shared source rather than skipping the sound -
                    // this slot is optional by design, so an empty one is not a broken step.
                    audioSource = AudioSourceTarget != null ? AudioSourceTarget : ComponentOn<AudioSource>(host);

                    if (Clip == null)
                    {
                        Debug.LogWarning(
                            "UIAnimationPlayer on '" + owner.name +
                            "': a Play Sound step has no Clip assigned. It will be skipped.", owner);
                    }
                    break;
            }
        }

        /// <summary>
        /// The GameObject an empty target slot falls back to: the child named by TargetPath, or
        /// the owner when no path is authored.
        ///
        /// Returns null when a path was authored and matched nothing, so the step ends up with no
        /// target and is skipped with the ordinary missing-target warning. Falling back to the
        /// owner instead would quietly animate the wrong object - a mistyped "Panel/Icon" would
        /// scale the whole panel, which is far harder to spot than a step that does nothing.
        ///
        /// Warns from here, which runs once per Initialize, rather than per frame - the same
        /// discipline as the shader property check below.
        /// </summary>
        private GameObject ResolveHost(GameObject owner)
        {
            GameObject host = FindHost(owner, TargetPath);
            if (host != null) return host;

            targetPathMissed = true;

            // A sound's slot is optional by design, so a miss there falls back rather than skipping.
            string consequence = TargetKindOf(Type) == UIAnimationTargetKind.Audio
                ? "The sound will play on the shared UI source instead."
                : "The step will be skipped.";

            Debug.LogWarning(
                "UIAnimationPlayer on '" + owner.name + "': a " + Type + " step has Target Path '" +
                TargetPath + "', which matches nothing from '" + owner.name + "'. " + consequence,
                owner);

            return null;
        }

        /// <summary>The step's own target slot for its current Type - the one the Inspector shows.</summary>
        private UnityEngine.Object DirectTarget()
        {
            switch (TargetKindOf(Type))
            {
                case UIAnimationTargetKind.CanvasGroup: return CanvasGroupTarget;
                case UIAnimationTargetKind.Graphic: return GraphicTarget;
                case UIAnimationTargetKind.Material: return MaterialTarget;
                case UIAnimationTargetKind.GameObject: return ActiveTarget;
                case UIAnimationTargetKind.Audio: return AudioSourceTarget;
                default: return RectTarget;
            }
        }

        /// <summary>
        /// The quiet half of ResolveHost: the owner when no path is authored, the object at the path
        /// when it matches, null when it does not. Public so the scene-view path editor resolves a
        /// step's target by exactly the rules playback uses, without the warning - it runs every
        /// repaint.
        /// </summary>
        public static GameObject FindHost(GameObject owner, string targetPath)
        {
            if (owner == null) return null;
            if (string.IsNullOrEmpty(targetPath)) return owner;

            // Transform.Find takes a slash-separated path and does find inactive children, which
            // matters for a SetActive step whose whole job is to switch a hidden one back on.
            Transform found = owner.transform.Find(targetPath);
            return found != null ? found.gameObject : null;
        }

        /// <summary>GetComponent that tolerates the null host a missed TargetPath produces.</summary>
        private static T ComponentOn<T>(GameObject host) where T : Component
        {
            return host != null ? host.GetComponent<T>() : null;
        }

        /// <summary>
        /// Empties every direct target slot, and reports whether it had to.
        ///
        /// For UIAnimationAsset, which cannot hold one: a ScriptableObject has no scene to
        /// reference, so a slot filled anyway - by a paste from a player, which carries live
        /// instance IDs - would serialize to null at the next save with nothing said about it.
        /// Cleared at authoring time instead, where it can be explained.
        /// </summary>
        public bool ClearDirectTargets()
        {
            bool any = RectTarget != null
                || CanvasGroupTarget != null
                || GraphicTarget != null
                || MaterialTarget != null
                || ActiveTarget != null
                || AudioSourceTarget != null;

            if (!any) return false;

            RectTarget = null;
            CanvasGroupTarget = null;
            GraphicTarget = null;
            MaterialTarget = null;
            ActiveTarget = null;
            AudioSourceTarget = null;

            return true;
        }

        /// <summary>
        /// Records the target resting value. Called once at Awake before anything animates,
        /// so every step touching the same target agrees on the same baseline.
        /// </summary>
        public void CaptureBaseline()
        {
            baselineType = Type;

            switch (Type)
            {
                // Punch and shake never read their baseline - they are relative to wherever they
                // start - but capturing it is what lets the edit-mode preview put back one that was
                // stopped half way through an oscillation.
                case UIAnimationStepType.AnchoredPosition:
                case UIAnimationStepType.PunchAnchoredPosition:
                case UIAnimationStepType.ShakeAnchoredPosition:
                    if (rect != null) baselineVector = rect.anchoredPosition;
                    break;

                case UIAnimationStepType.LocalPosition:
                    if (rect != null) baselineVector = rect.localPosition;
                    break;

                case UIAnimationStepType.Scale:
                case UIAnimationStepType.PunchScale:
                case UIAnimationStepType.ShakeScale:
                    if (rect != null) baselineVector = rect.localScale;
                    break;

                case UIAnimationStepType.Rotation:
                case UIAnimationStepType.PunchRotation:
                case UIAnimationStepType.ShakeRotation:
                    if (rect != null) baselineVector = rect.localEulerAngles;
                    break;

                case UIAnimationStepType.SizeDelta:
                    if (rect != null) baselineVector = rect.sizeDelta;
                    break;

                case UIAnimationStepType.OffsetMin:
                    if (rect != null) baselineVector = rect.offsetMin;
                    break;

                case UIAnimationStepType.OffsetMax:
                    if (rect != null) baselineVector = rect.offsetMax;
                    break;

                case UIAnimationStepType.CanvasGroupAlpha:
                    if (canvasGroup != null) baselineFloat = canvasGroup.alpha;
                    break;

                case UIAnimationStepType.GraphicColor:
                    if (graphic != null) baselineColor = graphic.color;
                    break;

                case UIAnimationStepType.GraphicAlpha:
                    if (graphic != null) baselineFloat = graphic.color.a;
                    break;

                case UIAnimationStepType.MaterialFloat:
                    if (HasMaterial()) baselineFloat = materialInstance.Material.GetFloat(shaderPropertyId);
                    break;

                case UIAnimationStepType.MaterialColor:
                    if (HasMaterial()) baselineColor = materialInstance.Material.GetColor(shaderPropertyId);
                    break;

                case UIAnimationStepType.SetActive:
                    if (activeObject != null) baselineActive = activeObject.activeSelf;
                    break;
            }
        }

        /// <summary>
        /// Writes the captured resting value back onto the target - the exact inverse of
        /// CaptureBaseline, property for property.
        ///
        /// This is what makes edit-mode preview safe: an animation previewed in the Inspector
        /// writes to real scene objects, and without this the values would simply stay wherever
        /// the preview stopped and get saved into the scene as if you had authored them.
        ///
        /// Only ever touches the one property this step drives, which is why it cannot be a
        /// blanket EditorJsonUtility round-trip of the component: that would also rewrite object
        /// reference fields (an Image's sprite and material) and blank them.
        ///
        /// Switches on the Type the baseline was captured under rather than the current one, so a
        /// Type changed in the Inspector mid-preview puts back the property that was really read
        /// instead of writing a scale into a position. PlaySound has nothing to restore.
        /// </summary>
        public void RestoreBaseline()
        {
            switch (baselineType)
            {
                // Punch and shake end where they began when they run to the end, but not when a
                // preview is stopped half way through one.
                case UIAnimationStepType.AnchoredPosition:
                case UIAnimationStepType.PunchAnchoredPosition:
                case UIAnimationStepType.ShakeAnchoredPosition:
                    if (rect != null) rect.anchoredPosition = baselineVector;
                    break;

                case UIAnimationStepType.LocalPosition:
                    if (rect != null) rect.localPosition = baselineVector;
                    break;

                case UIAnimationStepType.Scale:
                case UIAnimationStepType.PunchScale:
                case UIAnimationStepType.ShakeScale:
                    if (rect != null) rect.localScale = baselineVector;
                    break;

                case UIAnimationStepType.Rotation:
                case UIAnimationStepType.PunchRotation:
                case UIAnimationStepType.ShakeRotation:
                    if (rect != null) rect.localEulerAngles = baselineVector;
                    break;

                case UIAnimationStepType.SizeDelta:
                    if (rect != null) rect.sizeDelta = baselineVector;
                    break;

                case UIAnimationStepType.OffsetMin:
                    if (rect != null) rect.offsetMin = baselineVector;
                    break;

                case UIAnimationStepType.OffsetMax:
                    if (rect != null) rect.offsetMax = baselineVector;
                    break;

                case UIAnimationStepType.CanvasGroupAlpha:
                    if (canvasGroup != null) canvasGroup.alpha = baselineFloat;
                    break;

                case UIAnimationStepType.GraphicColor:
                    if (graphic != null) graphic.color = baselineColor;
                    break;

                case UIAnimationStepType.GraphicAlpha:
                    if (graphic != null)
                    {
                        Color c = graphic.color;
                        c.a = baselineFloat;
                        graphic.color = c;
                    }
                    break;

                case UIAnimationStepType.MaterialFloat:
                    if (HasMaterial()) materialInstance.Material.SetFloat(shaderPropertyId, baselineFloat);
                    break;

                case UIAnimationStepType.MaterialColor:
                    if (HasMaterial()) materialInstance.Material.SetColor(shaderPropertyId, baselineColor);
                    break;

                case UIAnimationStepType.SetActive:
                    if (activeObject != null) activeObject.SetActive(baselineActive);
                    break;
            }
        }

    #if UNITY_EDITOR
        /// <summary>
        /// Records where this step's property stands right now, for the edit-mode preview. Reuses
        /// CaptureBaseline's own switch with the baseline swapped out of the way for the duration.
        /// </summary>
        public void EditorCaptureSnapshot()
        {
            SwapSnapshot();
            CaptureBaseline();
            SwapSnapshot();

            snapshotTarget = ResolvedObject(snapshotType);
            hasSnapshot = true;
        }

        /// <summary>
        /// Writes back what EditorCaptureSnapshot recorded. Skipped when the step now resolves to a
        /// different object than it did then - a target changed in the Inspector mid-preview - so
        /// one object's value is never written onto another.
        /// </summary>
        public void EditorRestoreSnapshot()
        {
            if (!hasSnapshot || ResolvedObject(snapshotType) != snapshotTarget) return;

            SwapSnapshot();
            RestoreBaseline();
            SwapSnapshot();
        }

        private void SwapSnapshot()
        {
            Vector3 vector = baselineVector;
            baselineVector = snapshotVector;
            snapshotVector = vector;

            float single = baselineFloat;
            baselineFloat = snapshotFloat;
            snapshotFloat = single;

            Color color = baselineColor;
            baselineColor = snapshotColor;
            snapshotColor = color;

            bool active = baselineActive;
            baselineActive = snapshotActive;
            snapshotActive = active;

            UIAnimationStepType type = baselineType;
            baselineType = snapshotType;
            snapshotType = type;
        }

        /// <summary>The resolved object a step of this type writes to, including a material's owner.</summary>
        private UnityEngine.Object ResolvedObject(UIAnimationStepType type)
        {
            switch (TargetKindOf(type))
            {
                case UIAnimationTargetKind.CanvasGroup: return canvasGroup;
                case UIAnimationTargetKind.Graphic: return graphic;
                case UIAnimationTargetKind.Material: return materialInstance;
                case UIAnimationTargetKind.GameObject: return activeObject;
                case UIAnimationTargetKind.Audio: return audioSource;
                default: return rect;
            }
        }
    #endif

        /// <summary>
        /// The Unity object this step writes to, or null for steps that write to none.
        /// Used by the editor preview to build its Undo record before anything moves.
        /// </summary>
        public UnityEngine.Object ResolvedTarget()
        {
            switch (TargetKindOf(Type))
            {
                case UIAnimationTargetKind.Rect: return rect;
                case UIAnimationTargetKind.CanvasGroup: return canvasGroup;
                case UIAnimationTargetKind.Graphic: return graphic;
                case UIAnimationTargetKind.GameObject: return activeObject;

                // The material is a runtime clone owned by UIMaterialInstance, not a scene object,
                // so there is nothing for Undo to record. RestoreBaseline still puts it back.
                default: return null;
            }
        }

        /// <summary>Writes this step's FROM value straight to the target, without playing anything.</summary>
        public void ApplyFromValue()
        {
            if (!HasAuthoredStart) return;

            switch (Type)
            {
                case UIAnimationStepType.AnchoredPosition:
                    if (rect != null) rect.anchoredPosition = ResolveVector(FromMode, FromVector);
                    break;

                case UIAnimationStepType.LocalPosition:
                    if (rect != null) rect.localPosition = ResolveVector(FromMode, FromVector);
                    break;

                case UIAnimationStepType.Scale:
                    if (rect != null) rect.localScale = ResolveVector(FromMode, FromVector);
                    break;

                case UIAnimationStepType.Rotation:
                    if (rect != null) rect.localEulerAngles = ResolveVector(FromMode, FromVector);
                    break;

                case UIAnimationStepType.SizeDelta:
                    if (rect != null) rect.sizeDelta = ResolveVector(FromMode, FromVector);
                    break;

                case UIAnimationStepType.OffsetMin:
                    if (rect != null) rect.offsetMin = ResolveVector(FromMode, FromVector);
                    break;

                case UIAnimationStepType.OffsetMax:
                    if (rect != null) rect.offsetMax = ResolveVector(FromMode, FromVector);
                    break;

                case UIAnimationStepType.CanvasGroupAlpha:
                    if (canvasGroup != null) canvasGroup.alpha = ResolveFloat(FromMode, FromFloat);
                    break;

                case UIAnimationStepType.GraphicColor:
                    if (graphic != null) graphic.color = ResolveColor(FromMode, FromColor);
                    break;

                case UIAnimationStepType.GraphicAlpha:
                    if (graphic != null)
                    {
                        Color c = graphic.color;
                        c.a = ResolveFloat(FromMode, FromFloat);
                        graphic.color = c;
                    }
                    break;

                case UIAnimationStepType.MaterialFloat:
                    if (HasMaterial()) materialInstance.Material.SetFloat(shaderPropertyId, ResolveFloat(FromMode, FromFloat));
                    break;

                case UIAnimationStepType.MaterialColor:
                    if (HasMaterial()) materialInstance.Material.SetColor(shaderPropertyId, ResolveColor(FromMode, FromColor));
                    break;
            }
        }

        /// <summary>
        /// Builds a fully configured tween. Everything (From/Ease/Relative) is applied HERE,
        /// before the caller hands it to the Sequence - DOTween silently ignores those calls
        /// once a tween has been inserted into one. Delay is deliberately NOT applied: the
        /// player positions every tween explicitly, and DOTween adds a tween's delay on top of
        /// its insert position rather than instead of it.
        /// Returns null for instant steps (the player turns those into a callback) and for
        /// steps whose target is missing.
        ///
        /// frameRate above 0 makes the step advance in discrete frames instead of smoothly, and
        /// timelineOffset is where it sits in the sequence, so every step shares one frame grid.
        /// The frame rate arrives per step deliberately - a per-step override would only need the
        /// player to resolve a different number, not any new plumbing here.
        ///
        /// snapAll is the animation's own Snapping. It adds to this step's box rather than replacing
        /// it, so a step authored with Snapping ticked before the animation had one still snaps.
        /// </summary>
        public Tween BuildTween(bool applyFromImmediately, float frameRate, float timelineOffset, bool snapAll, string context)
        {
            if (IsInstant(Type)) return null;
            if (!HasTarget(context)) return null;

            bool useFrom = HasAuthoredStart;
            bool relative = IsRelative;
            bool snap = Snapping || snapAll;
            Tween tween;

            if (HasPath)
            {
                tween = BuildPathTween(applyFromImmediately, snap);
                if (tween == null) return null;
            }
            else switch (Type)
            {
                case UIAnimationStepType.AnchoredPosition:
                {
                    var t = rect.DOAnchorPos(ResolveVector(ToMode, ToVector), Duration, snap);
                    if (useFrom) t.From((Vector2)ResolveVector(FromMode, FromVector), applyFromImmediately);
                    else if (relative) t.SetRelative(true);
                    tween = t;
                    break;
                }

                case UIAnimationStepType.LocalPosition:
                {
                    var t = rect.DOLocalMove(ResolveVector(ToMode, ToVector), Duration, snap);
                    if (useFrom) t.From(ResolveVector(FromMode, FromVector), applyFromImmediately);
                    else if (relative) t.SetRelative(true);
                    tween = t;
                    break;
                }

                case UIAnimationStepType.Scale:
                {
                    var t = rect.DOScale(ResolveVector(ToMode, ToVector), Duration);
                    if (useFrom) t.From(ResolveVector(FromMode, FromVector), applyFromImmediately);
                    else if (relative) t.SetRelative(true);
                    tween = t;
                    break;
                }

                case UIAnimationStepType.Rotation:
                {
                    var t = rect.DOLocalRotate(ResolveVector(ToMode, ToVector), Duration, RotateMode.FastBeyond360);
                    if (useFrom) t.From(ResolveVector(FromMode, FromVector), applyFromImmediately);
                    else if (relative) t.SetRelative(true);
                    tween = t;
                    break;
                }

                case UIAnimationStepType.SizeDelta:
                {
                    var t = rect.DOSizeDelta(ResolveVector(ToMode, ToVector), Duration, snap);
                    if (useFrom) t.From((Vector2)ResolveVector(FromMode, FromVector), applyFromImmediately);
                    else if (relative) t.SetRelative(true);
                    tween = t;
                    break;
                }

                case UIAnimationStepType.OffsetMin:
                {
                    // DOTween has no offsetMin/offsetMax shortcut, so drive the property directly.
                    // The generic To() tween supports From, SetRelative and snapping just the same.
                    RectTransform target = rect;
                    var t = DOTween.To(() => target.offsetMin, v => target.offsetMin = v,
                                       (Vector2)ResolveVector(ToMode, ToVector), Duration);
                    t.SetOptions(snap);
                    if (useFrom) t.From((Vector2)ResolveVector(FromMode, FromVector), applyFromImmediately);
                    else if (relative) t.SetRelative(true);
                    tween = t;
                    break;
                }

                case UIAnimationStepType.OffsetMax:
                {
                    RectTransform target = rect;
                    var t = DOTween.To(() => target.offsetMax, v => target.offsetMax = v,
                                       (Vector2)ResolveVector(ToMode, ToVector), Duration);
                    t.SetOptions(snap);
                    if (useFrom) t.From((Vector2)ResolveVector(FromMode, FromVector), applyFromImmediately);
                    else if (relative) t.SetRelative(true);
                    tween = t;
                    break;
                }

                case UIAnimationStepType.CanvasGroupAlpha:
                {
                    var t = canvasGroup.DOFade(ResolveFloat(ToMode, ToFloat), Duration);
                    if (useFrom) t.From(ResolveFloat(FromMode, FromFloat), applyFromImmediately);
                    else if (relative) t.SetRelative(true);
                    tween = t;
                    break;
                }

                case UIAnimationStepType.GraphicColor:
                {
                    var t = graphic.DOColor(ResolveColor(ToMode, ToColor), Duration);
                    if (useFrom) t.From(ResolveColor(FromMode, FromColor), applyFromImmediately);
                    else if (relative) t.SetRelative(true);
                    tween = t;
                    break;
                }

                case UIAnimationStepType.GraphicAlpha:
                {
                    var t = graphic.DOFade(ResolveFloat(ToMode, ToFloat), Duration);
                    if (useFrom) t.From(ResolveFloat(FromMode, FromFloat), applyFromImmediately);
                    else if (relative) t.SetRelative(true);
                    tween = t;
                    break;
                }

                case UIAnimationStepType.MaterialFloat:
                {
                    var t = materialInstance.Material.DOFloat(ResolveFloat(ToMode, ToFloat), shaderPropertyId, Duration);
                    if (useFrom) t.From(ResolveFloat(FromMode, FromFloat), applyFromImmediately);
                    else if (relative) t.SetRelative(true);
                    tween = t;
                    break;
                }

                case UIAnimationStepType.MaterialColor:
                {
                    var t = materialInstance.Material.DOColor(ResolveColor(ToMode, ToColor), shaderPropertyId, Duration);
                    if (useFrom) t.From(ResolveColor(FromMode, FromColor), applyFromImmediately);
                    else if (relative) t.SetRelative(true);
                    tween = t;
                    break;
                }

                case UIAnimationStepType.PunchScale:
                    tween = rect.DOPunchScale(ToVector, Duration, Vibrato, Elasticity);
                    break;

                case UIAnimationStepType.PunchAnchoredPosition:
                    tween = rect.DOPunchAnchorPos(ToVector, Duration, Vibrato, Elasticity, snap);
                    break;

                case UIAnimationStepType.PunchRotation:
                    tween = rect.DOPunchRotation(ToVector, Duration, Vibrato, Elasticity);
                    break;

                case UIAnimationStepType.ShakeAnchoredPosition:
                    tween = rect.DOShakeAnchorPos(Duration, ToFloat, Vibrato, Randomness, snap);
                    break;

                // The rotation and scale shakes take a per-axis strength rather than ShakeAnchoredPosition's
                // single number. A single number shakes all three axes, and on a UI element rotating
                // around X or Y reads as the element tipping over in 3D - Z alone is the usual shake.
                case UIAnimationStepType.ShakeRotation:
                    tween = rect.DOShakeRotation(Duration, ToVector, Vibrato, Randomness);
                    break;

                case UIAnimationStepType.ShakeScale:
                    tween = rect.DOShakeScale(Duration, ToVector, Vibrato, Randomness);
                    break;

                default:
                    return null;
            }

            // Punch and shake carry their own internal easing; overriding it looks wrong. Frame
            // stepping rides on the ease, so it is also what leaves those two steps smooth.
            if (!IsImpulse(Type))
            {
                bool useCurve = UseCustomCurve && Curve != null && Curve.length > 0;

                if (frameRate > 0f)
                {
                    if (steppedEase == null) steppedEase = new UIAnimationSteppedEase();
                    tween.SetEase(steppedEase.Configure(EaseType, useCurve ? Curve : null, frameRate, timelineOffset));
                }
                else if (useCurve)
                {
                    tween.SetEase(Curve);
                }
                else
                {
                    tween.SetEase(EaseType);
                }
            }

            return tween;
        }

        /// <summary>
        /// True for the step types a movement path can drive: every vector step except Rotation,
        /// which DOTween rotates as a quaternion rather than through the Euler values a path would
        /// pass through, and punch/shake, which have no endpoint to travel to.
        /// </summary>
        public static bool SupportsPath(UIAnimationStepType type)
        {
            switch (type)
            {
                case UIAnimationStepType.AnchoredPosition:
                case UIAnimationStepType.LocalPosition:
                case UIAnimationStepType.Scale:
                case UIAnimationStepType.SizeDelta:
                case UIAnimationStepType.OffsetMin:
                case UIAnimationStepType.OffsetMax:
                    return true;

                default:
                    return false;
            }
        }

        /// <summary>True for vector steps whose Z is unused - the rect views, which are Vector2.</summary>
        public static bool IsTwoDimensional(UIAnimationStepType type)
        {
            return type == UIAnimationStepType.AnchoredPosition
                || type == UIAnimationStepType.PunchAnchoredPosition
                || type == UIAnimationStepType.SizeDelta
                || type == UIAnimationStepType.OffsetMin
                || type == UIAnimationStepType.OffsetMax;
        }

        /// <summary>
        /// True when this step travels along its authored path. A path with no points is a straight
        /// line, which the ordinary tween already is, so an empty list falls back to that - the step
        /// then behaves exactly as it would with the box unticked.
        /// </summary>
        public bool HasPath
        {
            get { return UseCustomPath && SupportsPath(Type) && Waypoints != null && Waypoints.Count > 0; }
        }

        /// <summary>
        /// Builds the path version of a vector step: start, then each waypoint, then To.
        ///
        /// DOTween has path shortcuts only for Transform.position/localPosition, so this uses the
        /// generic PathPlugin with a getter and setter - the same call DOTween's own Rigidbody module
        /// makes - which is what lets one path drive anchoredPosition, sizeDelta or anything else.
        ///
        /// Three things about the path plugin that this works around, all measured against DOTween
        /// 1.3.030:
        ///   - It needs a Transform as the tween's target and throws a NullReferenceException at
        ///     startup without one, so SetTarget is not optional here.
        ///   - It ignores From(). The start is whatever the getter returns at startup, so an
        ///     authored FROM is supplied by having the getter return it, and the setter writes it on
        ///     the first update like any other From.
        ///   - It prepends the start as the first point unless the first waypoint already equals it,
        ///     so the waypoint list never includes the start itself.
        ///
        /// SetRelative works as it does on every other step: every point, To included, is offset by
        /// the value at startup. That is why waypoints follow To's mode rather than having their own.
        /// </summary>
        private Tween BuildPathTween(bool applyFromImmediately, bool snap)
        {
            RectTransform target = rect;
            DOGetter<Vector3> read;
            DOSetter<Vector3> write;

            switch (Type)
            {
                case UIAnimationStepType.AnchoredPosition:
                    read = () => target.anchoredPosition;
                    write = v => target.anchoredPosition = v;
                    break;

                case UIAnimationStepType.LocalPosition:
                    read = () => target.localPosition;
                    write = v => target.localPosition = v;
                    break;

                case UIAnimationStepType.Scale:
                    read = () => target.localScale;
                    write = v => target.localScale = v;
                    break;

                case UIAnimationStepType.SizeDelta:
                    read = () => target.sizeDelta;
                    write = v => target.sizeDelta = v;
                    break;

                case UIAnimationStepType.OffsetMin:
                    read = () => target.offsetMin;
                    write = v => target.offsetMin = v;
                    break;

                case UIAnimationStepType.OffsetMax:
                    read = () => target.offsetMax;
                    write = v => target.offsetMax = v;
                    break;

                default:
                    return null;
            }

            // DOTween's own shortcuts round inside the plugin; the path plugin has no snapping
            // option, so it happens on the way out instead. Scale never offered Snapping.
            if (snap && Type != UIAnimationStepType.Scale)
            {
                DOSetter<Vector3> unsnapped = write;
                write = v => unsnapped(new Vector3(Mathf.Round(v.x), Mathf.Round(v.y), Mathf.Round(v.z)));
            }

            if (HasAuthoredStart)
            {
                Vector3 from = Flatten(ResolveVector(FromMode, FromVector));
                read = () => from;

                // The same thing From(value, setImmediately: true) does on every other step.
                if (applyFromImmediately) write(from);
            }

            Vector3[] points = PathPoints();
            PathType shape = PathShape == UIAnimationPathShape.Linear ? PathType.Linear : PathType.CatmullRom;

            // Transparent gizmo: DOTween draws a running path in the Scene view at the raw point
            // values, which for anchoredPosition or sizeDelta are not world positions and would
            // draw a misleading line somewhere unrelated. The path editor draws the real one.
            var path = new DG.Tweening.Plugins.Core.PathCore.Path(shape, points, PathResolution, Color.clear);

            var tween = DOTween.To(PathPlugin.Get(), read, write, path, Duration);
            tween.SetTarget(target);
            if (IsRelative) tween.SetRelative(true);

            return tween;
        }

        /// <summary>
        /// Subdivisions per segment for a curved path. DOTween's default; 5 is usually enough and
        /// UI paths are short, but there are never enough path steps for this to cost anything.
        /// </summary>
        private const int PathResolution = 10;

        /// <summary>
        /// The waypoints then To, resolved against To's mode, with Z dropped on the rect views and
        /// consecutive duplicates removed. A duplicate point makes a zero-length segment, and a
        /// waypoint added from the inspector starts on top of its neighbour until it is moved.
        /// </summary>
        private Vector3[] PathPoints()
        {
            var points = new List<Vector3>(Waypoints.Count + 1);

            for (int i = 0; i <= Waypoints.Count; i++)
            {
                Vector3 raw = i < Waypoints.Count ? Waypoints[i] : ToVector;
                Vector3 point = Flatten(ResolveVector(ToMode, raw));

                if (points.Count > 0 && points[points.Count - 1] == point) continue;
                points.Add(point);
            }

            return points.ToArray();
        }

        /// <summary>
        /// Zeroes Z on the rect views. The inspector edits them as X/Y, so a Z left behind by an
        /// earlier Type would otherwise bend a path through a dimension that is then thrown away.
        /// </summary>
        private Vector3 Flatten(Vector3 value)
        {
            if (IsTwoDimensional(Type)) value.z = 0f;
            return value;
        }

        /// <summary>
        /// Fills in fields that are still at their zero value, because Unity does not run C# field
        /// initialisers when you press + on a serialized list - a fresh step arrives with Duration 0
        /// and Ease Unset. Only ever fills blanks, so authored values are never overwritten.
        /// Editor-only, driven from UIAnimationPlayer.OnValidate.
        /// </summary>
        public void FillUnsetDefaults()
        {
            if (Duration == 0f) Duration = 0.25f;
            if (EaseType == Ease.Unset) EaseType = Ease.OutQuad;
            if (Vibrato == 0) Vibrato = 10;
            if (Elasticity == 0f) Elasticity = 1f;
            if (Randomness == 0f) Randomness = 90f;
            if (string.IsNullOrEmpty(ShaderProperty)) ShaderProperty = "_Progress";
            if (Curve == null || Curve.length == 0) Curve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
            if (Volume == 0f) Volume = 1f;
            if (Pitch == 0f) Pitch = 1f;

            // FromFloat/ToFloat and the colours are deliberately left alone - 0 and transparent
            // are legitimate authored values (fading to 0 is the whole point of a Hide).
        }

        /// <summary>Runs a SetActive step. The player calls this from a sequence callback.</summary>
        public void ApplyActiveValue()
        {
            if (activeObject != null) activeObject.SetActive(ActiveValue);
        }

        /// <summary>
        /// Runs a PlaySound step. The player calls this from a sequence callback, so a stopped or
        /// interrupted animation simply never reaches it.
        /// PlayOneShot is used rather than Play, so overlapping UI sounds do not cut each other off.
        /// </summary>
        public void PlaySound()
        {
            if (Clip == null) return;

    #if UNITY_EDITOR
            // Edit-mode preview leaves this on. A one-shot in edit mode would keep playing after
            // the preview stops and cannot be scrubbed, and the shared fallback source would spawn
            // a DontDestroyOnLoad GameObject into the open scene just to make a click noise.
            if (EditorSuppressSound) return;
    #endif

            AudioSource source = audioSource != null ? audioSource : UIAnimationAudio.Shared;
            if (source == null) return;

            source.pitch = PitchVariation > 0f
                ? Pitch + UnityEngine.Random.Range(-PitchVariation, PitchVariation)
                : Pitch;

            source.PlayOneShot(Clip, Mathf.Max(0f, Volume));
        }

        private Vector3 ResolveVector(UIAnimationEndpointMode mode, Vector3 value)
        {
            return mode == UIAnimationEndpointMode.Baseline ? baselineVector + value : value;
        }

        private float ResolveFloat(UIAnimationEndpointMode mode, float value)
        {
            return mode == UIAnimationEndpointMode.Baseline ? baselineFloat + value : value;
        }

        private Color ResolveColor(UIAnimationEndpointMode mode, Color value)
        {
            return mode == UIAnimationEndpointMode.Baseline ? baselineColor + value : value;
        }

        /// <summary>
        /// Caches the shader property id and validates it against the material's shader, so a typo or
        /// a blank name produces one clear warning instead of a Unity error on every tween frame.
        /// </summary>
        private void ResolveShaderProperty(GameObject owner)
        {
            shaderPropertyValid = false;

            if (string.IsNullOrEmpty(ShaderProperty))
            {
                Debug.LogWarning(
                    "UIAnimationPlayer on '" + owner.name + "': a " + Type +
                    " step has no Shader Property name set. The step will be skipped.", owner);
                return;
            }

            shaderPropertyId = Shader.PropertyToID(ShaderProperty);

            if (materialInstance == null || materialInstance.Material == null) return;

            if (!materialInstance.Material.HasProperty(shaderPropertyId))
            {
                Debug.LogWarning(
                    "UIAnimationPlayer on '" + owner.name + "': shader '" +
                    materialInstance.Material.shader.name + "' has no property '" + ShaderProperty +
                    "'. The step will be skipped.", owner);
                return;
            }

            shaderPropertyValid = true;
        }

        private bool HasMaterial()
        {
            return materialInstance != null && materialInstance.Material != null && shaderPropertyValid;
        }

        private bool HasTarget(string context)
        {
            // A missed Target Path was already reported once, at Resolve time, and named the path
            // rather than just the missing component. Saying it again on every Play - which is
            // where this runs - would turn one clear warning into a stream of vaguer ones.
            if (targetPathMissed) return false;

            switch (TargetKindOf(Type))
            {
                case UIAnimationTargetKind.Rect:
                    if (rect != null) return true;
                    break;

                case UIAnimationTargetKind.CanvasGroup:
                    if (canvasGroup != null) return true;
                    break;

                case UIAnimationTargetKind.Graphic:
                    if (graphic != null) return true;
                    break;

                case UIAnimationTargetKind.Material:
                    if (HasMaterial()) return true;

                    // A missing or misspelled shader property was already reported once, at Resolve
                    // time, with a far more useful message. Do not warn about it a second time here.
                    if (materialInstance != null && materialInstance.Material != null) return false;
                    break;

                case UIAnimationTargetKind.GameObject:
                    if (activeObject != null) return true;
                    break;

                case UIAnimationTargetKind.Audio:
                    // Never reached today - PlaySound is instant, so it never builds a tween.
                    // Returning true keeps a future change from producing a nonsense warning.
                    return true;
            }

            Debug.LogWarning(context + ": " + Type + " step has no " + TargetKindOf(Type) + " target and was skipped.");
            return false;
        }
    }
}

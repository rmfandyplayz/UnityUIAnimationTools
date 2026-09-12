// -----------------------------------------------------------------------------
// UI Animation Utility
//
// AI-GENERATED. Authored by Claude (Anthropic) via Claude Code, September 2026,
// to a written design brief by the project author. Not hand-written by the
// Twindrill Goose team. See the README.md beside this file for usage.
// -----------------------------------------------------------------------------

using System;
using DG.Tweening;
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

        [Tooltip("Optional path to a CHILD of the GameObject this player is on, e.g. \"Panel/Icon\".\n\n" +
                 "Targets resolve in this order: the slot above, then this path, then the player's own " +
                 "GameObject. Leave it empty and nothing changes.\n\n" +
                 "Uses Transform.Find, so names must match exactly and only descendants are searched. " +
                 "Inactive children are found. A path that matches nothing warns once and the step is skipped.")]
        public string TargetPath;

        [Tooltip("How long the tween runs, in seconds. Does not include Delay.")]
        public float Duration = 0.25f;

        [Tooltip("Seconds to wait before this step starts, measured from wherever Start places it.")]
        public float Delay;

        [Tooltip("Easing curve preset. Out* eases decelerate into the end value and suit most UI.")]
        public Ease EaseType = Ease.OutQuad;

        [Tooltip("Use a hand-drawn AnimationCurve instead of the Ease preset.\n" +
                 "The curve editor has a preset bar at the bottom for saving and reusing shapes.")]
        public bool UseCustomCurve;

        [Tooltip("Custom easing. Time runs 0 to 1 left to right; value 0 = the FROM value, 1 = the TO value.\n" +
                 "Going above 1 or below 0 overshoots, which is how you build a bounce.")]
        public AnimationCurve Curve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Tooltip("Tick to force a starting value. Untick to tween from wherever the property already is.")]
        public bool UseFrom;

        [Tooltip("Absolute = the value as typed.\nBaseline = resting value captured at Awake, plus the value as an offset.")]
        public UIAnimationEndpointMode FromMode = UIAnimationEndpointMode.Absolute;

        [Tooltip("Absolute = the value as typed.\n" +
                 "Baseline = resting value captured at Awake, plus the value as an offset. Use this to land on the authored state.\n" +
                 "Current = relative to the value when the tween starts. Only available when Use From is off.")]
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

        [Tooltip("Round positions to whole pixels each frame. Useful for pixel art, causes stepping otherwise.")]
        public bool Snapping;

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
        [NonSerialized] private int shaderPropertyId;
        [NonSerialized] private bool shaderPropertyValid;
        [NonSerialized] private bool targetPathMissed;

    #if UNITY_EDITOR
        /// <summary>
        /// Set by the inspector's edit-mode preview so PlaySound steps do nothing. Static because
        /// preview is a single, editor-only, one-at-a-time operation; there is nothing to scope it to.
        /// </summary>
        public static bool EditorSuppressSound;
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
            return type == UIAnimationStepType.PunchScale
                || type == UIAnimationStepType.PunchAnchoredPosition
                || type == UIAnimationStepType.ShakeAnchoredPosition;
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
            GameObject host = ResolveHost(owner);

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
            targetPathMissed = false;

            if (string.IsNullOrEmpty(TargetPath)) return owner;

            // Transform.Find takes a slash-separated path and does find inactive children, which
            // matters for a SetActive step whose whole job is to switch a hidden one back on.
            Transform found = owner.transform.Find(TargetPath);
            if (found != null) return found.gameObject;

            targetPathMissed = true;

            Debug.LogWarning(
                "UIAnimationPlayer on '" + owner.name + "': a " + Type + " step has Target Path '" +
                TargetPath + "', which matches no child of '" + owner.name + "'. The step will be skipped.",
                owner);

            return null;
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
            switch (Type)
            {
                case UIAnimationStepType.AnchoredPosition:
                    if (rect != null) baselineVector = rect.anchoredPosition;
                    break;

                case UIAnimationStepType.LocalPosition:
                    if (rect != null) baselineVector = rect.localPosition;
                    break;

                case UIAnimationStepType.Scale:
                    if (rect != null) baselineVector = rect.localScale;
                    break;

                case UIAnimationStepType.Rotation:
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
        /// Steps with nothing to restore - PlaySound, and punch/shake, which already end where
        /// they began - do nothing here.
        /// </summary>
        public void RestoreBaseline()
        {
            switch (Type)
            {
                case UIAnimationStepType.AnchoredPosition:
                    if (rect != null) rect.anchoredPosition = baselineVector;
                    break;

                case UIAnimationStepType.LocalPosition:
                    if (rect != null) rect.localPosition = baselineVector;
                    break;

                case UIAnimationStepType.Scale:
                    if (rect != null) rect.localScale = baselineVector;
                    break;

                case UIAnimationStepType.Rotation:
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

                // Punch and shake move a RectTransform and are excluded on purpose: they return to
                // their own start value, and the property they drive is already covered by whichever
                // ordinary step authored it.
                case UIAnimationStepType.SetActive:
                    if (activeObject != null) activeObject.SetActive(baselineActive);
                    break;
            }
        }

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
        /// </summary>
        public Tween BuildTween(bool applyFromImmediately, float frameRate, float timelineOffset, string context)
        {
            if (IsInstant(Type)) return null;
            if (!HasTarget(context)) return null;

            bool useFrom = HasAuthoredStart;
            bool relative = IsRelative;
            Tween tween;

            switch (Type)
            {
                case UIAnimationStepType.AnchoredPosition:
                {
                    var t = rect.DOAnchorPos(ResolveVector(ToMode, ToVector), Duration, Snapping);
                    if (useFrom) t.From((Vector2)ResolveVector(FromMode, FromVector), applyFromImmediately);
                    else if (relative) t.SetRelative(true);
                    tween = t;
                    break;
                }

                case UIAnimationStepType.LocalPosition:
                {
                    var t = rect.DOLocalMove(ResolveVector(ToMode, ToVector), Duration, Snapping);
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
                    var t = rect.DOSizeDelta(ResolveVector(ToMode, ToVector), Duration, Snapping);
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
                    t.SetOptions(Snapping);
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
                    t.SetOptions(Snapping);
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
                    tween = rect.DOPunchAnchorPos(ToVector, Duration, Vibrato, Elasticity, Snapping);
                    break;

                case UIAnimationStepType.ShakeAnchoredPosition:
                    tween = rect.DOShakeAnchorPos(Duration, ToFloat, Vibrato, Randomness, Snapping);
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

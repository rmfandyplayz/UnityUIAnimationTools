// -----------------------------------------------------------------------------
// Flipnote Style Animation Utility
//
// AI-GENERATED. Authored by Claude (Anthropic) via Claude Code, September 2026,
// to a written design brief by the project author. Not hand-written by the
// Twindrill Goose team. See the README.md beside this file for usage.
// -----------------------------------------------------------------------------

using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace rmf_claude.FlipbookAnimation
{
    /// <summary>
    /// Which flipbook a <see cref="UIFlipbookSelectable"/> is showing.
    ///
    /// Not serialized anywhere today, but the numbers are explicit anyway: the moment anything
    /// does serialize it, reordering these lines would silently repoint authored data.
    /// </summary>
    public enum UIFlipbookState
    {
        Normal = 0,
        Highlighted = 1,
        Pressed = 2,
        Selected = 3,
        Disabled = 4,
    }

    /// <summary>
    /// Swaps a <see cref="UISpriteFlipbook"/> between per-state clips as a Selectable is hovered,
    /// pressed, selected or disabled.
    ///
    /// This sits BESIDE the Selectable, it does not replace it. The EventSystem dispatches pointer
    /// events to every component on the object that implements the handler interface, so the
    /// Selectable's own onClick, navigation and transition all keep working untouched.
    ///
    /// It takes a Selectable rather than a Button, so a Toggle or a Slider handle works too.
    /// </summary>
    [DisallowMultipleComponent]
    public class UIFlipbookSelectable : MonoBehaviour,
        IPointerEnterHandler, IPointerExitHandler,
        IPointerDownHandler, IPointerUpHandler,
        ISelectHandler, IDeselectHandler, ISubmitHandler
    {
        private const float DefaultSubmitPressDuration = 0.1f;

        [Tooltip("The Button (or any Selectable) whose state is followed. Found on this GameObject if empty.")]
        [SerializeField] private Selectable Target;

        [Tooltip("The flipbook that gets driven. Found on this GameObject, then in children, if empty.")]
        [SerializeField] private UISpriteFlipbook Flipbook;

        [Tooltip("Idle. The fallback for every other state that has no frames.")]
        [SerializeField] private UIFlipbookClip Normal = new UIFlipbookClip();

        [Tooltip("Pointer is over the button. Falls back to Normal.")]
        [SerializeField] private UIFlipbookClip Highlighted = new UIFlipbookClip();

        [Tooltip("Button is held down, or was just submitted. Falls back to Highlighted, then Normal.")]
        [SerializeField] private UIFlipbookClip Pressed = new UIFlipbookClip();

        [Tooltip("Button holds EventSystem selection - keyboard/controller focus, or a mouse click that " +
                 "left it focused. Falls back to Normal. Leave empty for no focus look at all.")]
        [SerializeField] private UIFlipbookClip Selected = new UIFlipbookClip();

        [Tooltip("Button is not interactable. Falls back to Normal.")]
        [SerializeField] private UIFlipbookClip Disabled = new UIFlipbookClip();

        [Tooltip("How long the Pressed clip shows for a keyboard/controller Submit, which has no press-and-hold.")]
        [SerializeField] private float SubmitPressDuration = DefaultSubmitPressDuration;

        [Tooltip("Time the Submit flash above on unscaled time, so it still reads while Time.timeScale is 0.")]
        [SerializeField] private bool UseUnscaledTime = true;

        private bool pointerInside;
        private bool pointerDown;
        private bool hasSelection;
        private float submitTimer;

        private UIFlipbookState currentState = UIFlipbookState.Normal;

        /// <summary>The state currently being shown.</summary>
        public UIFlipbookState CurrentState
        {
            get { return currentState; }
        }

        /// <summary>Re-evaluate now and restart the matching clip. Rarely needed - Update already does this.</summary>
        public void Refresh()
        {
            ApplyState(ResolveState(), true);
        }

        private void Awake()
        {
            ResolveReferences();
        }

        private void OnEnable()
        {
            ResolveReferences();

            // Pointer and press flags cannot survive a disable - we get no exit event while off.
            pointerInside = false;
            pointerDown = false;
            submitTimer = 0f;

            var events = EventSystem.current;
            hasSelection = events != null && events.currentSelectedGameObject == gameObject;

            ApplyState(ResolveState(), true);
        }

        private void OnDisable()
        {
            pointerInside = false;
            pointerDown = false;
            hasSelection = false;
            submitTimer = 0f;
        }

        private void Start()
        {
            if (Flipbook == null)
            {
                Debug.LogWarning("[UIFlipbookSelectable] " + name + " has no UISpriteFlipbook to drive.", this);
                return;
            }

            if (Target == null)
            {
                Debug.LogWarning("[UIFlipbookSelectable] " + name + " has no Selectable, so it will sit on Disabled.", this);
            }
        }

        private void Update()
        {
            if (submitTimer > 0f)
            {
                submitTimer -= UseUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            }

            // `interactable` can be changed from code with no event to hook, so the state is
            // recomputed every frame. It is a handful of bool reads and allocates nothing;
            // ApplyState does the real work only when the state actually changed.
            ApplyState(ResolveState(), false);
        }

        // ---- EventSystem handlers. The Selectable on this object still receives all of these too. ----

        public void OnPointerEnter(PointerEventData eventData)
        {
            pointerInside = true;
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            pointerInside = false;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (eventData != null && eventData.button != PointerEventData.InputButton.Left)
            {
                return;
            }

            pointerDown = true;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (eventData != null && eventData.button != PointerEventData.InputButton.Left)
            {
                return;
            }

            pointerDown = false;
        }

        public void OnSelect(BaseEventData eventData)
        {
            hasSelection = true;
        }

        public void OnDeselect(BaseEventData eventData)
        {
            hasSelection = false;
        }

        public void OnSubmit(BaseEventData eventData)
        {
            // Keyboard/controller submit is instantaneous, so fake a press long enough to read.
            submitTimer = SubmitPressDuration > 0f ? SubmitPressDuration : DefaultSubmitPressDuration;
        }

        // ---- State ----

        /// <summary>
        /// Same priority order as UnityEngine.UI.Selectable itself:
        /// Disabled beats Pressed beats Selected beats Highlighted beats Normal.
        /// </summary>
        private UIFlipbookState ResolveState()
        {
            if (Target == null || !Target.IsInteractable())
            {
                return UIFlipbookState.Disabled;
            }

            if (pointerDown || submitTimer > 0f)
            {
                return UIFlipbookState.Pressed;
            }

            if (hasSelection)
            {
                return UIFlipbookState.Selected;
            }

            if (pointerInside)
            {
                return UIFlipbookState.Highlighted;
            }

            return UIFlipbookState.Normal;
        }

        private void ApplyState(UIFlipbookState state, bool force)
        {
            if (!force && state == currentState)
            {
                return;
            }

            currentState = state;

            if (Flipbook == null)
            {
                return;
            }

            var clip = ClipFor(state);

            if (clip == null)
            {
                Flipbook.Stop();
                return;
            }

            // Always restart. Entering a state is a fresh beat, and the flipbook only ever holds
            // one clip, so there is no way for two state animations to run at once.
            Flipbook.Play(clip, true);
        }

        private UIFlipbookClip ClipFor(UIFlipbookState state)
        {
            switch (state)
            {
                case UIFlipbookState.Disabled:
                    return FirstWithFrames(Disabled, Normal);

                case UIFlipbookState.Pressed:
                    return FirstWithFrames(Pressed, Highlighted, Normal);

                // Deliberately does NOT fall back to Highlighted: a button stays selected after a
                // click, and inheriting the hover look while the mouse is elsewhere reads as a bug.
                case UIFlipbookState.Selected:
                    return FirstWithFrames(Selected, Normal);

                case UIFlipbookState.Highlighted:
                    return FirstWithFrames(Highlighted, Normal);

                default:
                    return FirstWithFrames(Normal, null);
            }
        }

        // Explicit overloads rather than params, so a state change allocates no array. Each candidate
        // is resolved first, so "has frames" asks the shared asset when there is one.
        private static UIFlipbookClip FirstWithFrames(UIFlipbookClip a, UIFlipbookClip b)
        {
            if (a != null && a.Resolved().HasFrames)
            {
                return a;
            }

            return b != null && b.Resolved().HasFrames ? b : null;
        }

        private static UIFlipbookClip FirstWithFrames(UIFlipbookClip a, UIFlipbookClip b, UIFlipbookClip c)
        {
            var found = FirstWithFrames(a, b);
            if (found != null)
            {
                return found;
            }

            return c != null && c.Resolved().HasFrames ? c : null;
        }

        private void ResolveReferences()
        {
            if (Target == null)
            {
                Target = GetComponent<Selectable>();
            }

            if (Flipbook == null)
            {
                Flipbook = GetComponent<UISpriteFlipbook>();
            }

            if (Flipbook == null)
            {
                Flipbook = GetComponentInChildren<UISpriteFlipbook>(true);
            }
        }

        private void Reset()
        {
            ResolveReferences();
            FillClipDefaults();
        }

        private void OnValidate()
        {
            FillClipDefaults();

            if (SubmitPressDuration < 0f)
            {
                SubmitPressDuration = DefaultSubmitPressDuration;
            }

            // Sprite Swap writes Image.sprite itself and fights the flipbook for it. This used to be
            // checked in Start, which only runs in play mode - i.e. it warned you long after the
            // point where you could see what it was talking about. OnValidate catches it while you
            // are still looking at the component.
            if (Target != null && Target.transition == Selectable.Transition.SpriteSwap)
            {
                Debug.LogWarning("[UIFlipbookSelectable] " + name + ": the Selectable Transition is " +
                                 "Sprite Swap, which writes Image.sprite itself and will fight the " +
                                 "flipbook. Set Transition to None (or Color Tint).", this);
            }
        }

        private void FillClipDefaults()
        {
            if (Normal != null)
            {
                Normal.FillUnsetDefaults();
            }

            if (Highlighted != null)
            {
                Highlighted.FillUnsetDefaults();
            }

            if (Pressed != null)
            {
                Pressed.FillUnsetDefaults();
            }

            if (Selected != null)
            {
                Selected.FillUnsetDefaults();
            }

            if (Disabled != null)
            {
                Disabled.FillUnsetDefaults();
            }
        }

    #if UNITY_EDITOR

        /// <summary>The flipbook this drives, resolved the same way playback resolves it.</summary>
        public UISpriteFlipbook EditorFlipbook
        {
            get
            {
                ResolveReferences();
                return Flipbook;
            }
        }

        /// <summary>
        /// The clip a given state would actually play, fallback chain and all, so the inspector
        /// preview shows what the button really does rather than what its slots literally contain.
        /// </summary>
        public UIFlipbookClip EditorClipFor(UIFlipbookState state)
        {
            return ClipFor(state);
        }

    #endif
    }
}

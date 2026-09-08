using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;


// written by andy (rmfz/rmfandyplayz)
// can be slapped onto anything that inherits selectable to facilitate events for things like:
// pressed, hovered, unhovered (not exhaustive)
[RequireComponent(typeof(Selectable))]
public class SelectableEvents : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler, IPointerClickHandler, ISelectHandler, IDeselectHandler, ISubmitHandler
{
    [Header("pointer events")]
    public UnityEvent OnHover;
    public UnityEvent OnUnhover;
    [Tooltip("caution: applies for ONLY a mouse down event")]
    public UnityEvent OnPressed;
    [Tooltip("caution: applies for ONLY a mouse up event")]
    public UnityEvent OnReleased;
    [Tooltip("applies for a successful click event")]
    public UnityEvent OnClicked;

    [Header("selection events")]
    public UnityEvent OnSelected;
    public UnityEvent OnDeselected;
    public UnityEvent OnSubmitted;

    private Selectable selectable;


    private void Awake()
    {
        selectable = GetComponent<Selectable>();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        OnHover?.Invoke();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        OnUnhover?.Invoke();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (!selectable.IsInteractable()) return;

        OnPressed?.Invoke();
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (!selectable.IsInteractable()) return;

        OnReleased?.Invoke();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (!selectable.IsInteractable()) return;

        OnClicked?.Invoke();
    }

    public void OnSelect(BaseEventData eventData)
    {
        OnSelected?.Invoke();
    }

    public void OnDeselect(BaseEventData eventData)
    {
        OnDeselected?.Invoke();
    }

    public void OnSubmit(BaseEventData eventData)
    {
        if (!selectable.IsInteractable()) return;

        OnSubmitted?.Invoke();
    }
}

using UnityEngine;
using UnityEngine.EventSystems;
using System;

public class UIHoverExtention : MonoBehaviour, IPointerEnterHandler, ISelectHandler
{
    private Action onFocus;

    public void Setup(Action focusAction)
    {
        onFocus = focusAction;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        onFocus?.Invoke();
    }

    public void OnSelect(BaseEventData eventData)
    {
        onFocus?.Invoke();
    }
}

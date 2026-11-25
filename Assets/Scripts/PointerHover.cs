using UnityEngine;
using UnityEngine.EventSystems;

// Tells whoever added it when the pointer moves onto or off this UI element
public class PointerHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public System.Action Entered, Exited;

    public static PointerHover Add(GameObject target, System.Action entered, System.Action exited)
    {
        PointerHover hover = target.AddComponent<PointerHover>();
        hover.Entered = entered;
        hover.Exited = exited;
        return hover;
    }

    public void OnPointerEnter(PointerEventData eventData) => Entered?.Invoke();
    public void OnPointerExit(PointerEventData eventData) => Exited?.Invoke();
}

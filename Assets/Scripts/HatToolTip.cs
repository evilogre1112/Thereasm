using UnityEngine;
using UnityEngine.EventSystems;

public class HatTooltip : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    private CardInteraction cardInteraction;

    void Start()
    {
        cardInteraction = GetComponentInParent<CardInteraction>();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (cardInteraction != null && cardInteraction.isInspecting)
        {
            cardInteraction.ShowHatDescription();
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (cardInteraction != null && cardInteraction.isInspecting)
        {
            cardInteraction.RestoreCardDescription();
        }
    }
}
using System.Collections;
using UnityEngine;

/// <summary>Local hover and click feedback shared by host and guest; it does not send commands.</summary>
[DisallowMultipleComponent]
public sealed class CardLocalVisualFeedback : MonoBehaviour
{
    private CardInteraction interaction;
    private Coroutine hoverRoutine;
    private bool hovering;

    public static CardLocalVisualFeedback For(CardInteraction card)
    {
        if (card == null) return null;
        var effect = card.GetComponent<CardLocalVisualFeedback>();
        if (effect == null) effect = card.gameObject.AddComponent<CardLocalVisualFeedback>();
        effect.interaction = card;
        return effect;
    }

    public void PointerEnter()
    {
        if (interaction == null || interaction.visualChild == null || interaction.isPlaced ||
            interaction.canDrag || interaction.isInspecting || CardInteraction.isAnyCardDragging ||
            CardInteraction.currentlyInspectedCard != null || transform.parent == null ||
            transform.parent.name == "CemeteryCardPos" || GameStartUI.IsBlockingInput) return;
        var rect = interaction.visualChild as RectTransform;
        if (rect == null) return;
        hovering = true;
        AnimateTo(interaction.HoverBaseAnchor + Vector2.up * interaction.hoverOffsetY);
        if (AudioManager.instance != null && interaction.hoverSFX != null)
            AudioManager.instance.PlaySFX(interaction.hoverSFX, true);
    }

    public void PointerExit()
    {
        if (interaction == null) return;
        hovering = false;
        AnimateTo(interaction.HoverBaseAnchor);
    }

    // Called on drag, inspect, snapshot state changes and object disable.
    public void StopHover(bool immediate)
    {
        hovering = false;
        if (hoverRoutine != null) { StopCoroutine(hoverRoutine); hoverRoutine = null; }
        if (immediate && interaction != null && interaction.visualChild is RectTransform rect)
            rect.anchoredPosition = interaction.HoverBaseAnchor;
        else if (!immediate && interaction != null) AnimateTo(interaction.HoverBaseAnchor);
    }

    private void AnimateTo(Vector2 target)
    {
        if (hoverRoutine != null) { StopCoroutine(hoverRoutine); hoverRoutine = null; }
        if (isActiveAndEnabled) hoverRoutine = StartCoroutine(HoverTo(target));
    }

    private IEnumerator HoverTo(Vector2 target)
    {
        var rect = interaction != null ? interaction.visualChild as RectTransform : null;
        if (rect == null) yield break;
        while (rect != null && (rect.anchoredPosition - target).sqrMagnitude > .25f)
        {
            float speed = interaction != null ? interaction.hoverSpeed : 20f;
            rect.anchoredPosition = Vector2.Lerp(rect.anchoredPosition, target,
                Mathf.Clamp01(Time.unscaledDeltaTime * Mathf.Max(1f, speed)));
            yield return null;
        }
        if (rect != null) rect.anchoredPosition = target;
        hoverRoutine = null;
    }

    private void LateUpdate()
    {
        if (!hovering || interaction == null) return;
        var remote = GetComponent<OnlineCardInput>();
        if (interaction.isPlaced || interaction.isInspecting || interaction.canDrag ||
            CardInteraction.isAnyCardDragging || CardInteraction.currentlyInspectedCard != null ||
            (remote != null && (remote.IsDragging || remote.IsPresenting))) StopHover(true);
    }

    private void OnDisable() { StopHover(true); }
}

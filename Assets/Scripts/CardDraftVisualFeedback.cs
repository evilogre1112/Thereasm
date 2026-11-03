using System.Collections;
using UnityEngine;

/// <summary>Draft selection animation shared by the host and guest; it does not change the deck.</summary>
public static class CardDraftVisualFeedback
{
    public static IEnumerator Play(GameObject chosen, GameObject unchosen, bool restoreAfterwards)
    {
        const float duration = 0.18f;
        const float hold = 0.035f;
        Vector3 chosenStart = chosen != null ? chosen.transform.localScale : Vector3.one;
        Vector3 otherStart = unchosen != null ? unchosen.transform.localScale : Vector3.one;
        CanvasGroup group = unchosen != null ? unchosen.GetComponent<CanvasGroup>() : null;
        if (group == null && unchosen != null) group = unchosen.AddComponent<CanvasGroup>();
        float originalAlpha = group != null ? group.alpha : 1f;

        if (unchosen != null)
        {
            var interaction = unchosen.GetComponent<CardInteraction>();
            if (interaction != null && interaction.descriptionPanel != null)
                interaction.descriptionPanel.SetActive(false);
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float eased = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
            if (chosen != null)
                chosen.transform.localScale = Vector3.Lerp(chosenStart, chosenStart * 1.15f, eased);
            if (unchosen != null)
                unchosen.transform.localScale = Vector3.Lerp(otherStart, Vector3.zero, eased);
            if (group != null) group.alpha = Mathf.Lerp(originalAlpha, 0f, eased);
            yield return null;
        }
        yield return new WaitForSecondsRealtime(hold);
        if (restoreAfterwards)
        {
            // The guest will receive an authoritative snapshot. If the same pair is
            // still displayed (e.g. an action rejection), restore it for retry.
            if (chosen != null) chosen.transform.localScale = chosenStart;
            if (unchosen != null) unchosen.transform.localScale = otherStart;
            if (group != null) group.alpha = originalAlpha;
        }
    }
}

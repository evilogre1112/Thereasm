using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Board chatter is a HUD-like overlay INSIDE the clipped board viewport.
// It is not a child of a zoomed card while visible, so it stays readable,
// appears above other cards, and cannot escape into the fixed HUD/hand dock.
public class CardDialogue : MonoBehaviour
{
    [Header("Saved prefab UI")]
    public GameObject bubbleObject;
    public TextMeshProUGUI tmpText;
    [Min(.1f)] public float displayDuration = 4f;
    [Min(.1f)] public float initialDelay = 1.5f;

    private CardInstance card;
    private CardInteraction interaction;
    private Coroutine routine;
    private const int MaxSimultaneousSpeakers = 3;
    private static readonly List<CardDialogue> activeSpeakers = new List<CardDialogue>(MaxSimultaneousSpeakers);
    private static int lastReflowFrame = -1;
    private Rect occupiedBubbleBounds;
    // Direction is selected once per quote. A live bubble is never shifted to
    // make room for another speaker (even if a later zoom makes them overlap).
    private enum BubbleDirection { Above, Below, Left, Right }
    private BubbleDirection fixedDirection = BubbleDirection.Above;
    private RectTransform bubbleRect;
    private static RectTransform sharedOverlay;
    private static readonly System.Random cosmeticRandom = new System.Random();
    private readonly Vector3[] corners = new Vector3[4];

    void Awake()
    {
        card = GetComponent<CardInstance>();
        interaction = GetComponent<CardInteraction>();
        bubbleRect = bubbleObject != null ? bubbleObject.transform as RectTransform : null;
        HideBubble();
    }

    void OnEnable() { StartChatting(); }
    void OnDisable() { StopChatting(); }

    void OnDestroy()
    {
        activeSpeakers.Remove(this);
        // The bubble may have been moved under the shared overlay. Its
        // lifetime must still match the card's lifetime (no orphan bubbles).
        if (bubbleObject != null && bubbleRect != null && sharedOverlay != null &&
            bubbleRect.IsChildOf(sharedOverlay))
            Destroy(bubbleObject);
    }

    private bool OnBoard => card != null && card.cardData != null && !card.isDead &&
        interaction != null && interaction.isPlaced;

    private static bool Open(GameObject panel) => panel != null && panel.activeInHierarchy;

    private bool CanSpeak
    {
        get
        {
            var turns = TurnManager.instance;
            return OnBoard && !DialogueModalGuard.IsOpen &&
                !TutorialController.OverlapsCallout(bubbleRect) &&
                CardInteraction.currentlyInspectedCard == null &&
                !interaction.isInspecting && !CardInteraction.isAnyCardDragging &&
                !GameStartUI.IsBlockingInput &&
                (turns == null || (!turns.isGameOver && !turns.isTallyingVotes &&
                !Open(turns.notePanel) && !Open(turns.discoverPanel) && !Open(turns.cemeteryViewPanel))) &&
                (GetComponent<OnlineCardInput>() is OnlineCardInput online ? !online.IsPresenting : true);
        }
    }

    public void StartChatting()
    {
        if (routine != null || !isActiveAndEnabled || !OnBoard || bubbleObject == null ||
            tmpText == null || card.cardData.quotes == null || card.cardData.quotes.Count == 0) return;
        routine = StartCoroutine(DialogueRoutine());
    }

    public void StopChatting()
    {
        if (routine != null) StopCoroutine(routine);
        routine = null;
        HideBubble();
    }

    private void HideBubble()
    {
        if (bubbleObject != null) bubbleObject.SetActive(false);
        activeSpeakers.Remove(this);
    }

    public static void HideAllBubbles()
    {
        foreach (var dialogue in FindObjectsByType<CardDialogue>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            dialogue.HideBubble();
    }

    void LateUpdate()
    {
        if (!OnBoard) { if (routine != null) StopChatting(); return; }
        if (bubbleObject == null || !bubbleObject.activeSelf) return;
        if (!CanSpeak) { HideBubble(); return; }
        // Refresh the fixed anchors after card movement/zoom/pan. Older speakers
        // keep their slot; a newer overlapping bubble disappears, never shifts.
        if (lastReflowFrame != Time.frameCount)
        {
            lastReflowFrame = Time.frameCount;
            ReflowActiveBubbles();
        }
    }

    private static RectTransform GetOverlay(RectTransform viewport)
    {
        if (viewport == null) return null;
        if (sharedOverlay != null && sharedOverlay.parent == viewport) return sharedOverlay;

        // Reset after a scene reload or a different board viewport.
        Transform existing = viewport.Find("Board Dialogue Overlay (runtime)");
        if (existing is RectTransform existingRect)
        {
            sharedOverlay = existingRect;
            return sharedOverlay;
        }

        var go = new GameObject("Board Dialogue Overlay (runtime)", typeof(RectTransform));
        go.layer = viewport.gameObject.layer;
        sharedOverlay = go.GetComponent<RectTransform>();
        sharedOverlay.SetParent(viewport, false);
        sharedOverlay.anchorMin = Vector2.zero;
        sharedOverlay.anchorMax = Vector2.one;
        sharedOverlay.pivot = new Vector2(.5f, .5f);
        sharedOverlay.offsetMin = sharedOverlay.offsetMax = Vector2.zero;
        sharedOverlay.SetAsLastSibling();
        return sharedOverlay;
    }

    private bool EnsureBubbleOverlay()
    {
        RectTransform viewport = GameBoardLayout.Active != null ? GameBoardLayout.Active.Viewport : null;
        RectTransform overlay = GetOverlay(viewport);
        if (overlay == null || bubbleRect == null) return false;

        if (bubbleRect.parent != overlay)
        {
            // Detached from the card's nested DialogueCanvas. Both the board
            // and dialogue now render in the SAME root UI Canvas/mask.
            bubbleRect.SetParent(overlay, false);
            bubbleRect.localRotation = Quaternion.identity;
            bubbleRect.localScale = Vector3.one;
            bubbleRect.anchorMin = bubbleRect.anchorMax = new Vector2(.5f, .5f);
            bubbleRect.pivot = new Vector2(.5f, .5f);
            foreach (Graphic graphic in bubbleRect.GetComponentsInChildren<Graphic>(true))
                graphic.raycastTarget = false;
        }
        bubbleRect.SetAsLastSibling();
        return true;
    }

    private static void PruneSpeakers()
    {
        for (int i = activeSpeakers.Count - 1; i >= 0; --i)
        {
            CardDialogue dialogue = activeSpeakers[i];
            if (dialogue == null || !dialogue.isActiveAndEnabled ||
                dialogue.bubbleObject == null || !dialogue.bubbleObject.activeSelf)
                activeSpeakers.RemoveAt(i);
        }
    }

    private bool ShowQuote()
    {
        if (!CanSpeak) return false;
        PruneSpeakers();
        if (!activeSpeakers.Contains(this) && activeSpeakers.Count >= MaxSimultaneousSpeakers)
            return false;
        var quotes = card.cardData.quotes;
        string quote = quotes[cosmeticRandom.Next(quotes.Count)];
        if (string.IsNullOrWhiteSpace(quote)) return false;
        if (!EnsureBubbleOverlay()) return false;

        tmpText.text = Localizer.T(quote);
        bubbleObject.SetActive(true);
        Canvas.ForceUpdateCanvases();
        // Choose a stable orientation using seat/cemetery geometry ONLY.
        // If that position overlaps existing speech, reject this quote instead
        // of trying a different direction or sliding the bubble sideways.
        if (!TryStartBubble())
        {
            HideBubble();
            return false;
        }
        if (!activeSpeakers.Contains(this)) activeSpeakers.Add(this);
        return true;
    }

    private static void ReflowActiveBubbles()
    {
        PruneSpeakers();
        // Earlier speakers keep their fixed positions; later speakers that
        // collide after pan/zoom are hidden, not repositioned.
        for (int i = 0; i < activeSpeakers.Count;)
        {
            CardDialogue dialogue = activeSpeakers[i];
            if (!dialogue.CanSpeak || !dialogue.RefreshFixedBubble(i))
            {
                dialogue.HideBubble(); // Also removes from activeSpeakers.
                continue;
            }
            ++i;
        }
    }

    // All bounds below are in sharedOverlay's coordinates, independent of
    // GameBoardLayout zoom and of the card's nested RectTransform hierarchy.
    private void BoundsInOverlay(RectTransform rect,
        out float x0, out float x1, out float y0, out float y1)
    {
        rect.GetWorldCorners(corners);
        x0 = y0 = float.PositiveInfinity;
        x1 = y1 = float.NegativeInfinity;
        for (int i = 0; i < 4; ++i)
        {
            Vector3 point = sharedOverlay.InverseTransformPoint(corners[i]);
            x0 = Mathf.Min(x0, point.x);
            x1 = Mathf.Max(x1, point.x);
            y0 = Mathf.Min(y0, point.y);
            y1 = Mathf.Max(y1, point.y);
        }
    }

    // Geometric bounds are shared-overlay local coordinates, not screen pixels.
    // The UI remains aligned with zoom/pan, while the HUD is never covered.
    private static bool Overlap(Rect a, Rect b, float separation = 4f)
    {
        return a.xMin < b.xMax + separation && a.xMax > b.xMin - separation &&
               a.yMin < b.yMax + separation && a.yMax > b.yMin - separation;
    }

    private Rect BoundsRect(RectTransform target)
    {
        BoundsInOverlay(target, out float x0, out float x1, out float y0, out float y1);
        return Rect.MinMaxRect(x0, y0, x1, y1);
    }

    private bool IsClearOfOtherSeats(Rect bubble, SeatDropZone ownSeat)
    {
        var turns = TurnManager.instance;
        if (turns == null) return true;
        if (turns.allSeats != null)
        {
            foreach (SeatDropZone seat in turns.allSeats)
            {
                if (seat == null || seat == ownSeat || !seat.gameObject.activeInHierarchy ||
                    !(seat.transform is RectTransform seatRect)) continue;
                if (Overlap(bubble, BoundsRect(seatRect))) return false;
            }
        }
        // The cemetery is *not* in allSeats. Its CardPos parent is the
        // visible cemetery slot; keep dialogue away from it too.
        if (turns.cemeteryCardPos != null &&
            turns.cemeteryCardPos.parent is RectTransform cemeteryRect &&
            cemeteryRect.gameObject.activeInHierarchy &&
            Overlap(bubble, BoundsRect(cemeteryRect))) return false;
        return true;
    }

    // Coordinates are computed from the card on each frame. Only the card's
    // position and the board's zoom can move/scale a live bubble.
    private bool TryGetGeometry(out Rect cardBounds, out Rect viewport, out Vector2 size)
    {
        cardBounds = default;
        viewport = default;
        size = default;
        if (bubbleRect == null || sharedOverlay == null || bubbleRect.parent != sharedOverlay ||
            !(transform is RectTransform cardRect)) return false;

        cardBounds = BoundsRect(cardRect);
        const float margin = 9f;
        viewport = Rect.MinMaxRect(
            sharedOverlay.rect.xMin + margin, sharedOverlay.rect.yMin + margin,
            sharedOverlay.rect.xMax - margin, sharedOverlay.rect.yMax - margin);
        if (!cardBounds.Overlaps(viewport)) return false;

        float visualScale = GameBoardLayout.Active != null
            ? GameBoardLayout.Active.BoardVisualScale : 1f;
        float maxScaleX = viewport.width / Mathf.Max(1f, bubbleRect.rect.width);
        float maxScaleY = viewport.height / Mathf.Max(1f, bubbleRect.rect.height);
        float scale = Mathf.Max(.01f, Mathf.Min(visualScale, maxScaleX, maxScaleY));
        bubbleRect.localScale = new Vector3(scale, scale, 1f);
        size = bubbleRect.rect.size * scale;
        return true;
    }

    // Exactly one unshifted anchor for each direction; no clamping, offsets,
    // lateral nudging or searching around other speech bubbles.
    private static Rect FixedBounds(Rect cardBounds, Vector2 size, BubbleDirection direction)
    {
        const float gap = 12f;
        Vector2 center = cardBounds.center;
        Vector2 half = size * .5f;
        switch (direction)
        {
            case BubbleDirection.Above:
                center.y = cardBounds.yMax + gap + half.y;
                break;
            case BubbleDirection.Below:
                center.y = cardBounds.yMin - gap - half.y;
                break;
            case BubbleDirection.Left:
                center.x = cardBounds.xMin - gap - half.x;
                break;
            case BubbleDirection.Right:
                center.x = cardBounds.xMax + gap + half.x;
                break;
        }
        return new Rect(center - half, size);
    }

    // Accept only directions that leave the whole dialogue visible on start.
    // GameMaster seats may touch the top edge; the entire bubble must fit.
    private static bool FitsInsideViewport(Rect proposed, Rect viewport)
    {
        const float epsilon = .5f;
        return proposed.xMin >= viewport.xMin - epsilon &&
               proposed.xMax <= viewport.xMax + epsilon &&
               proposed.yMin >= viewport.yMin - epsilon &&
               proposed.yMax <= viewport.yMax + epsilon;
    }

    private bool OverlapsExistingSpeech(Rect proposed, int precedingSpeakers)
    {
        for (int i = 0; i < Mathf.Min(precedingSpeakers, activeSpeakers.Count); i++)
        {
            CardDialogue other = activeSpeakers[i];
            if (other == null || other == this || other.bubbleRect == null ||
                other.bubbleRect.parent != sharedOverlay ||
                other.bubbleObject == null || !other.bubbleObject.activeSelf) continue;
            if (Overlap(proposed, other.occupiedBubbleBounds)) return true;
        }
        return false;
    }

    private bool TryStartBubble()
    {
        if (!TryGetGeometry(out Rect cardBounds, out Rect viewport, out Vector2 size)) return false;
        SeatDropZone ownSeat = GetComponentInParent<SeatDropZone>();
        // Priority applies only to static seat/cemetery obstruction. Existing
        // speech NEVER causes a switch to another direction: it blocks speech.
        for (int d = 0; d < 4; d++)
        {
            BubbleDirection direction = (BubbleDirection)d;
            Rect proposal = FixedBounds(cardBounds, size, direction);
            // If Above would be hidden by the mask, try Below, Left, Right
            // at their original fixed anchors. Do NOT nudge for other speech.
            if (!FitsInsideViewport(proposal, viewport) ||
                Overlap(proposal, cardBounds) ||
                !IsClearOfOtherSeats(proposal, ownSeat)) continue;

            if (OverlapsExistingSpeech(proposal, activeSpeakers.Count))
                return false; // First valid anchor occupied: this speaker waits.

            fixedDirection = direction;
            bubbleRect.anchoredPosition = proposal.center;
            occupiedBubbleBounds = proposal;
            return true;
        }
        return false;
    }

    private bool RefreshFixedBubble(int precedingSpeakers)
    {
        if (!TryGetGeometry(out Rect cardBounds, out Rect viewport, out Vector2 size)) return false;
        Rect proposal = FixedBounds(cardBounds, size, fixedDirection);
        SeatDropZone ownSeat = GetComponentInParent<SeatDropZone>();
        if (!proposal.Overlaps(viewport) || Overlap(proposal, cardBounds) ||
            !IsClearOfOtherSeats(proposal, ownSeat) ||
            OverlapsExistingSpeech(proposal, precedingSpeakers)) return false;

        // Only pan/zoom (or the source card moving) may change this coordinate.
        bubbleRect.anchoredPosition = proposal.center;
        occupiedBubbleBounds = proposal;
        return true;
    }

    private IEnumerator DialogueRoutine()
    {
        yield return new WaitForSeconds(Mathf.Max(.1f, initialDelay) + (float)cosmeticRandom.NextDouble());
        bool first = true;
        while (OnBoard)
        {
            if ((first || cosmeticRandom.NextDouble() <= Mathf.Clamp01(card.cardData.customChance)) && ShowQuote())
            {
                first = false;
                yield return new WaitForSeconds(Mathf.Max(.1f, displayDuration));
                HideBubble();
            }
            yield return new WaitForSeconds(Mathf.Max(.5f, card.cardData.customInterval));
        }
        HideBubble();
        routine = null;
    }
}

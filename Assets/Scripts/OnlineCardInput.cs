using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

// Gameplay remains authoritative on the host. Card transforms respond locally to the pointer.
public class OnlineCardInput : MonoBehaviour, IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerEnterHandler, IPointerExitHandler
{
    public int CardId;
    public int DiscoverIndex = -1;
    private bool dragging;
    private bool movingCard, presenting;
    private int previewSequence, dragPhase;
    private RectTransform dragCanvas;
    private Camera dragCamera;
    private Vector2 initialMouse, initialPosition;
    private Transform savedParent;
    private int savedSibling;
    private Vector3 savedPosition, savedScale, savedWorldPosition;
    private Quaternion savedRotation, savedWorldRotation;
    private Vector3 savedVisualPosition;
    private Quaternion savedVisualRotation;
    private bool savedRaycasts;
    private float savedAlpha;
    private float suppressClickUntil;
    public bool IsMovingCard => dragging && movingCard;
    public bool IsDragging => dragging;
    public bool IsPresenting => presenting;
    // Local placement feedback is independent of the confirmed gameplay state.
    // A guest can show the publicly known printed faction immediately after drop;
    // a later authoritative snapshot still decides whether the move succeeded.
    public bool HasPlacementPreview { get; private set; }
    public bool PreviewIsOnBoard { get; private set; }
    public float SuppressClickUntil => suppressClickUntil;
    public void OnPointerEnter(PointerEventData data)
    {
        if (OnlineSession.Guest) CardLocalVisualFeedback.For(GetComponent<CardInteraction>()).PointerEnter();
    }

    public void OnPointerExit(PointerEventData data)
    {
        if (OnlineSession.Guest) CardLocalVisualFeedback.For(GetComponent<CardInteraction>()).PointerExit();
    }

    public void OnPointerClick(PointerEventData data)
    {
        CardActionRouter.PointerClick(GetComponent<CardInteraction>(), this, data);
    }
    public void OnBeginDrag(PointerEventData data)
    {
        if (OnlineSession.Guest && !GameStartUI.IsBlockingInput)
        {
            string blocked = ActionFeedback.BeginDragBlock(GetComponent<CardInteraction>(), data.button == PointerEventData.InputButton.Right);
            if (blocked != null) { ActionFeedback.Reject(blocked); return; }
            if (OnlineGameBridge.Instance != null && OnlineGameBridge.Instance.HasPhaseBarrier)
            { ActionFeedback.Reject("Your turn change is being confirmed. Wait a moment before the next action."); return; }
        }
        if (!OnlineSession.Guest || !OnlineSession.LocalTurn || GameStartUI.IsBlockingInput || DiscoverIndex >= 0 || presenting ||
            OnlineGameBridge.Instance == null || OnlineGameBridge.Instance.HasPhaseBarrier || TurnManager.instance.currentTurn == TurnState.SetupPhase) return;
        var card = GetComponent<CardInstance>(); var interaction = GetComponent<CardInteraction>();
        if (card == null || card.isDead || card.isFaceDown || interaction.isInspecting || CardInteraction.currentlyInspectedCard != null ||
            CardInteraction.isAnyCardDragging || data.button == PointerEventData.InputButton.Middle) return;
        var phase = TurnManager.instance.currentTurn;
        bool ritual = phase == TurnState.CursePhase || phase == TurnState.SacrificePhase;
        if (data.button == PointerEventData.InputButton.Right && (!interaction.isPlaced || card.cardData.cardSkill == null ||
            card.cardData.cardSkill.skillType == SkillType.Passive)) return;
        if (data.button == PointerEventData.InputButton.Left &&
            (TurnManager.instance.currentTurn == TurnState.SacrificePhase && card.cardData.owner != OnlineSession.LocalRole ||
             ritual && !interaction.isPlaced ||
             TurnManager.instance.currentTurn == TurnState.CursePhase && !TurnManager.instance.IsCur(card) ||
             (TurnManager.instance.currentTurn == TurnState.Player1Turn || TurnManager.instance.currentTurn == TurnState.Player2Turn) &&
             interaction.isPlaced && (interaction.lockState < 3 || interaction.isLockedByElder || interaction.isLockedBySkill))) return;
        CardLocalVisualFeedback.For(GetComponent<CardInteraction>()).StopHover(true);
        dragging = true;
        movingCard = data.button == PointerEventData.InputButton.Left && !ritual;
        dragPhase = (int)phase;
        CardInteraction.isAnyCardDragging = true;
        if (movingCard)
        {
            BeginPresentation();
            if (!presenting) { EndPointerDrag(); return; }
            dragCamera = data.pressEventCamera;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(dragCanvas, data.position, dragCamera, out initialMouse);
            initialPosition = ((RectTransform)transform).anchoredPosition;
            transform.localRotation = Quaternion.identity;
            if (TargetingArrow.instance != null) TargetingArrow.instance.Hide();
        }
        else
        {
            var group = GetComponent<CanvasGroup>();
            if (group != null) { savedRaycasts = group.blocksRaycasts; group.blocksRaycasts = false; }
            if (TargetingArrow.instance != null) TargetingArrow.instance.Show(transform.position);
        }
    }
    public void OnDrag(PointerEventData data)
    {
        if (!dragging) return;
        if (!movingCard) { if (TargetingArrow.instance != null) TargetingArrow.instance.UpdateArrow(data.position); return; }
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(dragCanvas, data.position, dragCamera, out var mouse))
            ((RectTransform)transform).anchoredPosition = initialPosition + mouse - initialMouse;
        var interaction = GetComponent<CardInteraction>();
        if (interaction.visualChild != null)
        {
            float tilt = Mathf.Clamp(-data.delta.x, -interaction.tiltAmount, interaction.tiltAmount);
            interaction.visualChild.localRotation = Quaternion.Lerp(interaction.visualChild.localRotation,
                Quaternion.Euler(0, 0, tilt), Time.unscaledDeltaTime * interaction.tiltSpeed);
        }
    }
    public void OnEndDrag(PointerEventData data)
    {
        if (!dragging) return;
        bool physicalDrop = movingCard;
        suppressClickUntil = Time.unscaledTime + .15f;
        if (!OnlineSession.LocalTurn || GameStartUI.IsBlockingInput || EventSystem.current == null)
        { EndPointerDrag(); RestorePresentation(); return; }
        var hits = new List<RaycastResult>();
        EventSystem.current.RaycastAll(data, hits);
        CardInstance target = null;
        SeatDropZone seat = null;
        foreach (var hit in hits)
        {
            var hitCard = hit.gameObject.GetComponentInParent<CardInstance>();
            if (target == null && hitCard != GetComponent<CardInstance>()) target = hitCard;
            if (seat == null) seat = hit.gameObject.GetComponentInParent<SeatDropZone>();
        }
        if (target == null && seat != null) target = seat.cardPos.GetComponentInChildren<CardInstance>();
        var interaction = GetComponent<CardInteraction>();
        // The source is lifted to the canvas while dragging, but still occupies its authoritative seat.
        if (target == null && seat != null && interaction.isPlaced && seat.cardPos == interaction.originalParent)
            target = GetComponent<CardInstance>();
        var input = target != null ? target.GetComponent<OnlineCardInput>() : null;
        int targetId = input != null ? input.CardId : 0;
        int seatIndex = seat != null ? TurnManager.instance.allSeats.IndexOf(seat) : -1;
        var bridge = OnlineGameBridge.Instance;
        if (physicalDrop)
        {
            bool sent = false;
            string blocked = null;
            if (seatIndex >= 0 && target != GetComponent<CardInstance>())
            {
                var other = target != null ? target.GetComponent<CardInteraction>() : null;
                blocked = other != null ? ActionFeedback.SwapBlock(interaction, other, seat.cardPos) : ActionFeedback.ExtraSeatBlock(interaction, seat.cardPos);
                int cost = other != null ? interaction.SwapCost(other) : interaction.PlacementCost;
                if (blocked == null && cost > ActionFeedback.Hearts) blocked = ActionFeedback.Cost(GetComponent<CardInstance>(), cost);
            }
            else if (seatIndex < 0 && interaction.isPlaced && GameStartUI.Current != null && GameStartUI.Current.IsOverHand(data.position))
            {
                if (interaction.ReturnPaymentCost > ActionFeedback.Hearts)
                    blocked = ActionFeedback.Cost(GetComponent<CardInstance>(), interaction.ReturnPaymentCost);
            }
            else if (seatIndex < 0)
                blocked = "Drop the card onto a seat, or into the hand area to retrieve a seated card.";
            if (blocked != null)
            { ActionFeedback.Reject(blocked); EndPointerDrag(); RestorePresentation(); return; }
            if (seatIndex >= 0 && target != GetComponent<CardInstance>())
            {
                sent = CardActionRouter.SendGuest(OnlineAction.Place, CardId, targetId, seat: seatIndex);
                if (sent)
                {
                    // Show the drop immediately, without waiting for the network round trip.
                    var swapped = target != null ? target.GetComponent<OnlineCardInput>() : null;
                    if (swapped != null)
                    {
                        swapped.BeginPresentation();
                        if (swapped.presenting)
                        {
                            swapped.previewSequence = bridge.PendingSequence;
                            if (interaction.isPlaced) swapped.transform.SetPositionAndRotation(savedWorldPosition, savedWorldRotation);
                            else swapped.PreviewAt(swapped.GetComponent<CardInteraction>().handParent);
                        }
                    }
                    PreviewAt(seat.cardPos);
                }
            }
            else if (seatIndex < 0 && interaction.isPlaced && GameStartUI.Current != null && GameStartUI.Current.IsOverHand(data.position))
            {
                sent = CardActionRouter.SendGuest(OnlineAction.Return, CardId);
                if (sent) PreviewAt(interaction.handParent);
            }
            if (sent) previewSequence = bridge.PendingSequence;
            EndPointerDrag();
            if (!sent) RestorePresentation();
            return;
        }
        EndPointerDrag();
        if (data.button == PointerEventData.InputButton.Right) CardActionRouter.SendGuest(OnlineAction.Skill, CardId, targetId);
        else if (TurnManager.instance.currentTurn == TurnState.SacrificePhase) CardActionRouter.SendGuest(OnlineAction.Vote, CardId, targetId, seat: seatIndex);
        else if (TurnManager.instance.currentTurn == TurnState.CursePhase) CardActionRouter.SendGuest(OnlineAction.Curse, CardId, targetId, seat: seatIndex);
    }

    private void BeginPresentation()
    {
        if (presenting) return;
        var canvas = GetComponentInParent<Canvas>();
        if (canvas == null) return;
        dragCanvas = canvas.rootCanvas.transform as RectTransform;
        savedParent = transform.parent; savedSibling = transform.GetSiblingIndex();
        savedPosition = transform.localPosition; savedRotation = transform.localRotation; savedScale = transform.localScale;
        savedWorldPosition = transform.position; savedWorldRotation = transform.rotation;
        dragPhase = (int)TurnManager.instance.currentTurn;
        var visual = GetComponent<CardInteraction>().visualChild;
        if (visual != null)
        {
            savedVisualPosition = visual.localPosition; savedVisualRotation = visual.localRotation;
            visual.localPosition = Vector3.zero; visual.localRotation = Quaternion.identity;
        }
        var group = GetComponent<CanvasGroup>();
        if (group != null) { savedRaycasts = group.blocksRaycasts; savedAlpha = group.alpha; group.blocksRaycasts = false; }
        presenting = true;
        HasPlacementPreview = false;
        PreviewIsOnBoard = false;
        transform.SetParent(dragCanvas, true);
        transform.SetAsLastSibling();
    }

    private void PreviewAt(Transform destination)
    {
        if (destination == null) return;
        // Cards are temporarily parented under the root canvas while awaiting
        // the host acknowledgement, so transform.parent cannot tell the badge
        // whether the card is visually on a seat.
        HasPlacementPreview = true;
        PreviewIsOnBoard = destination.GetComponentInParent<SeatDropZone>() != null;
        transform.SetPositionAndRotation(destination.position, destination.rotation);
        var scale = destination.lossyScale;
        var rootScale = dragCanvas.lossyScale;
        transform.localScale = new Vector3(scale.x / rootScale.x, scale.y / rootScale.y, scale.z / rootScale.z);
        var visual = GetComponent<CardInteraction>().visualChild;
        if (visual != null) visual.localRotation = Quaternion.identity;
        if (TryGetComponent<CanvasGroup>(out var group)) group.alpha = destination.gameObject.activeInHierarchy ? savedAlpha : 0;
        // Apply the same faction stamp now, without waiting for the snapshot.
        if (TryGetComponent<CardOwnershipView>(out var ownership)) ownership.Refresh();
    }

    // Snapshots update gameplay fields during a drag, but must not pull the card away from the pointer.
    // An acknowledgement (including rejection) releases the visual for the authoritative placement.
    public bool PreservePresentation(int acknowledged, int phase, bool dead, bool over)
    {
        if (dead || over || phase != dragPhase || !OnlineSession.LocalTurn ||
            previewSequence > 0 && acknowledged >= previewSequence)
        { EndPointerDrag(); RestorePresentation(); return false; }
        return presenting;
    }

    private void EndPointerDrag()
    {
        if (!dragging) return;
        dragging = false; movingCard = false;
        CardInteraction.isAnyCardDragging = false;
        if (TargetingArrow.instance != null) TargetingArrow.instance.Hide();
        if (!presenting && TryGetComponent<CanvasGroup>(out var group)) group.blocksRaycasts = savedRaycasts;
    }

    private void RestorePresentation()
    {
        if (!presenting) return;
        presenting = false; previewSequence = 0;
        HasPlacementPreview = false;
        PreviewIsOnBoard = false;
        var parent = savedParent != null ? savedParent : GetComponent<CardInteraction>().originalParent;
        if (parent != null)
        {
            transform.SetParent(parent, false); transform.SetSiblingIndex(savedSibling);
            transform.localPosition = savedPosition; transform.localRotation = savedRotation; transform.localScale = savedScale;
        }
        var visual = GetComponent<CardInteraction>().visualChild;
        if (visual != null) { visual.localPosition = savedVisualPosition; visual.localRotation = savedVisualRotation; }
        if (TryGetComponent<CanvasGroup>(out var group)) { group.blocksRaycasts = savedRaycasts; group.alpha = savedAlpha; }
        if (TryGetComponent<CardOwnershipView>(out var ownership)) ownership.Refresh();
    }

    void LateUpdate()
    {
        if ((dragging || presenting) && (!OnlineSession.Guest || OnlineSession.Instance.PeerLost))
        { EndPointerDrag(); RestorePresentation(); }
    }

    void OnDisable()
    {
        if (TryGetComponent<CardLocalVisualFeedback>(out var feedback)) feedback.StopHover(true);
        EndPointerDrag();
        RestorePresentation();
    }
}

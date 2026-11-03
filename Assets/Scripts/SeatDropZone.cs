using UnityEngine;
using UnityEngine.EventSystems;

public class SeatDropZone : MonoBehaviour, IDropHandler
{
    [Header("Vị trí đặt bài")]
    public Transform cardPos; 

    [Header("Đặc tính ghế (Dành cho Kỹ năng)")]
    [Tooltip("Đánh dấu TRUE nếu ghế này nằm sát Nghĩa trang (Dành cho Kẻ Mù)")]
    public bool isNextToCemetery = false; 

    public void OnDrop(PointerEventData eventData)
    {
        if (OnlineSession.Guest) return; // The client submits one command from OnlineCardInput.
        if (TurnManager.instance == null || TurnManager.instance.isGameOver || TurnManager.instance.isTallyingVotes ||
            TurnManager.instance.IsBotTurn || GameStartUI.IsBlockingInput) return;
        if (eventData.button != PointerEventData.InputButton.Left) return;

        if (TurnManager.instance.currentTurn == TurnState.SetupPhase || 
            TurnManager.instance.currentTurn == TurnState.CursePhase || 
            TurnManager.instance.currentTurn == TurnState.SacrificePhase) return;

        GameObject droppedObj = eventData.pointerDrag;
        if (droppedObj == null) return;

        CardInteraction draggedCard = droppedObj.GetComponent<CardInteraction>();
        if (draggedCard == null || !draggedCard.canDrag) return; 

        CardInteraction cardAlreadyOnSeat = cardPos.GetComponentInChildren<CardInteraction>();

        if (cardAlreadyOnSeat == null)
        {
            draggedCard.PlaceOnSeat(cardPos);
        }
        else
        {
            if (cardAlreadyOnSeat.lockState == 1 || cardAlreadyOnSeat.lockState == 3)
            {
                draggedCard.SwapWith(cardAlreadyOnSeat, cardPos);
            }
            else
            {
                ActionFeedback.Reject(ActionFeedback.SwapBlock(draggedCard, cardAlreadyOnSeat, cardPos));
                
                // Thả không hợp lệ thì khôi phục ghế gốc.
                if (draggedCard.isPlaced) 
                    draggedCard.ExecutePlace(draggedCard.originalParent);
                else 
                    draggedCard.ExecuteReturnToHand();

                draggedCard.StartCoroutine("ShakeCard");
            }
        }
    }
}

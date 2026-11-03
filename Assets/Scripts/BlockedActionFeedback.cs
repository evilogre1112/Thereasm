using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// A disabled Button still receives pointer events, so it can explain why it is unavailable.
public class BlockedActionFeedback : MonoBehaviour, IPointerClickHandler, IBeginDragHandler, IDragHandler
{
    public enum Control { Purge, EndTurn, StartMatch, InputShield, Draft }
    public Control control;
    public void OnPointerClick(PointerEventData data) { Explain(); }
    public void OnBeginDrag(PointerEventData data) { Explain(); data.pointerDrag = null; }
    public void OnDrag(PointerEventData data) { }
    private void Explain()
    {
        if (GameStartUI.IsBlockingInput || TurnManager.instance == null) return;
        var button = GetComponent<Button>();
        if (button != null && button.IsInteractable()) return;
        if (TutorialController.Active) { TutorialController.ExplainBlockedAction(); return; }
        if (OnlineSession.Active && OnlineSession.Instance.PeerLost)
        { TurnManager.instance.ShowLocalActionFeedback("Your opponent disconnected. Use Menu to leave, or wait for the connection to recover."); return; }
        if (OnlineSession.Guest && (OnlineGameBridge.Instance == null || !OnlineGameBridge.Instance.HasSnapshot))
        { TurnManager.instance.ShowLocalActionFeedback("The board is still synchronizing. Wait for the cards to finish loading."); return; }
        if (control == Control.Draft)
        {
            if (OnlineSession.Active && DeckManager.instance != null && DeckManager.instance.DraftChooser != OnlineSession.LocalRole)
                TurnManager.instance.ShowLocalActionFeedback("Wait for the other player's Neutral pick before choosing your next card.");
            else TurnManager.instance.ShowLocalActionFeedback("Your chosen card is being added. Wait for the next pair before choosing again.");
            return;
        }
        string reason = ActionFeedback.InputBlock();
        if (reason != null) { TurnManager.instance.ShowLocalActionFeedback(reason); return; }
        if (control == Control.Purge)
        {
            var t = TurnManager.instance;
            if (t.currentTurn != TurnState.Player1Turn && t.currentTurn != TurnState.Player2Turn)
                TurnManager.instance.ShowLocalActionFeedback("Purge is available only during an action turn. Finish the ritual first.");
            else TurnManager.instance.ShowLocalActionFeedback("You have used both Purges for this match. No Purges remain.");
        }
        else if (control == Control.StartMatch)
            TurnManager.instance.ShowLocalActionFeedback("Finish choosing the required Cursermancers in each card group before starting the match.");
        else TurnManager.instance.ShowLocalActionFeedback("Your turn change is being confirmed. Wait a moment before the next action.");
    }
}

using UnityEngine;
using UnityEngine.EventSystems;

public class CardVoteHint : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
{
    public string kind = "Votes";
    private string hoverMessage;
    public void OnPointerEnter(PointerEventData data)
    {
        var card=GetComponentInParent<CardInstance>();var turns=TurnManager.instance;
        if(card==null||turns==null||!string.IsNullOrEmpty(turns.LocalActionFeedback))return;
        hoverMessage=kind=="Detective"?string.Format("{0}: suspicion +{1}, not votes. Click this seal for details.",TurnManager.CardLabel(card),card.bonusSuspicion):
            kind=="Gambler"?"This seal marks your wager target. Click to read how Gambler resolves the bet.":
            kind=="Snooper"?"This eye marks Snooper's indicated target. Click for its vote effect; private selections stay hidden from the other player.":
            string.Format("{0}: {1} votes currently shown. Click for where these votes came from.",TurnManager.CardLabel(card),card.GetComponent<CardVotePresentation>()?.DisplayedVotes??0);
        turns.ShowLocalActionFeedback(hoverMessage);
    }
    public void OnPointerExit(PointerEventData data)
    {var turns=TurnManager.instance;if(turns!=null&&turns.LocalActionFeedback==hoverMessage)turns.ClearLocalActionFeedback();hoverMessage=null;}
    public void OnPointerClick(PointerEventData data)
    {
        var card = GetComponentInParent<CardInstance>();
        if (card == null) return;
        if (data.button == PointerEventData.InputButton.Right)
        { card.GetComponent<CardInteraction>().OnPointerClick(data); return; }
        if (data.button == PointerEventData.InputButton.Left && !GameStartUI.IsBlockingInput)
            VoteEffectsView.Current?.ShowCard(card,kind);
    }
}

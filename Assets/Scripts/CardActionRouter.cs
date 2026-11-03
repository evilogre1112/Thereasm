using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>Shares local click feedback and routes guest gameplay actions through the authoritative host.</summary>
public static class CardActionRouter
{
    // The host validates guest commands through OnlineGameBridge.
    public static bool SendGuest(OnlineAction action, int source = 0, int target = 0,
        int seat = -1, int option = -1)
    {
        return OnlineSession.Guest && OnlineGameBridge.Instance != null &&
            OnlineGameBridge.Instance.Send(action, source, target, seat, option);
    }

    public static void PointerClick(CardInteraction card, OnlineCardInput guest, PointerEventData data)
    {
        if (card == null || data == null || TurnManager.instance == null || GameStartUI.IsBlockingInput) return;
        if (!OnlineSession.Guest)
        {
            card.PlayLocalClickAudio();
            card.PerformAuthoritativePointerClick(data);
            return;
        }

        // A vote/inspect overlay may forward a click to CardInteraction directly.
        if (guest == null) guest = card.GetComponent<OnlineCardInput>();
        if (guest == null || guest.IsDragging || guest.IsPresenting || data.dragging ||
            Time.unscaledTime < guest.SuppressClickUntil) return;

        card.PlayLocalClickAudio();
        if (data.button == PointerEventData.InputButton.Right)
        {
            var visuals = CardLocalVisualFeedback.For(card);
            if (visuals != null) visuals.StopHover(true);
            // Inspection is local on the guest; it does not send a gameplay command.
            card.PerformAuthoritativePointerClick(data);
            return;
        }
        if (data.button != PointerEventData.InputButton.Left) return;
        if (!OnlineSession.LocalTurn)
        {
            ActionFeedback.Reject(ActionFeedback.InputBlock());
            return;
        }
        var bridge = OnlineGameBridge.Instance;
        if (bridge == null) return;
        if (guest.DiscoverIndex >= 0)
        {
            SendGuest(OnlineAction.Discover, option: guest.DiscoverIndex);
            return;
        }
        var phase = TurnManager.instance.currentTurn;
        if (phase == TurnState.SetupPhase)
        {
            SendGuest(OnlineAction.SelectCur, guest.CardId);
            return;
        }
        if (phase == TurnState.Player1Turn || phase == TurnState.Player2Turn || phase == TurnState.SacrificePhase)
        {
            var instance = card.GetComponent<CardInstance>();
            if (card.isPlaced && instance != null && instance.cardData != null &&
                instance.cardData.cardSkill != null &&
                instance.cardData.cardSkill.skillType != SkillType.Passive)
                SendGuest(OnlineAction.Skill, guest.CardId);
        }
    }
}

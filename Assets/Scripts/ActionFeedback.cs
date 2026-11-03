using UnityEngine;

// Reasons shown to players must not change gameplay or network IDs.
public static class ActionFeedback
{
    public static bool Reject(string message)
    {
        var turns = TurnManager.instance;
        if (turns == null || string.IsNullOrEmpty(message)) return false;
        if (TutorialWorkshop.Active) TutorialController.Instance.workshop.Refused(message);
        if (OnlineGameBridge.ApplyingCommand && OnlineGameBridge.Instance != null) OnlineGameBridge.Instance.ReportActionRefusal(message);
        else if (!turns.IsBotTurn || OnlineSession.Active) turns.ShowLocalActionFeedback(message);
        return false;
    }

    public static int Hearts
    {
        get
        {
            var t = TurnManager.instance;
            int confirmed = t.currentTurn == TurnState.Player1Turn || t.currentTurn == TurnState.CursePhase ||
                t.currentTurn == TurnState.SacrificePhase && t.currentVotingPlayer == DefaultOwner.Player1 ? t.p1Hearts : t.p2Hearts;
            return OnlineSession.Guest && OnlineGameBridge.Instance != null ? OnlineGameBridge.Instance.DisplayHearts(confirmed) : confirmed;
        }
    }

    public static string Cost(CardInstance card, int cost) =>
        string.Format("{0}: this action needs {1} hearts; you have {2}. Choose a cheaper action or end your turn.", TurnManager.CardLabel(card), cost, Hearts);

    public static string InputBlock()
    {
        var t = TurnManager.instance;
        if (t == null) return null;
        if (t.isGameOver) return "The match has ended. Choose Rematch or Menu.";
        if (t.isTallyingVotes) return "Votes are being resolved. Wait for the next turn.";
        if (OnlineSession.Active && !OnlineSession.LocalTurn || !OnlineSession.Active && t.IsBotTurn)
            return "It is your opponent's turn. Wait for your turn before playing or moving cards.";
        return null;
    }

    public static string MovementBlock(CardInteraction card, bool checkTurnLock = true)
    {
        var ci = card.GetComponent<CardInstance>();
        string label = TurnManager.CardLabel(ci);
        if (ci == null || ci.isDead) return "Cards in the cemetery cannot be moved. Use a resurrection skill to bring them back.";
        if (card.isLockedByElder) return string.Format("{0} is held by Elder. Move it outside Elder's influence with a card effect, or remove Elder first.", label);
        if (card.isLockedBySkill) return string.Format("{0} cannot be moved by hand while its extra seats are occupied. Empty those seats first; card effects can still push it.", label);
        if (checkTurnLock && card.isPlaced && card.lockState < 3)
            return string.Format("{0} is resting after being played. You can move or retrieve it on your next action turn.", label);
        return null;
    }

    public static string PhaseMovementBlock()
    {
        switch (TurnManager.instance.currentTurn)
        {
            case TurnState.SetupPhase: return "Choose the secret roles and start the match before moving cards.";
            case TurnState.CursePhase: return "Cards cannot move during the curse ritual. Drag from a seated Cursermancer to spread the curse, or continue to Sacrifice.";
            case TurnState.SacrificePhase: return "Cards cannot move during Sacrifice. Drag from each of your seated cards to another living card to vote.";
            default: return null;
        }
    }

    public static string BeginDragBlock(CardInteraction card, bool right)
    {
        string reason = InputBlock();
        if (reason != null) return reason;
        var ci = card.GetComponent<CardInstance>();
        var t = TurnManager.instance;
        if (CardInteraction.currentlyInspectedCard != null) return "Close the enlarged card with right-click before moving cards.";
        if (ci == null || ci.isDead) return MovementBlock(card);
        if (ci.isFaceDown) return "This card is face down. Its information and actions are unavailable.";
        if (t.currentTurn == TurnState.SetupPhase) return PhaseMovementBlock();
        if (right)
        {
            if (!card.isPlaced) return "Play this card onto a seat before using its skill. Right-click without dragging to read it.";
            if (ci.cardData.cardSkill == null || ci.cardData.cardSkill.skillType == SkillType.Passive)
                return "This card has no active skill. Its passive effect works automatically when its conditions are met.";
            return null;
        }
        if (t.currentTurn == TurnState.CursePhase)
            return !card.isPlaced || !t.IsCur(ci) ? "Start the curse arrow from one of your seated Cursermancers." : null;
        if (t.currentTurn == TurnState.SacrificePhase)
            return !card.isPlaced || !t.IsMyVoteTurn(ci) ? "Only your living cards on the board can cast your votes. Drag from one of them to another living card." : null;
        return MovementBlock(card);
    }

    public static string ExtraSeatBlock(CardInteraction card, Transform destination)
    {
        var seat = destination != null ? destination.GetComponentInParent<SeatDropZone>() : null;
        return card.GetComponent<CardInstance>()?.cardData?.cardSkill is Skill_GameMaster && seat != null && seat.name.Contains("ExtraSeat") ?
            "GameMaster cannot occupy an extra seat. Choose one of the seven main seats." : null;
    }

    public static string SwapBlock(CardInteraction source, CardInteraction target, Transform destination)
    {
        string reason = MovementBlock(source);
        if (reason != null) return reason;
        // A newly played target can be displaced (state 1); state 2 is protected until its next turn.
        reason = MovementBlock(target, false);
        if (reason != null) return reason;
        if (target.isPlaced && target.lockState != 1 && target.lockState != 3)
            return string.Format("{0} is resting and cannot be swapped yet. Wait until its next action turn.", TurnManager.CardLabel(target.GetComponent<CardInstance>()));
        reason = ExtraSeatBlock(source, destination);
        if (reason != null) return reason;
        if (source.isPlaced && ExtraSeatBlock(target, source.originalParent) != null) return ExtraSeatBlock(target, source.originalParent);
        if (source.lastSwappedCard == target || target.lastSwappedCard == source)
            return "These two cards have already swapped this turn. Choose a different pair or wait for the next turn.";
        return null;
    }

    // Called only after the real skill refuses a cast. Never expose hidden infection or role data.
    public static string SkillFailure(CardSkill skill, CardInstance caster, CardInstance target)
    {
        var t = TurnManager.instance;
        if (t == null || caster == null) return "This card is no longer available. Choose another card.";
        string reason = InputBlock();
        // Host command processing represents the remote player's valid turn.
        if (reason != null && !OnlineGameBridge.ApplyingCommand) return reason;
        if (!t.IsLivingBoardCard(caster)) return "Play this card onto a seat before using its skill. Right-click without dragging to read it.";
        if (caster.isSilenced) return string.Format("{0}'s printed skill is blocked by Saboteur. Move it out of Saboteur's influence first.", TurnManager.CardLabel(caster));
        if (target != null && !t.IsLivingBoardCard(target)) return "Choose a living target on the board, not a card in a hand or the cemetery.";
        if (skill is Skill_Witch || skill is Skill_Invoker)
        {
            if (target != null) return string.Format("{0}: left-click the card itself to use this skill; do not drag onto another card.", TurnManager.CardLabel(caster));
        }
        else if (target == null) return string.Format("{0}: hold the right mouse button on this card, drag the arrow to a living target, then release.", TurnManager.CardLabel(caster));
        int cost = 0;
        if (skill is Skill_Hunter hunter)
        {
            if (t.currentTurn != TurnState.Player2Turn || caster.cardData.owner != DefaultOwner.Player2)
                return "Hunter can shoot only during the Villagers' action turn.";
            if (target == caster) return "Hunter cannot shoot itself. Choose another living card on the board.";
            cost = hunter.skillCost;
        }
        if (skill is Skill_Witch witch)
        {
            if (t.currentTurn != TurnState.Player1Turn && t.currentTurn != TurnState.Player2Turn && t.currentTurn != TurnState.SacrificePhase)
                return "Witch can act during an action turn or your Sacrifice voting turn, not the curse ritual.";
            cost = witch.skillCost;
        }
        if (skill is Skill_Invoker invoker)
        {
            if (t.currentTurn != TurnState.SacrificePhase) return "Invoker can summon only during Sacrifice, when its last player is voting.";
            if (t.currentVotingPlayer != caster.lastPlacedBy) return "Only the player who last placed Invoker can summon with it during their voting turn.";
            int seat = t.GetSeatIndex(caster.GetComponent<CardInteraction>());
            if (seat < 0 || !t.allSeats[seat].isNextToCemetery) return "Invoker must sit next to the cemetery to summon. Move it there during an action turn.";
            if (t.deadCardsData.Count == 0) return "The cemetery is empty. Invoker needs a dead card to summon.";
            cost = invoker.skillCost;
        }
        if (skill is Skill_Gambler gambler)
        {
            if (t.currentTurn != TurnState.SacrificePhase) return "Gambler accepts bets only during your Sacrifice voting turn.";
            cost = gambler.skillCost;
        }
        if (skill is Skill_Snooper)
            return "Snooper normally acts automatically at the vote tally. If its active effect is available, Cursermancer selects a target during Sacrifice.";
        if ((skill is Skill_Hunter || skill is Skill_Witch || skill is Skill_Invoker) && caster.hasUsedSkill)
            return string.Format("{0} has already used its skill this round. It becomes ready after the Sacrifice ritual ends.", TurnManager.CardLabel(caster));
        if (cost > Hearts) return Cost(caster, cost);
        return "This skill cannot use that target now. Check its description for the required phase and target.";
    }
}

using UnityEngine;

// Perspective rules are shared by host rendering and the data sent to the guest.
public static class PlayerInformation
{
    public static bool Secrets(DefaultOwner viewer, bool over) => over || viewer == DefaultOwner.Player1;

    public static bool LocalTruth
    {
        get
        {
            var tm = TurnManager.instance;
            if (tm == null) return false;
            if (tm.isGameOver) return true;
            if (OnlineSession.Active) return OnlineSession.LocalCur;
            if (TutorialWorkshop.Active) return TutorialController.Instance.workshop.Viewer == DefaultOwner.Player1;
            if (GameSettings.currentMode == GameMode.PlayerVsCurBot) return false;
            if (GameSettings.currentMode == GameMode.PlayerVsVillBot) return true;
            return tm.currentTurn == TurnState.SetupPhase || tm.currentTurn == TurnState.Player1Turn ||
                tm.currentTurn == TurnState.CursePhase || tm.currentTurn == TurnState.SacrificePhase && tm.currentVotingPlayer == DefaultOwner.Player1;
        }
    }

    public static bool UsedSkill(CardInstance card, DefaultOwner viewer, bool over)
    {
        if (over) return card.hasUsedSkill;
        if (card.cardData.cardSkill is Skill_Gambler) return Skill_Gambler.HasUsedBy(card, viewer);
        if (card.cardData.cardSkill is Skill_Snooper) return viewer == DefaultOwner.Player1 && card.hasUsedSkill;
        return card.hasUsedSkill;
    }

    public static bool GamblerMark(CardInstance card, DefaultOwner viewer) =>
        TurnManager.instance.currentTurn == TurnState.SacrificePhase && !TurnManager.instance.isTallyingVotes &&
        !TurnManager.instance.isGameOver && Skill_Gambler.IsMarkedFor(card, viewer);

    public static bool SnooperMark(CardInstance target, DefaultOwner viewer)
    {
        var tm = TurnManager.instance;
        if (tm.currentTurn != TurnState.SacrificePhase || tm.isGameOver || target.isDead) return false;
        foreach (var seat in tm.allSeats)
        {
            var owner = seat.cardPos.GetComponentInChildren<CardInstance>();
            if (owner != null && !owner.isDead && !owner.isSilenced && owner.cardData.cardSkill is Skill_Snooper skill &&
                skill.IsMarkedFor(target, viewer, tm.isTallyingVotes)) return true;
        }
        return false;
    }

    public static void RefreshHostMarks()
    {
        if (OnlineSession.Guest || TurnManager.instance == null) return;
        var viewer = TurnManager.instance.LocalBallotViewer;
        foreach (var card in Object.FindObjectsByType<CardInstance>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (card.cardData == null) continue;
            if (card.gamblerMark != null) card.gamblerMark.SetActive(GamblerMark(card, viewer));
            if (card.snooperMark != null) card.snooperMark.SetActive(SnooperMark(card, viewer));
        }
    }

    public static void Render(CardInstance card)
    {
        var tm = TurnManager.instance;
        if (card == null || card.cardData == null || card.cardImage == null || tm == null) return;
        bool truth = LocalTruth;
        bool cur = tm.IsCur(card);
        bool infected = card.isDead ? card.wasInfectedWhenDied : card.isInfected;
        // Invoker candidates are UI copies of corpses, not new identities.
        if (truth && tm.discoverPanel != null && card.transform.IsChildOf(tm.discoverPanel.transform))
            foreach (var corpse in Object.FindObjectsByType<CardInstance>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (corpse != card && corpse.isDead && corpse.cardData == card.cardData && !corpse.transform.IsChildOf(tm.discoverPanel.transform))
                { cur = tm.IsCur(corpse); infected = corpse.wasInfectedWhenDied; break; }
        bool publicBlind = card.isDead && card.isRevealedByBlind && !truth;
        bool showCur = publicBlind ? card.blindRevealAsCur : truth && cur;
        bool showInfection = publicBlind ? card.blindRevealAsInfected : truth && infected;
        if (card.isFaceDown && !tm.isGameOver)
        { card.cardImage.sprite = card.cardData.backArtwork; showInfection = false; }
        else card.cardImage.sprite = showCur ? card.cardData.cursemancerArtwork : card.cardData.defaultArtwork;
        card.cardImage.material = showInfection ? card.infectedMaterial : card.normalMaterial;
        if (card.infectionEffect != null) card.infectionEffect.SetActive(showInfection);
        card.UpdateSkillUI();
    }
}

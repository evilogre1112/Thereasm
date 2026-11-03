using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Villager AI: evaluates public evidence, plans legal tactical actions and votes.
// In particular it never consults TurnManager.allCurs, IsCur or CardInstance.isInfected.
public class VillagerBotAI : MonoBehaviour
{
    public float thinkingTime = 1.2f;
    [Header("AI Tactical Limits")]
    [Range(1, 12)] public int maxActionsPerTurn = 5;

    [Header("Buttons (preserved for existing Unity scene references)")]
    public GameObject btnEndTurn;
    public GameObject btnCheck;

    private readonly BotKnowledge knowledge = new BotKnowledge(DefaultOwner.Player2);
    private readonly BotStrategicPlanner strategic = new BotStrategicPlanner();
    [Header("v12: vote pressure through board composition")]
    public bool enableForcedVote = true;
    private readonly BotForcedVotePlanner forcedVote = new BotForcedVotePlanner();
    [Header("v13: Villager Neutral shielding calibrated on the 7/9/10-seat headless fixture")]
    [Range(120f, 240f)] public float neutralShieldPriorityV13Level2 = 210f;
    [Range(120f, 240f)] public float neutralShieldPriorityV13Level3 = 225f;
    [Header("Three-ply search v11.1 (me / rival / me)")]
    public bool enableFivePlySearch = true; // Serialized field name retained; the search depth is three plies.
    [Range(2, 3)] public int searchDepth = 3;
    [Range(2, 8)] public int searchBeamWidth = 4;
    [Range(200, 3000)] public int searchNodeBudget = 950;
    // Clamp old serialized search depths to three plies.
    private void OnValidate()
    {
        searchDepth = Mathf.Clamp(searchDepth, 2, 3);
        neutralShieldPriorityV13Level2 = Mathf.Clamp(neutralShieldPriorityV13Level2, 120f, 240f);
        neutralShieldPriorityV13Level3 = Mathf.Clamp(neutralShieldPriorityV13Level3, 120f, 240f);
        searchBeamWidth = Mathf.Clamp(searchBeamWidth, 2, 8);
        searchNodeBudget = Mathf.Clamp(searchNodeBudget, 200, 1600);
    }
    private TurnManager subscribedManager;
    private bool isBotPlaying;
    private bool hasVotedInThisPhase;
    private bool actionSuccess;

    private void OnDisable()
    {
        if (subscribedManager != null)
            subscribedManager.PublicPurgeResult -= knowledge.OnPublicPurge;
        subscribedManager = null;
    }

    private void ObservePublicInformation()
    {
        TurnManager tm = TurnManager.instance;
        if (subscribedManager != tm)
        {
            if (subscribedManager != null)
                subscribedManager.PublicPurgeResult -= knowledge.OnPublicPurge;
            subscribedManager = tm;
            strategic.Reset();
            if (subscribedManager != null)
                subscribedManager.PublicPurgeResult += knowledge.OnPublicPurge;
        }
        if (tm != null && !tm.isGameOver) knowledge.Observe(tm);
    }

    private void Update()
    {
        ObservePublicInformation();
        TurnManager tm = TurnManager.instance;
        if (tm == null || tm.isGameOver || tm.isTallyingVotes ||
            GameStartUI.IsBlockingInput || isBotPlaying) return;
        if (tm.currentTurn != TurnState.SacrificePhase) hasVotedInThisPhase = false;
        if (tm.currentTurn == TurnState.Player2Turn ||
            (tm.currentTurn == TurnState.SacrificePhase &&
             tm.currentVotingPlayer == DefaultOwner.Player2 && !hasVotedInThisPhase))
            StartCoroutine(BotBrainRoutine());
    }

    private void ClickButton(GameObject obj)
    {
        if (obj != null && obj.activeInHierarchy)
        {
            Button btn = obj.GetComponent<Button>();
            if (btn != null && btn.interactable) btn.onClick.Invoke();
        }
    }

    private IEnumerator BotBrainRoutine()
    {
        isBotPlaying = true;
        yield return new WaitForSeconds(thinkingTime);
        TurnManager tm = TurnManager.instance;
        if (tm != null && !tm.isGameOver)
        {
            if (tm.currentTurn == TurnState.Player2Turn)
            {
                yield return StartCoroutine(PlayActionPhase(tm));
                if (!tm.isGameOver && tm.currentTurn == TurnState.Player2Turn)
                    ClickButton(btnEndTurn);
            }
            else if (tm.currentTurn == TurnState.SacrificePhase &&
                     tm.currentVotingPlayer == DefaultOwner.Player2)
                yield return StartCoroutine(PlaySacrificePhase(tm));
        }
        yield return new WaitForSeconds(0.5f);
        isBotPlaying = false;
    }

    private IEnumerator PlayActionPhase(TurnManager tm)
    {
        forcedVote.enabled = enableForcedVote;
        int actions = 0;
        while (!tm.isGameOver && tm.currentTurn == TurnState.Player2Turn &&
               tm.HasEnoughHearts(1) && actions++ < maxActionsPerTurn)
        {
            actionSuccess = false;
            CardInteraction recall;
            if (actions < maxActionsPerTurn &&
                forcedVote.TryRecallOwn(tm, DefaultOwner.Player2, out recall))
            {
                recall.ReturnToHand();
                actionSuccess = !recall.isPlaced;
            }
            // Ability planner never reads Cur identities for Villager.
            if (!actionSuccess)
                actionSuccess = BotFunctionalSkillAI.TryActionSkill(tm, DefaultOwner.Player2, knowledge);
            if (!actionSuccess && tm.HasEnoughHearts(5))
                yield return StartCoroutine(TryHunterAction(tm));
            if (!actionSuccess) yield return StartCoroutine(TryPlacement(tm));
            if (!actionSuccess) TryTacticalSwap(tm);
            // A legal Neutral/other placement is preferable to repeatedly
            // passing with cards still available. Keep tactical choices first.
            if (!actionSuccess) yield return StartCoroutine(TryPlacement(tm, true));
            if (!actionSuccess) break;
            yield return new WaitForSeconds(0.4f);
        }
        if (!tm.isGameOver && tm.currentTurn == TurnState.Player2Turn)
            yield return StartCoroutine(TryPublicCheck(tm));
    }

    private IEnumerator TryHunterAction(TurnManager tm)
    {
        foreach (var seat in tm.allSeats)
        {
            CardInstance hunter = seat.cardPos.GetComponentInChildren<CardInstance>();
            if (hunter == null || hunter.cardData == null || hunter.isSilenced ||
                hunter.hasUsedSkill || hunter.cardData.owner != DefaultOwner.Player2 ||
                !(hunter.cardData.cardSkill is Skill_Hunter)) continue;
            CardInstance victim = BotDecisionEngine.ChooseHunterTarget(tm, knowledge, hunter);
            if (victim != null && hunter.cardData.cardSkill.TryCastActiveSkill(hunter, victim))
            {
                actionSuccess = true;
                yield break;
            }
        }
    }

    private IEnumerator TryPlacement(TurnManager tm, bool allowProgressPlacement = false)
    {
        BotDecisionEngine.Placement choice;
        strategic.enableFivePlySearch = enableFivePlySearch;
        strategic.planningDepth = Mathf.Clamp(searchDepth, 2, 3);
        strategic.planningBeam = searchBeamWidth;
        strategic.planningNodeBudget = Mathf.Clamp(searchNodeBudget, 200, 1600);
        forcedVote.enabled = enableForcedVote;
        float neutralPriority = GameSettings.currentLevel == 3 ?
            neutralShieldPriorityV13Level3 : neutralShieldPriorityV13Level2;
        BotDecisionEngine.VillagerNeutralShieldPriority = Mathf.Clamp(
            neutralPriority, 120f, 240f);
        if (!forcedVote.TryPlaceOpponent(tm, DefaultOwner.Player2, knowledge, out choice) &&
            !strategic.TryFindPlacement(tm, DefaultOwner.Player2, knowledge,
                out choice, allowProgressPlacement))
            yield break;
        DeckManager.instance.SwitchToHand(choice.hand);
        yield return new WaitForSeconds(0.35f);
        if (tm.isGameOver || tm.currentTurn != TurnState.Player2Turn ||
            choice.seat < 0 || choice.seat >= tm.allSeats.Count ||
            choice.card == null || choice.card.isPlaced ||
            tm.allSeats[choice.seat].cardPos.childCount != 0)
        {
            DeckManager.instance.SwitchToHand(DefaultOwner.Player2);
            yield break;
        }
        choice.card.PlaceOnSeat(tm.allSeats[choice.seat].cardPos);
        if (choice.card.isPlaced)
        {
            StartCoroutine(SnapCardToCenter(choice.card));
            strategic.OnMoveExecuted(choice);
            actionSuccess = true;
        }
        DeckManager.instance.SwitchToHand(DefaultOwner.Player2);
    }

    private void TryTacticalSwap(TurnManager tm)
    {
        CardInteraction first, second;
        int seat;
        if (!BotDecisionEngine.TryFindTacticalSwap(tm, DefaultOwner.Player2,
            knowledge, out first, out second, out seat)) return;
        first.SwapWith(second, tm.allSeats[seat].cardPos);
        if (tm.GetSeatIndex(first) == seat)
        {
            actionSuccess = true;
            StartCoroutine(SnapCardToCenter(first));
            StartCoroutine(SnapCardToCenter(second));
        }
    }

    private IEnumerator PlaySacrificePhase(TurnManager tm)
    {
        if (hasVotedInThisPhase) yield break;
        // Witch is also legal at vote time: newly infected cards exist only now.
        BotFunctionalSkillAI.TryActionSkill(tm, DefaultOwner.Player2, knowledge);
        if (tm.isGameOver || tm.currentTurn != TurnState.SacrificePhase ||
            tm.currentVotingPlayer != DefaultOwner.Player2) yield break;
        yield return StartCoroutine(BotFunctionalSkillAI.TryInvoker(tm, DefaultOwner.Player2, knowledge));
        if (tm.isGameOver || tm.currentTurn != TurnState.SacrificePhase ||
            tm.currentVotingPlayer != DefaultOwner.Player2) yield break;
        BotFunctionalSkillAI.TrySacrificeSkills(tm, DefaultOwner.Player2, knowledge);
        hasVotedInThisPhase = true;
        List<CardInstance> voters = new List<CardInstance>();
        foreach (var seat in tm.allSeats)
        {
            CardInstance card = seat.cardPos.GetComponentInChildren<CardInstance>();
            if (card != null && !card.isDead && card.cardData != null &&
                card.cardData.owner == DefaultOwner.Player2) voters.Add(card);
        }
        int[] counts = new int[tm.allSeats.Count];
        bool controller = BotDecisionEngine.IsControllerActive(tm);
        foreach (CardInstance voter in voters)
        {
            if (tm.isGameOver || tm.currentTurn != TurnState.SacrificePhase ||
                tm.currentVotingPlayer != DefaultOwner.Player2) yield break;
            tm.StartVoteSelection(voter);
            int proposed = BotDecisionEngine.ChooseVoteTarget(tm, DefaultOwner.Player2,
                knowledge, voter, counts, controller);
            int legal = tm.FindLegalVoteTarget(voter, proposed);
            if (legal >= 0)
            {
                tm.ApplyVote(legal);
                counts[legal]++;
                HideBotVotes(tm);
            }
            yield return new WaitForSeconds(0.25f);
        }
        yield return new WaitForSeconds(0.45f);
        if (tm.currentTurn == TurnState.SacrificePhase && !tm.isGameOver)
            ClickButton(btnEndTurn);
    }

    private IEnumerator TryPublicCheck(TurnManager tm)
    {
        if (tm == null || tm.p2Checks <= 0 || btnCheck == null ||
            !btnCheck.activeInHierarchy || tm.currentTurn != TurnState.Player2Turn)
            yield break;
        // Check can remove infected villagers from hidden hands as well as the board.
        // Estimate collateral damage across the living deck, not just seats.
        // Never inspect isInfected/allCurs or a hidden role.
        float infectedTotal = 0f, nonVillagerInfected = 0f, ownVillagerRisk = 0f;
        int livePlayer2Cards = 0;
        var deck = DeckManager.instance;
        if (deck == null || deck.allCards == null) yield break;
        foreach (CardData data in deck.allCards)
        {
            if (data == null || tm.deadCardsData.Contains(data)) continue;
            float estimatedInfection = knowledge.Infection(data);
            infectedTotal += estimatedInfection;
            if (data.owner == DefaultOwner.Player2)
            {
                livePlayer2Cards++;
                ownVillagerRisk += estimatedInfection * (1f - knowledge.Cur(data));
            }
            else nonVillagerInfected += estimatedInfection;
        }
        int expectedCurTotal = 0;
        foreach (DefaultOwner owner in new[] { DefaultOwner.Player1,
            DefaultOwner.Player2, DefaultOwner.Neutral })
            expectedCurTotal += GameRules.RequiredCursForOwner(
                GameSettings.currentMode, GameSettings.currentLevel, owner);
        int p2CurQuota = GameRules.RequiredCursForOwner(
            GameSettings.currentMode, GameSettings.currentLevel, DefaultOwner.Player2);
        float villagerUpperBound = livePlayer2Cards - p2CurQuota;
        // Conservative gate: preserve at least one Villager above the maximum
        // possible remaining Cur count even after expected Check casualties.
        if (ownVillagerRisk > .45f ||
            villagerUpperBound - ownVillagerRisk <= expectedCurTotal + 1f)
            yield break;
        float infoGain = knowledge.CheckInformationValue(tm);
        float checkValue = nonVillagerInfected * 48f + infectedTotal * 12f +
            infoGain - ownVillagerRisk * 65f;
        bool baselineCheck = !knowledge.HasCleanCheckBaseline && tm.p2Checks >= 2 &&
            knowledge.RitualsObserved >= 1 && infectedTotal >= .35f &&
            ownVillagerRisk < .25f;
        bool tacticalCheck = infectedTotal >= .55f && checkValue >= 30f;
        if (baselineCheck || tacticalCheck)
        {
            int checksBefore = tm.p2Checks;
            knowledge.BeginOwnCheck();
            ClickButton(btnCheck);
            knowledge.FinishOwnCheck(tm.p2Checks < checksBefore);
            yield return new WaitForSeconds(0.6f);
        }
    }

    private void HideBotVotes(TurnManager tm)
    {
        if (!tm.isDisguiseModeActive) return;
        foreach (var seat in tm.allSeats)
        {
            CardInstance card = seat.cardPos.GetComponentInChildren<CardInstance>();
            if (card != null && card.voteText != null)
                card.voteText.gameObject.SetActive(false);
        }
    }

    private IEnumerator SnapCardToCenter(CardInteraction card)
    {
        yield return new WaitForEndOfFrame();
        if (card == null) yield break;
        RectTransform rt = card.transform as RectTransform;
        if (rt != null)
        {
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.localPosition = Vector3.zero;
            rt.localScale = card.PrefabRootScale;
        }
        // Only root card is centered in its seat; the artwork retains
        // its individually authored offset in CardUI.
        card.RestoreVisualPrefabPose();
    }
}

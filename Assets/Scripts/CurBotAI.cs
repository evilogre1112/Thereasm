using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class CurBotAI : MonoBehaviour
{
    public TurnManager tm;
    public float thinkingTime = 1.5f;
    [Header("AI Tactical Limits")]
    [Range(1, 12)] public int maxActionsPerTurn = 5;

    [Header("Các nút UI Bot cần bấm")]
    public GameObject btnEndTurn;      
    public GameObject btnCheck;        

    [Header("Adaptive deception v9")]
    [Range(0f, 1f)] public float deceptionIntensity = 0.12f;
    private readonly CurDeceptionPlanner deception = new CurDeceptionPlanner();
    private readonly BotStrategicPlanner strategic = new BotStrategicPlanner();
    [Header("v12: vote pressure through board composition")]
    public bool enableForcedVote = true;
    private readonly BotForcedVotePlanner forcedVote = new BotForcedVotePlanner();
    [Tooltip("Level 3 has a tested conservative v9 fallback. Enable only after testing this lookahead against the specific Cur meta.")]
    // Retain the serialized field for existing scenes; lookahead is enabled at every level.
    public bool enableCurLookaheadLevel3 = false;
    [Header("Three-ply search v11.1 (me / rival / me)")]
    public bool enableFivePlySearch = true; // Serialized field name retained; the search depth is three plies.
    [Range(2, 3)] public int searchDepth = 3;
    [Range(2, 8)] public int searchBeamWidth = 4;
    [Range(200, 3000)] public int searchNodeBudget = 950;
    // Clamp old serialized search depths to three plies.
    private void OnValidate()
    {
        searchDepth = Mathf.Clamp(searchDepth, 2, 3);
        searchBeamWidth = Mathf.Clamp(searchBeamWidth, 2, 8);
        searchNodeBudget = Mathf.Clamp(searchNodeBudget, 200, 1600);
    }
    private int lastObservedVillagerChecks = -1;

    // Check clears infections, so old curse memories are no longer guaranteed.
    private void ObserveOpponentCheck()
    {
        if (tm == null) return;
        if (lastObservedVillagerChecks >= 0 && tm.p2Checks < lastObservedVillagerChecks)
            knownCurseVictims.Clear();
        if (lastObservedVillagerChecks >= 0 &&
            tm.p2Checks < lastObservedVillagerChecks) strategic.Reset();
        lastObservedVillagerChecks = tm.p2Checks;
    }

    private bool isBotPlaying = false;
    private bool hasVotedInThisPhase = false; 

    // Known only through this Cur bot's own successful curse selections.
    // A victim can leave the board: Check still purges infected cards in hands.
    private readonly HashSet<CardInstance> knownCurseVictims = new HashSet<CardInstance>();
    [Header("Check strategy (Cur)")]
    [Min(1)] public int preferredCheckVictims = 2;

    // No hidden isInfected flag is read to decide to use Check.
    // Keep the list through rituals because TurnManager clears currentVictim
    // at the start of every ritual, while old infections may persist.
    private void RememberOwnCurse(CardInstance cur, CardInstance victim)
    {
        if (cur != null && victim != null && !victim.isDead &&
            cur.currentVictim == victim)
            knownCurseVictims.Add(victim);
    }

    private bool TryStrategicCheck()
    {
        if (tm == null || tm.isGameOver || tm.currentTurn != TurnState.Player1Turn ||
            tm.p1Checks <= 0 || tm.isTallyingVotes) return false;

        // The bot knows which cards are Cur, and remembers only those victims
        // that it itself successfully cursed. Do not inspect isInfected on any
        // opponent card, because that is hidden information in normal gameplay.
        int targetVillagers = 0;
        foreach (CardInstance victim in knownCurseVictims)
        {
            if (victim == null || victim.isDead || victim.cardData == null ||
                tm.allCurs.Contains(victim)) continue;
            if (victim.cardData.owner == DefaultOwner.Player2) targetVillagers++;
        }
        if (targetVillagers == 0) return false;

        int aliveCur = 0;
        foreach (CardInstance cur in tm.allCurs)
            if (cur != null && !cur.isDead) aliveCur++;
        int aliveVillagers = 0;
        CardInstance[] cards = FindObjectsByType<CardInstance>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (CardInstance c in cards)
        {
            if (c == null || c.isDead || c.cardData == null ||
                c.gameObject.scene.buildIndex == -1 || tm.allCurs.Contains(c) ||
                (tm.discoverPanel != null && c.transform.IsChildOf(tm.discoverPanel.transform)) ||
                c.transform.parent == tm.cemeteryCardPos ||
                c.transform.parent == tm.globalCemetery) continue;
            if (c.cardData.owner == DefaultOwner.Player2) aliveVillagers++;
        }

        // Free Check (0 Tim): purge now if it is likely decisive or hits
        // several known villagers. With one target, preserve the charge unless
        // the game is close or the Villager has already exhausted its Checks.
        bool gameEnding = aliveCur > 0 && aliveVillagers - targetVillagers <= aliveCur;
        bool strongPurge = targetVillagers >= Mathf.Max(1, preferredCheckVictims);
        bool urgent = targetVillagers >= 1 &&
            (aliveVillagers - targetVillagers <= aliveCur + 1 || tm.p2Checks == 0);
        if (!gameEnding && !strongPurge && !urgent) return false;

        int before = tm.p1Checks;
        // ExecuteCheck enforces turn ownership, remaining charges and
        // Tutorial/Online guards. Calling it directly avoids an unwired button.
        tm.ExecuteCheck();
        if (tm.p1Checks >= before) return false;
        // Public Check removes ALL living infections (including cards in hand).
        knownCurseVictims.Clear();
        return true;
    }


   void Update()
    {
        ObserveOpponentCheck();
        if (tm == null || isBotPlaying || tm.isGameOver || tm.isTallyingVotes || GameStartUI.IsBlockingInput) return;

        if (tm.currentTurn == TurnState.SetupPhase && DeckManager.instance != null)
        {
            if (DeckManager.instance.draftPanel != null && DeckManager.instance.draftPanel.activeSelf) return;
            if (DeckManager.instance.player1Hand != null && DeckManager.instance.player1Hand.childCount == 0) return;
        }

        if (tm.currentTurn != TurnState.SacrificePhase) hasVotedInThisPhase = false;

        bool isMyTurn = false;
        
        if (tm.currentTurn == TurnState.SetupPhase && DeckManager.instance != null && DeckManager.instance.IsReady) isMyTurn = true;
        
        else if (tm.currentTurn == TurnState.Player1Turn || tm.currentTurn == TurnState.CursePhase) isMyTurn = true;
        else if (tm.currentTurn == TurnState.SacrificePhase && tm.currentVotingPlayer == DefaultOwner.Player1 && !hasVotedInThisPhase) isMyTurn = true;

        if (isMyTurn) StartCoroutine(BotBrainRoutine());
    }

    private void ClickButton(GameObject btnObj)
    {
        if (btnObj != null && btnObj.activeInHierarchy)
        {
            Button btn = btnObj.GetComponent<Button>();
            if (btn != null && btn.interactable) btn.onClick.Invoke();
        }
    }

    private IEnumerator BotBrainRoutine()
    {
        isBotPlaying = true;
        yield return new WaitForSeconds(thinkingTime);

        switch (tm.currentTurn)
        {
            case TurnState.SetupPhase: yield return StartCoroutine(PlaySetupPhase()); break;
            case TurnState.Player1Turn: 
                yield return StartCoroutine(PlayActionPhase()); 
                if (tm.currentTurn == TurnState.Player1Turn && !tm.isGameOver) ClickButton(btnEndTurn);
                break;
            case TurnState.CursePhase: 
                yield return StartCoroutine(PlayCursePhase()); 
                if (tm.currentTurn == TurnState.CursePhase && !tm.isGameOver) ClickButton(btnEndTurn);
                break;
            case TurnState.SacrificePhase: 
                yield return StartCoroutine(PlaySacrificePhase()); 
                break;
        }

        yield return new WaitForSeconds(1f);
        isBotPlaying = false;
    }

    private IEnumerator PlaySetupPhase()
    {
        knownCurseVictims.Clear();
        strategic.Reset();
        BotFunctionalSkillAI.ResetInvokerMemory();
        if (lastObservedVillagerChecks >= 0 &&
            tm.p2Checks < lastObservedVillagerChecks) strategic.Reset();
        lastObservedVillagerChecks = tm.p2Checks;
        // Prefer a direct opening at levels 1 and 2 rather than spending on deception.
        int currentLevel = GameRules.ClampLevel(GameSettings.currentLevel);
        deception.Reset(currentLevel >= 3 ? deceptionIntensity : 0f);
        if (!tm.isDisguiseModeActive) tm.ToggleDisguiseMode();
        
        int level = GameRules.ClampLevel(GameSettings.currentLevel);
        int botDeckCount = GameRules.RequiredCursForOwner(GameSettings.currentMode, level, DefaultOwner.Player1);
        int playerDeckCount = GameRules.RequiredCursForOwner(GameSettings.currentMode, level, DefaultOwner.Player2);
        int neutralDeckCount = GameRules.RequiredCursForOwner(GameSettings.currentMode, level, DefaultOwner.Neutral);

        yield return StartCoroutine(PickCursFromHand(DefaultOwner.Player1, botDeckCount));
        yield return StartCoroutine(PickCursFromHand(DefaultOwner.Player2, playerDeckCount));
        yield return StartCoroutine(PickCursFromHand(DefaultOwner.Neutral, neutralDeckCount));

        if (tm.btnStartGame != null && tm.btnStartGame.GetComponent<Button>().interactable) 
            ClickButton(tm.btnStartGame);
    }

    private IEnumerator PickCursFromHand(DefaultOwner owner, int count)
    {
        count -= tm.allCurs.FindAll(c => c != null && c.cardData != null && c.cardData.owner == owner).Count;
        for (int i = 0; i < count; i++)
        {
            SelectRandomCur(owner);
            yield return new WaitForSeconds(0.3f);
        }
    }

    private void SelectRandomCur(DefaultOwner owner)
    {
        // Keep essential support cards and Hunter out of the Cur selection.
        Transform hand = owner == DefaultOwner.Player1 ? DeckManager.instance.player1Hand :
            owner == DefaultOwner.Player2 ? DeckManager.instance.player2Hand : DeckManager.instance.neutralHand;
        if (hand == null) return;
        CardInstance best = null;
        float bestScore = float.NegativeInfinity;
        foreach (Transform child in hand)
        {
            CardInstance card = child.GetComponent<CardInstance>();
            if (card == null || card.cardData == null || card.isDead ||
                card.cardData.owner != owner || card.cardData.cardNumber == 9 ||
                card.cardData.cardType == CardType.Hunter || tm.allCurs.Contains(card)) continue;
            CardSkill skill = card.cardData.cardSkill;
            float score = Random.Range(0f, 1.5f);
            if (skill == null) score += 12;
            if (skill is Skill_Preacher || skill is Skill_Invoker || skill is Skill_Controller) score -= 15;
            if (skill is Skill_Hunter) score -= 100;
            if (score > bestScore) { bestScore = score; best = card; }
        }
        if (best != null) tm.SelectCurCard(best);
    }

    private IEnumerator PlayActionPhase()
    {
        // Check has no Tim cost: it should not depend on having spare hearts.
        TryStrategicCheck();
        deception.BeginActionTurn(tm);
        forcedVote.enabled = enableForcedVote;
        int actions = 0;
        while (!tm.isGameOver && tm.currentTurn == TurnState.Player1Turn &&
               tm.HasEnoughHearts(1) && actions++ < maxActionsPerTurn)
        {
            // Tactical active skills are evaluated before committing scarce Tim to placement.
            // If the board is full during THIS turn, consider freeing one
            // own seat and replacing it with an opponent card before EndTurn.
            // No changes are applied to the live board during planning.
            bool actionSuccess = false;
            CardInteraction recall;
            if (actions < maxActionsPerTurn &&
                forcedVote.TryRecallOwn(tm, DefaultOwner.Player1, out recall))
            {
                recall.ReturnToHand();
                actionSuccess = !recall.isPlaced;
            }
            if (!actionSuccess)
                actionSuccess = BotFunctionalSkillAI.TryActionSkill(tm, DefaultOwner.Player1, null);
            if (!actionSuccess)
                yield return StartCoroutine(BotPlannedPlaceAction(result => actionSuccess = result));
            if (!actionSuccess) actionSuccess = TryTacticalSwap();
            // Only after useful placements, skills and swaps are exhausted:
            // choose the least harmful legal move to prevent endless passing.
            if (!actionSuccess)
                yield return StartCoroutine(BotPlannedPlaceAction(
                    result => actionSuccess = result, true));
            if (!actionSuccess) break;
            yield return new WaitForSeconds(0.45f);
        }
        if (tm.currentTurn == TurnState.Player1Turn && !tm.isGameOver)
            TryStrategicCheck();
    }

    private IEnumerator BotPlannedPlaceAction(System.Action<bool> callback,
        bool allowProgressPlacement = false)
    {
        BotDecisionEngine.Placement plan;
        // Both factions get at most 3-ply counterplay evaluation.
        // Fall back to the immediate planner if deeper search finds no legal candidate.
        strategic.enableFivePlySearch = enableFivePlySearch;
        strategic.planningDepth = Mathf.Clamp(searchDepth, 2, 3);
        strategic.planningBeam = searchBeamWidth;
        strategic.planningNodeBudget = Mathf.Clamp(searchNodeBudget, 200, 1600);
        bool usingLookahead = true;
        forcedVote.enabled = enableForcedVote;
        bool forced = forcedVote.TryPlaceOpponent(tm, DefaultOwner.Player1, null, out plan);
        bool hasPlan = forced || (usingLookahead
            ? strategic.TryFindPlacement(tm, DefaultOwner.Player1,
                null, out plan, allowProgressPlacement)
            : BotDecisionEngine.TryFindPlacement(tm, DefaultOwner.Player1,
                null, out plan, allowProgressPlacement));
        bool decoy = false;
        if (hasPlan && !forced && !allowProgressPlacement && plan.card != null)
        {
            CardInstance plannedCard = plan.card.GetComponent<CardInstance>();
            // Never replace a real Cur deployment with a decoy.
            if (plannedCard != null && !tm.IsCur(plannedCard))
            {
                BotDecisionEngine.Placement bait;
                if (deception.TryDecoyPlacement(tm, out bait) &&
                    (!usingLookahead ||
                     strategic.EvaluateAlternative(tm, DefaultOwner.Player1, null, bait) >=
                         plan.score - 8f))
                {
                    plan = bait;
                    decoy = true;
                }
            }
        }
        if (!hasPlan)
        {
            callback(false);
            yield break;
        }
        DeckManager.instance.SwitchToHand(plan.hand);
        yield return new WaitForSeconds(0.35f);
        if (tm.currentTurn != TurnState.Player1Turn || plan.seat < 0 ||
            plan.seat >= tm.allSeats.Count || tm.GetBoardCard(plan.seat) != null ||
            plan.card == null || plan.card.isPlaced)
        {
            DeckManager.instance.SwitchToHand(DefaultOwner.Player1);
            callback(false);
            yield break;
        }
        plan.card.PlaceOnSeat(tm.allSeats[plan.seat].cardPos);
        if (plan.card.isPlaced)
        {
            if (decoy) deception.ConfirmDecoy();
            strategic.OnMoveExecuted(plan);
            StartCoroutine(SnapCardToCenter(plan.card));
        }
        DeckManager.instance.SwitchToHand(DefaultOwner.Player1);
        callback(plan.card.isPlaced);
    }

    private bool TryTacticalSwap()
    {
        CardInteraction first, second;
        int targetSeat;
        if (!BotDecisionEngine.TryFindTacticalSwap(tm, DefaultOwner.Player1,
            null, out first, out second, out targetSeat)) return false;
        first.SwapWith(second, tm.allSeats[targetSeat].cardPos);
        if (tm.GetSeatIndex(first) != targetSeat) return false;
        StartCoroutine(SnapCardToCenter(first));
        StartCoroutine(SnapCardToCenter(second));
        return true;
    }

    private IEnumerator PlayCursePhase()
    {
        // Only legal target seats offered by TurnManager can be selected.
        // Prevented targets and already infected victims are deliberately avoided.
        List<CardInstance> curs = new List<CardInstance>(tm.allCurs);
        foreach (CardInstance cur in curs)
        {
            if (tm.currentTurn != TurnState.CursePhase || tm.isGameOver) yield break;
            if (cur == null || cur.isDead || cur.hasUsedSkillInRitual) continue;
            CardInteraction interaction = cur.GetComponent<CardInteraction>();
            if (interaction == null || !interaction.isPlaced) continue;
            tm.StartCurseSelection(cur);
            yield return new WaitForSeconds(0.25f);
            int baseline = BotDecisionEngine.ChooseCurseTarget(tm, cur);
            int target = deception.ChooseCurseTarget(tm, cur, baseline);
            if (target < 0) continue;
            CardInstance victim = tm.GetBoardCard(target);
            CardInstance previousVictim = cur.currentVictim;
            tm.ApplyCurse(target);
            if (cur.currentVictim == victim)
            {
                if (previousVictim != null && previousVictim != victim)
                    knownCurseVictims.Remove(previousVictim);
                RememberOwnCurse(cur, victim);
            }
            yield return new WaitForSeconds(0.3f);
        }
    }


    private IEnumerator PlaySacrificePhase()
    {
        if (hasVotedInThisPhase) yield break;
        hasVotedInThisPhase = true;
        // Optional resurrection and tactical active skills *before* vote decisions.
        // Witch is also legal at vote time: newly infected cards exist only now.
        BotFunctionalSkillAI.TryActionSkill(tm, DefaultOwner.Player1, null);
        if (tm.isGameOver || tm.currentTurn != TurnState.SacrificePhase ||
            tm.currentVotingPlayer != DefaultOwner.Player1) yield break;
        yield return StartCoroutine(BotFunctionalSkillAI.TryInvoker(tm, DefaultOwner.Player1, null));
        if (tm.isGameOver || tm.currentTurn != TurnState.SacrificePhase ||
            tm.currentVotingPlayer != DefaultOwner.Player1) yield break;
        BotFunctionalSkillAI.TrySacrificeSkills(tm, DefaultOwner.Player1, null);
        List<CardInstance> voters = new List<CardInstance>();
        foreach (var seat in tm.allSeats)
        {
            CardInstance ci = seat.cardPos.GetComponentInChildren<CardInstance>();
            if (ci != null && !ci.isDead && ci.cardData != null &&
                ci.cardData.owner == DefaultOwner.Player1) voters.Add(ci);
        }
        int[] plannedVotes = new int[tm.allSeats.Count];
        bool controllerActive = BotDecisionEngine.IsControllerActive(tm);
        foreach (CardInstance voter in voters)
        {
            if (tm.currentTurn != TurnState.SacrificePhase || tm.isGameOver) yield break;
            tm.StartVoteSelection(voter);
            int selected = BotDecisionEngine.ChooseVoteTarget(tm, DefaultOwner.Player1,
                null, voter, plannedVotes, controllerActive);
            int legal = tm.FindLegalVoteTarget(voter, selected);
            if (legal >= 0)
            {
                tm.ApplyVote(legal);
                plannedVotes[legal]++;
                HideBotVotes();
            }
            yield return new WaitForSeconds(0.3f);
        }
        yield return new WaitForSeconds(0.5f);
        ClickButton(btnEndTurn);
    }


    private void HideBotVotes() 
    {
        if (tm.isDisguiseModeActive)
            foreach(var seat in tm.allSeats) 
            {
                var ci = seat.cardPos.GetComponentInChildren<CardInstance>();
                if (ci != null && ci.voteText != null) ci.voteText.gameObject.SetActive(false);
            }
    }

    private IEnumerator SnapCardToCenter(CardInteraction card) 
    {
        float timeElapsed = 0f;
        
        // Giữ vị trí trong lúc hiệu ứng chia bài còn chạy để tránh bị kéo về tay.
        while (timeElapsed < 0.8f)
        {
            yield return null; 
            if (card == null) yield break;
            
            RectTransform rt = card.transform as RectTransform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero; 
            rt.localPosition = Vector3.zero;
            rt.localScale = card.PrefabRootScale;
            
            // Giữ góc quay của FaceUp để hiệu ứng lật không bị giật.

            card.RestoreVisualPrefabPose();

            timeElapsed += Time.deltaTime;
        }
    }
}

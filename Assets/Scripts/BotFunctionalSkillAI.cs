using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Active-skill tactics. Passive skills are evaluated through placement; Villager uses public evidence only.</summary>
public static class BotFunctionalSkillAI
{
    // Prevent low-value resurrection cycles without changing the Invoker rule.
    private static TurnManager invokerHistoryOwner;
    private static readonly Dictionary<DefaultOwner, List<CardData>> recentRevives =
        new Dictionary<DefaultOwner, List<CardData>>();

    private static List<CardData> ReviveHistory(TurnManager tm, DefaultOwner role)
    {
        if (invokerHistoryOwner != tm)
        {
            invokerHistoryOwner = tm;
            recentRevives.Clear();
        }
        List<CardData> history;
        if (!recentRevives.TryGetValue(role, out history))
        {
            history = new List<CardData>(8);
            recentRevives[role] = history;
        }
        return history;
    }

    public static void ResetInvokerMemory()
    {
        invokerHistoryOwner = null;
        recentRevives.Clear();
    }

    static DefaultOwner Opponent(DefaultOwner role) =>
        role == DefaultOwner.Player1 ? DefaultOwner.Player2 : DefaultOwner.Player1;

    static bool Available(TurnManager tm, CardInstance card)
    {
        if (tm == null || card == null || card.isDead || card.isFaceDown || card.isSilenced ||
            card.cardData == null || card.cardData.cardSkill == null) return false;
        var interaction = card.GetComponent<CardInteraction>();
        return interaction != null && interaction.isPlaced && tm.IsLivingBoardCard(card);
    }

    static IEnumerable<CardInstance> Board(TurnManager tm)
    {
        // Snapshot is important: Witch/Invoker can mutate the scene while a turn is running.
        var cards = new List<CardInstance>();
        if (tm == null || tm.allSeats == null) return cards;
        foreach (var seat in tm.allSeats)
        {
            if (seat == null || seat.cardPos == null) continue;
            var card = seat.cardPos.GetComponentInChildren<CardInstance>();
            if (card != null && !card.isDead && card.cardData != null) cards.Add(card);
        }
        return cards;
    }

    /// <summary>Return true only if an active skill was actually accepted by its script.</summary>
    public static bool TryActionSkill(TurnManager tm, DefaultOwner role, BotKnowledge knowledge)
    {
        if (tm == null || tm.isGameOver || tm.isTallyingVotes) return false;
        bool ownActionTurn = tm.currentTurn ==
            (role == DefaultOwner.Player1 ? TurnState.Player1Turn : TurnState.Player2Turn);
        bool ownVoteTurn = tm.currentTurn == TurnState.SacrificePhase && tm.currentVotingPlayer == role;
        if (!ownActionTurn && !ownVoteTurn) return false;
        // Witch is explicitly legal both in Action phase and in Sacrifice phase.
        // Cur's best time to use it is often *after* its new curse has landed.

        // Witch removes infected cards rather than Curs, so a blind sweep can harm Village survival.
        // Cursermancer can evaluate a sweep using its own information.
        if (role == DefaultOwner.Player1)
        {
            int infectedVillagers = 0;
            int livingCurs = 0;
            int livingVillagers = 0;
            foreach (var card in Board(tm))
            {
                if (tm.IsCur(card)) livingCurs++;
                else if (card.cardData.owner == DefaultOwner.Player2)
                {
                    livingVillagers++;
                    if (card.isInfected) infectedVillagers++;
                }
            }
            bool goodSweep = infectedVillagers >= 2 ||
                (infectedVillagers >= 1 && livingVillagers <= livingCurs + 1);
            if (goodSweep)
            {
                foreach (var card in Board(tm))
                {
                    if (!Available(tm, card) || card.lastPlacedBy != role) continue;
                    var witch = card.cardData.cardSkill as Skill_Witch;
                    if (witch == null || card.hasUsedSkill || !tm.HasEnoughHearts(witch.skillCost)) continue;
                    if (witch.TryCastActiveSkill(card, null))
                    {
                        Debug.Log("[BotSkill][Cur] Witch sweep: affected infected villagers=" + infectedVillagers);
                        return true;
                    }
                }
            }
        }
        else if (knowledge != null)
        {
            // A Villager Witch never checks the true infection bit. Only act if
            // PUBLIC evidence strongly favors removing enemy utility over the
            // certain cost of a 5-Tim action and potential friendly casualties.
            float enemyRisk = 0f, friendlyRisk = 0f, neutralRisk = 0f;
            foreach (var card in Board(tm))
            {
                float risk = knowledge.Infection(card);
                if (card.cardData.owner == DefaultOwner.Player1) enemyRisk += risk;
                else if (card.cardData.owner == DefaultOwner.Player2) friendlyRisk += risk;
                else neutralRisk += risk;
            }
            float expected = enemyRisk * 90f - friendlyRisk * 155f - neutralRisk * 25f - 35f;
            if (enemyRisk >= 1.1f && expected > 65f)
            {
                foreach (var card in Board(tm))
                {
                    if (!Available(tm, card) || card.lastPlacedBy != role) continue;
                    var witch = card.cardData.cardSkill as Skill_Witch;
                    if (witch == null || card.hasUsedSkill || !tm.HasEnoughHearts(witch.skillCost)) continue;
                    if (witch.TryCastActiveSkill(card, null))
                    {
                        Debug.Log("[BotSkill][Villager] Witch: public evidence expected utility=" + expected);
                        return true;
                    }
                }
            }
        }
        return false;
    }

    /// <summary>Cur: infected Snooper produces an extra vote; both roles may use Gambler.</summary>
    public static void TrySacrificeSkills(TurnManager tm, DefaultOwner role, BotKnowledge knowledge)
    {
        if (tm == null || tm.isGameOver || tm.isTallyingVotes ||
            tm.currentTurn != TurnState.SacrificePhase || tm.currentVotingPlayer != role) return;
        var cards = Board(tm);
        CardInstance bestTarget = null;
        float bestScore = float.NegativeInfinity;
        foreach (var target in cards)
        {
            if (!Available(tm, target)) continue;
            float score = BotDecisionEngine.VoteTargetScore(tm, role, knowledge, target);
            // Gambler/Snooper don't have a voter-specific self-vote restriction.
            score += UnityEngine.Random.Range(0f, .001f);
            if (score > bestScore) { bestScore = score; bestTarget = target; }
        }
        if (bestTarget == null || bestScore <= -1000) return;

        if (role == DefaultOwner.Player1)
        {
            foreach (var caster in cards)
            {
                if (!Available(tm, caster) || !caster.isInfected || caster.hasUsedSkill ||
                    !(caster.cardData.cardSkill is Skill_Snooper)) continue;
                // Snooper is explicitly Cur-only when infected, even if Player2 owns the card.
                if (caster.cardData.cardSkill.TryCastActiveSkill(caster, bestTarget))
                    Debug.Log("[BotSkill][Cur] Infected Snooper marks seat " +
                        tm.GetSeatIndex(bestTarget.GetComponent<CardInteraction>()));
            }
        }

        // Use the public turn order to evaluate Gambler without reading the rival's secret bet.
        if (Skill_Gambler.playersWhoGambled.Contains(role)) return;
        foreach (var caster in cards)
        {
            if (!Available(tm, caster) || !(caster.cardData.cardSkill is Skill_Gambler gambler) ||
                !tm.HasEnoughHearts(gambler.skillCost)) continue;
            bool first = (tm.firstPlayerThisRound == TurnState.Player1Turn) == (role == DefaultOwner.Player1);
            // First voter gets +1 if the opponent abstains; second voter gets +1
            // only if the first voter gambled. No free win is assumed here.
            // Do not gamble when the desirable target is only marginally preferable.
            if (bestScore < (role == DefaultOwner.Player1 ? 35f : 15f)) break;
            if (gambler.TryCastActiveSkill(caster, bestTarget))
                Debug.Log("[BotSkill][" + role + "] Gambler bet, acting " + (first ? "first" : "second"));
            break; // Never register more than one bet per player per round.
        }
    }

    /// <summary>Invoker is legal only for the player who last placed it, during their vote step.</summary>
    public static IEnumerator TryInvoker(TurnManager tm, DefaultOwner role, BotKnowledge knowledge)
    {
        if (tm == null || tm.isGameOver || tm.isTallyingVotes ||
            tm.currentTurn != TurnState.SacrificePhase || tm.currentVotingPlayer != role ||
            tm.deadCardsData == null || tm.deadCardsData.Count == 0) yield break;

        CardInstance caster = null;
        foreach (var card in Board(tm))
        {
            if (!Available(tm, card) || card.lastPlacedBy != role || card.hasUsedSkill) continue;
            var invoker = card.cardData.cardSkill as Skill_Invoker;
            if (invoker == null || !tm.HasEnoughHearts(invoker.skillCost)) continue;
            int seat = tm.GetSeatIndex(card.GetComponent<CardInteraction>());
            if (seat >= 0 && seat < tm.allSeats.Count && tm.allSeats[seat].isNextToCemetery)
            { caster = card; break; }
        }
        if (caster == null || !caster.cardData.cardSkill.TryCastActiveSkill(caster, null)) yield break;
        yield return new WaitForSeconds(0.2f);
        List<CardData> recent = ReviveHistory(tm, role);
        CardData[] options = tm.GetOnlineDiscoverOptions();
        int chosen = -1;
        float best = float.NegativeInfinity;
        foreach (var data in options)
        {
            if (data == null) continue;
            int idx = tm.deadCardsData.IndexOf(data);
            if (idx < 0 || !tm.CanChooseOnlineDiscover(idx, role)) continue;
            float score = RevivalScore(tm, role, data, knowledge);
            // A resurrection followed by another death does not create
            // a permanent advantage. Repeated picks become less attractive.
            int repeats = 0;
            foreach (CardData earlier in recent) if (earlier == data) repeats++;
            score -= 140f * repeats;
            score += UnityEngine.Random.Range(0f, .01f);
            if (score > best) { best = score; chosen = idx; }
        }
        if (chosen >= 0 && best >= 0f)
        {
            CardData revived = tm.deadCardsData[chosen];
            tm.ResolveInvokerSelection(chosen);
            if (revived != null)
            {
                recent.Add(revived);
                if (recent.Count > 8) recent.RemoveAt(0);
            }
            Debug.Log("[BotSkill][" + role + "] Invoker revives choice index=" + chosen);
        }
        else if (tm.discoverPanel != null && tm.discoverPanel.activeSelf)
        {
            // Avoid hanging an open modal on an invalid discover result.
            tm.CloseInvokerDiscoverUI();
        }
        yield return new WaitForSeconds(0.2f);
    }

    static float RevivalScore(TurnManager tm, DefaultOwner role, CardData data, BotKnowledge knowledge)
    {
        float score = data.owner == role ? 100f :
            data.owner == DefaultOwner.Neutral ? 12f : -65f;
        if (role == DefaultOwner.Player1)
        {
            // Cur already knows its own Cur identities, including dead Curs.
            foreach (var cur in tm.allCurs)
                if (cur != null && cur.cardData == data) { score += 170f; break; }
            if (data.owner == DefaultOwner.Player2) score -= 60f;
            if (data.cardSkill is Skill_Controller) score += 28f;
            if (data.cardSkill is Skill_Saboteur) score += 22f;
        }
        else
        {
            // No IsCur/allCurs/isInfected here: evaluate only public card definition.
            if (data.cardType == CardType.Hunter || data.cardNumber == 9) score += 55f;
            if (data.cardSkill is Skill_Preacher) score += 30f;
            if (data.cardSkill is Skill_Hunter) score += 45f;
            if (data.cardSkill is Skill_Elder) score += 20f;
            if (data.cardSkill is Skill_Detective) score += 18f;
            if (data.owner == DefaultOwner.Player1) score -= 20f;
            // A dead card in ANY deck may be Cur. Do not resurrect a high-risk
            // Neutral or Player2 candidate using owner alone.
            if (knowledge != null) score -= 210f * knowledge.Cur(data);
        }
        return score;
    }
}

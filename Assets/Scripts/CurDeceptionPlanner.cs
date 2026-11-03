using System.Collections.Generic;
using UnityEngine;

/// <summary>Considers one decoy after deploying a Cur, when it creates ambiguity around a Villager.</summary>
public sealed class CurDeceptionPlanner
{
    private float intensity;
    private bool mayTryThisTurn;
    private bool alreadyTriedThisTurn;
    private int decoysPlaced;

    public void Reset(float value)
    {
        intensity = Mathf.Clamp01(value);
        mayTryThisTurn = false;
        alreadyTriedThisTurn = false;
        decoysPlaced = 0;
    }

    public void BeginActionTurn(TurnManager tm)
    {
        alreadyTriedThisTurn = false;
        mayTryThisTurn = intensity > 0f && tm != null &&
                         tm.currentTurn == TurnState.Player1Turn &&
                         decoysPlaced < 1 && Random.value < intensity;
    }

    public bool TryDecoyPlacement(TurnManager tm, out BotDecisionEngine.Placement plan)
    {
        plan = new BotDecisionEngine.Placement { seat = -1, score = float.NegativeInfinity };
        if (!mayTryThisTurn || alreadyTriedThisTurn || tm == null ||
            DeckManager.instance == null || tm.allSeats == null || decoysPlaced >= 1 ||
            tm.currentTurn != TurnState.Player1Turn) return false;
        alreadyTriedThisTurn = true;
        if (!tm.HasEnoughHearts(4)) return false;
        Transform hand = DeckManager.instance.player1Hand;
        if (hand == null) return false;

        // Do not disguise while any P1 Cur remains on hand. Get Cur into play first.
        foreach (Transform child in hand)
        {
            CardInstance c = child.GetComponent<CardInstance>();
            if (c != null && !c.isDead && tm.IsCur(c)) return false;
        }
        bool curDeployed = false;
        int freeSeats = 0;
        for (int i = 0; i < tm.allSeats.Count; ++i)
        {
            CardInstance c = tm.GetBoardCard(i);
            if (c == null) freeSeats++;
            else if (!c.isDead && tm.IsCur(c)) curDeployed = true;
        }
        if (!curDeployed || freeSeats < 3) return false;

        float best = float.NegativeInfinity;
        foreach (Transform child in hand)
        {
            CardInstance card = child.GetComponent<CardInstance>();
            CardInteraction interaction = child.GetComponent<CardInteraction>();
            if (card == null || interaction == null || card.cardData == null ||
                card.isDead || interaction.isPlaced || tm.IsCur(card) ||
                card.cardData.owner != DefaultOwner.Player1) continue;
            CardSkill skill = card.cardData.cardSkill;
            if (skill is Skill_VillageChief || skill is Skill_Sacrifier ||
                skill is Skill_GameMaster) continue;
            int cost = interaction.PlacementCost;
            if (cost < 1 || cost > 2 || !tm.HasEnoughHearts(cost + 2)) continue;

            for (int i = 0; i < tm.allSeats.Count; ++i)
            {
                var seat = tm.allSeats[i];
                if (seat == null || seat.cardPos == null ||
                    seat.cardPos.childCount != 0 || tm.GetBoardCard(i) != null)
                    continue;
                bool touchesCur = false, touchesVillager = false;
                foreach (int adjacentSeat in ImmediateNeighbors(tm, i))
                {
                    CardInstance near = tm.GetBoardCard(adjacentSeat);
                    if (near == null || near.isDead) continue;
                    if (tm.IsCur(near)) touchesCur = true;
                    if (near.cardData != null && near.cardData.owner == DefaultOwner.Player2 &&
                        !tm.IsCur(near)) touchesVillager = true;
                }
                // Tactical reason is required: make a plausible competing source
                // next to a victim near an existing Cur, not merely waste Tim.
                if (!touchesCur || !touchesVillager) continue;
                float value = 65f - 7f * cost + Random.Range(0f, 0.2f);
                if (value > best)
                {
                    best = value;
                    plan = new BotDecisionEngine.Placement {
                        card = interaction, hand = DefaultOwner.Player1,
                        seat = i, score = value
                    };
                }
            }
        }
        return plan.seat >= 0;
    }

    public void ConfirmDecoy() { decoysPlaced++; }

    public int ChooseCurseTarget(TurnManager tm, CardInstance cur, int baselineSeat)
    {
        if (tm == null || cur == null || baselineSeat < 0 || intensity <= 0)
            return baselineSeat;
        CardInstance baselineCard = tm.GetBoardCard(baselineSeat);
        if (baselineCard == null || baselineCard.cardData == null)
            return baselineSeat;
        float baselineValue = CurseTacticalValue(baselineCard);
        int baselineSources = PlausibleSources(tm, baselineSeat);
        int bestSeat = baselineSeat;
        float bestValue = baselineValue + Mathf.Min(2, baselineSources - 1) * 6f * intensity;
        int[] legal = { tm.validLeftTarget, tm.validRightTarget,
                        tm.validTopTarget, tm.validBottomTarget };
        var seen = new HashSet<int>();
        foreach (int seat in legal)
        {
            if (seat < 0 || !seen.Add(seat) || seat == baselineSeat) continue;
            CardInstance c = tm.GetBoardCard(seat);
            if (c == null || c.isDead || c.cardData == null || tm.IsCur(c)) continue;
            CardInteraction interaction = c.GetComponent<CardInteraction>();
            if (interaction == null || interaction.isProtectedByPreacher ||
                (c.isInfected && c != cur.currentVictim)) continue;
            // Never substitute a P2 victim with a P1/Neutral victim purely
            // to conceal a Cur. The strategic objective is to defeat Villager.
            if (c.cardData.owner != baselineCard.cardData.owner) continue;
            float tactical = CurseTacticalValue(c);
            if (tactical < baselineValue - 7f) continue;
            int sources = PlausibleSources(tm, seat);
            if (sources <= baselineSources) continue;
            float score = tactical + Mathf.Min(2, sources - 1) * 6f * intensity;
            if (score > bestValue)
            {
                bestValue = score;
                bestSeat = seat;
            }
        }
        return bestSeat;
    }

    private static float CurseTacticalValue(CardInstance c)
    {
        float value = c.cardData.owner == DefaultOwner.Player2 ? 110f :
                      c.cardData.owner == DefaultOwner.Player1 ? 30f : 15f;
        CardSkill skill = c.cardData.cardSkill;
        if (skill is Skill_Hunter) value += 32f;
        if (skill is Skill_Preacher) value += 27f;
        if (skill is Skill_Invoker) value += 18f;
        return value;
    }

    private static int PlausibleSources(TurnManager tm, int seat)
    {
        int total = 0;
        foreach (int neighbor in ImmediateNeighbors(tm, seat))
        {
            CardInstance c = tm.GetBoardCard(neighbor);
            if (c == null || c.isDead || c.cardData == null ||
                c.cardData.cardNumber == 9 || c.cardData.cardType == CardType.Hunter)
                continue;
            if (GameRules.RequiredCursForOwner(GameSettings.currentMode,
                    GameRules.ClampLevel(GameSettings.currentLevel),
                    c.cardData.owner) > 0) total++;
        }
        return total;
    }

    private static IEnumerable<int> ImmediateNeighbors(TurnManager tm, int seat)
    {
        if (tm.allSeats == null || seat < 0 || seat >= tm.allSeats.Count ||
            tm.allSeats[seat] == null) yield break;
        var root = tm.allSeats[seat].GetComponent<RectTransform>();
        if (root == null) yield break;
        Vector2 pos = root.anchoredPosition;
        int[] near = { -1, -1, -1, -1 };
        float[] distances = { float.MaxValue, float.MaxValue, float.MaxValue, float.MaxValue };
        for (int i = 0; i < tm.allSeats.Count; i++)
        {
            if (i == seat || tm.allSeats[i] == null) continue;
            var other = tm.allSeats[i].GetComponent<RectTransform>();
            if (other == null) continue;
            Vector2 d = other.anchoredPosition - pos;
            int direction = -1;
            float distance = 0;
            if (Mathf.Abs(d.y) < 5 && d.x < -5) { direction = 0; distance = -d.x; }
            else if (Mathf.Abs(d.y) < 5 && d.x > 5) { direction = 1; distance = d.x; }
            else if (Mathf.Abs(d.x) < 5 && d.y > 5) { direction = 2; distance = d.y; }
            else if (Mathf.Abs(d.x) < 5 && d.y < -5) { direction = 3; distance = -d.y; }
            if (direction >= 0 && distance < distances[direction])
            { near[direction] = i; distances[direction] = distance; }
        }
        foreach (int i in near) if (i >= 0) yield return i;
    }
}

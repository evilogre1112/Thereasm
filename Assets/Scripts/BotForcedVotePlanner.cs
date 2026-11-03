using UnityEngine;

// Plans board compositions that force the rival to vote on its own cards.
// No hidden role/infection reads in the Villager branch.
// A "pure" opposing board is only valuable if the bot can force a ritual,
// and it may be countered by withdrawals, skills or voluntary swaps.
public sealed class BotForcedVotePlanner
{
    public bool enabled = true;
    public int maxEmptySeatsForSetup = 2;
    public int minimumOpponentVoters = 2;
    public float finalSeatBonus = 225f;
    public float setupBonus = 135f;

    public bool TryPlaceOpponent(TurnManager tm, DefaultOwner role,
        BotKnowledge knowledge, out BotDecisionEngine.Placement selected)
    {
        selected = new BotDecisionEngine.Placement { seat = -1, score = float.NegativeInfinity };
        if (!enabled || tm == null || DeckManager.instance == null || tm.allSeats == null)
            return false;
        DefaultOwner opponent = role == DefaultOwner.Player1 ?
            DefaultOwner.Player2 : DefaultOwner.Player1;
        int placedOpponent = 0, others = 0, empty = 0;
        float risk = 0f;
        for (int i = 0; i < tm.allSeats.Count; ++i)
        {
            CardInstance c = tm.GetBoardCard(i);
            if (c == null) { ++empty; continue; }
            if (c.cardData == null || c.cardData.owner != opponent) { ++others; continue; }
            ++placedOpponent;
            if (role == DefaultOwner.Player1)
            {
                // Cur knows its own selected Curs, including those in enemy hand.
                if (tm.IsCur(c)) risk += 1f;
            }
            else if (knowledge != null) risk += Mathf.Clamp01(knowledge.Cur(c, tm));
        }
        // This is a finishing / near-finishing tactic, not a random 5-Tim opener.
        if (others > 0 || placedOpponent < minimumOpponentVoters || empty < 1 ||
            empty > Mathf.Max(1, maxEmptySeatsForSetup)) return false;
        if (role == DefaultOwner.Player1 && risk > 0f) return false;

        Transform hand = opponent == DefaultOwner.Player1 ?
            DeckManager.instance.player1Hand : DeckManager.instance.player2Hand;
        if (hand == null) return false;
        float best = float.NegativeInfinity;
        foreach (Transform child in hand)
        {
            CardInteraction interaction = child.GetComponent<CardInteraction>();
            CardInstance ci = child.GetComponent<CardInstance>();
            if (interaction == null || ci == null || ci.isDead || interaction.isPlaced ||
                ci.cardData == null || ci.cardData.owner != opponent) continue;
            int cost = interaction.PlacementCost;
            if (cost > 5 || !tm.HasEnoughHearts(cost)) continue;
            // Cur must not force a vote on a card it knows is its own Cur.
            if (role == DefaultOwner.Player1 && tm.IsCur(ci)) continue;
            float chanceCur = role == DefaultOwner.Player2 && knowledge != null ?
                Mathf.Clamp01(knowledge.Cur(ci, tm)) : 0f;
            float incentive = empty == 1 ? finalSeatBonus : setupBonus;
            // Villager should prioritize finishing an enemy-only board when
            // public evidence indicates the rival cards could contain Cur.
            incentive += role == DefaultOwner.Player2 ? 12f * chanceCur : 0f;
            // If an opponent could retrieve on its turn, an unfinished board is fragile.
            if (empty > 1) incentive -= 25f;
            if (role == DefaultOwner.Player1) incentive -= 32f;
            for (int i = 0; i < tm.allSeats.Count; ++i)
            {
                if (tm.GetBoardCard(i) != null || tm.allSeats[i] == null ||
                    tm.allSeats[i].cardPos == null ||
                    tm.allSeats[i].cardPos.childCount != 0) continue;
                if (ci.cardData.cardSkill is Skill_GameMaster &&
                    tm.allSeats[i].gameObject.name.Contains("ExtraSeat")) continue;
                float score = incentive - 7f * cost;
                if (score > best)
                {
                    best = score;
                    selected = new BotDecisionEngine.Placement
                        { card = interaction, hand = opponent, seat = i, score = score };
                }
            }
        }
        return selected.card != null && best >= 45f;
    }

    // Immediate withdrawal + replacement opportunity: the bot can free a
    // chair for a rival card, without violating the source's return mechanics.
    // Only attempt when it can also afford the 5-Tim follow-up placement.
    public bool TryRecallOwn(TurnManager tm, DefaultOwner role,
        out CardInteraction toRecall)
    {
        toRecall = null;
        if (!enabled || tm == null || DeckManager.instance == null || tm.allSeats == null)
            return false;
        DefaultOwner rival = role == DefaultOwner.Player1 ?
            DefaultOwner.Player2 : DefaultOwner.Player1;
        int selfCount = 0, enemyCount = 0, neutralCount = 0, free = 0;
        CardInteraction own = null;
        for (int i = 0; i < tm.allSeats.Count; ++i)
        {
            CardInstance c = tm.GetBoardCard(i);
            if (c == null) { ++free; continue; }
            if (c.cardData == null) return false;
            if (c.cardData.owner == rival) ++enemyCount;
            else if (c.cardData.owner == role)
            {
                ++selfCount;
                own = c.GetComponent<CardInteraction>();
            }
            else ++neutralCount;
        }
        if (selfCount != 1 || neutralCount != 0 || enemyCount < 2 || free != 0 ||
            own == null || !own.isPlaced || own.isLockedByElder || own.isLockedBySkill ||
            own.lockState < 3 || !tm.HasEnoughHearts(3)) return false;
        // Avoid recalling the only deployed Cur, which would discard offence.
        if (role == DefaultOwner.Player1 && tm.IsCur(own.GetComponent<CardInstance>())) return false;
        Transform rivalHand = rival == DefaultOwner.Player1 ?
            DeckManager.instance.player1Hand : DeckManager.instance.player2Hand;
        if (rivalHand == null) return false;
        foreach (Transform child in rivalHand)
        {
            CardInteraction interaction = child.GetComponent<CardInteraction>();
            CardInstance ci = child.GetComponent<CardInstance>();
            if (interaction == null || ci == null || ci.isDead || interaction.isPlaced ||
                ci.cardData == null || ci.cardData.owner != rival) continue;
            // Return reimburses up to 2 Tim; this is enough for a 5-Tim card.
            int refund = 2;
            if (tm.HasEnoughHearts(Mathf.Max(0, interaction.PlacementCost - refund)) &&
                !(role == DefaultOwner.Player1 && tm.IsCur(ci)))
            {
                toRecall = own;
                return true;
            }
        }
        return false;
    }
}

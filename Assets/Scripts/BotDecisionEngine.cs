using System.Collections.Generic;
using UnityEngine;

// Villager decisions use BotKnowledge estimates rather than secret Cur identities.
public static class BotDecisionEngine
{
    public static float VillagerNeutralShieldPriority = 160f;

    public struct Placement
    {
        public CardInteraction card;
        public DefaultOwner hand;
        public int seat;
        public float score;
    }

    private static DefaultOwner Other(DefaultOwner role) =>
        role == DefaultOwner.Player1 ? DefaultOwner.Player2 : DefaultOwner.Player1;

    public static bool TryFindPlacement(TurnManager tm, DefaultOwner role,
        BotKnowledge knowledge, out Placement best, bool allowProgressPlacement = false)
    {
        best = new Placement { seat = -1, score = float.NegativeInfinity };
        if (tm == null || DeckManager.instance == null) return false;
        Transform[] hands = { DeckManager.instance.player1Hand,
            DeckManager.instance.player2Hand, DeckManager.instance.neutralHand };
        DefaultOwner[] owners = { DefaultOwner.Player1, DefaultOwner.Player2, DefaultOwner.Neutral };
        int seats = tm.allSeats.Count;
        CardInstance[] board = new CardInstance[seats];
        for (int i = 0; i < seats; ++i) board[i] = tm.GetBoardCard(i);

        for (int h = 0; h < hands.Length; ++h)
        {
            if (hands[h] == null) continue;
            foreach (Transform child in hands[h])
            {
                CardInteraction interaction = child.GetComponent<CardInteraction>();
                CardInstance card = child.GetComponent<CardInstance>();
                if (interaction == null || interaction.isPlaced || card == null || card.isDead || card.cardData == null) continue;
                int cost = interaction.PlacementCost; // Totem=1, Prankster=3, normal=2/5.
                if (!tm.HasEnoughHearts(cost)) continue;
                for (int i = 0; i < seats; ++i)
                {
                    if (board[i] != null || tm.allSeats[i].cardPos.childCount != 0) continue;
                    if (card.cardData.cardSkill is Skill_GameMaster &&
                        tm.allSeats[i].gameObject.name.Contains("ExtraSeat")) continue;
                    float score = PlacementScore(tm, role, knowledge, board, card, i, cost);
                    // Small seeded noise breaks ties without outweighing tactical scores.
                    score += Random.Range(0f, 0.35f);
                    if (score > best.score)
                        best = new Placement { card = interaction, hand = owners[h], seat = i, score = score };
                }
            }
        }
        // Fall back to the best legal placement when no stronger skill or swap exists.
        // This avoids passing into a ritual while playable cards remain.
        return best.card != null && (allowProgressPlacement || best.score >= 20f);
    }

    // Position-based neighbor lookup; never assumes 7, 9 or 10 fixed indices.
    // This reads only public seat coordinates and is safe for Villager beliefs.
    private static int VerticalNeighbor(TurnManager tm, int seat, int direction)
    {
        if (tm == null || tm.allSeats == null || seat < 0 ||
            seat >= tm.allSeats.Count || tm.allSeats[seat] == null) return -1;
        RectTransform originRt = tm.allSeats[seat].GetComponent<RectTransform>();
        if (originRt == null) return -1;
        Vector2 origin = originRt.anchoredPosition;
        float distance = float.MaxValue;
        int best = -1;
        for (int i = 0; i < tm.allSeats.Count; ++i)
        {
            if (i == seat || tm.allSeats[i] == null) continue;
            RectTransform other = tm.allSeats[i].GetComponent<RectTransform>();
            if (other == null) continue;
            Vector2 delta = other.anchoredPosition - origin;
            float along = delta.y * direction;
            if (Mathf.Abs(delta.x) < 5f && along > 5f && along < distance)
            { distance = along; best = i; }
        }
        return best;
    }

    internal static float PlacementScore(TurnManager tm, DefaultOwner role,
        BotKnowledge knowledge, CardInstance[] board, CardInstance card, int seat, int cost)
    {
        bool mine = card.cardData.owner == role;
        float score = mine ? 90f : card.cardData.owner == DefaultOwner.Neutral ? -20f : -65f;
        score -= 7f * cost;
        if (mine && cost == 2) score += 10f;
        // Reserve scarce Player2 villagers as voters; Neutral cards can serve as decoys.
        // At level 3, a Neutral may also be a Cur.
        // This decision uses a public fixed-count posterior, never IsCur().
        if (role == DefaultOwner.Player2 && knowledge != null &&
            card.cardData.owner == DefaultOwner.Neutral)
        {
            float pCurNeutral = knowledge.Cur(card, tm);
            score += VillagerNeutralShieldPriority * (1f - .75f * pCurNeutral);
        }

        // A face-down card from another hand may conceal its identity.
        CardSkill skill = role == DefaultOwner.Player2 &&
            card.cardData.owner != role && card.isFaceDown ? null : card.cardData.cardSkill;
        if (skill is Skill_Invoker)
        {
            score += tm.allSeats[seat].isNextToCemetery ?
                (tm.deadCardsData.Count > 0 ? 110 : 25) : -170;
        }
        if (skill is Skill_Hunter && role == DefaultOwner.Player2 && mine) score += 24;
        // Passive skills activate from the board, not through CastActiveSkill.
        // Sacrifier takes priority over every vote: avoid sacrificing a valuable
        // Villager, but Cur can deploy a non-Cur Sacrifier as a defensive decoy.
        if (skill is Skill_Sacrifier)
        {
            if (role == DefaultOwner.Player2) score += mine ? -105 : 18;
            else score += tm.IsCur(card) ? -220 :
                (card.cardData.owner == DefaultOwner.Player2 ? 78 : 22);
        }
        if (skill is Skill_VillageChief)
        {
            // VillageChief forces an early ritual upon observing another normal card.
            // Cur wants this only when it can already curse from a deployed Cur.
            bool curReady = false;
            if (role == DefaultOwner.Player1)
                foreach (var placed in board)
                    if (placed != null && tm.IsCur(placed)) { curReady = true; break; }
            score += role == DefaultOwner.Player1 && curReady ? 20 : -70;
        }
        if (skill is Skill_Controller) score += role == DefaultOwner.Player1 ? 18 : -14;
        if (skill is Skill_Gambler) score += 11;
        if (skill is Skill_Snooper) score += role == DefaultOwner.Player2 ? 17 : 7;
        if (skill is Skill_Trader) score -= 8; // Unknown incoming votes can be redistributed.
        if (skill is Skill_Witch)
            score += role == DefaultOwner.Player1 ? 18 : -18;
        if (skill is Skill_GameMaster) score += role == DefaultOwner.Player1 ? 12 : 6;
        // Deliberately create a future Check-based information trap: a board
        // with exactly one legal infection source is more informative.
        if (role == DefaultOwner.Player2 && knowledge != null)
            score += knowledge.TrapPlacementValue(tm, board, card, seat);

        if (skill is Skill_Blind blind && mine)
            score += blind.mustSitNearCemetery ?
                (tm.allSeats[seat].isNextToCemetery ? 32 : -85) : 12;

        int left = tm.HorizontalNeighbor(seat, -1);
        int right = tm.HorizontalNeighbor(seat, 1);
        // Villager evaluation includes GameMaster seats above and below the main row.
        List<int> neighbors = new List<int>(4);
        if (left >= 0) neighbors.Add(left);
        if (right >= 0) neighbors.Add(right);
        if (role == DefaultOwner.Player2)
        {
            int above = VerticalNeighbor(tm, seat, 1);
            int below = VerticalNeighbor(tm, seat, -1);
            if (above >= 0 && !neighbors.Contains(above)) neighbors.Add(above);
            if (below >= 0 && !neighbors.Contains(below)) neighbors.Add(below);
        }
        foreach (int index in neighbors)
        {
            if (index < 0 || index >= board.Length || board[index] == null) continue;
            CardInstance neighbor = board[index];
            bool friend = neighbor.cardData.owner == role;
            bool rival = neighbor.cardData.owner == Other(role);
            CardSkill neighborSkill = role == DefaultOwner.Player2 &&
                neighbor.cardData.owner != role && neighbor.isFaceDown ? null : neighbor.cardData.cardSkill;
            bool important = neighborSkill is Skill_Preacher ||
                neighborSkill is Skill_Hunter ||
                neighborSkill is Skill_Invoker ||
                neighborSkill is Skill_Elder;
            float impact = important ? 28 : 13;

            if (skill is Skill_Saboteur) score += rival ? impact * 1.6f : friend ? -impact * 1.9f : 0;
            if (skill is Skill_Preacher) score += friend ? impact * 1.3f : rival ? -impact : 0;
            if (skill is Skill_Elder) score += rival ? impact * .7f : friend ? impact * .2f : 0;
            if (skill is Skill_Detective && role == DefaultOwner.Player2)
                score += rival ? 30 : friend ? -10 : 0;

            if (neighborSkill is Skill_Saboteur && !neighbor.isSilenced)
                score -= friend ? 12 : 30;
            if (neighborSkill is Skill_Preacher && !neighbor.isSilenced)
                score += friend ? 18 : -20;
        }
        if (skill is Skill_Detective && (left < 0 || right < 0 ||
            board[left] == null || board[right] == null)) score -= 35;

        if (role == DefaultOwner.Player1)
        {
            // Cursermancer knows the cards it selected as Curs.
            bool isCur = tm.IsCur(card);
            if (isCur)
            {
                int deployed = 0;
                foreach (CardInstance c in board) if (c != null && tm.IsCur(c)) ++deployed;
                score += deployed == 0 ? 145 : -25;
                if (card.bonusSuspicion > 0) score -= card.bonusSuspicion * 25;
                foreach (int index in neighbors)
                {
                    if (index < 0 || board[index] == null) continue;
                    CardInstance target = board[index];
                    if (!tm.IsCur(target) && !target.isInfected)
                        score += target.cardData.owner == DefaultOwner.Player2 ? 56 : 18;
                    var ci = target.GetComponent<CardInteraction>();
                    if (ci != null && ci.isProtectedByPreacher) score -= 38;
                }
            }
            else if (card.cardData.owner == DefaultOwner.Player2 && !mine)
                score -= 30;
        }
        else if (role == DefaultOwner.Player2 && knowledge != null)
        {
            // Never read IsCur or isInfected for this role.
            float p = knowledge.Cur(card, tm);
            score -= p * 105;
            // Avoid offering an unprotected P2 villager directly beside a
            // suspected Cur. Never inspect secret infection to do this.
            if (card.cardData.owner == DefaultOwner.Player2)
            {
                float exposure = 0f;
                foreach (int index in neighbors)
                {
                    if (index < 0 || board[index] == null) continue;
                    CardInteraction adjacent = board[index].GetComponent<CardInteraction>();
                    if (adjacent != null && adjacent.isProtectedByPreacher) continue;
                    exposure += knowledge.Cur(board[index], tm);
                }
                score -= 12f * exposure;
            }
            // Protect a valuable neighbor only to the degree it is believed innocent.
            if (skill is Skill_Preacher)
                foreach (int neighborIndex in neighbors)
                {
                    if (neighborIndex < 0 || board[neighborIndex] == null) continue;
                    float innocency = 1f - knowledge.Cur(board[neighborIndex], tm);
                    if (board[neighborIndex].cardData.owner == DefaultOwner.Player2)
                        score += 26f * innocency - 25f * (1f - innocency);
                }
            if (mine && skill is Skill_Elder) score += 14;
            if (skill is Skill_Detective && left >= 0 && right >= 0 &&
                board[left] != null && board[right] != null)
            {
                // A Detective only chooses the more suspicious of two neighbors.
                // A public Detective mark is evidence, not an identity reveal.
                float a = knowledge.Cur(board[left], tm);
                float b = knowledge.Cur(board[right], tm);
                float oneCur = a * (1f - b) + b * (1f - a);
                score += 14f * (a + b) + 24f * oneCur;
            }
            if (skill is Skill_Snooper)
            {
                // A surviving, uninfected Snooper marks a Cur during tally;
                // Preacher protection improves the chance of a truthful mark.
                foreach (int neighborIndex in neighbors)
                {
                    if (neighborIndex < 0 || board[neighborIndex] == null) continue;
                    var neighbor = board[neighborIndex];
                    if (!neighbor.isSilenced && !neighbor.isFaceDown &&
                        neighbor.cardData.cardSkill is Skill_Preacher)
                        score += 19f;
                }
            }
        }
        return score;
    }

    public static int ChooseCurseTarget(TurnManager tm, CardInstance source)
    {
        if (tm == null || source == null) return -1;
        int[] candidates = { tm.validLeftTarget, tm.validRightTarget,
            tm.validTopTarget, tm.validBottomTarget };
        int best = -1;
        float scoreBest = float.NegativeInfinity;
        foreach (int i in candidates)
        {
            if (i < 0) continue;
            CardInstance target = tm.GetBoardCard(i);
            if (target == null || tm.IsCur(target)) continue;
            CardInteraction interaction = target.GetComponent<CardInteraction>();
            if (interaction == null || interaction.isProtectedByPreacher) continue;
            if (target.isInfected && target != source.currentVictim) continue;
            // Changing a previously useful target uninfects it; switching has a cost.
            float score = target.cardData.owner == DefaultOwner.Player2 ? 110 :
                target.cardData.owner == DefaultOwner.Player1 ? 30 : 15;
            if (target.cardData.cardSkill is Skill_Hunter) score += 32;
            if (target.cardData.cardSkill is Skill_Preacher) score += 27;
            if (target.cardData.cardSkill is Skill_Invoker) score += 18;
            score += target.bonusSuspicion * 2;
            if (target == source.currentVictim) score += 30;
            if (source.currentVictim != null && source.currentVictim != target &&
                !source.currentVictim.isDead && source.currentVictim.isInfected) score -= 35;
            if (score > scoreBest) { scoreBest = score; best = i; }
        }
        // Do not re-curse an already infected victim merely to consume an action.
        if (best >= 0 && tm.GetBoardCard(best) == source.currentVictim &&
            source.currentVictim.isInfected) return -1;
        return best;
    }

    public static float VoteTargetScore(TurnManager tm, DefaultOwner role,
        BotKnowledge knowledge, CardInstance target)
    {
        if (target == null || target.cardData == null) return -10000;
        bool isOpponent = target.cardData.owner == Other(role);
        if (role == DefaultOwner.Player1)
        {
            if (tm.IsCur(target)) return -10000; // Preserve Curs in every deck.
            float value = target.cardData.owner == DefaultOwner.Player2 ? 125 :
                isOpponent ? 90 : target.cardData.owner == DefaultOwner.Neutral ? 5 : -45;
            if (target.cardData.cardSkill is Skill_Hunter) value += 35;
            if (target.cardData.cardSkill is Skill_Preacher) value += 20;
            return value;
        }
        float pCur = knowledge != null ? knowledge.Cur(target, tm) : 0f;
        // At level 3, Curs may belong to either faction or the Neutral deck.
        // Use the fixed-count posterior rather than treating the bot's own deck as safe.
        // Killing a real Player2 villager reduces the win-condition village count;
        // it remains more costly than eliminating an innocent Player1/Neutral.
        float innocentLoss = target.cardData.owner == DefaultOwner.Player2 ? 125f :
            target.cardData.owner == DefaultOwner.Neutral ? 34f : 28f;
        // Late-game tempo: it matters if the living Cur may be in our own deck.
        float pressure = knowledge != null ?
            knowledge.ExpectedLivingCur(tm, target.cardData.owner) : 0f;
        float valueP2 = pCur * (210f + 18f * pressure) - (1f - pCur) * innocentLoss;
        if (target.cardData.owner == DefaultOwner.Player2 && pCur < .20f)
            valueP2 -= 8f; // don't sacrifice villagers solely to explore
        if (!target.isFaceDown && target.cardData.cardSkill is Skill_Invoker && isOpponent)
            valueP2 += 8f;
        return valueP2;
    }

    public static int ChooseVoteTarget(TurnManager tm, DefaultOwner role,
        BotKnowledge knowledge, CardInstance voter, int[] plannedVotes, bool controllerActive)
    {
        // Randomize equal scores to avoid a persistent bias toward the lowest seat index.
        const float tieTolerance = 0.0001f;
        float scoreBest = float.NegativeInfinity;
        List<int> tiedBest = new List<int>();

#if UNITY_EDITOR
        // Debug only; does not read IsCur/allCurs or hidden infection state.
        bool firstVote = true;
        foreach (int count in plannedVotes) if (count > 0) { firstVote = false; break; }
        // Never write a bot's private vote decision into the visible Editor Console.
        System.Text.StringBuilder debug = firstVote && role == DefaultOwner.Player2 &&
            tm.CanLocalViewerSeeCurrentVotes && tm.LocalBallotViewer == role
            ? new System.Text.StringBuilder("[VillagerVote] ") : null;
#endif
        for (int i = 0; i < tm.allSeats.Count; ++i)
        {
            CardInstance target = tm.GetBoardCard(i);
            if (target == null || target.isDead || target.cardData == null || voter == target) continue;
            float score = VoteTargetScore(tm, role, knowledge, target);
            if (controllerActive)
            {
                // Controller executes every candidate with the MINIMUM positive vote count.
                // Prefer separate good victims; never scatter onto our own Curs.
                score -= plannedVotes[i] * 100f;
            }
            else score += plannedVotes[i] * 32f; // Ordinary vote: concentrate fire.

#if UNITY_EDITOR
            if (debug != null)
                debug.Append($"seat={i}, owner={target.cardData.owner}, " +
                    $"P(Cur)={(knowledge != null ? knowledge.Cur(target, tm) : 0f):F3}, " +
                    $"score={score:F2}; ");
#endif
            if (score > scoreBest + tieTolerance)
            {
                scoreBest = score;
                tiedBest.Clear();
                tiedBest.Add(i);
            }
            else if (Mathf.Abs(score - scoreBest) <= tieTolerance)
            {
                tiedBest.Add(i);
            }
        }
        // Keep the highest-scoring opening vote; randomize ties.
        int best = tiedBest.Count > 0
            ? tiedBest[Random.Range(0, tiedBest.Count)]
            : -1;
#if UNITY_EDITOR
        if (debug != null)
            Debug.Log(debug.Append($"tiedBest={tiedBest.Count}, chosenSeat={best}").ToString());
#endif
        return best;
    }

    public static bool IsControllerActive(TurnManager tm)
    {
        bool hasSacrifier = false, hasController = false;
        foreach (var seat in tm.allSeats)
        {
            if (seat == null || seat.cardPos == null) continue;
            CardInstance card = seat.cardPos.GetComponentInChildren<CardInstance>();
            if (card == null || card.isDead || card.isSilenced || card.isFaceDown || card.cardData == null) continue;
            if (card.cardData.cardSkill is Skill_Sacrifier) hasSacrifier = true;
            if (card.cardData.cardSkill is Skill_Controller) hasController = true;
        }
        return hasController && !hasSacrifier;
    }

    public static bool TryFindTacticalSwap(TurnManager tm, DefaultOwner role,
        BotKnowledge knowledge, out CardInteraction first, out CardInteraction second, out int secondSeat)
    {
        first = null; second = null; secondSeat = -1;
        int n = tm.allSeats.Count;
        CardInstance[] board = new CardInstance[n];
        for (int i = 0; i < n; ++i) board[i] = tm.GetBoardCard(i);
        float bestImprovement = 30f;
        for (int i = 0; i < n; ++i)
        {
            CardInteraction a = board[i] != null ? board[i].GetComponent<CardInteraction>() : null;
            if (a == null || a.lockState != 3 || a.isLockedByElder || a.isLockedBySkill) continue;
            for (int j = i + 1; j < n; ++j)
            {
                CardInteraction b = board[j] != null ? board[j].GetComponent<CardInteraction>() : null;
                if (b == null || b.lockState != 3 || b.isLockedByElder || b.isLockedBySkill ||
                    a.lastSwappedCard == b || b.lastSwappedCard == a) continue;
                if ((board[i].cardData.cardSkill is Skill_GameMaster &&
                     tm.allSeats[j].gameObject.name.Contains("ExtraSeat")) ||
                    (board[j].cardData.cardSkill is Skill_GameMaster &&
                     tm.allSeats[i].gameObject.name.Contains("ExtraSeat"))) continue;
                int enemyCount = (board[i].cardData.owner != role && board[i].cardData.owner != DefaultOwner.Neutral ? 1 : 0) +
                    (board[j].cardData.owner != role && board[j].cardData.owner != DefaultOwner.Neutral ? 1 : 0);
                int cost = enemyCount == 0 ? 2 : enemyCount == 1 ? 3 : 5;
                if (!tm.HasEnoughHearts(cost)) continue;
                float before = PlacementScore(tm, role, knowledge, board, board[i], i, 0) +
                               PlacementScore(tm, role, knowledge, board, board[j], j, 0);
                CardInstance temp = board[i]; board[i] = board[j]; board[j] = temp;
                float after = PlacementScore(tm, role, knowledge, board, board[i], i, 0) +
                              PlacementScore(tm, role, knowledge, board, board[j], j, 0);
                temp = board[i]; board[i] = board[j]; board[j] = temp;
                float improvement = after - before - 8f * cost;
                if (improvement > bestImprovement)
                {
                    bestImprovement = improvement; first = a; second = b; secondSeat = j;
                }
            }
        }
        return first != null;
    }

    public static CardInstance ChooseHunterTarget(TurnManager tm, BotKnowledge knowledge,
        CardInstance hunter)
    {
        CardInstance best = null;
        // Hunter costs five hearts, so require a positive expected return from public evidence.
        float bestScore = 0f;
        foreach (var seat in tm.allSeats)
        {
            if (seat == null || seat.cardPos == null) continue;
            CardInstance target = seat.cardPos.GetComponentInChildren<CardInstance>();
            if (target == null || target.isDead || target == hunter || target.cardData == null) continue;
            CardInteraction interaction = target.GetComponent<CardInteraction>();
            if (interaction == null || !interaction.isPlaced || interaction.isProtectedByPreacher) continue;
            float pCur = knowledge != null ? knowledge.Cur(target, tm) : 0;
            float innocentLoss = target.cardData.owner == DefaultOwner.Player2 ? 140f :
                target.cardData.owner == DefaultOwner.Neutral ? 80f : 80f;
            // Higher reward for confirmed-looking Curs, still penalize friendly fire.
            // An initial ~1/8 Cur prior alone does not justify shooting.
            float payoff = pCur * 270f - (1f - pCur) * innocentLoss - 8f;
            // A cur in the Villager deck is a legal target on Level >= 2.
            // Never reject a target solely because it belongs to Player2.
            if (payoff > bestScore) { best = target; bestScore = payoff; }
        }
        return best; // Null means save 5 hearts instead of shooting a likely innocent.
    }
}

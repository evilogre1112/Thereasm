using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Bounded placement search with a three-ply fallback.
/// It scores follow-up actions and public counter-placements without mutating live cards.
/// Villager identity estimates come from BotKnowledge.
/// </summary>
public sealed class BotStrategicPlanner
{
    // Future plies model occupancy rather than complete turns or hidden hands.
    public bool enableFivePlySearch = true;
    public int planningDepth = 3;
    public int planningBeam = 4;
    public int planningNodeBudget = 950;
    private readonly BotFivePlyPlanner deep = new BotFivePlyPlanner();
    public int LastNodesExpanded => deep.NodesExpanded;
    public int beamWidth = 6;
    public float nextActionWeight = 0.16f;
    public float boardWeight = 0.85f;
    public float counterWeight = 0.65f;

    private struct Candidate
    {
        public BotDecisionEngine.Placement move;
        public int cost;
        public float immediate;
    }

    private CardInstance intendedCard;
    private int intendedSeat = -1;

    public void Reset()
    {
        intendedCard = null;
        intendedSeat = -1;
    }

    public bool TryFindPlacement(TurnManager tm, DefaultOwner role,
        BotKnowledge knowledge, out BotDecisionEngine.Placement choice,
        bool allowProgressPlacement = false)
    {
        choice = new BotDecisionEngine.Placement { seat = -1, score = float.NegativeInfinity };
        if (tm == null || tm.allSeats == null || DeckManager.instance == null) return false;

        CardInstance[] board = Snapshot(tm);
        if (enableFivePlySearch && planningDepth >= 3)
        {
            BotDecisionEngine.Placement predicted;
            if (deep.TryChoose(tm, role, knowledge, board,
                Mathf.Clamp(planningDepth, 2, 3), Mathf.Clamp(planningBeam, 2, 8),
                Mathf.Clamp(planningNodeBudget, 200, 1600),
                allowProgressPlacement, out predicted))
            {
                // Rival occupancy is only a hypothesis, not a true future turn.
                BotDecisionEngine.Placement immediateBaseline;
                if (BotDecisionEngine.TryFindPlacement(tm, role, knowledge,
                    out immediateBaseline, allowProgressPlacement) &&
                    immediateBaseline.card != null && predicted.card != null)
                {
                    CardInstance a = predicted.card.GetComponent<CardInstance>();
                    CardInstance b = immediateBaseline.card.GetComponent<CardInstance>();
                    int priceA = predicted.card.PlacementCost;
                    int priceB = immediateBaseline.card.PlacementCost;
                    if (a != null && b != null &&
                        BotDecisionEngine.PlacementScore(tm, role, knowledge, board,
                            b, immediateBaseline.seat, priceB) >
                        BotDecisionEngine.PlacementScore(tm, role, knowledge, board,
                            a, predicted.seat, priceA) + 9f)
                        predicted = immediateBaseline;
                }
                choice = predicted;
                // Use comparable scores when CurDeceptionPlanner considers substituting a decoy.
                CardInstance prediction = predicted.card != null ?
                    predicted.card.GetComponent<CardInstance>() : null;
                if (prediction != null)
                {
                    Candidate scored = new Candidate { move = predicted,
                        cost = predicted.card.PlacementCost,
                        immediate = BotDecisionEngine.PlacementScore(tm, role,
                            knowledge, board, prediction, predicted.seat,
                            predicted.card.PlacementCost) };
                    CardInstance predictedNextCard;
                    int predictedNextSeat;
                    choice.score = EvaluateCandidate(tm, role, knowledge, board,
                        scored, out predictedNextCard, out predictedNextSeat);
                }
                return true;
            }
        }
        List<Candidate> candidates = BuildCandidates(tm, role, knowledge, board);
        if (candidates.Count == 0) return false;
        candidates.Sort((a, b) => b.immediate.CompareTo(a.immediate));

        // Primary moves require tactical value; emergency placement only requires legality.
        if (!allowProgressPlacement && candidates[0].immediate < 20f) return false;

        int count = Mathf.Min(Mathf.Clamp(beamWidth, 1, 12), candidates.Count);
        float bestScore = float.NegativeInfinity;
        Candidate selected = candidates[0];
        CardInstance nextCard = null;
        int nextSeat = -1;
        for (int i = 0; i < count; ++i)
        {
            CardInstance continuation;
            int continuationSeat;
            float score = EvaluateCandidate(tm, role, knowledge, board,
                candidates[i], out continuation, out continuationSeat);
            if (score > bestScore)
            {
                bestScore = score;
                selected = candidates[i];
                nextCard = continuation;
                nextSeat = continuationSeat;
            }
        }
        // Do not allow search to turn a rational low-variance placement into
        // a huge loss just to optimize an unreliable opponent model.
        if (selected.immediate < candidates[0].immediate - 27f)
        {
            selected = candidates[0];
            nextCard = null;
            nextSeat = -1;
            bestScore = selected.immediate;
        }
        choice = selected.move;
        choice.score = bestScore;
        intendedCard = nextCard;
        intendedSeat = nextSeat;
        return true;
    }

    public float EvaluateAlternative(TurnManager tm, DefaultOwner role,
        BotKnowledge knowledge, BotDecisionEngine.Placement move)
    {
        if (tm == null || move.card == null || move.seat < 0 ||
            move.seat >= tm.allSeats.Count) return float.NegativeInfinity;
        CardInstance[] board = Snapshot(tm);
        CardInstance c = move.card.GetComponent<CardInstance>();
        if (c == null || c.cardData == null || board[move.seat] != null) return float.NegativeInfinity;
        int cost = move.card.PlacementCost;
        if (cost > Hearts(tm, role)) return float.NegativeInfinity;
        Candidate candidate = new Candidate { move = move, cost = cost,
            immediate = BotDecisionEngine.PlacementScore(tm, role,
                knowledge, board, c, move.seat, cost) };
        CardInstance unused;
        int unusedSeat;
        return EvaluateCandidate(tm, role, knowledge, board,
            candidate, out unused, out unusedSeat);
    }

    public void OnMoveExecuted(BotDecisionEngine.Placement move)
    {
        // The saved continuation remains only if this was the move selected.
        // Next turn re-evaluates legality against the actual board.
        if (move.card == null) Reset();
    }

    private static CardInstance[] Snapshot(TurnManager tm)
    {
        var board = new CardInstance[tm.allSeats.Count];
        for (int i = 0; i < board.Length; ++i) board[i] = tm.GetBoardCard(i);
        return board;
    }

    private static int Hearts(TurnManager tm, DefaultOwner role)
    {
        return role == DefaultOwner.Player1 ? tm.p1Hearts : tm.p2Hearts;
    }

    private static Transform Hand(DefaultOwner owner)
    {
        DeckManager deck = DeckManager.instance;
        if (deck == null) return null;
        return owner == DefaultOwner.Player1 ? deck.player1Hand :
            owner == DefaultOwner.Player2 ? deck.player2Hand : deck.neutralHand;
    }

    private static bool LegalSeat(TurnManager tm, CardInstance[] board,
        CardInstance card, int seat)
    {
        if (seat < 0 || seat >= board.Length || board[seat] != null ||
            tm.allSeats[seat] == null || tm.allSeats[seat].cardPos == null ||
            tm.allSeats[seat].cardPos.childCount != 0) return false;
        if (card.cardData.cardSkill is Skill_GameMaster &&
            tm.allSeats[seat].gameObject.name.Contains("ExtraSeat")) return false;
        return true;
    }

    private List<Candidate> BuildCandidates(TurnManager tm, DefaultOwner role,
        BotKnowledge knowledge, CardInstance[] board)
    {
        List<Candidate> result = new List<Candidate>();
        DefaultOwner[] owners = { DefaultOwner.Player1, DefaultOwner.Player2,
            DefaultOwner.Neutral };
        foreach (DefaultOwner owner in owners)
        {
            // Never inspect the rival's private hand in the fallback path.
            // The AI may still interact with cards already public on the board.
            if (owner != role && owner != DefaultOwner.Neutral) continue;
            Transform hand = Hand(owner);
            if (hand == null) continue;
            foreach (Transform child in hand)
            {
                CardInstance card = child.GetComponent<CardInstance>();
                CardInteraction ci = child.GetComponent<CardInteraction>();
                if (card == null || ci == null || card.cardData == null ||
                    card.isDead || ci.isPlaced || ci.PlacementCost > Hearts(tm, role)) continue;
                for (int seat = 0; seat < board.Length; ++seat)
                {
                    if (!LegalSeat(tm, board, card, seat)) continue;
                    float immediate = BotDecisionEngine.PlacementScore(tm, role,
                        knowledge, board, card, seat, ci.PlacementCost);
                    // Preserve tie diversification, but never let randomness
                    // override a materially better position.
                    immediate += Random.Range(0f, 0.2f);
                    result.Add(new Candidate {
                        move = new BotDecisionEngine.Placement {
                            card = ci, hand = owner, seat = seat, score = immediate },
                        cost = ci.PlacementCost, immediate = immediate });
                }
            }
        }
        return result;
    }

    private float EvaluateCandidate(TurnManager tm, DefaultOwner role,
        BotKnowledge knowledge, CardInstance[] board, Candidate candidate,
        out CardInstance continuationCard, out int continuationSeat)
    {
        continuationCard = null;
        continuationSeat = -1;
        CardInstance card = candidate.move.card.GetComponent<CardInstance>();
        board[candidate.move.seat] = card;
        int heartsLeft = Hearts(tm, role) - candidate.cost;

        float position = BoardValue(tm, role, knowledge, board);
        float opponent = CounterplayRisk(tm, role, knowledge, board);
        float next = BestNextMove(tm, role, knowledge, board,
            card, heartsLeft, out continuationCard, out continuationSeat);
        board[candidate.move.seat] = null;

        float memory = card == intendedCard && candidate.move.seat == intendedSeat ? 5f : 0f;
        // The next move may not become available if the opposing player
        // interrupts the phase. Its weight is deliberately capped.
        return candidate.immediate + boardWeight * position -
            counterWeight * opponent + nextActionWeight * next + memory;
    }

    private float BestNextMove(TurnManager tm, DefaultOwner role,
        BotKnowledge knowledge, CardInstance[] board, CardInstance justPlaced,
        int remainingHearts, out CardInstance bestCard, out int bestSeat)
    {
        bestCard = null;
        bestSeat = -1;
        if (remainingHearts <= 0) return 0f;
        float best = 0f;
        foreach (DefaultOwner owner in new[] { role, DefaultOwner.Neutral })
        {
            Transform hand = Hand(owner);
            if (hand == null) continue;
            foreach (Transform child in hand)
            {
                CardInteraction ci = child.GetComponent<CardInteraction>();
                CardInstance card = child.GetComponent<CardInstance>();
                if (ci == null || card == null || card == justPlaced ||
                    card.isDead || ci.isPlaced || card.cardData == null ||
                    ci.PlacementCost > remainingHearts) continue;
                for (int seat = 0; seat < board.Length; ++seat)
                {
                    if (board[seat] != null || !LegalSeat(tm, board, card, seat)) continue;
                    float raw = BotDecisionEngine.PlacementScore(tm, role,
                        knowledge, board, card, seat, ci.PlacementCost);
                    // Only real extra value above a fallback placement counts.
                    float marginal = Mathf.Max(0, raw - 65f);
                    if (marginal > best)
                    {
                        best = marginal;
                        bestCard = card;
                        bestSeat = seat;
                    }
                }
            }
        }
        return Mathf.Min(best, 120f);
    }

    private static float BoardValue(TurnManager tm, DefaultOwner role,
        BotKnowledge knowledge, CardInstance[] board)
    {
        float value = 0f;
        for (int seat = 0; seat < board.Length; ++seat)
        {
            CardInstance c = board[seat];
            if (c == null || c.isDead || c.cardData == null) continue;
            if (role == DefaultOwner.Player2)
            {
                // Only public ownership and probabilistic identity; never a
                // Cur identity bit or infection flag in this branch.
                if (c.cardData.owner != DefaultOwner.Player2) continue;
                float innocent = knowledge == null ? 0.85f :
                    1f - knowledge.Cur(c, tm);
                float exposure = 0f;
                foreach (int neighbor in Neighbors(tm, seat))
                {
                    CardInstance other = board[neighbor];
                    if (other == null || other.isDead) continue;
                    float p = knowledge == null ? 0.12f : knowledge.Cur(other, tm);
                    exposure += p;
                }
                CardInteraction ci = c.GetComponent<CardInteraction>();
                if (ci != null && ci.isProtectedByPreacher) exposure *= .22f;
                value -= Mathf.Min(2f, exposure) * innocent * 22f;
                // Information skill value is meaningful only if the card can
                // eventually observe and survive, not just if it is deployed.
                if (!c.isSilenced && c.cardData.cardSkill is Skill_Detective)
                    value += 5f;
            }
            else
            {
                // Cur is allowed to know its own nominees. Knowledge in this
                // branch is never used by the Villager branch above.
                if (!tm.IsCur(c)) continue;
                value -= Mathf.Min(3, c.bonusSuspicion) * 6f;
                foreach (int neighbor in Neighbors(tm, seat))
                {
                    CardInstance other = board[neighbor];
                    if (other == null || other.isDead || tm.IsCur(other)) continue;
                    if (other.cardData.owner != DefaultOwner.Player2) continue;
                    CardInteraction ci = other.GetComponent<CardInteraction>();
                    bool shielded = ci != null && ci.isProtectedByPreacher;
                    value += shielded ? 2f : 14f;
                    if (other.cardData.cardSkill is Skill_Hunter) value += shielded ? 0 : 5f;
                }
            }
        }
        return Mathf.Clamp(value, -80f, 80f);
    }

    // Public, plausible next responses; never peeks at another player's hand.
    // The penalty measures how easily an opponent could alter this position.
    private static float CounterplayRisk(TurnManager tm, DefaultOwner role,
        BotKnowledge knowledge, CardInstance[] board)
    {
        float worst = 0f;
        for (int seat = 0; seat < board.Length; ++seat)
        {
            if (board[seat] != null) continue;
            float risk = 0f;
            foreach (int neighbor in Neighbors(tm, seat))
            {
                CardInstance c = board[neighbor];
                if (c == null || c.cardData == null) continue;
                if (role == DefaultOwner.Player2)
                {
                    if (c.cardData.owner == DefaultOwner.Player2)
                    {
                        float innocent = knowledge == null ? .85f :
                            1f - knowledge.Cur(c, tm);
                        risk += 11f * innocent;
                    }
                }
                else
                {
                    if (tm.IsCur(c)) risk += 9f;
                    if (c.cardData.owner == DefaultOwner.Player2 &&
                        c.cardData.cardSkill is Skill_Preacher) risk += 6f;
                }
            }
            // Count only the strongest hypothetical counter-placement,
            // rather than pretending the rival can play all of them.
            if (risk > worst) worst = risk;
        }
        return Mathf.Min(worst, 40f);
    }

    private static IEnumerable<int> Neighbors(TurnManager tm, int seat)
    {
        if (seat < 0 || seat >= tm.allSeats.Count || tm.allSeats[seat] == null)
            yield break;
        RectTransform rt = tm.allSeats[seat].GetComponent<RectTransform>();
        if (rt == null) yield break;
        Vector2 pos = rt.anchoredPosition;
        int[] best = { -1, -1, -1, -1 };
        float[] distance = { float.MaxValue, float.MaxValue,
            float.MaxValue, float.MaxValue };
        for (int i = 0; i < tm.allSeats.Count; ++i)
        {
            if (i == seat || tm.allSeats[i] == null) continue;
            RectTransform other = tm.allSeats[i].GetComponent<RectTransform>();
            if (other == null) continue;
            Vector2 d = other.anchoredPosition - pos;
            int direction = -1;
            float length = 0f;
            if (Mathf.Abs(d.y) < 5f && d.x < -5f)
            { direction = 0; length = -d.x; }
            else if (Mathf.Abs(d.y) < 5f && d.x > 5f)
            { direction = 1; length = d.x; }
            else if (Mathf.Abs(d.x) < 5f && d.y > 5f)
            { direction = 2; length = d.y; }
            else if (Mathf.Abs(d.x) < 5f && d.y < -5f)
            { direction = 3; length = -d.y; }
            if (direction >= 0 && length < distance[direction])
            { distance[direction] = length; best[direction] = i; }
        }
        foreach (int index in best) if (index >= 0) yield return index;
    }
}

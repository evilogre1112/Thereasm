using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Bounded three-ply placement forecast; future nodes model occupancy, not skill resolution.
/// Villager search uses public information, and only the root move reaches TurnManager.
/// The class name is retained for serialized references.
/// </summary>
public sealed class BotFivePlyPlanner
{
    private struct Move
    {
        public CardInteraction interaction;
        public CardInstance card;
        public DefaultOwner hand;
        public int seat;
        public int cost;
        public float score;
    }
    private struct Threat
    {
        public int seat;
        public float score;
    }

    private readonly List<Move> handCards = new List<Move>(32);
    private readonly List<Move> moveBuffer = new List<Move>(128);
    private readonly HashSet<CardInstance> used = new HashSet<CardInstance>();
    private CardInstance[] board;
    private bool[] opponentOccupied;
    private TurnManager tm;
    private BotKnowledge knowledge;
    private DefaultOwner role;
    private int beam;
    private int budget;
    private int activeLimit;
    private readonly Dictionary<CardData, float> cachedBeliefs = new Dictionary<CardData, float>();
    private int[] left;
    private int[] right;
    private int[] above;
    private int[] below;
    public int NodesExpanded { get; private set; }

    public bool TryChoose(TurnManager manager, DefaultOwner botRole,
        BotKnowledge beliefs, CardInstance[] boardSnapshot, int depth,
        int beamWidth, int maxNodes, bool allowProgress,
        out BotDecisionEngine.Placement result)
    {
        result = new BotDecisionEngine.Placement { seat = -1,
            score = float.NegativeInfinity };
        if (manager == null || manager.allSeats == null ||
            DeckManager.instance == null || boardSnapshot == null ||
            (botRole != DefaultOwner.Player1 && botRole != DefaultOwner.Player2))
            return false;

        tm = manager;
        role = botRole;
        knowledge = beliefs;
        board = (CardInstance[])boardSnapshot.Clone();
        opponentOccupied = new bool[board.Length];
        // A depth-three line is: own placement -> rival occupancy/pass -> own follow-up.
        depth = Mathf.Clamp(depth, 2, 3);
        beam = Mathf.Clamp(beamWidth, 2, 8);
        budget = Mathf.Clamp(maxNodes, 200, 1600);
        activeLimit = budget;
        NodesExpanded = 0;
        cachedBeliefs.Clear();
        used.Clear();
        CacheDirectionalNeighbors();
        CollectVisibleLegalHandCards();
        if (handCards.Count == 0) return false;

        int hearts = role == DefaultOwner.Player1 ? tm.p1Hearts : tm.p2Hearts;
        List<Move> roots = GenerateTopMoves(hearts, beam, true);
        if (roots.Count == 0 || (!allowProgress && roots[0].score < 20f))
            return false;

        Move selected = roots[0];
        float best = float.NegativeInfinity;
        int remainingNodes = Mathf.Max(0, budget - NodesExpanded);
        int quotaPerRoot = Mathf.Max(1, remainingNodes / roots.Count);
        for (int i = 0; i < roots.Count; i++)
        {
            // Allocate comparable search effort to each candidate. Without
            // this, the first root can consume the entire shared node budget.
            activeLimit = Mathf.Min(budget, NodesExpanded + quotaPerRoot);
            Move root = roots[i];
            board[root.seat] = root.card;
            used.Add(root.card);
            // Only count a bounded fraction of an uncertain positional forecast;
            // the simulator does not yet model Check, curses, tally or Invoker.
            float forecast = OpponentResponse(depth - 1, 0.70f);
            float value = root.score + 0.35f * Mathf.Clamp(forecast, -28f, 28f);
            used.Remove(root.card);
            board[root.seat] = null;
            if (value > best)
            {
                best = value;
                selected = root;
            }
        }
        // If the projection exhausted its budget, do not overturn an
        // obviously stronger guaranteed immediate move because of truncation.
        if (selected.score < roots[0].score - 9f) selected = roots[0];
        result = new BotDecisionEngine.Placement {
            card = selected.interaction, hand = selected.hand,
            seat = selected.seat, score = selected.score
        };
        return result.card != null;
    }

    private void CollectVisibleLegalHandCards()
    {
        handCards.Clear();
        DeckManager deck = DeckManager.instance;
        Transform own = role == DefaultOwner.Player1 ? deck.player1Hand :
            deck.player2Hand;
        AddHand(own, role);
        // The shared Neutral hand is publicly selectable in Thereasm.
        AddHand(deck.neutralHand, DefaultOwner.Neutral);
    }

    private void AddHand(Transform hand, DefaultOwner owner)
    {
        if (hand == null) return;
        foreach (Transform child in hand)
        {
            CardInteraction ci = child.GetComponent<CardInteraction>();
            CardInstance card = child.GetComponent<CardInstance>();
            if (ci == null || card == null || card.cardData == null ||
                card.isDead || ci.isPlaced || ci.PlacementCost < 1) continue;
            handCards.Add(new Move { interaction = ci, card = card,
                hand = owner, cost = ci.PlacementCost });
        }
    }

    private void CacheDirectionalNeighbors()
    {
        left = new int[board.Length];
        right = new int[board.Length];
        above = new int[board.Length];
        below = new int[board.Length];
        for (int i = 0; i < board.Length; i++)
        {
            left[i] = tm.HorizontalNeighbor(i, -1);
            right[i] = tm.HorizontalNeighbor(i, 1);
            above[i] = -1;
            below[i] = -1;
            if (role != DefaultOwner.Player2 || tm.allSeats[i] == null) continue;
            RectTransform current = tm.allSeats[i].GetComponent<RectTransform>();
            if (current == null) continue;
            Vector2 origin = current.anchoredPosition;
            float upDistance = float.MaxValue;
            float downDistance = float.MaxValue;
            for (int j = 0; j < board.Length; j++)
            {
                if (i == j || tm.allSeats[j] == null) continue;
                RectTransform other = tm.allSeats[j].GetComponent<RectTransform>();
                if (other == null) continue;
                Vector2 delta = other.anchoredPosition - origin;
                if (Mathf.Abs(delta.x) >= 5f) continue;
                if (delta.y > 5f && delta.y < upDistance)
                { upDistance = delta.y; above[i] = j; }
                if (delta.y < -5f && -delta.y < downDistance)
                { downDistance = -delta.y; below[i] = j; }
            }
        }
    }

    private bool Free(int seat, CardInstance card)
    {
        if (seat < 0 || seat >= board.Length || board[seat] != null ||
            opponentOccupied[seat] || tm.allSeats[seat] == null ||
            tm.allSeats[seat].cardPos == null) return false;
        // Actual scene check must be honored; simulated seats are in board[].
        if (tm.allSeats[seat].cardPos.childCount != 0) return false;
        if (card.cardData.cardSkill is Skill_GameMaster &&
            tm.allSeats[seat].gameObject.name.Contains("ExtraSeat")) return false;
        return true;
    }

    private List<Move> GenerateTopMoves(int hearts, int take, bool root)
    {
        moveBuffer.Clear();
        if (NodesExpanded >= activeLimit) return new List<Move>();
        foreach (Move candidate in handCards)
        {
            if (used.Contains(candidate.card) || candidate.cost > hearts) continue;
            for (int seat = 0; seat < board.Length; seat++)
            {
                if (!Free(seat, candidate.card)) continue;
                if (NodesExpanded >= activeLimit) break;
                NodesExpanded++;
                float score;
                if (root)
                {
                    // Root scoring includes live card effects and belief heuristics.
                    score = BotDecisionEngine.PlacementScore(tm, role, knowledge,
                        board, candidate.card, seat, candidate.cost);
                }
                else
                {
                    // Future nodes use a fast positional heuristic. This
                    // avoids extrapolating live-skill methods into fake states.
                    score = FuturePlacementScore(candidate, seat);
                }
                Move m = candidate;
                m.seat = seat;
                m.score = score;
                moveBuffer.Add(m);
            }
            if (NodesExpanded >= activeLimit) break;
        }
        moveBuffer.Sort((a, b) => b.score.CompareTo(a.score));
        var top = new List<Move>(take);
        var counts = new Dictionary<CardInstance, int>();
        foreach (Move move in moveBuffer)
        {
            int appearances;
            counts.TryGetValue(move.card, out appearances);
            // Do not waste the entire beam on 6 adjacent seats of one card.
            if (appearances >= 2 && moveBuffer.Count > take) continue;
            top.Add(move);
            counts[move.card] = appearances + 1;
            if (top.Count >= take) break;
        }
        return top;
    }

    private float FuturePlacementScore(Move move, int seat)
    {
        CardInstance card = move.card;
        CardSkill skill = card.cardData.cardSkill;
        float value = card.cardData.owner == role ? 24f :
            card.cardData.owner == DefaultOwner.Neutral ? 17f : -15f;
        value -= move.cost * 5f;
        if (skill is Skill_Detective && role == DefaultOwner.Player2)
            value += 9f;
        if (skill is Skill_Invoker && tm.allSeats[seat].isNextToCemetery)
            value += tm.deadCardsData != null && tm.deadCardsData.Count > 0 ? 4f : 0f;
        if (skill is Skill_Preacher && role == DefaultOwner.Player2)
            value += 9f;
        if (skill is Skill_Hunter && role == DefaultOwner.Player2)
            value += 8f;
        if (skill is Skill_Saboteur) value += 3f;
        if (skill is Skill_Sacrifier && role == DefaultOwner.Player2)
            value -= 18f;
        if (role == DefaultOwner.Player1 && tm.IsCur(card))
            value += 22f;
        if (role == DefaultOwner.Player2 && knowledge != null)
            value -= 35f * Belief(card);
        // Villager exposure estimates include GameMaster's vertical seats.
        int[] ns = role == DefaultOwner.Player2
            ? new int[] { left[seat], right[seat], above[seat], below[seat] }
            : new int[] { left[seat], right[seat] };
        foreach (int n in ns)
        {
            if (n < 0 || board[n] == null || board[n].cardData == null) continue;
            CardInstance other = board[n];
            if (role == DefaultOwner.Player1 && tm.IsCur(card))
            {
                if (other.cardData.owner == DefaultOwner.Player2)
                    value += 11f;
            }
            if (role == DefaultOwner.Player2 && knowledge != null &&
                card.cardData.owner == DefaultOwner.Player2)
                value -= 8f * Belief(other);
            if (skill is Skill_Preacher && other.cardData.owner == role)
                value += 8f;
            if (skill is Skill_Detective && role == DefaultOwner.Player2)
                value += 6f * Belief(other);
        }
        // Cap speculative follow-up scores so they cannot dominate the immediate move.
        return Mathf.Clamp(value, -40f, 65f);
    }

    // Model rival responses as hypothetical occupancy without reading its private hand.
    private float OpponentResponse(int remaining, float discount)
    {
        if (remaining <= 0 || NodesExpanded >= activeLimit) return 0f;
        var threats = new List<Threat>();
        for (int seat = 0; seat < board.Length; seat++)
        {
            if (board[seat] != null || opponentOccupied[seat] ||
                tm.allSeats[seat] == null || tm.allSeats[seat].cardPos == null ||
                tm.allSeats[seat].cardPos.childCount != 0) continue;
            float threat = ThreatOfOccupation(seat);
            if (threat > 0.1f) threats.Add(new Threat { seat = seat, score = threat });
        }
        threats.Sort((a, b) => b.score.CompareTo(a.score));
        // Pass is always an allowed adversarial response. Block top 2 threat
        // seats; keep computing bounded even in crowded late-game positions.
        float worst = MyFutureMove(remaining - 1, discount);
        int count = Mathf.Min(2, threats.Count);
        for (int i = 0; i < count && NodesExpanded < budget; i++)
        {
            Threat threat = threats[i];
            opponentOccupied[threat.seat] = true;
            // Bounded opponent control, no fabricated skill resolution.
            float score = -0.40f * threat.score +
                MyFutureMove(remaining - 1, discount);
            opponentOccupied[threat.seat] = false;
            if (score < worst) worst = score;
        }
        return worst;
    }

    private float MyFutureMove(int remaining, float discount)
    {
        if (remaining <= 0 || NodesExpanded >= activeLimit) return 0f;
        // The intervening opposing turn allows hearts to refill before the next own move.
        List<Move> choices = GenerateTopMoves(5, Mathf.Min(beam, 4), false);
        if (choices.Count == 0) return -5f;
        float best = float.NegativeInfinity;
        foreach (Move move in choices)
        {
            if (NodesExpanded >= activeLimit) break;
            board[move.seat] = move.card;
            used.Add(move.card);
            float value = move.score + discount * OpponentResponse(
                remaining - 1, discount * .85f);
            used.Remove(move.card);
            board[move.seat] = null;
            if (value > best) best = value;
        }
        return best == float.NegativeInfinity ? 0f : best;
    }

    private float Belief(CardInstance card)
    {
        if (card == null || card.cardData == null || knowledge == null)
            return .20f;
        float value;
        if (cachedBeliefs.TryGetValue(card.cardData, out value)) return value;
        value = Mathf.Clamp01(knowledge.Cur(card, tm));
        cachedBeliefs[card.cardData] = value;
        return value;
    }

    private float ThreatOfOccupation(int seat)
    {
        float threat = 0f;
        // Villager exposure estimates include GameMaster's vertical seats.
        int[] ns = role == DefaultOwner.Player2
            ? new int[] { left[seat], right[seat], above[seat], below[seat] }
            : new int[] { left[seat], right[seat] };
        foreach (int neighbor in ns)
        {
            if (neighbor < 0 || board[neighbor] == null ||
                board[neighbor].cardData == null) continue;
            CardInstance c = board[neighbor];
            if (role == DefaultOwner.Player1)
            {
                if (tm.IsCur(c)) threat += 17f;
            }
            else
            {
                // Only PUBLIC owner and the posterior probability of Cur.
                if (c.cardData.owner == DefaultOwner.Player2)
                    threat += 14f * (1f - (knowledge == null ? .2f :
                        Belief(c)));
                if (c.cardData.cardSkill is Skill_Detective && !c.isFaceDown)
                    threat += 4f;
            }
        }
        return Mathf.Min(threat, 35f);
    }
}

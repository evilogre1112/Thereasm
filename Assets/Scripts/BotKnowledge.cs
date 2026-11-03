using System;
using System.Collections.Generic;
using UnityEngine;

// The Villager's inference boundary: no reads of allCurs, IsCur or isInfected
// on the Player2 path. Keep history across rituals, not just one action.
public sealed class BotKnowledge
{
    private sealed class Belief
    {
        public float logEvidence;
        public float infected;
        public int hats;
        public bool blindSeen;
        public int opponentPlacements;
        public bool provenCur;  // Only mechanical evidence can establish a role; behavior alone cannot.
    }
    private sealed class RitualFrame
    {
        public readonly Dictionary<CardData, Vector2> positions = new Dictionary<CardData, Vector2>();
        public readonly List<Vector2> seats = new List<Vector2>();
        public int ritual;
    }
    private sealed class EvidenceEvent
    {
        public CardData card;
        public string cause;
        public float likelihoodRatio;
        public int ritual;
    }

    private readonly DefaultOwner role;
    private readonly Dictionary<CardData, Belief> beliefs = new Dictionary<CardData, Belief>();
    private readonly HashSet<CardData> snooperSeen = new HashSet<CardData>();
    private readonly HashSet<CardData> purgeSeen = new HashSet<CardData>();
    private readonly Dictionary<CardData, int> previousBoard = new Dictionary<CardData, int>();
    private readonly List<RitualFrame> frames = new List<RitualFrame>();
    private readonly List<EvidenceEvent> history = new List<EvidenceEvent>();
    private TurnState previous = (TurnState)(-1);
    // A public Check removes infection from EVERY living card, including hands.
    // A later public Check can then prove a source if the intervening ritual
    // histories leave one and only one mechanically possible Cur source.
    private int lastCleanCheckRitual = -1;
    private bool completeSinceCheck = true;
    private int checkUsesObserved;
    private int lastObservedP1Checks = -1;
    private int lastObservedP2Checks = -1;
    public bool HasCleanCheckBaseline => lastCleanCheckRitual >= 0 && completeSinceCheck;
    private int lastOpportunityRitual = -1;
    private readonly Dictionary<CardData, int> watchedSuspects = new Dictionary<CardData, int>();
    private readonly HashSet<CardData> movedUnderChallenge = new HashSet<CardData>();
    private readonly HashSet<CardData> provenCur = new HashSet<CardData>();
    private int rituals;
    public int EvidenceEvents => history.Count;
    public int RitualsObserved => rituals;
    public bool HasMechanicalProof(CardData card) => card != null && provenCur.Contains(card);
    public int ProvenCurCount => provenCur.Count;
    public int RecentEvidenceCount(CardData card)
    {
        int count = 0;
        foreach (EvidenceEvent ev in history)
            if (ev.card == card && ev.ritual >= rituals - 2) count++;
        return count;
    }

    public BotKnowledge(DefaultOwner role) { this.role = role; }

    private static bool Eligible(CardData data) =>
        data != null && data.cardType != CardType.Hunter && data.cardNumber != 9;

    private Belief For(CardData data)
    {
        Belief belief;
        if (data == null) return null;
        if (!beliefs.TryGetValue(data, out belief))
        {
            belief = new Belief();
            beliefs.Add(data, belief);
        }
        return belief;
    }

    private void AddEvidence(CardData card, float ratio, string cause)
    {
        if (role != DefaultOwner.Player2 || !Eligible(card) || ratio <= 0f) return;
        Belief b = For(card);
        b.logEvidence = Mathf.Clamp(b.logEvidence + Mathf.Log(ratio), -8f, 8f);
        history.Add(new EvidenceEvent { card = card, cause = cause,
            likelihoodRatio = ratio, ritual = rituals });
        if (history.Count > 128) history.RemoveAt(0);
    }

    // Fixed-count posterior, P(card is Cur | public evidence, exactly k Curs
    // in this deck). Every eligible card is included, also dead/unplaced ones
    // unless a PUBLIC reveal has updated its likelihood. Do not peek dead roles.
    // Normalize per-deck probabilities so their sum matches the remaining Cur count.
    public float Cur(CardData data)
    {
        if (!Eligible(data)) return 0f;
        var deck = DeckManager.instance;
        if (deck == null || deck.allCards == null) return 0f;
        int required = GameRules.RequiredCursForOwner(
            GameSettings.currentMode, GameSettings.currentLevel, data.owner);
        if (required <= 0) return 0f;
        List<CardData> cards = new List<CardData>();
        foreach (CardData d in deck.allCards)
            if (d != null && d.owner == data.owner && Eligible(d) && !cards.Contains(d))
                cards.Add(d);
        if (!cards.Contains(data)) cards.Add(data);
        int verified = 0;
        foreach (CardData c in cards) if (provenCur.Contains(c)) verified++;
        // Mechanical constraints: exactly required Cur cards in each owner deck.
        // Never extrapolate a proof from a mere suspect score or one adversarial move.
        if (verified > required) return required / (float)cards.Count; // conflict-safe
        if (provenCur.Contains(data)) return 1f;
        int remaining = required - verified;
        if (remaining <= 0) return 0f;
        List<CardData> unknown = new List<CardData>();
        foreach (CardData c in cards) if (!provenCur.Contains(c)) unknown.Add(c);
        if (remaining >= unknown.Count) return 1f;
        int index = unknown.IndexOf(data);
        double[] weights = new double[unknown.Count];
        for (int i = 0; i < unknown.Count; ++i)
            weights[i] = Math.Exp(Mathf.Clamp(For(unknown[i]).logEvidence, -8f, 8f)) *
                (unknown[i].cardSkill == null ? 1.2d : 1.0d);
        double denominator = Symmetric(weights, remaining, -1);
        if (denominator <= 0) return remaining / (float)unknown.Count;
        double numerator = weights[index] * Symmetric(weights, remaining - 1, index);
        return Mathf.Clamp01((float)(numerator / denominator));
    }

    // Elementary symmetric polynomial: the weighted probability of choosing
    // 'count' cards without replacement, in O(deckSize * requiredCurCount).
    private static double Symmetric(double[] weights, int count, int exclude)
    {
        if (count < 0) return 0;
        double[] dp = new double[count + 1];
        dp[0] = 1;
        int seen = 0;
        for (int i = 0; i < weights.Length; ++i)
        {
            if (i == exclude) continue;
            ++seen;
            for (int j = Math.Min(count, seen); j >= 1; --j)
                dp[j] += weights[i] * dp[j - 1];
        }
        return dp[count];
    }

    public float Cur(CardInstance card, TurnManager tm)
    {
        if (card == null || card.cardData == null) return 0;
        if (role == DefaultOwner.Player1) return tm.IsCur(card) ? 1f : 0f;
        return Cur(card.cardData);
    }

    // All-hand estimate for safe Check planning. This is only a posterior
    // maintained from public observations; it cannot read CardInstance.isInfected.
    public float Infection(CardData data)
    {
        if (role != DefaultOwner.Player2 || data == null) return 0f;
        return For(data).infected;
    }

    public float Infection(CardInstance card)
    {
        if (card == null || card.cardData == null) return 0f;
        return role == DefaultOwner.Player1 ? (card.isInfected ? 1f : 0f) : For(card.cardData).infected;
    }

    public float ExpectedLivingCur(TurnManager tm, DefaultOwner owner)
    {
        if (tm == null) return 0;
        float total = 0;
        foreach (var seat in tm.allSeats)
        {
            if (seat == null || seat.cardPos == null) continue;
            var card = seat.cardPos.GetComponentInChildren<CardInstance>();
            if (card != null && !card.isDead && card.cardData != null && card.cardData.owner == owner)
                total += Cur(card, tm);
        }
        return total;
    }

    public void Observe(TurnManager tm)
    {
        if (role != DefaultOwner.Player2 || tm == null) return;
        // Check is public information even when Player1 uses it. Its purge
        // events have already been delivered synchronously by TurnManager.
        // Detect the consumed charge to start a new clean temporal baseline.
        bool publicCheckFinished =
            (lastObservedP1Checks >= 0 && tm.p1Checks < lastObservedP1Checks) ||
            (lastObservedP2Checks >= 0 && tm.p2Checks < lastObservedP2Checks);
        if (publicCheckFinished)
            RecordCleanCheckBaseline();
        lastObservedP1Checks = tm.p1Checks;
        lastObservedP2Checks = tm.p2Checks;
        if (previous != tm.currentTurn)
        {
            if (tm.currentTurn == TurnState.CursePhase)
            {
                rituals++;
                purgeSeen.Clear();
                RecordRitual(tm);
            }
            if (previous == TurnState.CursePhase && frames.Count > 0)
            {
                RitualFrame frame = frames[frames.Count - 1];
                foreach (var seat in tm.allSeats)
                {
                    var victim = seat != null && seat.cardPos != null ?
                        seat.cardPos.GetComponentInChildren<CardInstance>() : null;
                    if (victim == null || victim.isDead || victim.cardData == null) continue;
                    float risk = 0f;
                    foreach (CardData source in frame.positions.Keys)
                    {
                        if (source == victim.cardData || !CouldReach(frame, source, victim.cardData)) continue;
                        risk = 1f - (1f - risk) * (1f - Cur(source) * .30f);
                    }
                    var b = For(victim.cardData);
                    b.infected = 1f - (1f - b.infected) * (1f - risk);
                }
            }
            if (tm.currentTurn == TurnState.Player1Turn && previous == TurnState.Player2Turn)
                ArmPublicPositionChallenges(tm);
            if (tm.currentTurn != TurnState.SacrificePhase) snooperSeen.Clear();
            previous = tm.currentTurn;
        }

        var now = new Dictionary<CardData, int>();
        for (int i = 0; i < tm.allSeats.Count; ++i)
        {
            var card = tm.GetBoardCard(i);
            if (card == null || card.cardData == null || card.isDead) continue;
            var b = For(card.cardData);
            if (card.bonusSuspicion > b.hats)
                AddEvidence(card.cardData,
                    Mathf.Pow(2.8f, card.bonusSuspicion - b.hats), "Detective suspicion");
            b.hats = card.bonusSuspicion;
            if (tm.isTallyingVotes && PlayerInformation.SnooperMark(card, DefaultOwner.Player2) &&
                snooperSeen.Add(card.cardData))
                AddEvidence(card.cardData, 12f, "Public Snooper mark");
            int oldSeat;
            if (!previousBoard.TryGetValue(card.cardData, out oldSeat) &&
                card.lastPlacedBy == DefaultOwner.Player1 && tm.currentTurn != TurnState.SetupPhase &&
                b.opponentPlacements++ < 3)
                AddEvidence(card.cardData, 2.8f, "Observed Cur-player placement");
            now[card.cardData] = i;
        }
        // A movement while the rival controls the board is behavioral evidence,
        // Protection may be genuine or a bluff; it does not prove a Cur identity.
        if (tm.currentTurn == TurnState.Player1Turn)
            foreach (var watch in watchedSuspects)
            {
                int oldSeat, newSeat;
                if (previousBoard.TryGetValue(watch.Key, out oldSeat) &&
                    now.TryGetValue(watch.Key, out newSeat) && oldSeat != newSeat &&
                    movedUnderChallenge.Add(watch.Key))
                    AddEvidence(watch.Key, 1.20f, "Moved during publicly visible isolation challenge");
            }
        previousBoard.Clear();
        foreach (var pair in now) previousBoard[pair.Key] = pair.Value;
        // Blind có thể kiểm tra xác cũ nếu chưa đọc.
        // Chỉ tiếp nhận từng tiết lộ một lần, kể cả sau khi refresh aura.
        foreach (var corpse in UnityEngine.Object.FindObjectsByType<CardInstance>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (corpse == null || corpse.gameObject.scene != tm.gameObject.scene ||
                !corpse.isDead || corpse.cardData == null || !corpse.isRevealedByBlind ||
                (tm.discoverPanel != null && corpse.transform.IsChildOf(tm.discoverPanel.transform)))
                continue;
            var b = For(corpse.cardData);
            if (b.blindSeen) continue;
            b.blindSeen = true;
            AddEvidence(corpse.cardData, corpse.blindRevealAsCur ? 7f : .4f,
                "Blind public reveal");
        }
    }

    public void RecordRitual(TurnManager tm)
    {
        if (tm == null) return;
        RitualFrame frame = new RitualFrame { ritual = rituals };
        foreach (var seat in tm.allSeats)
        {
            if (seat == null) continue;
            var rect = seat.GetComponent<RectTransform>();
            if (rect == null) continue;
            Vector2 position = rect.anchoredPosition;
            frame.seats.Add(position); // empty chairs block infection rays
            var c = seat.cardPos != null ? seat.cardPos.GetComponentInChildren<CardInstance>() : null;
            if (c != null && !c.isDead && c.cardData != null)
                frame.positions[c.cardData] = position;
        }
        frames.Add(frame);
        // Keep complete history since the last known clean Check. If memory
        // must be truncated, invalidate all hard deductions for that window.
        if (frames.Count > 96)
        {
            RitualFrame removed = frames[0];
            if (lastCleanCheckRitual >= 0 && removed.ritual > lastCleanCheckRitual)
                completeSinceCheck = false;
            frames.RemoveAt(0);
        }
        // Decay stale weak action evidence, retaining stronger public observations.
        // Do not erase accumulated evidence or per-deck fixed Cur counts.
        foreach (Belief belief in beliefs.Values) belief.logEvidence *= .98f;
    }

    private static bool CouldReach(RitualFrame frame, CardData source, CardData victim)
    {
        Vector2 a, b;
        if (!frame.positions.TryGetValue(source, out a) || !frame.positions.TryGetValue(victim, out b))
            return false;
        if (Mathf.Abs(a.x - b.x) >= 5 && Mathf.Abs(a.y - b.y) >= 5) return false;
        float distance = Vector2.Distance(a, b);
        foreach (Vector2 seat in frame.seats)
        {
            if (Vector2.Distance(a, seat) < 5 || Vector2.Distance(b, seat) < 5) continue;
            if (Mathf.Abs(Vector2.Distance(a, seat) + Vector2.Distance(seat, b) - distance) > 1f)
                continue;
            bool occupied = false;
            foreach (Vector2 position in frame.positions.Values)
                if (Vector2.Distance(position, seat) < 5) { occupied = true; break; }
            if (!occupied) return false;
        }
        return true;
    }

    // Called synchronously around the Villager's own Check button.
    // It does NOT read any infection state or hidden Cur identity.
    // Kept as a compatibility hook. The Check baseline is committed on success.
    public void BeginOwnCheck() { }

    public void FinishOwnCheck(bool succeeded)
    {
        if (!succeeded) return;
        // Every infected living card, including those in hands, has been removed.
        RecordCleanCheckBaseline();
    }

    private void RecordCleanCheckBaseline()
    {
        lastCleanCheckRitual = rituals;
        completeSinceCheck = true;
        checkUsesObserved++;
        foreach (Belief b in beliefs.Values) b.infected = 0f;
    }

    // Count all POSSIBLE Cur sources from every curse-stage board since the last
    // clean Check.  We deliberately over-approximate: any occupied intervening
    // chair might have been infected, and a source may later move or die.  That
    // makes a singleton result meaningful without inspecting secret infection.
    private HashSet<CardData> PossibleSourcesSinceCheck(CardData victim,
        out bool observedWindow)
    {
        var possible = new HashSet<CardData>();
        observedWindow = false;
        if (victim == null || lastCleanCheckRitual < 0 || !completeSinceCheck)
            return possible;
        foreach (RitualFrame frame in frames)
        {
            if (frame.ritual <= lastCleanCheckRitual) continue;
            observedWindow = true;
            // Only a stage when the victim was physically on the board
            // could have infected that victim.
            if (!frame.positions.ContainsKey(victim)) continue;
            foreach (CardData source in frame.positions.Keys)
            {
                if (source == victim || !Eligible(source) ||
                    GameRules.RequiredCursForOwner(GameSettings.currentMode,
                        GameSettings.currentLevel, source.owner) <= 0)
                    continue;
                if (CouldReach(frame, source, victim)) possible.Add(source);
            }
        }
        return possible;
    }

    private void TryProveCurFromCheck(CardData victim)
    {
        if (lastCleanCheckRitual < 0 || victim == null ||
            !completeSinceCheck) return;
        bool observedWindow;
        HashSet<CardData> possible = PossibleSourcesSinceCheck(victim, out observedWindow);
        if (!observedWindow || possible.Count != 1) return;
        CardData only = null;
        foreach (CardData c in possible) only = c;
        int required = GameRules.RequiredCursForOwner(GameSettings.currentMode,
            GameSettings.currentLevel, only.owner);
        int known = 0;
        foreach (CardData c in provenCur) if (c.owner == only.owner) known++;
        if (known >= required || !provenCur.Add(only)) return;
        For(only).provenCur = true;
        AddEvidence(only, 100f,
            "MECHANICAL PROOF: sole possible Cur source in all post-Check curse snapshots");
    }

    // Estimate the informational value of Check without reading any real infection.
    // It rewards a prepared isolation trap only when all earlier curses since
    // baseline have been observed, otherwise it falls back to soft evidence.
    public float CheckInformationValue(TurnManager tm)
    {
        if (tm == null || tm.p2Checks <= 0) return 0f;
        float value = 0f;
        foreach (var seat in tm.allSeats)
        {
            if (seat == null || seat.cardPos == null) continue;
            var victim = seat.cardPos.GetComponentInChildren<CardInstance>();
            if (victim == null || victim.isDead || victim.cardData == null) continue;
            float p = Infection(victim);
            bool hasWindow;
            HashSet<CardData> sources = PossibleSourcesSinceCheck(victim.cardData, out hasWindow);
            if (hasWindow && sources.Count == 1) value += 52f * p;
            else if (hasWindow && sources.Count > 1) value += 15f * p / sources.Count;
            else value += 3f * p;
        }
        return value;
    }

    // Public-board counterfactual: by placing this card here, can we make a
    // victim's next infection have exactly one geometric candidate source?
    // This is a *trap opportunity*, not hard proof until a later Check.
    public float TrapPlacementValue(TurnManager tm, CardInstance[] board,
        CardInstance candidate, int seatIndex)
    {
        if (role != DefaultOwner.Player2 || tm == null || candidate == null ||
            candidate.cardData == null || tm.p2Checks <= 0 || seatIndex < 0 ||
            seatIndex >= tm.allSeats.Count || board[seatIndex] != null) return 0f;
        var frame = new RitualFrame();
        for (int i = 0; i < tm.allSeats.Count; i++)
        {
            var seat = tm.allSeats[i];
            if (seat == null) continue;
            var rect = seat.GetComponent<RectTransform>();
            if (rect == null) continue;
            Vector2 pos = rect.anchoredPosition;
            frame.seats.Add(pos);
            CardInstance ci = (i == seatIndex) ? candidate : board[i];
            if (ci != null && ci.cardData != null && !ci.isDead)
                frame.positions[ci.cardData] = pos;
        }
        float total = 0f;
        foreach (CardData victim in frame.positions.Keys)
        {
            if (Cur(victim) > .35f) continue; // need a plausible decoy
            CardData lone = null;
            int count = 0;
            foreach (CardData source in frame.positions.Keys)
            {
                if (source == victim || !Eligible(source) ||
                    GameRules.RequiredCursForOwner(GameSettings.currentMode,
                        GameSettings.currentLevel, source.owner) <= 0 ||
                    !CouldReach(frame, source, victim)) continue;
                lone = source;
                count++;
                if (count > 1) break;
            }
            if (count != 1 || lone == null || Cur(lone) < .12f) continue;
            // New isolation made by this placement should involve the new card.
            if (candidate.cardData != victim && candidate.cardData != lone) continue;
            float sacrificePenalty = victim.owner == DefaultOwner.Player2 ? 6f : 0f;
            if (victim.cardSkill is Skill_Hunter) sacrificePenalty += 13f;
            total += Mathf.Max(0f, Cur(lone) * 55f - sacrificePenalty);
        }
        return Mathf.Min(35f, total);
    }

    // Challenges affect only a weak behavioral prior, retained across turns and rituals.
    private void ArmPublicPositionChallenges(TurnManager tm)
    {
        watchedSuspects.Clear();
        movedUnderChallenge.Clear();
        if (tm == null) return;
        RitualFrame frame = CapturePublicBoard(tm);
        foreach (CardData source in frame.positions.Keys)
        {
            if (!Eligible(source) || Cur(source) < .12f) continue;
            foreach (CardData victim in frame.positions.Keys)
            {
                if (victim == source || victim.owner != DefaultOwner.Player2 ||
                    Cur(victim) > .30f || !CouldReach(frame, source, victim)) continue;
                watchedSuspects[source] = rituals;
                break;
            }
        }
        lastOpportunityRitual = rituals;
    }

    private static RitualFrame CapturePublicBoard(TurnManager tm)
    {
        RitualFrame frame = new RitualFrame();
        foreach (var seat in tm.allSeats)
        {
            if (seat == null) continue;
            var rect = seat.GetComponent<RectTransform>();
            if (rect == null) continue;
            Vector2 pos = rect.anchoredPosition;
            frame.seats.Add(pos);
            CardInstance ci = seat.cardPos != null ?
                seat.cardPos.GetComponentInChildren<CardInstance>() : null;
            if (ci != null && !ci.isDead && ci.cardData != null)
                frame.positions[ci.cardData] = pos;
        }
        return frame;
    }

    public void OnPublicPurge(CardInstance victim)
    {
        if (role != DefaultOwner.Player2 || victim == null || victim.cardData == null ||
            !purgeSeen.Add(victim.cardData)) return;
        // Purge clears infection risk while preserving suspicion of possible sources.
        For(victim.cardData).infected = 0f;
        TryProveCurFromCheck(victim.cardData);
        // Trace the confirmed infected card through the actual *past ritual*
        // seats, not its current seat (it may have moved since infection).
        bool hasWindow;
        HashSet<CardData> afterCheck = PossibleSourcesSinceCheck(victim.cardData, out hasWindow);
        if (hasWindow && afterCheck.Count > 0)
        {
            // Soft evidence: multiple candidate Curs remain ambiguous.
            float share = 1f / afterCheck.Count;
            foreach (CardData c in afterCheck)
                AddEvidence(c, 1f + 4f * share,
                    "Check death: geometric infection source in recorded rituals");
            return;
        }
        // No clean baseline: older infections cannot be excluded, so only
        // a mild, recency-weighted signal is justified, never a hard proof.
        var supports = new Dictionary<CardData, float>();
        for (int age = 0; age < Math.Min(3, frames.Count); age++)
        {
            RitualFrame frame = frames[frames.Count - 1 - age];
            if (!frame.positions.ContainsKey(victim.cardData)) continue;
            foreach (CardData source in frame.positions.Keys)
            {
                if (source == victim.cardData || !Eligible(source) ||
                    GameRules.RequiredCursForOwner(GameSettings.currentMode,
                        GameSettings.currentLevel, source.owner) <= 0 ||
                    !CouldReach(frame, source, victim.cardData)) continue;
                if (!supports.ContainsKey(source)) supports[source] = 0f;
                supports[source] += Mathf.Pow(.5f, age);
            }
        }
        float sum = 0f;
        foreach (float v in supports.Values) sum += v;
        if (sum > 0f)
            foreach (var pair in supports)
                AddEvidence(pair.Key, 1f + pair.Value / sum,
                    "Public Check: possible source without clean temporal baseline");
    }
}

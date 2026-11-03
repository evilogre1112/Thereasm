using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[Serializable]
public class VoteChange
{
    public int number, before, after;
    public string label;
}

[Serializable]
public class VoteEffectRecord
{
    public int id, round, sourceNumber;
    public string source, kind, reason;
    public bool tally;
    public VoteChange[] changes = new VoteChange[0];
    public VoteChange[] participants = new VoteChange[0];
    public float PresentationSeconds => kind == "Trader" ? 1.7f : kind == "Jester" ? 1.5f : kind == "Snooper" || kind == "Gambler" ? 1.4f : .8f;
}

// A presentation audit of real results. Never simulates a skill or consumes randomness.
// Each viewer has an independent sequence: hidden selections cannot leak via skipped IDs.
public class VoteEffectFeed : MonoBehaviour
{
    private readonly List<VoteEffectRecord> p1 = new List<VoteEffectRecord>();
    private readonly List<VoteEffectRecord> p2 = new List<VoteEffectRecord>();
    private int p1Sequence, p2Sequence, round;
    private bool recording;
    public int TallySteps => p1.Count(r => r.tally);
    public float TallyPresentationSeconds => p1.Where(r => r.tally && r.kind != "Base").Sum(r => r.PresentationSeconds);
    public static VoteEffectFeed Current => TurnManager.instance != null ? TurnManager.instance.voteEffects : null;
    public VoteEffectRecord[] For(DefaultOwner viewer) => (viewer == DefaultOwner.Player1 ? p1 : p2).ToArray();
    public VoteEffectRecord[] Local => For(CardOwnershipView.Perspective);
    public IReadOnlyList<VoteEffectRecord> LocalRecords => CardOwnershipView.Perspective == DefaultOwner.Player1 ? p1 : p2;
    public void ApplyRemote(VoteEffectRecord[] rows)
    {
        var target = CardOwnershipView.Perspective == DefaultOwner.Player1 ? p1 : p2;
        target.Clear(); if (rows != null) target.AddRange(rows.Where(r => r != null).Take(64));
    }
    private void Add(CardInstance source, string kind, string reason, VoteChange[] changes, bool tally, DefaultOwner visibility = DefaultOwner.Neutral, VoteChange[] participants = null)
    {
        void Push(List<VoteEffectRecord> list, ref int sequence)
        {
            list.Add(new VoteEffectRecord { id = ++sequence, round = round, sourceNumber = source != null ? source.cardData.cardNumber : 0,
                source = source != null ? TurnManager.CardLabel(source) : "Players' ballots", kind = kind, reason = reason, tally = tally, changes = changes, participants = participants ?? new VoteChange[0] });
            if (list.Count > 64) list.RemoveAt(0);
        }
        if (visibility != DefaultOwner.Player2) Push(p1, ref p1Sequence);
        if (visibility != DefaultOwner.Player1) Push(p2, ref p2Sequence);
    }
    private VoteChange[] Changes(int[] before, int[] after, bool includeUnchanged = false)
    {
        var t = TurnManager.instance;var rows = new List<VoteChange>();
        for (int i = 0; i < after.Length && i < t.allSeats.Count; i++)
        {
            var target = t.GetBoardCard(i);
            if (target == null || !includeUnchanged && before[i] == after[i]) continue;
            rows.Add(new VoteChange { number = target.cardData.cardNumber, label = TurnManager.CardLabel(target), before = before[i], after = after[i] });
        }
        return rows.ToArray();
    }
    public void BeginTally(int[] counts)
    {
        round++;p1.Clear();p2.Clear();recording = true;
        Add(null,"Base","These are the players' original ballots. Neutral effects are applied next.",Changes(new int[counts.Length], counts, true),true);
    }
    public void EndCapture() { recording = false; }
    public void RecordChange(CardInstance source, string kind, string reason, int[] before, int[] after, int[] groupSeats = null)
    {
        if (!recording) return;
        var participants = groupSeats == null ? null : groupSeats.Distinct().Where(i => i >= 0 && i < after.Length && TurnManager.instance.GetBoardCard(i) != null)
            .Select(i => new VoteChange { number = TurnManager.instance.GetBoardCard(i).cardData.cardNumber,
                label = TurnManager.CardLabel(TurnManager.instance.GetBoardCard(i)), before = before[i], after = after[i] }).ToArray();
        Add(source,kind,reason,Changes(before,after),true,participants:participants);
    }
    public void RecordOverride(CardInstance source, List<int> seats)
    {
        if (!recording) return;
        var t = TurnManager.instance;
        var changes = seats.Where(i => i >= 0 && i < t.allSeats.Count).Select(t.GetBoardCard).Where(c => c != null)
            .Select(c => new VoteChange { number = c.cardData.cardNumber, label = TurnManager.CardLabel(c) }).ToArray();
        Add(source,source.cardData.cardSkill is Skill_Sacrifier ? "Sacrifier" : "Controller",
            source.cardData.cardSkill is Skill_Sacrifier ? "Sacrifier takes the sacrifice itself. The displayed votes stay unchanged; they no longer choose the victim." :
            "Controller changes the verdict: cards with the lowest positive vote count are sacrificed, instead of the highest count.",changes,true);
    }
    public void Detective(CardInstance source, CardInstance target)
    {
        SkillAudio.Emit(source);
        Add(source,"Detective","Detective adds 1 suspicion here. Suspicion guides its later comparisons; it is not an extra vote.",
            new[]{new VoteChange { number = target.cardData.cardNumber,label = TurnManager.CardLabel(target),before = target.bonusSuspicion-1,after = target.bonusSuspicion }},false);
    }
    public void Activated(CardInstance source, CardInstance target)
    {
        if (source == null || source.cardData == null) return;
        var skill = source.cardData.cardSkill;string kind = "Skill";DefaultOwner visibility = DefaultOwner.Neutral;
        if (skill is Skill_Gambler) { kind = "Gambler"; visibility = TurnManager.instance.currentVotingPlayer; }
        if (skill is Skill_Snooper) { kind = "Snooper"; visibility = DefaultOwner.Player1; }
        if (skill is Skill_Invoker) visibility = source.lastPlacedBy;
        SkillAudio.Emit(source,visibility);
        string reason = kind == "Gambler" ? "Your wager is selected. The other player's wager remains hidden until the tally." :
            kind == "Snooper" ? "Your Snooper target is selected. This selection stays private until the tally." : "This card just activated its skill.";
        Add(source,kind,reason,target == null ? new VoteChange[0] : new[]{new VoteChange {number=target.cardData.cardNumber,label=TurnManager.CardLabel(target)}},false,visibility);
    }
}

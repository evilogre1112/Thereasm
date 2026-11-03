using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "Skill_Snooper", menuName = "Card Skills/Hybrid/Snooper")]
public class Skill_Snooper : CardSkill
{
    // Mục tiêu bí mật do Cursermancer chọn khi Snooper bị nhiễm.
    private Dictionary<CardInstance, CardInstance> manipulatedTargets = new Dictionary<CardInstance, CardInstance>();
    private HashSet<CardInstance> publicTargets = new HashSet<CardInstance>();
    public bool IsMarkedFor(CardInstance target, DefaultOwner viewer, bool tallying) =>
        tallying ? publicTargets.Contains(target) : viewer == DefaultOwner.Player1 && manipulatedTargets.ContainsValue(target);
    public void ResetRound() { manipulatedTargets.Clear(); publicTargets.Clear(); }
    public override void OnRemoveFromBoard(CardInstance owner, int seatIndex) { manipulatedTargets.Remove(owner); }
    public override void OnDeath(CardInstance owner, int seatIndex) { manipulatedTargets.Remove(owner); }

    public override bool CastActiveSkill(CardInstance caster, CardInstance target)
    {
        if (!caster.isInfected) return false;
        if (TurnManager.instance == null) return false;
        if (TurnManager.instance.currentTurn != TurnState.SacrificePhase) return false;

        bool isVotingPlayerCur = TurnManager.instance.currentVotingPlayer == DefaultOwner.Player1;

        if (!isVotingPlayerCur) return false;

        if (target == null) return false;
        
        int targetSeatIndex = TurnManager.instance.GetSeatIndex(target.GetComponent<CardInteraction>());
        if (targetSeatIndex == -1) return false;

        if (manipulatedTargets.TryGetValue(caster, out var previous) && previous != null && previous.snooperMark != null)
            previous.snooperMark.SetActive(false);
        manipulatedTargets[caster] = target;
        caster.hasUsedSkill = true;

        if (target.snooperMark != null)
        {
            target.snooperMark.SetActive(!OnlineSession.Active || OnlineSession.LocalCur);
        }
        PlayerInformation.RefreshHostMarks();
        return true;
    }

    public override void OnBeforeVoteCalculate(CardInstance caster, int[] voteCounts, int mySeatIndex)
    {
        if (caster.isInfected)
        {
            // Trạng thái nhiễm bệnh: Đổ phiếu vào mục tiêu phe Quỷ đã găm từ trước
            if (manipulatedTargets.ContainsKey(caster))
            {
                var target = manipulatedTargets[caster];
                int chosenTarget = target != null && !target.isDead ? TurnManager.instance.GetSeatIndex(target.GetComponent<CardInteraction>()) : -1;
                if (chosenTarget >= 0 && chosenTarget < voteCounts.Length)
                {
                    voteCounts[chosenTarget]++;
                    publicTargets.Add(target);
                    if (target.snooperMark != null) target.snooperMark.SetActive(true);
                }
                manipulatedTargets.Remove(caster);
            }
        }
        else
        {
            List<CardInstance> activeCurs = new List<CardInstance>();
            foreach (CardInstance cur in TurnManager.instance.allCurs)
            {
                if (TurnManager.instance.IsLivingBoardCard(cur))
                {
                    activeCurs.Add(cur);
                }
            }

            if (activeCurs.Count > 0)
            {
                // Nếu có nhiều Cursermancer hợp lệ, chọn ngẫu nhiên một lá để chỉ điểm.
                CardInstance targetedCur = activeCurs[Random.Range(0, activeCurs.Count)];
                int cursemancerSeatIndex = TurnManager.instance.GetSeatIndex(targetedCur.GetComponent<CardInteraction>());
                
                if (cursemancerSeatIndex != -1)
                {
                    voteCounts[cursemancerSeatIndex]++;
                    publicTargets.Add(targetedCur);
                    
                    CardInstance targetCard = TurnManager.instance.allSeats[cursemancerSeatIndex].cardPos.GetComponentInChildren<CardInstance>();
                    if (targetCard != null && targetCard.snooperMark != null)
                    {
                        targetCard.snooperMark.SetActive(true);
                    }
                    
                    Debug.Log($"<color=magenta>Kẻ Tò Mò {caster.name} đã lén lút bỏ phiếu chỉ điểm Kẻ lây nhiễm ở ghế số {cursemancerSeatIndex}!</color>");
                }
            }
        }
    }
}

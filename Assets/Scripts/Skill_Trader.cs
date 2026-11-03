using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "Skill_Trader", menuName = "Card Skills/Passive/Trader Shared Votes")]
public class Skill_Trader : CardSkill
{
    public override void Activate(CardInstance owner)
    {
        Debug.Log($"<color=yellow>Thương nhân {owner.name} đã sẵn sàng giao dịch phiếu bầu!</color>");
    }

    public override void OnBeforeVoteCalculate(CardInstance owner, int[] voteCounts, int mySeatIndex)
    {
        if (TurnManager.instance == null) return;
        List<SeatDropZone> allSeats = TurnManager.instance.allSeats;
        
        List<int> tradeGroup = new List<int>();
        int left = TurnManager.instance.HorizontalNeighbor(mySeatIndex, -1);
        int right = TurnManager.instance.HorizontalNeighbor(mySeatIndex, 1);
        if (left >= 0) tradeGroup.Add(left);
        tradeGroup.Add(mySeatIndex);
        if (right >= 0) tradeGroup.Add(right);
        tradeGroup.RemoveAll(index => TurnManager.instance.GetBoardCard(index) == null);
        if (tradeGroup.Count == 0) return;

        int maxVoteInGroup = 0;
        foreach (int idx in tradeGroup)
        {
            if (voteCounts[idx] > maxVoteInGroup) maxVoteInGroup = voteCounts[idx];
        }

        // Chia theo số lá đang ngồi trong vùng; làm tròn xuống và bỏ phần dư.
        if (maxVoteInGroup > 0)
        {
            int sharedVotes = maxVoteInGroup / tradeGroup.Count;
            foreach (int idx in tradeGroup)
            {
                voteCounts[idx] = sharedVotes;
            }
            Debug.Log($"<color=yellow>Trader {owner.name} đã can thiệp, chia đều ({sharedVotes} phiếu/người)!</color>");
        }
    }
}

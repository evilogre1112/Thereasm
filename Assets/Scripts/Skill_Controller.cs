using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "Skill_Controller", menuName = "Card Skills/Passive/Controller Manipulation")]
public class Skill_Controller : CardSkill
{
    public override int OverridePriority => 50;
    public override void Activate(CardInstance owner)
    {
        Debug.Log($"<color=magenta>Controller {owner.name} đã sẵn sàng thao túng kết quả hiến tế!</color>");
    }

    public override bool OnOverrideVoteResult(CardInstance owner, int[] voteCounts, ref List<int> targetSeats, int mySeatIndex)
    {
        int minVotes = int.MaxValue;

        for (int i = 0; i < voteCounts.Length; i++)
        {
            if (voteCounts[i] >= 1 && voteCounts[i] < minVotes)
            {
                minVotes = voteCounts[i];
            }
        }

        if (minVotes != int.MaxValue)
        {
            Debug.Log($"<color=magenta>Controller {owner.name} thao túng kết quả! Kẻ có {minVotes} phiếu sẽ phải chết.</color>");
            
            targetSeats.Clear();
            
            for (int i = 0; i < voteCounts.Length; i++)
            {
                if (voteCounts[i] == minVotes)
                {
                    targetSeats.Add(i);
                }
            }

            return true; // True để dùng danh sách tử hình của Controller thay cho luật số đông.
        }

        return false;
    }
}
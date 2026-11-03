using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "Skill_Sacrifier", menuName = "Card Skills/Passive/Sacrifier")]
public class Skill_Sacrifier : CardSkill
{
    public override int OverridePriority => 100;
    public override void Activate(CardInstance owner)
    {
        Debug.Log($"<color=red>{owner.name} sẵn sàng hiến tế linh hồn để bảo vệ mọi người.</color>");
    }

    // Sacrifier nhận quyền bị hiến tế thay cho danh sách mục tiêu hiện tại.
    public override bool OnOverrideVoteResult(CardInstance owner, int[] voteCounts, ref List<int> targetSeats, int mySeatIndex)
    {
        Debug.Log($"<color=red>Sacrifier ({owner.name}) xuất hiện! Mọi phiếu bầu khác đều vô nghĩa.</color>");
        
        targetSeats.Clear();
        targetSeats.Add(mySeatIndex);
        
        return true;
    }
}
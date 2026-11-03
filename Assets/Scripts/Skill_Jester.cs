using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "Skill_Jester", menuName = "Card Skills/Passive/Jester Random Votes")]
public class Skill_Jester : CardSkill
{
    public override void Activate(CardInstance owner)
    {
        Debug.Log($"<color=orange>Jester {owner.name} đang cười khúc khích, chực chờ quậy phá hòm phiếu!</color>");
    }

    public override void OnBeforeVoteCalculate(CardInstance owner, int[] voteCounts, int mySeatIndex)
    {
        if (TurnManager.instance == null) return;
        
        // 75% kích hoạt, 25% không có hiệu ứng.
        float roll = Random.Range(0f, 100f);
        if (roll >= 75f)
        {
            Debug.Log($"<color=orange>Jester {owner.name} nay hiền, không thèm ném phiếu mồi.</color>");
            return;
        }

        // Mỗi lần kích hoạt thành công, Jester luôn tung đúng 3 phiếu.
        const int votesToCast = 3;

        List<int> validTargets = new List<int>();
        for (int i = 0; i < TurnManager.instance.allSeats.Count; i++)
        {
            if (i == mySeatIndex) continue;
            
            CardInstance cardInSeat = TurnManager.instance.allSeats[i].cardPos.GetComponentInChildren<CardInstance>();
            if (cardInSeat != null)
            {
                validTargets.Add(i);
            }
        }

        if (validTargets.Count == 0) return;

        // Mỗi phiếu chọn độc lập nên có thể cùng nhắm một lá.
        Debug.Log($"<color=orange>!!! Jester {owner.name} nổi điên! Tung {votesToCast} phiếu mù vào hòm! !!!</color>");
        
        for (int i = 0; i < votesToCast; i++)
        {
            int randomValidIndex = Random.Range(0, validTargets.Count);
            int targetSeatIndex = validTargets[randomValidIndex];

            voteCounts[targetSeatIndex]++; 
            
            CardInstance victim = TurnManager.instance.allSeats[targetSeatIndex].cardPos.GetComponentInChildren<CardInstance>();
            Debug.Log($"<color=red>-> LỘP BỘP! 1 phiếu oan nghiệt bay trúng đầu {victim.name} (Ghế {targetSeatIndex})</color>");
        }
    }
}
using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "Skill_Preacher", menuName = "Card Skills/Passive/Preacher Shield")]
public class Skill_Preacher : CardSkill
{
    public override void ApplyAura(CardInstance owner, int mySeatIndex)
    {
        if (TurnManager.instance == null) return;

        List<SeatDropZone> seats = TurnManager.instance.allSeats;

        int left = TurnManager.instance.HorizontalNeighbor(mySeatIndex, -1);
        int right = TurnManager.instance.HorizontalNeighbor(mySeatIndex, 1);
        if (left >= 0) ProtectSeat(seats[left]);
        if (right >= 0) ProtectSeat(seats[right]);
    }

    private void ProtectSeat(SeatDropZone seat)
    {
        if (seat == null || seat.cardPos == null) return;

        CardInteraction neighbor = seat.cardPos.GetComponentInChildren<CardInteraction>();
        
        if (neighbor != null)
        {
            neighbor.isProtectedByPreacher = true;
            Debug.Log($"<color=white>Phát hiện Preacher, {neighbor.name} đã được buff Thánh Khiên!</color>");
        }
    }
}

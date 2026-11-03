using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "Skill_Elder", menuName = "Card Skills/Passive/Elder Protection")]
public class Skill_Elder : CardSkill
{
    public override void Activate(CardInstance owner)
    {
        Debug.Log($"Elder {owner.name} đã giáng lâm, chuẩn bị kích hoạt hào quang.");
    }

    public override void ApplyAura(CardInstance owner, int mySeatIndex)
    {
        if (TurnManager.instance == null) return;

        List<SeatDropZone> seats = TurnManager.instance.allSeats;

        int left = TurnManager.instance.HorizontalNeighbor(mySeatIndex, -1);
        int right = TurnManager.instance.HorizontalNeighbor(mySeatIndex, 1);
        if (left >= 0)
        {
            LockSeat(seats[left]);
        }

        if (right >= 0)
        {
            LockSeat(seats[right]);
        }
    }

    private void LockSeat(SeatDropZone seat)
    {
        if (seat == null || seat.cardPos == null) return;

        CardInteraction neighbor = seat.cardPos.GetComponentInChildren<CardInteraction>();
        
        if (neighbor != null)
        {
            neighbor.isLockedByElder = true;
        }
    }
}

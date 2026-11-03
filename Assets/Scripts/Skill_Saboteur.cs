using UnityEngine;

[CreateAssetMenu(fileName = "Skill_Saboteur", menuName = "Card Skills/Passive/Saboteur")]
public class Skill_Saboteur : CardSkill
{
    public override void ApplyAura(CardInstance owner, int mySeatIndex)
    {
        if (TurnManager.instance == null || owner.isFaceDown) return;


        int leftSeat = TurnManager.instance.HorizontalNeighbor(mySeatIndex, -1);
        if (leftSeat >= 0 && leftSeat < TurnManager.instance.allSeats.Count) 
        {
            CardInstance leftCard = TurnManager.instance.allSeats[leftSeat].cardPos.GetComponentInChildren<CardInstance>();
            if (leftCard != null)
            {
                leftCard.isSilenced = true;
            }
        }

        int rightSeat = TurnManager.instance.HorizontalNeighbor(mySeatIndex, 1);
        if (rightSeat >= 0 && rightSeat < TurnManager.instance.allSeats.Count) 
        {
            CardInstance rightCard = TurnManager.instance.allSeats[rightSeat].cardPos.GetComponentInChildren<CardInstance>();
            if (rightCard != null)
            {
                rightCard.isSilenced = true;
            }
        }
    }
}

using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "Skill_Prankster", menuName = "Card Skills/Passive/Prankster")]
public class Skill_Prankster : CardSkill
{
    public override void Activate(CardInstance owner)
    {
        if (TurnManager.instance == null) return;
        
        if (owner.isSilenced) 
        {
            Debug.Log($"<color=red>Kỹ năng thất bại: {owner.name} đang bị Kẻ Phá Hoại khóa chặt!</color>");
            return;
        }

        CardInteraction interaction = owner.GetComponent<CardInteraction>();
        if (interaction == null || !interaction.isPlaced) return;

        int myIndex = TurnManager.instance.GetSeatIndex(interaction);
        
        int left = TurnManager.instance.HorizontalNeighbor(myIndex, -1);
        if (left < 0)
        {
            owner.hasUsedSkill = true; 
            return; 
        }

        CardInstance neighbor = TurnManager.instance.GetBoardCard(left);
        CardInteraction neighborLeft = neighbor != null ? neighbor.GetComponent<CardInteraction>() : null;

        if (neighborLeft != null)
        {
            if (neighborLeft.isLockedByElder)
            {
                return;
            }

            if (!owner.hasUsedSkill)
            {
                owner.hasUsedSkill = true; 

                float roll = Random.value;
                if (roll <= 0.1f) 
                {
                    if (!TurnManager.instance.RegisterPranksterReaction(owner, neighbor)) return;
                    Debug.Log($"<color=red>JACKPOT! Prankster vừa đáp xuống đã sút {neighborLeft.name} về tay!</color>");
                    neighborLeft.ForceReturnToHand();
                    SkillAudio.Emit(owner);
                    return; 
                }
            }

            List<Transform> emptySeats = new List<Transform>();
            foreach (var seat in TurnManager.instance.allSeats)
            {
                // Không chọn ghế phụ làm đích đẩy để tránh vòng lặp với Game Master.
                if (seat.cardPos.childCount == 0 && !seat.gameObject.name.Contains("ExtraSeat")) 
                {
                    emptySeats.Add(seat.cardPos);
                }
            }

            if (emptySeats.Count > 0 && TurnManager.instance.RegisterPranksterReaction(owner, neighbor))
            {
                Transform randomSeat = emptySeats[Random.Range(0, emptySeats.Count)];
                Debug.Log($"<color=orange>Prankster đẩy {neighborLeft.name} sang ghế trống.</color>");
                neighborLeft.ExecutePlace(randomSeat);
                SkillAudio.Emit(owner);
            }
        }
        else
        {
            owner.hasUsedSkill = true;
        }
    }
}

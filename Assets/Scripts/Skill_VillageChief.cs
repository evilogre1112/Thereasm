using UnityEngine;
using System.Collections;

[CreateAssetMenu(fileName = "Skill_VillageChief", menuName = "Card Skills/Passive/Village Chief")]
public class Skill_VillageChief : CardSkill
{
    public override void ApplyAura(CardInstance owner, int mySeatIndex)
    {
        if (TurnManager.instance == null || owner.isFaceDown) return;

        TurnState phase = TurnManager.instance.currentTurn;
        if (phase != TurnState.Player1Turn && phase != TurnState.Player2Turn) return;

        if (owner.isSilenced || owner.hasUsedSkill) return;

        bool hasNormalVillager = false;
        foreach (var seat in TurnManager.instance.allSeats)
        {
            CardInstance ci = seat.cardPos.GetComponentInChildren<CardInstance>();
            if (ci != null && ci != owner && ci.GetComponent<CardInteraction>().isPlaced)
            {
                if (ci.cardData.owner != DefaultOwner.Neutral && !TurnManager.instance.IsCur(ci))
                {
                    hasNormalVillager = true;
                    break; 
                }
            }
        }

        if (hasNormalVillager)
        {
            owner.hasUsedSkill = true; // Đánh dấu trước khi chuyển pha để tránh kích hoạt lặp.
            Debug.Log($"<color=yellow>Trưởng Làng {owner.name} thấy đủ người! Bắt đầu nghi thức ngay lập tức!</color>");
            
            // Chờ một frame để thao tác rút Saboteur và cập nhật aura hoàn tất.
            owner.StartCoroutine(DelayedForceCurse(owner));
        }
    }

    private IEnumerator DelayedForceCurse(CardInstance owner)
    {
        yield return new WaitForEndOfFrame(); 
        
        if (TurnManager.instance != null && TurnManager.instance.IsLivingBoardCard(owner) &&
            !owner.isSilenced && !owner.isFaceDown)
        {
            foreach (var seat in TurnManager.instance.allSeats)
            {
                var card = seat.cardPos.GetComponentInChildren<CardInstance>();
                if (card != owner && TurnManager.instance.IsLivingBoardCard(card) &&
                    card.cardData.owner != DefaultOwner.Neutral && !TurnManager.instance.IsCur(card))
                { SkillAudio.Emit(owner); TurnManager.instance.ForceCursePhase(); yield break; }
            }
        }
        if (owner != null) owner.hasUsedSkill = false;
    }
}

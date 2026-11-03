using UnityEngine;

[CreateAssetMenu(fileName = "Skill_Hunter", menuName = "Card Skills/Active/Hunter Kill")]
public class Skill_Hunter : CardSkill
{
    public int skillCost = 5; 

    public override bool CastActiveSkill(CardInstance caster, CardInstance target)
    {
        // Hunter yêu cầu kéo vào mục tiêu; click chỉ mở mô tả.
        if (target == null) return false;

        TurnState currentTurn = TurnManager.instance.currentTurn;
        DefaultOwner controller = caster.cardData.owner;

        if (controller != DefaultOwner.Player2)
        {
            Debug.Log("<color=red>Hunter: Súng săn là vũ khí độc quyền của phe Dân Làng!</color>");
            return false;
        }

        if (currentTurn != TurnState.Player2Turn)
        {
            Debug.Log("<color=yellow>Hunter: Chỉ được nổ súng trong lượt hành động của Dân Làng!</color>");
            return false;
        }

        if (!TurnManager.instance.HasEnoughHearts(skillCost))
        {
            Debug.Log($"<color=red>Hunter: Không đủ {skillCost} Tim để bóp cò!</color>");
            caster.GetComponent<CardInteraction>().StartCoroutine("ShakeCard");
            return false;
        }

        if (caster.hasUsedSkill)
        {
            Debug.Log("<color=orange>Hunter: Súng đang phải nạp đạn (Đã bắn vòng này)!</color>");
            return false;
        }

        CardInteraction targetInt = target.GetComponent<CardInteraction>();
        if (targetInt == null || !targetInt.isPlaced || target == caster)
        {
            Debug.Log("<color=red>Hunter: Mục tiêu không nằm trong tầm ngắm hợp lệ!</color>");
            return false;
        }

        
        TurnManager.instance.ConsumeHearts(skillCost);
        caster.hasUsedSkill = true;

        if (targetInt.isProtectedByPreacher)
        {
            // Khiên cứu mục tiêu nhưng vẫn tiêu hao tim và lượt bắn.
            ActionFeedback.Reject("Preacher blocked Hunter's shot. The shot still spent its hearts and can be used again after Sacrifice.");
            return true; 
        }

        Debug.Log($"<color=green>Đoàng! Hunter {caster.name} đã kết liễu {target.name}!</color>");
        TurnManager.instance.BanishCardToCemetery(targetInt);

        return true;
    }
}

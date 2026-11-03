using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "Skill_Witch", menuName = "Card Skills/Active/Witch Sweep")]
public class Skill_Witch : CardSkill
{
    public int skillCost = 5;

    public override bool CastActiveSkill(CardInstance caster, CardInstance target)
    {
        // Witch dùng bằng click; không yêu cầu mục tiêu kéo thả.
        if (target != null) 
        {
            Debug.Log("<color=red>Witch: Hãy Click vào lá bài, KHÔNG kéo thả vào mục tiêu!</color>");
            return false; 
        }

        if (TurnManager.instance == null) return false;

        TurnState currentTurn = TurnManager.instance.currentTurn;
        
        bool isValidPhase = (currentTurn == TurnState.Player1Turn || 
                             currentTurn == TurnState.Player2Turn || 
                             currentTurn == TurnState.SacrificePhase);

        if (!isValidPhase)
        {
            Debug.Log("<color=red>Witch: Chỉ được sử dụng kỹ năng trong lượt hành động hoặc lượt Vote!</color>");
            return false;
        }

        if (!TurnManager.instance.HasEnoughHearts(skillCost))
        {
            Debug.Log($"<color=red>Witch: Cần {skillCost} Tim để thi triển ma pháp!</color>");
            caster.GetComponent<CardInteraction>().StartCoroutine("ShakeCard");
            return false;
        }

        if (caster.hasUsedSkill)
        {
            Debug.Log("<color=orange>Witch: Kỹ năng đang trong thời gian hồi (Đã dùng trong vòng này)!</color>");
            return false;
        }

        SeatDropZone mySeat = TurnManager.instance.allSeats.Find(s => s.cardPos.GetComponentInChildren<CardInstance>() == caster);
        if (mySeat == null)
        {
            Debug.Log("<color=red>Witch: Phải nằm trên bàn cờ mới được dùng ma pháp!</color>");
            return false;
        }

        
        TurnManager.instance.ConsumeHearts(skillCost);
        caster.hasUsedSkill = true; 

        Debug.Log($"<color=magenta>Witch {caster.name} kích hoạt ma pháp dọn dẹp TOÀN BỘ bàn cờ. Quét mầm bệnh!</color>");

        bool foundAny = false;
        List<CardInteraction> cardsToBanish = new List<CardInteraction>();

        foreach (SeatDropZone seat in TurnManager.instance.allSeats)
        {
            CardInstance ci = seat.cardPos.GetComponentInChildren<CardInstance>();
            
            if (ci == caster) continue;
            
            if (ci != null && ci.isInfected)
            {
                cardsToBanish.Add(ci.GetComponent<CardInteraction>());
                foundAny = true;
            }
        }

        if (foundAny)
        {
            foreach (CardInteraction doomedCard in cardsToBanish)
            {
                Debug.Log($"<color=red>-> Ma pháp Witch đã trừng phạt: {doomedCard.name}!</color>");
                TurnManager.instance.BanishCardToCemetery(doomedCard);
            }
            TurnManager.instance.CheckGameOver();
        }
        else
        {
            Debug.Log($"<color=gray>Witch đã quét toàn bộ bàn cờ nhưng không phát hiện mầm bệnh nào.</color>");
        }

        return true;
    }
}
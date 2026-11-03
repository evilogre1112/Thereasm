using UnityEngine;

[CreateAssetMenu(fileName = "Skill_Invoker", menuName = "Card Skills/Active/Invoker Discover")]
public class Skill_Invoker : CardSkill
{
    public int skillCost = 0;

    public override bool CastActiveSkill(CardInstance caster, CardInstance target)
    {
        // Invoker mở bảng chọn bằng click; không nhận mục tiêu kéo thả.
        if (target != null) return false; 

        if (TurnManager.instance == null) return false;

        TurnState currentTurn = TurnManager.instance.currentTurn;
        DefaultOwner controller = caster.lastPlacedBy;

        bool isMyTurn = (TurnManager.instance.currentVotingPlayer == controller);
        if (!isMyTurn || currentTurn != TurnState.SacrificePhase)
        {
            Debug.Log($"<color=red>Invoker: Kỹ năng chỉ dùng được bởi người đang có quyền Vote!</color>");
            return false;
        }

        if (!TurnManager.instance.HasEnoughHearts(skillCost))
        {
            Debug.Log("<color=red>Invoker: Không đủ Tim để mở cổng địa ngục!</color>");
            return false;
        }

        if (caster.hasUsedSkill)
        {
            Debug.Log("<color=orange>Invoker: Kẻ gọi hồn cần nghỉ ngơi (Đã dùng vòng này)!</color>");
            return false;
        }

        // Dựa vào isNextToCemetery để hỗ trợ bố cục có ghế phụ.
        int seatIdx = TurnManager.instance.GetSeatIndex(caster.GetComponent<CardInteraction>());
        
        if (seatIdx >= 0 && seatIdx < TurnManager.instance.allSeats.Count)
        {
            SeatDropZone currentSeat = TurnManager.instance.allSeats[seatIdx];
            if (!currentSeat.isNextToCemetery)
            {
                Debug.Log("<color=red>Invoker: Hãy ngồi vào ghế sát Nghĩa Trang để gọi hồn!</color>");
                return false;
            }
        }
        else
        {
            return false;
        }

        if (TurnManager.instance.deadCardsData.Count <= 0)
        {
            Debug.Log("<color=orange>Invoker: Nghĩa trang không có linh hồn nào để gọi!</color>");
            return false;
        }

        // Chỉ tiêu hao lượt dùng khi chọn xong lá hồi sinh, không phải lúc mở bảng.

        if (!TurnManager.instance.TryManualActivateInvoker(caster)) return false;
        if (skillCost > 0)
        {
            TurnManager.instance.ConsumeHearts(skillCost);
        }
        
        Debug.Log($"<color=cyan>Invoker {caster.name} đang triệu hồi linh hồn...</color>");

        return true;
    }
}

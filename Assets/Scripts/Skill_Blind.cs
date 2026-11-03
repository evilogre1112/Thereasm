using UnityEngine;

[CreateAssetMenu(fileName = "Skill_Blind", menuName = "Card Skills/Passive/Blind")]
public class Skill_Blind : CardSkill
{
    [Header("Cấu hình Chỗ Ngồi")]
    [Tooltip("Bật nếu Blind chỉ được kiểm tra mộ khi ngồi ở ghế sát nghĩa trang")]
    public bool mustSitNearCemetery = false;

    // Kích hoạt khi Blind vừa được đặt vào ghế hợp lệ (hoặc khi Aura làm mới
    // mà Blind vẫn chưa sử dụng kỹ năng). hasUsedSkill ngăn quét lặp khi
    // RefreshAllAuras được gọi nhiều lần trong cùng một chu kỳ bỏ phiếu.
    public override void ApplyAura(CardInstance owner, int mySeatIndex)
    {
        var turns = TurnManager.instance;
        if (turns == null || turns.isGameOver || owner == null || owner.isDead ||
            owner.isFaceDown || owner.isSilenced || owner.hasUsedSkill)
            return;

        if (mySeatIndex < 0 || turns.allSeats == null || mySeatIndex >= turns.allSeats.Count)
            return;
        if (mustSitNearCemetery)
        {
            SeatDropZone seat = turns.allSeats[mySeatIndex];
            if (seat == null || !seat.isNextToCemetery) return;
        }

        // Một lần kích hoạt: kiểm tra đúng MỘT xác, chọn xác
        // chết gần nhất CHƯA ĐƯỢC BLIND NÀY kiểm tra. cemeteryOrder
        // là định danh của lần chết (kể cả lá hồi sinh rồi chết lại).
        CardInstance target = null;
        int newestOrder = 0;
        foreach (var corpse in FindObjectsByType<CardInstance>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (corpse == null || corpse.gameObject.scene != turns.gameObject.scene ||
                !corpse.isDead || corpse.cardData == null || corpse.cemeteryOrder <= 0 ||
                owner.HasBlindInspected(corpse.cemeteryOrder))
                continue;

            // Bỏ qua bản sao dùng để chọn lá hồi sinh trong bảng Discover.
            if (turns.discoverPanel != null &&
                corpse.transform.IsChildOf(turns.discoverPanel.transform))
                continue;

            if (corpse.cemeteryOrder > newestOrder)
            {
                newestOrder = corpse.cemeteryOrder;
                target = corpse;
            }
        }
        if (target == null) return;

        // Chỉ tiêu hao kỹ năng khi thực sự có lá hợp lệ để kiểm tra.
        // TurnManager đặt lại hasUsedSkill SAU KHI chốt/tính phiếu của chu kỳ.
        owner.hasUsedSkill = true;
        // Đánh dấu theo từng Blind, KHÔNG lưu trên ScriptableObject được chia sẻ.
        owner.MarkBlindInspected(newestOrder);

        bool isCur = turns.IsCur(target);
        bool wasInfected = target.wasInfectedWhenDied;
        target.isRevealedByBlind = true;
        SkillAudio.Emit(owner);

        if (!owner.isInfected)
        {
            // Bình thường: nói đúng thân phận và trạng thái nhiễm lúc chết.
            target.blindRevealAsCur = isCur;
            target.blindRevealAsInfected = wasInfected;
        }
        else if (isCur || wasInfected)
        {
            // Decay che giấu thân phận Cursermancer và trạng thái nhiễm thật.
            target.blindRevealAsCur = false;
            target.blindRevealAsInfected = false;
        }
        else
        {
            // Decay: lá vô tội bị báo sai là đã nhiễm bệnh.
            target.blindRevealAsCur = false;
            target.blindRevealAsInfected = true;
        }
    }
}

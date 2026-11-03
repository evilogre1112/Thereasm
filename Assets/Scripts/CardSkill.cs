using UnityEngine;
using System.Collections.Generic;

public enum SkillType { 
    Active, 
    Passive,
    Hybrid
}
public enum PassiveTriggerTime { 
    None, 
    OnDraw, 
    OnPlay, 
    InCemetery, 
    OnTurnStart 
}

public enum CardTag
{
    Aura,   // Tác dụng vào 2 lá bài 2 bên
    Direction, // Tác dụng theo bốn hướng quanh lá bài.
    Action, // Chỉ có thể sử dụng trong Action Phase
    Curse, // Chỉ dùng trong pha nguyền rủa.
    Sacrifice, // Chỉ dùng trong pha hiến tế.
    HotPoint, // Ai là người tương tác cuối cùng thì được sài kỹ năng
    Only, // Chỉ dùng một lần trước khi được hồi kỹ năng.
    Once, // Kỹ năng sài một lần mỗi khi đặt bài xuống
    BothEffect, // Hiệu ứng phụ thuộc thứ tự người dùng.
    Decay // Sau khi bị nhiễm bệnh kỹ năng biến thành kỹ năng khác 
}


public abstract class CardSkill : ScriptableObject
{
    [Header("Thông tin cơ bản")]
    public string skillName;
    public Sprite skillIcon;
    [TextArea] public string description;

    [Header("Phân loại kỹ năng")]
    public SkillType skillType;
    
    [Tooltip("Chỉ thiết lập khi skillType là Passive")]
    public PassiveTriggerTime triggerTime;

    public virtual int OverridePriority => 0;

    [Header("Hệ thống Tags")]
    public List<CardTag> tags = new List<CardTag>();

    public bool HasTag(CardTag tagToCheck)
    {
        return tags.Contains(tagToCheck);
    }

    public virtual void Activate(CardInstance owner)
    {
        Debug.LogWarning($"<color=yellow>{skillName}: Chưa code logic bị động!</color>");
    }

    // Trả về 'true' nếu dùng chiêu thành công, 'false' nếu trượt hoặc sai mục tiêu
    public virtual bool CastActiveSkill(CardInstance owner, CardInstance target)
    {
        Debug.LogWarning($"<color=yellow>{skillName}: Chưa code logic chủ động!</color>");
        return false;
    }

    
    [Tooltip("Được TurnManager gọi để quét các hiệu ứng tĩnh (như Elder khóa bài, Preacher tạo khiên)")]
    public virtual void ApplyAura(CardInstance owner, int mySeatIndex)
    {
    }

    
    [Tooltip("Cho phép thao túng mảng phiếu bầu TRƯỚC khi TurnManager tìm ra kẻ chết (như Trader chia phiếu)")]
    public virtual void OnBeforeVoteCalculate(CardInstance owner, int[] voteCounts, int mySeatIndex)
    {
    }

    [Tooltip("Cho phép lật kèo kết quả cuối cùng. Trả về 'true' nếu thẻ bài đã tự định đoạt người chết (như Sacrifier, Controller)")]
    public virtual bool OnOverrideVoteResult(CardInstance owner, int[] voteCounts, ref List<int> targetSeats, int mySeatIndex)
    {
        // False để TurnManager tiếp tục áp dụng luật số đông.
        return false; 
    }
    
    [Tooltip("Gọi khi lá bài VỪA ĐƯỢC ĐẶT XUỐNG một cái ghế trên bàn")]
    public virtual void OnPlaceOnBoard(CardInstance owner, int seatIndex)
    {
    }

    [Tooltip("Gọi khi lá bài BỊ NHẤC LÊN hoặc BỊ TIÊU DIỆT khỏi bàn")]
    public virtual void OnRemoveFromBoard(CardInstance owner, int seatIndex)
    {
    }

    public virtual void OnDeath(CardInstance owner, int seatIndex)
    {
        
    }
    // Gọi các hàm Try... để kiểm tra Saboteur trước khi chạy kỹ năng gốc.

    public bool TryCastActiveSkill(CardInstance owner, CardInstance target)
    {
        if (!TutorialController.Allow(TutorialAction.Skill, owner, target)) return false;
        if (owner == null || owner.isDead || owner.isFaceDown || owner.cardData == null ||
            owner.cardData.cardSkill != this || TurnManager.instance == null ||
            !TurnManager.instance.IsLivingBoardCard(owner) ||
            (target != null && !TurnManager.instance.IsLivingBoardCard(target)) ||
            TurnManager.instance.isGameOver || TurnManager.instance.isTallyingVotes)
            return ActionFeedback.Reject(ActionFeedback.SkillFailure(this, owner, target));
        if (owner.isSilenced)
        {
            Debug.Log($"<color=red>Thất bại: Thẻ bài {owner.name} đang bị Kẻ Phá Hoại câm lặng!</color>");
            
            CardInteraction interaction = owner.GetComponent<CardInteraction>();
            if (interaction != null) interaction.StartCoroutine("ShakeCard");
            
            return ActionFeedback.Reject(ActionFeedback.SkillFailure(this, owner, target));
        }
        TurnManager.instance.ClearLocalActionFeedback();
        bool cast = CastActiveSkill(owner, target);
        if (!cast) ActionFeedback.Reject(ActionFeedback.SkillFailure(this, owner, target));
        else VoteEffectFeed.Current?.Activated(owner,target);
        return cast;
    }

    public void TryOnPlaceOnBoard(CardInstance owner, int seatIndex)
    {
        if (owner.isSilenced)
        {
            Debug.Log($"<color=red>Kỹ năng bị động của {owner.name} bị vô hiệu hóa khi đặt xuống bàn!</color>");
            return;
        }
        OnPlaceOnBoard(owner, seatIndex);
    }

    public void TryOnBeforeVoteCalculate(CardInstance owner, int[] voteCounts, int mySeatIndex)
    {
        if (owner.isSilenced) return;
        OnBeforeVoteCalculate(owner, voteCounts, mySeatIndex);
    }
}

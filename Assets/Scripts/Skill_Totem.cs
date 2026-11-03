using UnityEngine;

[CreateAssetMenu(fileName = "Skill_Totem", menuName = "Card Skills/Passive/Totem")]
public class Skill_Totem : CardSkill
{
    // Totem thay đổi phí di chuyển, không có kỹ năng kích hoạt.
    public override void Activate(CardInstance owner)
    {
        Debug.Log($"Totem {owner.name} đang kích hoạt nội tại giảm chi phí.");
    }
}
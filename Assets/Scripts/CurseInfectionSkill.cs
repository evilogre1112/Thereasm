using UnityEngine;

[CreateAssetMenu(fileName = "New Curse Skill", menuName = "Cards/Skills/CurseInfection")]
public class CurseInfectionSkill : CardSkill
{
    public override void Activate(CardInstance owner)
    {
        Debug.Log($"Kích hoạt kỹ năng: {skillName} trên lá {owner.cardData.name}");
        
        
        if (TurnManager.instance != null)
        {
            TurnManager.instance.StartCurseSelection(owner);
        }
    }
}
using UnityEngine;
using UnityEngine.UI;

// Public phase plus this screen's perspective. Never derives identity from a card.
[DefaultExecutionOrder(650)]
public class PlayerStageHUD : MonoBehaviour
{
    public TurnManager turns;
    public Text factionLabel,stageLabel;
    public Image factionTint;
    public static string Faction(DefaultOwner owner)=>owner==DefaultOwner.Player1?"Cursermancer":"Village";
    void LateUpdate()
    {
        if(turns==null)return;
        var viewer=CardOwnershipView.Perspective;
        factionLabel.text=TutorialAcademyUI.L("YOUR FACTION: ","PHE CỦA BẠN: ")+Faction(viewer);
        if(factionTint!=null)factionTint.color=Color.white;
        string phase,actor="";
        if(turns.isGameOver)phase=TutorialAcademyUI.L("MATCH ENDED","VÁN CHƠI KẾT THÚC");
        else if(turns.isTallyingVotes)phase=TutorialAcademyUI.L("VOTE RESOLUTION","TỔNG KẾT PHIẾU");
        else if(turns.currentTurn==TurnState.SetupPhase)phase=TutorialAcademyUI.L("MATCH SETUP","CHUẨN BỊ VÁN");
        else if(turns.currentTurn==TurnState.CursePhase)phase=TutorialAcademyUI.L("CURSE RITUAL","NGUYỀN RỦA");
        else if(turns.currentTurn==TurnState.SacrificePhase)
        {phase=TutorialAcademyUI.L("SACRIFICE","HIẾN TẾ");actor=Faction(turns.currentVotingPlayer)+TutorialAcademyUI.L(" is voting"," đang bỏ phiếu");}
        else
        {phase=TutorialAcademyUI.L("ACTION","HÀNH ĐỘNG");actor=Faction(turns.currentTurn==TurnState.Player1Turn?DefaultOwner.Player1:DefaultOwner.Player2);}
        stageLabel.text=TutorialAcademyUI.L("PHASE: ","GIAI ĐOẠN: ")+phase+(actor.Length>0?" · "+actor:"");
    }
}

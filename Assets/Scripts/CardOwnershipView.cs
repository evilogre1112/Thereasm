using UnityEngine;
using UnityEngine.UI;

// Printed faction and public last placer only. Hidden roles/infection never determine ownership.
public class CardOwnershipView : MonoBehaviour
{
    public Text ownerLabel;
    public Image badgeBackground;
    public Outline frame;
    public Color mineColor=new Color32(232,202,145,255);
    public Color opponentColor=new Color32(201,138,116,255);
    public Color neutralColor=new Color32(190,179,162,255);
    private CardInstance card;
    void Awake(){card=GetComponent<CardInstance>();}
    public static DefaultOwner Perspective
    {
        get
        {
            if(OnlineSession.Active)return OnlineSession.LocalRole;
            if(TutorialWorkshop.Active)return TutorialController.Instance.workshop.Viewer;
            if(GameSettings.currentMode==GameMode.PlayerVsCurBot)return DefaultOwner.Player2;
            if(GameSettings.currentMode==GameMode.PlayerVsVillBot)return DefaultOwner.Player1;
            var tm=TurnManager.instance;
            return tm!=null&&(tm.currentTurn==TurnState.Player2Turn||tm.currentTurn==TurnState.SacrificePhase&&tm.currentVotingPlayer==DefaultOwner.Player2)?DefaultOwner.Player2:DefaultOwner.Player1;
        }
    }
    void LateUpdate(){Refresh();}
    public void Refresh()
    {
        if(card==null)card=GetComponent<CardInstance>();
        if(card==null||card.cardData==null)return;
        bool neutral=card.cardData.owner==DefaultOwner.Neutral;
        bool mine=card.cardData.owner==Perspective;
        Color color=neutral?neutralColor:mine?mineColor:opponentColor;
        string tag=neutral?"TRUNG LẬP":mine?"CỦA BẠN":"ĐỐI THỦ";
        var input=card.GetComponent<CardInteraction>();
        var onlineInput=card.GetComponent<OnlineCardInput>();
        // The guest previews the dropped card before the host's snapshot arrives.
        // While it is visually presented at a seat, display the printed faction
        // stamp immediately; on rejection/snapshot restore the real placement.
        bool onBoard=onlineInput!=null&&onlineInput.IsPresenting&&onlineInput.HasPlacementPreview
            ? onlineInput.PreviewIsOnBoard : input!=null&&input.isPlaced;
        if(ownerLabel!=null){ownerLabel.text=Localizer.T(tag);ownerLabel.color=color;ownerLabel.fontStyle=FontStyle.Bold;}
        if(badgeBackground!=null)
        {
            // The shared hand header identifies a whole fan; only board/dead/inspected cards need a stamp.
            badgeBackground.gameObject.SetActive(card.isDead||onBoard||input!=null&&input.isInspecting);
            badgeBackground.color=Color.white;
        }
        if(frame!=null)frame.effectColor=new Color(color.r,color.g,color.b,.6f);
    }
}

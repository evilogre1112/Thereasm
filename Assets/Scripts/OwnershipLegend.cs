using UnityEngine;
using UnityEngine.UI;

[ExecuteAlways]
public class OwnershipLegend : MonoBehaviour
{
    public Text curTab,villageTab,neutralTab,legend;
    [SerializeField] private DeckManager deck;
    void LateUpdate()
    {
        bool cur=CardOwnershipView.Perspective==DefaultOwner.Player1;
        if(deck==null)deck=DeckManager.instance;
        bool neutral=deck!=null&&(Application.isPlaying?deck.SelectedHand==DefaultOwner.Neutral:deck.neutralPanel!=null&&deck.neutralPanel.activeSelf);
        bool village=!neutral&&deck!=null&&(Application.isPlaying?deck.SelectedHand==DefaultOwner.Player2:deck.p2Panel!=null&&deck.p2Panel.activeSelf);
        Label(curTab,"Cursermancer",!neutral&&!village);
        Label(villageTab,"Dân làng",village);
        Label(neutralTab,"TRUNG LẬP",neutral);
        if(legend!=null)
        {
            bool mine=village?!cur:cur;
            legend.text="<b>"+Localizer.T(neutral?"BÀI TRUNG LẬP":mine?"TAY BÀI CỦA BẠN":"TAY BÀI ĐỐI THỦ")+"</b>\n<size=13>"+
                Localizer.T(neutral?"Cả hai phe có thể sử dụng":"Nhóm bài gốc · không phải vai trò bí mật")+"</size>";
            legend.color=neutral?new Color32(190,179,162,255):mine?new Color32(232,202,145,255):new Color32(201,138,116,255);
        }
    }
    private static void Label(Text text,string group,bool selected)
    {
        if(text==null)return;
        text.text=Localizer.T(group);
        text.fontStyle=selected?FontStyle.Bold:FontStyle.Normal;
        text.color=selected?new Color32(247,212,153,255):new Color32(202,181,164,255);
    }
}

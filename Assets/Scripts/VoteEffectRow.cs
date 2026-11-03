using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public class VoteEffectRow : MonoBehaviour
{
    public Text source, reason, changes;
    public Image icon, background;
    public Sprite ballotIcon, detectiveIcon, snooperIcon, gamblerIcon;
    public int RecordId { get; private set; }
    public void Bind(VoteEffectRecord record, int index)
    {
        RecordId = record.id;
        source.text = Localizer.T(record.source);
        reason.text = Localizer.T(record.reason);
        icon.sprite = record.kind == "Detective" ? detectiveIcon : record.kind == "Snooper" ? snooperIcon : record.kind == "Gambler" ? gamblerIcon : ballotIcon;
        bool verdict = record.kind == "Controller" || record.kind == "Sacrifier";
        bool selection = !record.tally && record.kind != "Detective";
        if (record.changes == null || record.changes.Length == 0) changes.text = Localizer.T("No vote count changed in this step.");
        else changes.text = string.Join(record.kind == "Base" ? "   ·   " : "\n",record.changes.Select(c =>
            Localizer.T(c.label)+(selection ? "  ·  "+Localizer.T("Selected target") : verdict ? "  ·  "+Localizer.T("Chosen for sacrifice") :
                "  <color="+(c.after>c.before?"#91DEBC":c.after<c.before?"#F0AF90":"#F3D99A")+">"+c.before+" → "+c.after+"</color> "+Localizer.T(record.kind == "Detective" ? "suspicion" : "votes"))));
        float height = Mathf.Max(114,48+Mathf.Max(reason.preferredHeight,changes.preferredHeight));
        var layout=GetComponent<LayoutElement>();if(layout!=null)layout.preferredHeight=height;
        reason.rectTransform.sizeDelta=new Vector2(reason.rectTransform.sizeDelta.x,height-46);
        changes.rectTransform.sizeDelta=new Vector2(changes.rectTransform.sizeDelta.x,height-44);
    }
    public void Emphasize(bool active)
    { if(background!=null)background.color = active ? new Color(.32f,.15f,.08f,.98f) : new Color(.13f,.035f,.045f,.96f); }
}

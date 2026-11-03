using TMPro;
using UnityEngine;
using UnityEngine.UI;

[ExecuteAlways,DefaultExecutionOrder(20000)]
public class LocalizedLabel : MonoBehaviour
{
    public Text label;
    public TMP_Text tmpLabel;
    [Tooltip("Original TMP material: keeps outline, glow and other text styling when the atlas changes.")]
    public Material originalTextMaterial;
    [TextArea(1,8)] public string sourceText;
    private string canonical,lastOutput;
    private int revision=-1;
    private bool initialized;
    private Material styledMaterial;
    void OnEnable(){initialized=false;revision=-1;}
    void LateUpdate(){Refresh();}
    public void Refresh()
    {
        if(label==null&&tmpLabel==null)return;
        string current=label!=null?label.text:tmpLabel.text;
        if(!initialized){canonical=string.IsNullOrEmpty(current)?sourceText:current;initialized=true;}
        else if(current!=lastOutput)canonical=current;
        if(revision==Localizer.Revision&&current==lastOutput)return;
        string translated=Localizer.T(canonical);
        var catalog=Localizer.Catalog;
        if(label!=null){if(catalog!=null&&catalog.readableFont!=null&&canonical!="THEREASM")label.font=catalog.readableFont;label.text=translated;}
        if(tmpLabel!=null)
        {
            if(catalog!=null&&catalog.readableTmpFont!=null)
            {
                if(originalTextMaterial==null)originalTextMaterial=tmpLabel.fontSharedMaterial;
                tmpLabel.font=catalog.readableTmpFont;
                if(styledMaterial==null&&originalTextMaterial!=null)
                {
                    styledMaterial=new Material(originalTextMaterial){name="Bilingual text - preserved style",hideFlags=HideFlags.HideAndDontSave};
                    var metrics=catalog.readableTmpFont.material;
                    styledMaterial.mainTexture=catalog.readableTmpFont.atlasTexture;
                    foreach(string property in new[]{"_TextureWidth","_TextureHeight","_GradientScale","_ScaleX","_ScaleY"})
                        if(styledMaterial.HasProperty(property)&&metrics.HasProperty(property))styledMaterial.SetFloat(property,metrics.GetFloat(property));
                }
                if(styledMaterial!=null)tmpLabel.fontSharedMaterial=styledMaterial;
            }
            tmpLabel.text=translated;
        }
        lastOutput=translated;revision=Localizer.Revision;
    }
    void OnDestroy()
    {
        if(styledMaterial==null)return;
        if(Application.isPlaying)Destroy(styledMaterial);else DestroyImmediate(styledMaterial);
    }
}

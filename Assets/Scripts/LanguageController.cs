using UnityEngine;
using UnityEngine.UI;

[ExecuteAlways,DefaultExecutionOrder(-1000)]
public class LanguageController : MonoBehaviour
{
    public GameLocalization catalog;
    public GameLanguage previewLanguage=GameLanguage.English;
    public Button englishButton,vietnameseButton;
    void Awake(){Localizer.Initialize(catalog,previewLanguage);}
    void OnEnable(){Localizer.Initialize(catalog,previewLanguage);}
    void LateUpdate()
    {
        if(!Application.isPlaying||Localizer.Catalog!=catalog)Localizer.Initialize(catalog,previewLanguage);
        UpdateButton(englishButton,Localizer.Language==GameLanguage.English);
        UpdateButton(vietnameseButton,Localizer.Language==GameLanguage.Vietnamese);
    }
    public void English(){Choose(GameLanguage.English);}
    public void Vietnamese(){Choose(GameLanguage.Vietnamese);}
    private static void UpdateButton(Button button,bool selected)
    {
        if(button==null)return;
        var colors=button.colors;colors.disabledColor=new Color(1,.85f,.55f,1);button.colors=colors;
        button.interactable=!selected;
        var label=button.GetComponentInChildren<Text>();if(label!=null)label.fontStyle=selected?FontStyle.Bold:FontStyle.Normal;
    }
    private void Choose(GameLanguage language)
    {
        previewLanguage=language;Localizer.SetLanguage(language);
        foreach(var label in FindObjectsByType<LocalizedLabel>(FindObjectsInactive.Include,FindObjectsSortMode.None))label.Refresh();
    }
}

// Run only in the isolated validation project.
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

[InitializeOnLoad]
public static class MenuModeValidation
{
    private const string Request="Thereasm.MenuModeValidation";
    private static int assertions;
    private static double began;
    [Serializable] private class Report { public bool passed; public int assertions; public string message; }
    static MenuModeValidation()
    {
        EditorApplication.playModeStateChanged += change =>
        {
            if(change==PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Request,false))
            { began=EditorApplication.timeSinceStartup; EditorApplication.update+=Check; }
        };
    }
    public static void Run()
    {
        SessionState.SetBool(Request,true);
        EditorSceneManager.OpenScene("Assets/Scenes/MenuScenes.unity");
        EditorApplication.isPlaying=true;
    }
    static void Expect(bool value,string message) {assertions++;if(!value)throw new Exception(message);}
    static GameObject Named(string name)
    {
        foreach(var item in UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include,FindObjectsSortMode.None))
            if(item.gameObject.scene.name=="MenuScenes"&&item.name==name)return item.gameObject;
        return null;
    }
    static void Check()
    {
        if(EditorApplication.timeSinceStartup-began<1) return;
        try
        {
            Canvas.ForceUpdateCanvases();
            var view=GameStartUI.Current;
            Expect(view!=null,"Missing menu view.");
            var names=new[]{"Mode — Phe Dân làng","Mode — Phe Cur","Mode — Online"};
            var methods=new[]{"PlayWithCurBot","PlayWithVillBot","OpenOnline"};
            var symbols=new[]{view.Theme.villagerModeIcon,view.Theme.curModeIcon,view.Theme.onlineModeIcon};
            int modes=0;
            foreach(var item in UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include,FindObjectsSortMode.None))
                if(item.gameObject.scene.name=="MenuScenes"&&item.name.StartsWith("Mode — ",StringComparison.Ordinal))modes++;
            Expect(modes==3,"Menu must contain exactly three mode cards.");
            for(int i=0;i<names.Length;i++)
            {
                var mode=Named(names[i]);Expect(mode!=null&&mode.activeInHierarchy,"Missing mode "+names[i]);
                Button button=null;Image symbol=null;
                foreach(var child in mode.GetComponentsInChildren<Button>(true))if(child.name=="Play Button")button=child;
                foreach(var child in mode.GetComponentsInChildren<Image>(true))if(child.name.StartsWith("Mode Symbol",StringComparison.Ordinal))symbol=child;
                Expect(button!=null&&button.interactable,"Mode button is unavailable.");
                Expect(button.onClick.GetPersistentMethodName(0)==methods[i],"Wrong mode action.");
                Expect(symbol!=null&&symbol.sprite!=null&&symbol.sprite==symbols[i],"Mode does not reference its generated icon.");
                Expect(symbol.preserveAspect,"Icon aspect ratio is not preserved.");
                var importer=AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(symbol.sprite)) as TextureImporter;
                Expect(importer!=null&&importer.textureType==TextureImporterType.Sprite&&importer.alphaIsTransparency,"Icon transparency/Sprite import is incorrect.");
                foreach(var label in mode.GetComponentsInChildren<Text>(true))
                    Expect(label.preferredHeight<=label.rectTransform.rect.height+1,"Clipped mode text: "+label.name);
            }
            Expect(Named("Mode — Hai người")==null,"Removed local two-player card still exists.");
            Expect(Named("Online Play Button")==null,"Duplicate online entry still exists.");
            Expect(typeof(MainMenu).GetMethod("PlaySolo")==null,"Local two-player entry API still exists.");
            Expect(Named("Tutorial Play Button")!=null,"Tutorial entry was lost.");
            Text roleTitle=null;
            foreach(var label in Named("Mode — Phe Cur").GetComponentsInChildren<Text>())
                if(label.name=="Mode Title")roleTitle=label;
            Expect(roleTitle!=null&&System.Text.RegularExpressions.Regex.IsMatch(roleTitle.text,@"\bCursermancer\b",System.Text.RegularExpressions.RegexOptions.IgnoreCase),"Role name must contain the full Cursermancer name: "+(roleTitle!=null?roleTitle.text:"<missing>"));
            foreach(var language in new[]{GameLanguage.English,GameLanguage.Vietnamese})
            {
                Localizer.SetLanguage(language,false);
                foreach(var label in UnityEngine.Object.FindObjectsByType<LocalizedLabel>(FindObjectsInactive.Include,FindObjectsSortMode.None))label.Refresh();
                Canvas.ForceUpdateCanvases();
                foreach(string name in names)
                    foreach(var label in Named(name).GetComponentsInChildren<Text>(true))
                        Expect(label.preferredHeight<=label.rectTransform.rect.height+1,"Clipped "+language+" mode text: "+label.name);
                foreach(var button in view.GetComponent<LanguageController>().GetComponentsInChildren<Button>(true))
                    if(button.name.Contains("Language"))
                        foreach(var label in button.GetComponentsInChildren<Text>())
                            Expect(label.preferredWidth<=label.rectTransform.rect.width+1,"Clipped language selector: "+label.text);
            }
            Localizer.SetLanguage(GameLanguage.English,false);
            var difficulty=Named("Difficulty Description").GetComponent<Text>();
            Expect(difficulty.preferredHeight<=difficulty.rectTransform.rect.height+1,"Full role name clips the difficulty description.");
            view.ApplyTheme();
            var title=Named("Game Title").GetComponent<Text>();
            title.cachedTextGenerator.Populate(title.text,title.GetGenerationSettings(title.rectTransform.rect.size));
            Expect(title.cachedTextGenerator.vertexCount>4,"The title is fully clipped.");
            var online=Named("Mode — Online").GetComponentInChildren<Button>();
            online.onClick.Invoke();
            Expect(view.OnlinePanel!=null&&view.OnlinePanel.activeInHierarchy,"Online mode does not open the lobby.");
            Finish(true,"Three mode cards, generated Sprite icons, title, text bounds and online lobby route passed.");
        }
        catch(Exception exception){Finish(false,exception.ToString());}
    }
    static void Finish(bool passed,string message)
    {
        EditorApplication.update-=Check;SessionState.SetBool(Request,false);
        File.WriteAllText(Path.Combine(Directory.GetParent(Application.dataPath).FullName,"MenuModeValidationResult.json"),
            JsonUtility.ToJson(new Report{passed=passed,assertions=assertions,message=message},true));
        Debug.Log("MENU VALIDATION: "+message);EditorApplication.Exit(passed?0:1);
    }
}

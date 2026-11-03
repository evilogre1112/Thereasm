using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;

// Explicit authoring in the isolated project; never executes automatically or inside the player.
public static class LocalizationAuthoring
{
    [Serializable] public class Row { public string key,source; }
    [Serializable] public class Sources { public Row[] rows; public string fontMissing; }
    public static void Export()
    {
        try
        {
            var rows=new List<Row>();
            foreach(string id in AssetDatabase.FindAssets("t:CardData",new[]{"Assets/ScriptableObjects"}))
            {
                var card=AssetDatabase.LoadAssetAtPath<CardData>(AssetDatabase.GUIDToAssetPath(id));
                rows.Add(new Row{key=card.name+".name",source=card.cardName});
                foreach(string field in new[]{"description","curDescription","infectedDescription","curInfectedDescription"})
                    rows.Add(new Row{key=card.name+"."+field,source=(string)typeof(CardData).GetField(field).GetValue(card)});
                for(int i=0;i<card.quotes.Count;i++)rows.Add(new Row{key=card.name+".quote."+i,source=card.quotes[i]});
            }
            var lesson=AssetDatabase.LoadAssetAtPath<TutorialLesson>("Assets/UI/Tutorial/FirstMatchLesson.asset");
            for(int i=0;i<lesson.steps.Length;i++)foreach(string field in new[]{"title","instruction","purpose","success"})
                rows.Add(new Row{key="tutorial."+i+"."+field,source=(string)typeof(TutorialStep).GetField(field).GetValue(lesson.steps[i])});
            rows.Add(new Row{key="tutorial.inspect",source=lesson.inspectDescription});
            foreach(string name in new[]{"MenuScenes","MainBoard","TutorialBoard"})
            {
                EditorSceneManager.OpenScene("Assets/Scenes/"+name+".unity");
                foreach(var text in UnityEngine.Object.FindObjectsByType<Text>(FindObjectsInactive.Include,FindObjectsSortMode.None))
                    if(!string.IsNullOrWhiteSpace(text.text))rows.Add(new Row{key=name+"."+text.name,source=text.text});
            }
            var body=AssetDatabase.LoadAssetAtPath<Font>("Assets/TextMesh Pro/Fonts/LiberationSans.ttf");
            string vietnamese="ĂăÂâĐđÊêÔôƠơƯưÁÀẢÃẠẮẰẲẴẶẤẦẨẪẬÉÈẺẼẸẾỀỂỄỆÍÌỈĨỊÓÒỎÕỌỐỒỔỖỘỚỜỞỠỢÚÙỦŨỤỨỪỬỮỰÝỲỶỸỴáàảãạắằẳẵặấầẩẫậéèẻẽẹếềểễệíìỉĩịóòỏõọốồổỗộớờởỡợúùủũụứừửữựýỳỷỹỵ";
            string missing=new string(vietnamese.Where(c=>!body.HasCharacter(c)).Distinct().ToArray());
            if(missing.Length>0)throw new Exception("Bundled font lacks Vietnamese: "+missing);
            string folder="Assets/UI/Localization";Directory.CreateDirectory(folder);AssetDatabase.Refresh();
            var font=TMP_FontAsset.CreateFontAsset(body,64,6,GlyphRenderMode.SDFAA,2048,2048,AtlasPopulationMode.Dynamic,false);
            File.WriteAllText(Path.Combine(Directory.GetParent(Application.dataPath).FullName,"LocalizationSources.json"),JsonUtility.ToJson(new Sources{rows=rows.ToArray(),fontMissing=missing},true));
            string corpus=new string((string.Concat(rows.Select(r=>r.source))+vietnamese+"ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789 ·—–…→!?.,:;+-/()[]#%<>\"' ").Distinct().Where(c=>!char.IsControl(c)).ToArray());
            font.TryAddCharacters(corpus,out string missingGlyphs);
            if(!string.IsNullOrEmpty(missingGlyphs))throw new Exception("Font atlas missing: "+missingGlyphs);
            font.name="Thereasm Bilingual SDF";font.atlasPopulationMode=AtlasPopulationMode.Static;
            AssetDatabase.CreateAsset(font,folder+"/Thereasm Bilingual SDF.asset");
            AssetDatabase.AddObjectToAsset(font.material,font);
            foreach(var texture in font.atlasTextures)AssetDatabase.AddObjectToAsset(texture,font);
            EditorUtility.SetDirty(font);AssetDatabase.SaveAssets();
            File.WriteAllText(Path.Combine(Directory.GetParent(Application.dataPath).FullName,"LocalizationSources.json"),JsonUtility.ToJson(new Sources{rows=rows.ToArray(),fontMissing=missing},true));
            Debug.Log("LOCALIZATION EXPORT: "+rows.Count+" sources; Vietnamese glyph coverage passed.");EditorApplication.Exit(0);
        }
        catch(Exception exception){Debug.LogException(exception);EditorApplication.Exit(1);}
    }
}

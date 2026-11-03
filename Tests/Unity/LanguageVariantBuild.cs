using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class LanguageVariantBuild
{
    [Serializable] class AssetSize {public string path;public long bytes;}
    [Serializable] class Report { public bool passed; public string english,vietnamese,message;public long englishBytes,vietnameseBytes;public AssetSize[] largestAssets; }
    public static void Build()
    {
        var catalog=AssetDatabase.LoadAssetAtPath<GameLocalization>("Assets/UI/Localization/GameLocalization.asset");
        string root=Directory.GetParent(Application.dataPath).FullName;
        string english=Path.Combine(root,"LanguageBuilds","Thereasm-English","Thereasm.exe");
        string vietnamese=Path.Combine(root,"LanguageBuilds","Thereasm-Vietnamese","Thereasm.exe");
        bool success=false;string message="";AssetSize[] largest=null;
        try
        {
            foreach(var language in new[]{GameLanguage.English,GameLanguage.Vietnamese})
            {
                catalog.defaultLanguage=language;EditorUtility.SetDirty(catalog);AssetDatabase.SaveAssets();
                string output=language==GameLanguage.English?english:vietnamese;Directory.CreateDirectory(Path.GetDirectoryName(output));
                var result=BuildPipeline.BuildPlayer(new BuildPlayerOptions{
                    scenes=new[]{"Assets/Scenes/MenuScenes.unity","Assets/Scenes/MainBoard.unity","Assets/Scenes/TutorialBoard.unity"},
                    locationPathName=output,target=BuildTarget.StandaloneWindows64,options=BuildOptions.CompressWithLz4HC,
                    extraScriptingDefines=new[]{"THEREASM_RELEASE"}
                });
                if(result.summary.result!=BuildResult.Succeeded)throw new Exception(language+" build failed: "+result.summary.result);
                largest=result.packedAssets.SelectMany(p=>p.contents).GroupBy(p=>p.sourceAssetPath)
                    .Select(g=>new AssetSize{path=g.Key,bytes=g.Sum(p=>(long)p.packedSize)}).OrderByDescending(p=>p.bytes).Take(24).ToArray();
            }
            success=true;message="Clean English and Vietnamese Windows builds completed; both contain the language selector.";
        }
        catch(Exception exception){message=exception.ToString();Debug.LogException(exception);}
        finally {catalog.defaultLanguage=GameLanguage.English;EditorUtility.SetDirty(catalog);AssetDatabase.SaveAssets();}
        long Size(string path)=>Directory.Exists(Path.GetDirectoryName(path))?Directory.GetFiles(Path.GetDirectoryName(path),"*",SearchOption.AllDirectories).Sum(p=>new FileInfo(p).Length):0;
        File.WriteAllText(Path.Combine(root,"LanguageVariantBuildResult.json"),JsonUtility.ToJson(new Report{passed=success,english=english,vietnamese=vietnamese,message=message,
            englishBytes=Size(english),vietnameseBytes=Size(vietnamese),largestAssets=largest},true));
        EditorApplication.Exit(success?0:1);
    }
}

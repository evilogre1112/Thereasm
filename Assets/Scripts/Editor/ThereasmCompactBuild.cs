using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class ThereasmCompactBuild
{
    [MenuItem("Thereasm/Build/English Windows — compact")]
    public static void English(){Build(GameLanguage.English);}
    [MenuItem("Thereasm/Build/Vietnamese Windows — compact")]
    public static void Vietnamese(){Build(GameLanguage.Vietnamese);}

    static void Build(GameLanguage language)
    {
        ValidateConfiguration();
        var catalog=AssetDatabase.LoadAssetAtPath<GameLocalization>("Assets/UI/Localization/GameLocalization.asset");
        var original=catalog.defaultLanguage;
        try
        {
            catalog.defaultLanguage=language;EditorUtility.SetDirty(catalog);AssetDatabase.SaveAssets();
            string output=Path.Combine(Directory.GetParent(Application.dataPath).FullName,"Builds","Thereasm-"+language,"Thereasm.exe");
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{
                scenes=EditorBuildSettings.scenes.Where(s=>s.enabled).Select(s=>s.path).ToArray(),
                locationPathName=output,target=BuildTarget.StandaloneWindows64,
                options=BuildOptions.CompressWithLz4HC,extraScriptingDefines=new[]{"THEREASM_RELEASE"}});
            if(report.summary.result!=BuildResult.Succeeded)throw new Exception("Compact build failed: "+report.summary.result);
            Debug.Log("Compact "+language+" build saved to "+output);
        }
        finally{catalog.defaultLanguage=original;EditorUtility.SetDirty(catalog);AssetDatabase.SaveAssets();}
    }

    public static void ValidateConfiguration()
    {
        if(AssetDatabase.LoadAssetAtPath<GameLocalization>("Assets/UI/Localization/GameLocalization.asset")==null)
            throw new Exception("Missing saved language catalog.");
        var scenes=EditorBuildSettings.scenes.Where(s=>s.enabled).Select(s=>s.path).ToArray();
        if(scenes.Length==0||scenes[0]!="Assets/Scenes/MenuScenes.unity")
            throw new Exception("The enabled build scenes must start with MenuScenes.");
        Debug.Log("Compact Windows build configuration is valid; uses LZ4HC and the saved language catalog.");
    }
}

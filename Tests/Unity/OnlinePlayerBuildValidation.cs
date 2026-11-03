using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class OnlinePlayerBuildValidation
{
    public static void Build()
    {
        string root=Directory.GetParent(Application.dataPath).FullName;
        string output=Path.Combine(root,"OnlinePlayerBuild","Thereasm.exe");
        Directory.CreateDirectory(Path.GetDirectoryName(output));
        var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions {
            scenes=new[]{"Assets/Scenes/MenuScenes.unity","Assets/Scenes/MainBoard.unity","Assets/Scenes/TutorialBoard.unity"},
            locationPathName=output,target=BuildTarget.StandaloneWindows64,options=BuildOptions.Development
        });
        File.WriteAllText(Path.Combine(root,"OnlinePlayerBuildResult.json"),JsonUtility.ToJson(new Result {
            passed=report.summary.result==BuildResult.Succeeded,output=output,message=report.summary.result.ToString()
        },true));
        EditorApplication.Exit(report.summary.result==BuildResult.Succeeded?0:1);
    }
    [Serializable] class Result { public bool passed;public string output,message; }
}

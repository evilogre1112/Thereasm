using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class NeutralTournamentBuild
{
    public static void Build()
    {
        string root=Directory.GetParent(Application.dataPath).FullName;
        string output=Path.Combine(root,"NeutralTournamentPlayer","NeutralTournament.exe");
        Directory.CreateDirectory(Path.GetDirectoryName(output));
        var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{
            scenes=new[]{"Assets/Scenes/MainBoard.unity"},locationPathName=output,target=BuildTarget.StandaloneWindows64,
            options=BuildOptions.None,extraScriptingDefines=new[]{"THEREASM_RELEASE","THEREASM_BOT_BENCHMARK"}});
        File.WriteAllText(Path.Combine(root,"NeutralTournamentBuild.json"),JsonUtility.ToJson(new Result{
            passed=report.summary.result==BuildResult.Succeeded,output=output,result=report.summary.result.ToString()},true));
        EditorApplication.Exit(report.summary.result==BuildResult.Succeeded?0:1);
    }
    [Serializable] class Result {public bool passed;public string output,result;}
}

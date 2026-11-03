using System;
using UnityEngine;

[Serializable] public class TutorialCopy
{
    [TextArea(1,6)] public string english,vietnamese;
    public string Text => Localizer.Vietnamese?vietnamese:english;
}
public enum LessonAction { Read,Inspect,CloseInspect,Place,Return,BlockedReturn,BlockedPlace,Skill,Vote,EndTurn,Tally,Purge,OpenNotes,PlaceNote,CloseNotes,OpenCemetery,CloseCemetery,Curse,OpenInvoker,Resurrect,Quiz,Swap,BlockedSkill,View,Wager }
public enum LessonSetupKind { Place,Return,Hearts,Phase,Lock,Infect,Cur,Kill,ResetSkill,Vote,Curse,ClosePanels,Cast,ResetTable }
[Serializable] public class LessonSetup
{
    public LessonSetupKind kind;
    public int number,value,target;
}
[Serializable] public class LessonBeat
{
    public TutorialCopy title,instruction,purpose,success;
    public LessonAction action;
    public int source,target,seat=-1;
    public LessonSetup[] prepare=Array.Empty<LessonSetup>();
    public TutorialCopy choiceA,choiceB;
    [Tooltip("Optional observation copy selected by the player's real Gambler decision.")]
    public TutorialCopy betResult,passResult;
    public int answer;
}
[CreateAssetMenu(menuName="Thereasm/Tutorial/Short exercise")]
public class TutorialExercise : ScriptableObject
{
    public string id;
    public TutorialCopy title,summary;
    public CardData[] cards;
    public LessonSetup[] setup;
    public LessonBeat[] beats;
}
[Serializable] public class TutorialChapter
{
    public string id;
    public TutorialCopy title,summary;
    public Sprite icon;
    public TutorialExercise[] lessons;
}
public static class TutorialJourney
{
    public static TutorialExercise Selected;
    public static bool OpenOnReturn;
    // Stable across versions and both language choices; never reset on menu return.
    public const string WelcomeKey="thereasm.first-launch-answered";
    public static string CompletionKey(string id)=>"thereasm.tutorial.done."+id;
    public static bool Completed(TutorialExercise exercise)=>exercise!=null&&PlayerPrefs.GetInt(CompletionKey(exercise.id),0)==1;
}

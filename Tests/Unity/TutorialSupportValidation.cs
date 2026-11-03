using System;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object=UnityEngine.Object;

[InitializeOnLoad]
public static class TutorialSupportValidation
{
    const string Request="Thereasm.TutorialSupport";
    static int assertions;
    [Serializable] class Report {public bool passed;public int assertions;public string message;}
    static TutorialSupportValidation(){EditorApplication.playModeStateChanged+=s=>{if(s==PlayModeStateChange.EnteredPlayMode&&SessionState.GetBool(Request,false))EditorApplication.delayCall+=Check;};}
    public static void Run(){SessionState.SetBool(Request,true);EditorSceneManager.OpenScene("Assets/Scenes/MenuScenes.unity");EditorApplication.isPlaying=true;}
    static void Expect(bool value,string message){assertions++;if(!value)throw new Exception(message);}
    static void Check()
    {
        try
        {
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/NotePanel.prefab");
            var talking=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/CardUI.prefab"),GameStartUI.Current.transform).GetComponent<CardDialogue>();
            talking.bubbleObject.SetActive(true);
            var note=Object.Instantiate(prefab,GameStartUI.Current.transform);note.SetActive(true);
            Expect(!talking.bubbleObject.activeSelf,"Opening notes did not immediately hide an existing speech bubble.");
            Expect(DialogueModalGuard.IsOpen&&note.GetComponent<Canvas>().sortingOrder>talking.bubbleObject.GetComponentInParent<Canvas>().sortingOrder,"Notebook does not sort above card speech.");
            var row=note.GetComponentsInChildren<Transform>(true).First(t=>t.name=="Panel");
            var slots=row.GetComponentsInChildren<NoteSlot>(true);
            Expect(slots.Length==7,"Notebook is missing a seventh drop slot.");
            foreach(var language in new[]{GameLanguage.English,GameLanguage.Vietnamese})
            {
                Localizer.SetLanguage(language,false);
                foreach(var label in note.GetComponentsInChildren<LocalizedLabel>(true))label.Refresh();Canvas.ForceUpdateCanvases();
                foreach(var label in note.GetComponentsInChildren<TMP_Text>(true))
                {label.ForceMeshUpdate();Expect(!label.isTextOverflowing,"Clipped notebook text: "+label.name+" / "+language);}
            }
            for(int index=0;index<7;index++)
            {
                var caption=row.GetComponentsInChildren<Text>(true).First(t=>t.name=="Note Seat "+(index+1)+" Number");
                Expect(Mathf.Abs(caption.rectTransform.TransformPoint(caption.rectTransform.rect.center).x-slots[index].transform.position.x)<1,"Notebook seat number is not aligned with its drop slot: "+(index+1));
            }
            NoteBlock Token(string number)=>note.GetComponentsInChildren<NoteBlock>(true).First(b=>b.GetComponentInChildren<TMP_Text>(true).text==number);
            var seven=Token("7");var eight=Token("8");
            var start=typeof(NoteBlock).GetMethod("Start",BindingFlags.Instance|BindingFlags.NonPublic);start.Invoke(seven,null);start.Invoke(eight,null);
            void Drop(NoteBlock token)
            {
                var pointer=new PointerEventData(EventSystem.current){pointerDrag=token.gameObject,button=PointerEventData.InputButton.Left};
                token.OnBeginDrag(pointer);Expect(token.transform.parent==note.transform,"Dragged notebook token left its modal canvas layer.");slots[6].OnDrop(pointer);token.OnEndDrag(pointer);
            }
            Drop(seven);Expect(seven.transform.parent==slots[6].transform,"Seat 7 cannot accept the number 7 token.");
            Expect(seven.GetComponent<RectTransform>().anchoredPosition3D==Vector3.zero,"Token 7 is off-centre.");
            Drop(eight);Expect(eight.transform.parent==slots[6].transform&&seven.transform.parent==seven.homeParent,"Replacing seat 7's token does not return the old token to its pool.");
            foreach(string scene in new[]{"Assets/Scenes/MainBoard.unity","Assets/Scenes/TutorialBoard.unity"})
                Expect(File.ReadAllText(scene).Contains("Note Seat 7"),"Authored scene is missing seat 7: "+scene);
            Finish(true,"Seven notebook seats, centred drag/drop/replacement and bilingual text fit passed.");
        }
        catch(Exception error){Finish(false,error.ToString());}
    }
    static void Finish(bool passed,string message)
    {
        SessionState.SetBool(Request,false);File.WriteAllText(Path.Combine(Directory.GetParent(Application.dataPath).FullName,"TutorialSupportValidationResult.json"),JsonUtility.ToJson(new Report{passed=passed,assertions=assertions,message=message},true));
        Debug.Log(message);EditorApplication.Exit(passed?0:1);
    }
}

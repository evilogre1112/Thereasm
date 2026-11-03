using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object=UnityEngine.Object;

[InitializeOnLoad]
public static class TutorialAcademyValidation
{
    const string Request="Thereasm.AcademyValidation";
    const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    static double started,changed;
    static int phase,lessonIndex,lastIndex=-1,assertions;
    static bool acted;
    static int gamblerRuns,wagerChoices;
    static TutorialExercise[] lessons;
    static string context="menu";
    static List<string> visited=new List<string>();
    [Serializable] class Pref{public string key;public int value;public bool exists;}
    [Serializable] class Prefs{public Pref[] rows;}
    [Serializable] class Report{public bool passed;public int assertions,lessonsCompleted;public string message;public string[] lessons;}
    static TutorialAcademyValidation()
    {
        EditorApplication.playModeStateChanged+=s=>{if(s==PlayModeStateChange.EnteredPlayMode&&SessionState.GetBool(Request,false))
        {started=changed=EditorApplication.timeSinceStartup;EditorApplication.update+=Tick;}};
    }
    public static void Run()
    {
        var course=AssetDatabase.LoadAssetAtPath<TutorialCourse>("Assets/UI/Tutorial/TutorialCourse.asset");
        var keys=course.chapters.SelectMany(c=>c.lessons).Select(l=>TutorialJourney.CompletionKey(l.id)).Concat(new[]{TutorialJourney.WelcomeKey}).ToArray();
        SessionState.SetString(Request+"Prefs",JsonUtility.ToJson(new Prefs{rows=keys.Select(k=>new Pref{key=k,value=PlayerPrefs.GetInt(k),exists=PlayerPrefs.HasKey(k)}).ToArray()}));
        PlayerPrefs.DeleteKey(TutorialJourney.WelcomeKey);PlayerPrefs.Save();
        SessionState.SetBool(Request,true);EditorSceneManager.OpenScene("Assets/Scenes/MenuScenes.unity");EditorApplication.isPlaying=true;
    }
    public static void RunLayoutOnly(){SessionState.SetBool(Request+"LayoutOnly",true);Run();}
    public static void RunInteractionOnly(){SessionState.SetBool(Request+"InteractionOnly",true);Run();}
    static void Expect(bool value,string message){assertions++;if(!value)throw new Exception(context+": "+message);}
    static void Late(object target)=>target.GetType().GetMethod("LateUpdate",Private).Invoke(target,null);
    static void TextFits(Text text)
    {if(text!=null&&text.gameObject.activeInHierarchy)Expect(text.preferredHeight<=text.rectTransform.rect.height+1,"Clipped text "+text.name+" / "+Localizer.Language+" / "+text.text+" ("+text.preferredHeight+">"+text.rectTransform.rect.height+")");}
    static void MenuLayout(TutorialAcademyUI academy)
    {
        foreach(var lang in new[]{GameLanguage.English,GameLanguage.Vietnamese})
        {
            Localizer.SetLanguage(lang,false);Late(academy);Canvas.ForceUpdateCanvases();
            foreach(var text in new[]{academy.welcomeTitle,academy.welcomeBody,academy.newLabel,academy.returningLabel,academy.academyTitle,academy.academyHint,academy.backLabel})TextFits(text);
            foreach(var tile in academy.chapterTiles){TextFits(tile.title);TextFits(tile.summary);TextFits(tile.progress);}
            foreach(var tile in academy.lessonTiles){TextFits(tile.title);TextFits(tile.summary);TextFits(tile.status);}
        }
    }
    static void Tick()
    {
        try
        {
            if(EditorApplication.timeSinceStartup-started>900)throw new Exception("Academy validation timeout: "+context);
            if(phase<2)
            {
                var academy=Object.FindFirstObjectByType<TutorialAcademyUI>();if(academy==null||EditorApplication.timeSinceStartup-changed<.8)return;
                if(phase==0)
                {
                    Expect(academy.welcomePanel.activeSelf,"First launch question is absent.");MenuLayout(academy);Capture("Welcome-Vietnamese");
                    Localizer.SetLanguage(GameLanguage.English,false);Late(academy);Capture("Welcome-English");
                    Expect(PlayerPrefs.HasKey(TutorialJourney.WelcomeKey),"First launch display is not persisted.");
                    academy.ReturningPlayer();Expect(!academy.IsOpen,"Experienced choice does not open the regular menu.");
                    phase=1;changed=EditorApplication.timeSinceStartup;SceneManager.LoadScene("MenuScenes");return;
                }
                Expect(!academy.welcomePanel.activeSelf,"First launch question repeats on menu return.");
                var course=academy.course;Expect(course.chapters.Length==4,"Expected four subjects.");lessons=course.chapters.SelectMany(c=>c.lessons).ToArray();
                Expect(lessons.Length==22&&lessons.Select(l=>l.id).Distinct().Count()==22,"Missing or duplicate combined lessons.");
                Expect(course.chapters[0].lessons.Length==1&&course.chapters[1].lessons.Length==1,"Controls/energy are not combined.");
                Expect(lessons.All(l=>l.beats.All(b=>b.action!=LessonAction.Quiz&&b.action!=LessonAction.View)),"A quiz or manual view switch is still present.");
                Expect(!lessons.Any(l=>l.id.EndsWith("-decay")||l.id.EndsWith("-counter")),"Separate variant lesson remains.");
                foreach(var chapter in course.chapters)Expect(chapter.icon!=null&&chapter.lessons.Length>0,"Subject icon or lessons missing.");
                academy.Open();MenuLayout(academy);Capture("Subjects-Vietnamese");Localizer.SetLanguage(GameLanguage.English,false);Late(academy);Capture("Subjects-English");
                for(int i=0;i<4;i++){academy.ShowChapter(i);MenuLayout(academy);}
                Capture("Strategy-lessons-Vietnamese");academy.ShowChapter(2);Late(academy);Capture("Card-lessons-Vietnamese");
                foreach(var exercise in lessons)
                {
                    Expect(exercise.cards.All(c=>c!=null)&&exercise.cards.Select(c=>c.cardNumber).Distinct().Count()==exercise.cards.Length,"Invalid exercise card set: "+exercise.id);
                    Expect(exercise.beats.Length>=2&&exercise.beats.All(b=>!string.IsNullOrWhiteSpace(b.title.english)&&!string.IsNullOrWhiteSpace(b.title.vietnamese)),"Missing lesson title: "+exercise.id);
                    foreach(var copyBeat in exercise.beats)
                    {
                        var copy=copyBeat.action==LessonAction.Read?copyBeat.purpose:copyBeat.instruction;
                        Expect(!string.IsNullOrWhiteSpace(copy.english)&&!string.IsNullOrWhiteSpace(copy.vietnamese),"Missing meaningful bilingual instruction: "+exercise.id);
                        if(copyBeat.action==LessonAction.EndTurn)Expect(!copyBeat.purpose.english.Contains("hearts")&&!copyBeat.purpose.vietnamese.Contains("tim"),"Unrelated energy lecture on a handoff: "+exercise.id);
                    }
                }
                if(SessionState.GetBool(Request+"LayoutOnly",false))lessons=new[]{lessons[0]};
                academy.welcomePanel.SetActive(true);academy.NewPlayer();Expect(TutorialJourney.Selected==lessons[0],"New player does not route to the first short lesson.");
                if(SessionState.GetBool(Request+"InteractionOnly",false))
                {lessons=lessons.Where(l=>l.id=="card-gambler"||l.id=="strategy-notes").ToArray();TutorialJourney.Selected=lessons[0];}
                phase=2;changed=EditorApplication.timeSinceStartup;return;
            }
            if(phase==3)
            {
                var academy=Object.FindFirstObjectByType<TutorialAcademyUI>();if(academy==null||EditorApplication.timeSinceStartup-changed<.8)return;
                Expect(!academy.welcomePanel.activeSelf&&academy.academyPanel.activeSelf,"Finishing a lesson does not return to subjects, or repeats onboarding.");
                TutorialJourney.Selected=null;phase=4;changed=EditorApplication.timeSinceStartup;SceneManager.LoadScene("TutorialBoard");return;
            }
            var work=Object.FindFirstObjectByType<TutorialWorkshop>();if(work==null||!work.Running||work.Card(10)==null)return;
            if(phase==4)
            {
                if(EditorApplication.timeSinceStartup-changed<.8)return;
                Expect(work.Exercise==work.previewExercise&&work.Exercise.id=="controls-first-night","Direct scene Play does not start the saved preview exercise.");
                Expect(!work.notesButton.gameObject.activeInHierarchy&&!work.legacyPractice.activeSelf,"Hidden workshop controls cover the introductory Continue button.");
                Finish(true,"First-launch persistence, 22 combined lessons, no quiz, automatic turn perspectives, refusal feedback, modal bounds and bilingual layout passed.");return;
            }
            if(work.Complete)
            {
                if(work.Exercise.id=="card-gambler"&&gamblerRuns++==0)
                {
                    wagerChoices=0;lastIndex=-1;acted=false;
                    TutorialJourney.Selected=work.Exercise;SceneManager.LoadScene("TutorialBoard");return;
                }
                Expect(TutorialJourney.Completed(work.Exercise),"Lesson completion was not saved.");visited.Add(work.Exercise.id);lessonIndex++;lastIndex=-1;acted=false;
                if(lessonIndex==lessons.Length){phase=3;changed=EditorApplication.timeSinceStartup;work.Exit();return;}
                TutorialJourney.Selected=lessons[lessonIndex];SceneManager.LoadScene("TutorialBoard");return;
            }
            context=work.Exercise.id+" / "+work.Index+" / "+work.Beat.action;
            if(lastIndex!=work.Index){lastIndex=work.Index;acted=false;changed=EditorApplication.timeSinceStartup;Debug.Log("ACADEMY STEP: "+context);}
            if(EditorApplication.timeSinceStartup-changed>35)throw new Exception("Exercise stuck at "+context+" / phase="+TurnManager.instance.currentTurn+" / voter="+TurnManager.instance.currentVotingPlayer+" / feedback="+TurnManager.instance.LocalActionFeedback);
            if(work.AwaitingRefusal)
            {
                if(EditorApplication.timeSinceStartup-changed<1)return;
                Expect(work.continueButton.gameObject.activeInHierarchy,"Refusal has no acknowledgement.");
                Expect(work.purpose.text==work.Beat.success.Text,"Refusal reason was not shown after attempting.");
                Capture("Refusal-"+work.Exercise.id+"-"+work.Index);
                work.Continue();return;
            }
            if(acted||EditorApplication.timeSinceStartup-changed<.28)return;
            var beat=work.Beat;
            foreach(var lang in new[]{GameLanguage.English,GameLanguage.Vietnamese})
            {
                Localizer.SetLanguage(lang,false);Late(work);Canvas.ForceUpdateCanvases();
                foreach(var text in new[]{work.progress,work.title,work.instruction,work.purpose,work.feedback,work.header,work.headerHint,work.continueLabel,work.choiceLabel,work.quizFirstLabel,work.quizQuestion})TextFits(text);
                var hud=Object.FindFirstObjectByType<PlayerStageHUD>();Expect(hud!=null,"Missing faction/phase HUD.");Late(hud);TextFits(hud.factionLabel);TextFits(hud.stageLabel);
                TextFits(work.passWagerLabel);
                if(beat.action==LessonAction.EndTurn||beat.action==LessonAction.Tally)
                    Expect(work.instruction.text.Contains(work.endControl.GetComponentInChildren<Text>(true).text),"Instruction does not name the visible button.");
            }
            Expect(!work.quizPanel.activeInHierarchy,"Quiz panel is visible.");
            Expect(work.Viewer==work.Actor&&CardOwnershipView.Perspective==work.Actor,"Perspective does not follow the real turn.");
            Expect(PlayerInformation.LocalTruth==(work.Actor==DefaultOwner.Player1),"Truth is visible to the wrong side.");
            Expect(!Object.FindObjectsByType<Transform>(FindObjectsInactive.Include,FindObjectsSortMode.None).Any(t=>t.name.StartsWith("Cursermancer-only infection hint")||t.name.StartsWith("Workshop Cursermancer viewpoint")||t.name.StartsWith("Workshop Village viewpoint")),"Removed labels or manual view controls still exist.");
            var cemetery=TurnManager.instance.cemeteryView;
            if(TurnManager.instance.notePanel.activeSelf)
            {
                var window=TurnManager.instance.notePanel.transform.Find("NoteWindow") as RectTransform;
                Expect(window!=null&&Bounds(window).xMin>Bounds(work.title.rectTransform).xMax,"Notebook overlaps the lesson sidebar.");
                var tokens=TurnManager.instance.notePanel.GetComponentsInChildren<NoteBlock>(true);
                var values=tokens.Select(n=>n.GetComponentInChildren<TMPro.TMP_Text>(true).text.Trim()).ToArray();
                Expect(tokens.Length==16&&values.Distinct().Count()==16,"Notebook has missing or duplicate number tokens.");
                Expect(tokens.All(n=>n.gameObject.activeSelf)&&Enumerable.Range(1,16).All(n=>values.Contains(n.ToString())),"Notebook token 12 or another number is unavailable.");
            }
            if(cemetery.gameObject.activeInHierarchy)
            {
                var modal=(RectTransform)cemetery.transform;
                Expect(Bounds(modal).xMin>Bounds(work.title.rectTransform).xMax,"Cemetery overlaps the lesson sidebar.");
                Expect(Bounds(modal).Contains(Bounds(cemetery.content).min)&&Bounds(modal).Contains(Bounds(cemetery.content).max),"Cemetery grid exceeds modal bounds.");
                Capture("Cemetery-"+work.Exercise.id+"-"+work.Index);
            }
            if(work.directionArrow.gameObject.activeSelf)
            {
                Vector3 delta=work.targetFrame.position-work.sourceFrame.position;
                Expect(Vector3.Dot(work.directionArrow.right,delta.normalized)>.999f,"Arrow is offset from its real target direction.");
            }
            if(work.Exercise.id=="controls-first-night"&&work.Index==0)Capture("Short-lesson-Vietnamese");
            if(SessionState.GetBool(Request+"LayoutOnly",false)&&beat.action==LessonAction.Read)Capture("Stage-"+TurnManager.instance.currentTurn+"-"+work.Index);
            if(work.Exercise.id=="energy-empty"&&beat.action==LessonAction.BlockedPlace)Capture("Energy-refusal-exercise");
            if(beat.action==LessonAction.Read&&(work.Exercise.id=="card-snooper"||work.Exercise.id=="card-blind"||work.Exercise.id=="card-witch"||work.Exercise.id=="card-gambler"))Capture(work.Exercise.id+"-"+work.Index+"-"+work.Viewer);
            if(beat.action==LessonAction.Wager)Capture("Gambler-choice-"+gamblerRuns+"-"+work.Index);
            if(work.Exercise.id=="strategy-notes"&&beat.action==LessonAction.PlaceNote)Capture("Seven-seat-notebook");
            var turns=TurnManager.instance;var source=work.Card(beat.source);var input=source!=null?source.GetComponent<CardInteraction>():null;
            acted=true;
            switch(beat.action)
            {
                case LessonAction.Read:work.Continue();break;
                case LessonAction.Wager:
                    Expect(work.Viewer==DefaultOwner.Player2&&turns.currentVotingPlayer==DefaultOwner.Player2,"Gambler asks the learner to act as the scripted opponent.");
                    Expect(beat.prepare.Length==0,"Opponent state changed during the learner's decision.");
                    bool bet=(wagerChoices++ + gamblerRuns)%2==0;
                    if(bet)DragCard(source,work.Card(beat.target),PointerEventData.InputButton.Right);
                    else{Expect(work.passWagerButton.gameObject.activeInHierarchy&&work.passWagerButton.interactable,"No real Pass action.");Click(work.passWagerButton);}
                    break;
                case LessonAction.Quiz:
                    int index=work.Index;work.Answer(1-beat.answer);Expect(work.Index==index,"Wrong answer advances lesson.");Late(work);TextFits(work.feedback);work.Answer(beat.answer);break;
                case LessonAction.Inspect:case LessonAction.CloseInspect:
                    Expect(input.gameObject.activeInHierarchy,"Inspection card was hidden by a hand switch.");
                    input.OnPointerClick(new PointerEventData(EventSystem.current){button=PointerEventData.InputButton.Right});break;
                case LessonAction.Place:case LessonAction.BlockedPlace:input.PlaceOnSeat(turns.allSeats[beat.seat].cardPos);break;
                case LessonAction.Return:input.ReturnToHand();break;
                case LessonAction.BlockedReturn:
                    input.OnBeginDrag(new PointerEventData(EventSystem.current){button=PointerEventData.InputButton.Left,pointerDrag=input.gameObject});
                    if(string.IsNullOrEmpty(turns.LocalActionFeedback))input.ReturnToHand();break;
                case LessonAction.Skill:case LessonAction.BlockedSkill:case LessonAction.OpenInvoker:
                    source.cardData.cardSkill.TryCastActiveSkill(source,work.Card(beat.target));break;
                case LessonAction.Vote:
                    DragCard(source,turns.GetBoardCard(beat.seat),PointerEventData.InputButton.Left);
                    Expect(turns.GetVoteTarget(source)==beat.seat,"Pointer drag did not record a ballot.");break;
                case LessonAction.EndTurn:case LessonAction.Tally:Click(work.endControl.GetComponent<Button>());break;
                case LessonAction.Purge:turns.ExecuteCheck();break;
                case LessonAction.OpenNotes:case LessonAction.CloseNotes:
                    Expect(work.notesButton.gameObject.activeInHierarchy&&work.notesButton.interactable,"Notebook lesson button is not actually reachable.");Click(work.notesButton);break;
                case LessonAction.PlaceNote:
                    var block=turns.notePanel.GetComponentsInChildren<NoteBlock>(true).First(n=>n.GetComponentInChildren<TMPro.TMP_Text>(true).text.Trim()=="12");
                    Expect(work.noteSlots.Length==7&&block!=null,"Notebook does not expose seven seats and draggable notes.");
                    DragNote(block,work.noteSlots[beat.seat]);break;
                case LessonAction.OpenCemetery:case LessonAction.CloseCemetery:turns.ToggleCemeteryView();break;
                case LessonAction.Curse:turns.StartCurseSelection(source);turns.ApplyCurse(beat.seat);break;
                case LessonAction.Resurrect:turns.ResolveInvokerSelection(turns.deadCardsData.FindIndex(c=>c.cardNumber==beat.target));break;
                case LessonAction.Swap:input.SwapWith(work.Card(beat.target).GetComponent<CardInteraction>(),turns.allSeats[beat.seat].cardPos);break;
            }
            if(beat.action==LessonAction.BlockedReturn||beat.action==LessonAction.BlockedPlace||beat.action==LessonAction.BlockedSkill)
                Expect(work.RefusalAnimating,"Refused card has no visible reaction.");
        }
        catch(Exception e){Finish(false,e.ToString());}
    }
    static Vector2 ScreenCenter(Component component)
    {
        Canvas.ForceUpdateCanvases();
        var rect=component.transform as RectTransform;var canvas=component.GetComponentInParent<Canvas>().rootCanvas;
        return RectTransformUtility.WorldToScreenPoint(canvas.renderMode==RenderMode.ScreenSpaceOverlay?null:canvas.worldCamera,rect.TransformPoint(rect.rect.center));
    }
    static RaycastResult Hit(PointerEventData data)
    {
        Canvas.ForceUpdateCanvases();var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(data,hits);
        Expect(hits.Count>0,"Pointer hits no UI at "+data.position);return hits[0];
    }
    static void Click(Button button)
    {
        var data=new PointerEventData(EventSystem.current){position=ScreenCenter(button),button=PointerEventData.InputButton.Left};
        var hit=Hit(data);Expect(ExecuteEvents.GetEventHandler<IPointerClickHandler>(hit.gameObject)==button.gameObject,"Button is covered by "+hit.gameObject.name);
        ExecuteEvents.Execute(button.gameObject,data,ExecuteEvents.pointerDownHandler);
        ExecuteEvents.Execute(button.gameObject,data,ExecuteEvents.pointerUpHandler);
        ExecuteEvents.Execute(button.gameObject,data,ExecuteEvents.pointerClickHandler);
    }
    static void DragCard(CardInstance source,CardInstance target,PointerEventData.InputButton button)
    {
        var input=source.GetComponent<CardInteraction>();
        var data=new PointerEventData(EventSystem.current){position=ScreenCenter(source.cardImage),button=button,pointerDrag=input.gameObject};
        data.pressPosition=data.position;data.pointerPressRaycast=Hit(data);
        Expect(ExecuteEvents.GetEventHandler<IDragHandler>(data.pointerPressRaycast.gameObject)==input.gameObject,"Card cannot receive drag; pointer hits "+data.pointerPressRaycast.gameObject.name);
        ExecuteEvents.Execute(input.gameObject,data,ExecuteEvents.initializePotentialDrag);
        ExecuteEvents.Execute(input.gameObject,data,ExecuteEvents.beginDragHandler);
        Expect(input.canDrag,"Card refused BeginDrag: "+TurnManager.instance.LocalActionFeedback);
        data.position=ScreenCenter(target.cardImage);
        ExecuteEvents.Execute(input.gameObject,data,ExecuteEvents.dragHandler);
        ExecuteEvents.Execute(input.gameObject,data,ExecuteEvents.endDragHandler);
        Expect(!CardInteraction.isAnyCardDragging,"Pointer did not release the shared drag state.");
    }
    static void DragNote(NoteBlock block,NoteSlot slot)
    {
        var data=new PointerEventData(EventSystem.current){position=ScreenCenter(block),button=PointerEventData.InputButton.Left,pointerDrag=block.gameObject};
        var hit=Hit(data);Expect(ExecuteEvents.GetEventHandler<IDragHandler>(hit.gameObject)==block.gameObject,"Number 12 is not draggable.");
        ExecuteEvents.Execute(block.gameObject,data,ExecuteEvents.beginDragHandler);data.position=ScreenCenter(slot);
        ExecuteEvents.Execute(block.gameObject,data,ExecuteEvents.dragHandler);hit=Hit(data);
        Expect(ExecuteEvents.GetEventHandler<IDropHandler>(hit.gameObject)==slot.gameObject,"Notebook slot is covered by "+hit.gameObject.name);
        ExecuteEvents.ExecuteHierarchy(hit.gameObject,data,ExecuteEvents.dropHandler);
        ExecuteEvents.Execute(block.gameObject,data,ExecuteEvents.endDragHandler);
        Expect(block.transform.parent==slot.transform,"Number 12 did not land in its notebook seat.");
    }
    static Rect Bounds(RectTransform rect)
    {
        var points=new Vector3[4];rect.GetWorldCorners(points);
        return Rect.MinMaxRect(points.Min(p=>p.x),points.Min(p=>p.y),points.Max(p=>p.x),points.Max(p=>p.y));
    }
    static void Capture(string name)
    {
        var go=new GameObject("Test capture camera");var camera=go.AddComponent<Camera>();camera.enabled=false;
        camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.03f,.015f,.015f);camera.orthographic=true;camera.transform.position=new Vector3(0,0,-1000);camera.nearClipPlane=.1f;camera.farClipPlane=2000;
        var target=new RenderTexture(1600,900,24);camera.targetTexture=target;
        var roots=Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None).Where(c=>c.isRootCanvas).ToArray();
        var modes=roots.Select(c=>c.renderMode).ToArray();var cameras=roots.Select(c=>c.worldCamera).ToArray();
        for(int i=0;i<roots.Length;i++){roots[i].renderMode=RenderMode.ScreenSpaceCamera;roots[i].worldCamera=camera;roots[i].planeDistance=100;}
        Canvas.ForceUpdateCanvases();camera.Render();var image=new Texture2D(1600,900,TextureFormat.RGBA32,false);RenderTexture.active=target;
        image.ReadPixels(new Rect(0,0,1600,900),0,0);image.Apply();var dir=Path.Combine(Directory.GetParent(Application.dataPath).FullName,"TutorialAcademyPreview");Directory.CreateDirectory(dir);File.WriteAllBytes(Path.Combine(dir,name+".png"),image.EncodeToPNG());
        RenderTexture.active=null;for(int i=0;i<roots.Length;i++){roots[i].renderMode=modes[i];roots[i].worldCamera=cameras[i];}
        Object.DestroyImmediate(image);Object.DestroyImmediate(target);Object.DestroyImmediate(go);Canvas.ForceUpdateCanvases();
    }
    static void Finish(bool passed,string message)
    {
        EditorApplication.update-=Tick;SessionState.SetBool(Request,false);Time.timeScale=1;
        var prefs=JsonUtility.FromJson<Prefs>(SessionState.GetString(Request+"Prefs","{}"));
        if(prefs?.rows!=null)foreach(var row in prefs.rows){if(row.exists)PlayerPrefs.SetInt(row.key,row.value);else PlayerPrefs.DeleteKey(row.key);}PlayerPrefs.Save();
        bool layout=SessionState.GetBool(Request+"LayoutOnly",false);SessionState.SetBool(Request+"LayoutOnly",false);
        bool interaction=SessionState.GetBool(Request+"InteractionOnly",false);SessionState.SetBool(Request+"InteractionOnly",false);
        if(layout&&passed)message="Updated faction/phase frame, real phase explanations and full controls exercise passed in both languages.";
        if(interaction&&passed)message="Gambler wagers and ballots passed all four choice paths through real pointer handlers/raycasts; notebook bounds and token 12 drag/drop passed.";
        File.WriteAllText(Path.Combine(Directory.GetParent(Application.dataPath).FullName,interaction?"TutorialInteractionValidationResult.json":layout?"TutorialLayoutValidationResult.json":"TutorialAcademyValidationResult.json"),JsonUtility.ToJson(new Report{passed=passed,assertions=assertions,lessonsCompleted=visited.Count,message=message,lessons=visited.ToArray()},true));
        Debug.Log("ACADEMY RESULT: "+message);EditorApplication.Exit(passed?0:1);
    }
}

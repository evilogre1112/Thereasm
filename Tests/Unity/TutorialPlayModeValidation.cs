// Copied into an isolated validation project's Assets/Editor by Run-TutorialValidation.ps1.
// Exercises gameplay, raycasts and pointer handlers via automated batch runner.
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;

[InitializeOnLoad]
public static class TutorialPlayModeValidation
{
    const string Request = "Thereasm.TutorialValidation";
    private static int previous = -1, assertions;
    private static double stageStarted, began;
    private static bool acted, released;
    private static readonly List<string> visited = new List<string>();
    [Serializable] private class Report { public bool passed; public string message; public int assertions; public string[] stages; }
    static TutorialPlayModeValidation()
    {
        EditorApplication.playModeStateChanged += change =>
        {
            if (change == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Request,false))
            { began = EditorApplication.timeSinceStartup; EditorApplication.update += Tick; }
        };
    }
    public static void Run()
    {
        SessionState.SetBool(Request,true);
        EditorSceneManager.OpenScene("Assets/Scenes/TutorialBoard.unity");
        EditorApplication.isPlaying = true;
    }
    static void Expect(bool condition, string message)
    { assertions++; if (!condition) throw new Exception(message); }
    static CardInstance Card(int number)
    {
        foreach (var card in UnityEngine.Object.FindObjectsByType<CardInstance>(FindObjectsInactive.Include,FindObjectsSortMode.None))
            if (card.cardData != null && card.cardData.cardNumber == number) return card;
        throw new Exception("Missing card " + number);
    }
    static CardInteraction Interaction(int number) => Card(number).GetComponent<CardInteraction>();
    static bool Ready(TutorialController guide) => (bool)typeof(TutorialController).GetField("ready",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(guide);
    static void DragCardToSeat(int number,int seat)
    {
        Canvas.ForceUpdateCanvases();
        var card=Interaction(number);var rect=card.GetComponent<RectTransform>();
        Expect(card.gameObject.activeInHierarchy,"Tutorial source card is hidden: "+number);
        var data=new PointerEventData(EventSystem.current){button=PointerEventData.InputButton.Left,pointerDrag=card.gameObject,
            position=RectTransformUtility.WorldToScreenPoint(null,rect.TransformPoint(rect.rect.center))};
        var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(data,hits);
        Expect(hits.Count>0&&hits[0].gameObject.GetComponentInParent<CardInstance>()==Card(number),"Tutorial drag source is covered: "+number);
        card.OnInitializePotentialDrag(data);Expect(data.pointerDrag!=null,"Required drag was rejected: "+number);
        card.OnBeginDrag(data);Expect(card.canDrag,"Required card cannot be picked up: "+number);
        data.position=RectTransformUtility.WorldToScreenPoint(null,TurnManager.instance.allSeats[seat].cardPos.position);
        card.OnDrag(data);TurnManager.instance.allSeats[seat].OnDrop(data);card.OnEndDrag(data);
        Expect(TurnManager.instance.allSeats[seat].cardPos.GetComponentInChildren<CardInstance>()==Card(number),"Pointer drag did not place card: "+number);
    }
    static void ClickEndTurn(TurnManager tm)
    {
        var button=tm.btnEndTurn.GetComponent<UnityEngine.UI.Button>();
        var rect=button.GetComponent<RectTransform>();
        Expect(rect.parent.name.Contains("Hand Dock"),"End Turn is not beside the hand.");
        var data=new PointerEventData(EventSystem.current){button=PointerEventData.InputButton.Left,
            position=RectTransformUtility.WorldToScreenPoint(null,rect.TransformPoint(rect.rect.center))};
        var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(data,hits);
        Expect(hits.Count>0&&hits[0].gameObject.GetComponentInParent<UnityEngine.UI.Button>()==button,"End Turn is covered or outside its click target.");
        ExecuteEvents.Execute(button.gameObject,data,ExecuteEvents.pointerDownHandler);
        ExecuteEvents.Execute(button.gameObject,data,ExecuteEvents.pointerUpHandler);
        ExecuteEvents.Execute(button.gameObject,data,ExecuteEvents.pointerClickHandler);
        Expect(button.GetComponent<ButtonPressFeedback>().flash.color.a>.2f,"End Turn click did not show confirmation feedback.");
    }
    static void CheckPointerCoordinates()
    {
        var cursor=UnityEngine.Object.FindFirstObjectByType<CustomCusor>();
        var root=cursor.GetComponentInParent<Canvas>().rootCanvas;
        var mode=root.renderMode;var oldCamera=root.worldCamera;float plane=root.planeDistance;
        var camera=new GameObject("Pointer coordinate validation camera").AddComponent<Camera>();
        camera.orthographic=true;camera.transform.position=new Vector3(0,0,-1000);camera.nearClipPlane=.1f;camera.farClipPlane=2000;
        try
        {
            foreach(var renderMode in new[]{RenderMode.ScreenSpaceOverlay,RenderMode.ScreenSpaceCamera})
            {
                root.renderMode=renderMode;root.worldCamera=renderMode==RenderMode.ScreenSpaceCamera?camera:null;root.planeDistance=100;
                Canvas.ForceUpdateCanvases();var eventCamera=renderMode==RenderMode.ScreenSpaceOverlay?null:camera;
                TargetingArrow.instance.Show(Vector3.zero);
                var target=(RectTransform)typeof(TargetingArrow).GetField("targetRect",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(TargetingArrow.instance);
                foreach(var factor in new[]{new Vector2(.2f,.3f),new Vector2(.5f,.5f),new Vector2(.82f,.72f)})
                {
                    Vector2 screen=new Vector2(Screen.width*factor.x,Screen.height*factor.y);
                    TargetingArrow.instance.UpdateArrow(screen);cursor.MoveTo(screen);
                    Expect(Vector2.Distance(RectTransformUtility.WorldToScreenPoint(eventCamera,target.position),screen)<.5f,"Target cursor offset in "+renderMode);
                    Expect(Vector2.Distance(RectTransformUtility.WorldToScreenPoint(eventCamera,cursor.transform.position),screen)<.5f,"Custom cursor offset in "+renderMode);
                }
                TargetingArrow.instance.Hide();
            }
        }
        finally{root.renderMode=mode;root.worldCamera=oldCamera;root.planeDistance=plane;UnityEngine.Object.DestroyImmediate(camera.gameObject);Canvas.ForceUpdateCanvases();}
    }
    static void ClickCard(int number, PointerEventData.InputButton button)
    {
        Canvas.ForceUpdateCanvases();
        var card=Card(number); var rect=card.GetComponent<RectTransform>();
        var data=new PointerEventData(EventSystem.current) {button=button,
            position=RectTransformUtility.WorldToScreenPoint(null,rect.TransformPoint(rect.rect.center)), pointerDrag=card.gameObject};
        var hits=new List<RaycastResult>(); EventSystem.current.RaycastAll(data,hits);
        Expect(hits.Count>0,"No UI raycast at card "+number);
        var hit=hits[0].gameObject.GetComponentInParent<CardInstance>();
        Expect(hit==card,"Card "+number+" is covered by "+hits[0].gameObject.name);
        Interaction(number).OnInitializePotentialDrag(data);
        Expect(data.pointerDrag==null,"Click-only lesson still enters drag processing.");
        ExecuteEvents.ExecuteHierarchy(hits[0].gameObject,data,ExecuteEvents.pointerClickHandler);
    }
    static void CheckHand(Transform hand)
    {
        var rectangles=new List<RectTransform>();
        foreach (Transform child in hand) if (child.GetComponent<CardInstance>()!=null) rectangles.Add(child.GetComponent<RectTransform>());
        rectangles.Sort((a,b)=>a.anchoredPosition.x.CompareTo(b.anchoredPosition.x));
        for(int i=1;i<rectangles.Count;i++)
        {
            var a=rectangles[i-1];var b=rectangles[i];
            Expect(b.anchoredPosition.x-a.anchoredPosition.x>(a.rect.width+b.rect.width)*.5f,"Tutorial cards overlap.");
        }
    }
    static void Tick()
    {
        try
        {
            double now=EditorApplication.timeSinceStartup;
            if(now-began>120) throw new Exception("Tutorial validation timed out.");
            var guide=TutorialController.Instance;
            if(guide==null||!Ready(guide)) return;
            var tm=TurnManager.instance; var stage=guide.Stage;
            if((int)stage!=previous)
            {
                previous=(int)stage; stageStarted=now; acted=released=false;
                visited.Add(stage.ToString()); Debug.Log("TUTORIAL CHECK: "+stage);
            }
            if(now-stageStarted>18) throw new Exception("Tutorial stuck at "+stage);
            if(now-stageStarted<.7) return; // Wait for actual hand animations and controls to settle.
            if(stage==TutorialStage.Reveal)
            {
                Expect(Card(1).cardImage.sprite==Card(1).cardData.cursemancerArtwork,"Cursermancer perspective is not automatic.");
                Expect(!UnityEngine.Object.FindFirstObjectByType<HoldToReveal>(FindObjectsInactive.Include).gameObject.activeInHierarchy,"Removed reveal button remains visible.");
                guide.Continue();
                return;
            }
            if(acted) return;
            var savedLanguage=Localizer.Language;
            foreach(var language in new[]{GameLanguage.English,GameLanguage.Vietnamese})
            {
                Localizer.SetLanguage(language,false);
                foreach(var label in UnityEngine.Object.FindObjectsByType<LocalizedLabel>(FindObjectsInactive.Include,FindObjectsSortMode.None))label.Refresh();
                Canvas.ForceUpdateCanvases();
                foreach(string field in new[]{"progress","title","instruction","purpose","feedback","header","headerHint","continueLabel"})
                {
                    var label=(UnityEngine.UI.Text)typeof(TutorialController).GetField(field,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(guide);
                    if(label!=null&&label.gameObject.activeInHierarchy)
                        Expect(label.preferredHeight<=label.rectTransform.rect.height+1,"Clipped "+language+" tutorial text at "+stage+": "+field);
                }
                var reference=(UnityEngine.UI.Text)typeof(TutorialController).GetField("heartGuideText",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(guide);
                if(reference.gameObject.activeInHierarchy)
                    Expect(reference.preferredHeight<=reference.rectTransform.rect.height+1,"Heart reference text clips: "+language+" / "+stage);
                typeof(TutorialController).GetMethod("LateUpdate",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(guide,null);
                var arrow=(RectTransform)typeof(TutorialController).GetField("directionArrow",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(guide);
                if(stage==TutorialStage.BudgetOpponentIntro||stage==TutorialStage.BudgetRefill||stage==TutorialStage.BudgetSwapIntro)
                    Expect(!arrow.gameObject.activeSelf,"A reading/continue step displays a stale drag arrow.");
                if(arrow.gameObject.activeSelf)
                {
                    var source=(RectTransform)typeof(TutorialController).GetField("sourceFrame",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(guide);
                    var target=(RectTransform)typeof(TutorialController).GetField("targetFrame",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(guide);
                    Expect(source.gameObject.activeInHierarchy&&target.gameObject.activeInHierarchy,"Arrow has an invisible endpoint.");
                    Vector3 delta=target.position-source.position;
                    Expect(Vector3.Dot(arrow.right,delta.normalized)>.999f,"Tutorial arrow points away from the required target.");
                    float along=Vector3.Dot(arrow.position-source.position,delta)/delta.sqrMagnitude;
                    Expect(along>.35f&&along<.65f,"Tutorial arrow is not on the source-to-target path.");
                    Expect(Vector3.Cross(arrow.position-source.position,delta).magnitude<delta.magnitude*.1f,"Tutorial arrow is offset from its target path.");
                }
                var sourceCaption=(UnityEngine.UI.Text)typeof(TutorialController).GetField("sourceActionLabel",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(guide);
                var targetCaption=(UnityEngine.UI.Text)typeof(TutorialController).GetField("targetActionLabel",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(guide);
                if(sourceCaption.gameObject.activeInHierarchy && targetCaption.gameObject.activeInHierarchy)
                {
                    var first=RectTransformUtility.CalculateRelativeRectTransformBounds(guide.transform,sourceCaption.rectTransform);
                    var second=RectTransformUtility.CalculateRelativeRectTransformBounds(guide.transform,targetCaption.rectTransform);
                    Expect(!(first.min.x<second.max.x && first.max.x>second.min.x && first.min.y<second.max.y && first.max.y>second.min.y),"Tutorial action captions overlap: "+stage);
                }
                var spotlight=UnityEngine.Object.FindFirstObjectByType<TutorialSpotlight>();
                Expect(spotlight!=null&&spotlight.masks.Length>=15,"Tutorial spotlight masks are missing.");
                foreach(var mask in spotlight.masks)
                {
                    Expect(!mask.GetComponent<UnityEngine.UI.Image>().raycastTarget,"Tutorial shade intercepts the player's mouse.");
                    if(!mask.gameObject.activeSelf)continue;
                    foreach(string field in new[]{"sourceFrame","targetFrame"})
                    {
                        var frame=(RectTransform)typeof(TutorialController).GetField(field,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(guide);
                        if(frame.gameObject.activeSelf)Expect(!mask.rect.Contains(mask.InverseTransformPoint(frame.position)),"Tutorial spotlight covers the required target.");
                    }
                }
                TutorialPresentationCapture.Capture(guide);
            }
            Localizer.SetLanguage(savedLanguage,false);
            foreach(var label in UnityEngine.Object.FindObjectsByType<LocalizedLabel>(FindObjectsInactive.Include,FindObjectsSortMode.None))label.Refresh();
            acted=true;
            switch(stage)
            {
                case TutorialStage.Welcome:
                    Expect(!tm.cemeteryViewPanel.activeSelf,"Cemetery overlay covers startup.");
                    CheckHand(DeckManager.instance.player1Hand); guide.Continue(); break;
                case TutorialStage.ChooseCur: ClickCard(1,PointerEventData.InputButton.Left); break;
                case TutorialStage.OpenVillageHand: GameStartUI.Current.ShowVillageHand(); break;
                case TutorialStage.ChooseHiddenCur:
                    CheckHand(DeckManager.instance.player2Hand); ClickCard(11,PointerEventData.InputButton.Left); break;
                case TutorialStage.StartMatch: tm.StartRealGame(); break;
                case TutorialStage.Inspect:
                    ClickCard(10,PointerEventData.InputButton.Right);
                    Expect(Interaction(10).isInspecting,"Right-click did not inspect card 10.");
                    GameStartUI.Current.ShowRules();
                    Expect(!Interaction(10).isInspecting && CardInteraction.currentlyInspectedCard==null,"Pause did not close inspection.");
                    Expect(!CanvasDimmer.instance.GetComponent<UnityEngine.CanvasGroup>().blocksRaycasts,"Inspection dimmer still intercepts input while paused.");
                    GameStartUI.Current.CloseRules();
                    Expect(Time.timeScale>0 && !GameStartUI.IsBlockingInput,"Resume did not release pause.");
                    ClickCard(10,PointerEventData.InputButton.Right);
                    Expect(Interaction(10).isInspecting,"Card cannot be interacted with after inspection / pause / resume."); break;
                case TutorialStage.CloseInspect: ClickCard(10,PointerEventData.InputButton.Right); break;
                case TutorialStage.PlaceVillager:
                    Interaction(10).PlaceOnSeat(tm.allSeats[1].cardPos);
                    Expect(tm.p2Hearts==5&&!Interaction(10).isPlaced,"Wrong tutorial drop changed the board or spent energy.");
                    Interaction(10).PlaceOnSeat(tm.allSeats[0].cardPos);
                    Expect(tm.p2Hearts==3,"First placement cost is wrong."); break;
                case TutorialStage.PlaceHunter:
                    Interaction(9).PlaceOnSeat(tm.allSeats[1].cardPos);
                    Expect(tm.p2Hearts==1,"Second placement cost is wrong."); break;
                case TutorialStage.EndVillageTurn: case TutorialStage.EndCurTurn:
                case TutorialStage.EndCurse: case TutorialStage.ConfirmVotes: ClickEndTurn(tm); break;
                case TutorialStage.PlaceCur: Interaction(1).PlaceOnSeat(tm.allSeats[2].cardPos); break;
                case TutorialStage.Shoot:
                    CheckPointerCoordinates();
                    Expect(!Card(9).cardData.cardSkill.TryCastActiveSkill(Card(9),Card(9)),"Wrong skill target accepted.");
                    Expect(Card(9).cardData.cardSkill.TryCastActiveSkill(Card(9),Card(1)),"Hunter skill rejected correct target."); break;
                case TutorialStage.OpenCemetery: case TutorialStage.CloseCemetery: tm.ToggleCemeteryView(); break;
                case TutorialStage.RitualIntro: case TutorialStage.OpponentExample: guide.Continue(); break;
                case TutorialStage.Curse:
                    tm.StartCurseSelection(Card(11));tm.ApplyCurse(0);
                    Expect(!Card(10).isInfected,"Wrong curse target was accepted.");tm.ApplyCurse(1);break;
                case TutorialStage.VoteVillager:
                    Expect(tm.p2Hearts==0,"Sacrifice unexpectedly refilled hearts instead of carrying the action budget.");
                    tm.StartVoteSelection(Card(10));tm.ApplyVote(3);break;
                case TutorialStage.VoteHunter: tm.StartVoteSelection(Card(9));tm.ApplyVote(3);break;
                case TutorialStage.VoteHiddenCur: tm.StartVoteSelection(Card(11));tm.ApplyVote(3);break;
                case TutorialStage.Tally: break;
                case TutorialStage.Purge: tm.ExecuteCheck();break;
                case TutorialStage.HeartIntro: case TutorialStage.BudgetOpponentIntro:
                case TutorialStage.BudgetRefill: case TutorialStage.BudgetSwapIntro:
                case TutorialStage.BudgetReference: case TutorialStage.BudgetSwapReference: case TutorialStage.BudgetSkills: guide.Continue();break;
                case TutorialStage.BudgetPlace: Interaction(12).PlaceOnSeat(tm.allSeats[0].cardPos);break;
                case TutorialStage.BudgetMove: Interaction(12).PlaceOnSeat(tm.allSeats[1].cardPos);break;
                case TutorialStage.BudgetReturn: Interaction(12).ReturnToHand();break;
                case TutorialStage.BudgetOpponentPlace:
                    Expect(DeckManager.instance.SelectedHand==DefaultOwner.Player1,"Step 32 opened the wrong hand after the turn refill.");
                    DragCardToSeat(3,0);break;
                case TutorialStage.BudgetOpponentMove: DragCardToSeat(3,1);break;
                case TutorialStage.BudgetSwap: Interaction(12).SwapWith(Interaction(3),tm.allSeats[1].cardPos);break;
                case TutorialStage.Complete:
                    Expect(visited.Count==TutorialRules.StageCount,"The tutorial skipped stages.");
                    Expect(Card(9).isDead&&tm.p2Checks==1,"Final purge state is incorrect.");
                    Finish(true,"All 40 tutorial stages passed, including pause/resume inspection and real heart costs/refunds.");break;
            }
        }
        catch(Exception exception) {Finish(false,exception.ToString());}
    }
    static void Finish(bool passed,string message)
    {
        EditorApplication.update-=Tick;SessionState.SetBool(Request,false);
        File.WriteAllText(Path.Combine(Directory.GetParent(Application.dataPath).FullName,"TutorialValidationResult.json"),JsonUtility.ToJson(new Report {
            passed=passed,message=message,assertions=assertions,stages=visited.ToArray()},true));
        Debug.Log("TUTORIAL VALIDATION: "+message);
        EditorApplication.Exit(passed?0:1);
    }
}

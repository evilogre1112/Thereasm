using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object=UnityEngine.Object;

// Actual MainBoard, current prefab, custom art materials and normal inspection.
[InitializeOnLoad]
public static class BoardPresentationValidation
{
    const string Request="Thereasm.BoardPresentationValidation";
    const BindingFlags Private=BindingFlags.NonPublic|BindingFlags.Instance;
    static TurnManager turns;static DeckManager deck;static GameBoardLayout layout;
    static CardInstance inspected;static GameObject detachedBubble;
    static int phase,assertions;static double due,started;
    static readonly List<string> checks=new List<string>();
    static Matrix4x4 hudMatrix;static RectTransform hud;
    [Serializable] class Report{public bool passed;public int assertions;public string message;public string[] checks;}
    static BoardPresentationValidation()
    {
        EditorApplication.playModeStateChanged+=s=>{if(s==PlayModeStateChange.EnteredPlayMode&&SessionState.GetBool(Request,false))
        {started=EditorApplication.timeSinceStartup;due=started+1;EditorApplication.update+=Tick;}};
    }
    public static void Run()
    {
        SessionState.SetBool(Request,true);GameSettings.currentMode=GameMode.PvP;
        EditorSceneManager.OpenScene("Assets/Scenes/MainBoard.unity");EditorApplication.isPlaying=true;
    }
    static void Expect(bool value,string message){assertions++;if(!value)throw new Exception(message);}
    static void Call(object target,string method)=>target.GetType().GetMethod(method,Private).Invoke(target,null);
    static void Field(object target,string name,object value)=>target.GetType().GetField(name,Private).SetValue(target,value);
    static void Capture(string name)=>typeof(TutorialAcademyValidation).GetMethod("Capture",BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,new object[]{"Board-review-"+name});
    static CardInstance[] Cards()=>Object.FindObjectsByType<CardInstance>(FindObjectsInactive.Include,FindObjectsSortMode.None)
        .Where(c=>c.gameObject.scene.name=="MainBoard"&&c.cardData!=null&&!c.isDead&&
        (deck.draftPanel==null||!c.transform.IsChildOf(deck.draftPanel.transform))).ToArray();
    static CardInstance Card(int n)=>Cards().Single(c=>c.cardData.cardNumber==n);
    static void Zoom(float value)
    {Field(layout,"zoom",value);Field(layout,"pan",Vector2.zero);Call(layout,"ApplyView");Call(layout,"LateUpdate");Canvas.ForceUpdateCanvases();}
    static Rect Bounds(RectTransform rect,RectTransform parent)
    {
        var corners=new Vector3[4];rect.GetWorldCorners(corners);var points=corners.Select(p=>parent.InverseTransformPoint(p)).ToArray();
        return Rect.MinMaxRect(points.Min(p=>p.x),points.Min(p=>p.y),points.Max(p=>p.x),points.Max(p=>p.y));
    }
    static VertexHelper Quad(RectTransform rect)
    {
        var vh=new VertexHelper();var r=rect.rect;var v=UIVertex.simpleVert;
        foreach(var p in new[]{new Vector2(r.xMin,r.yMin),new Vector2(r.xMin,r.yMax),new Vector2(r.xMax,r.yMax),new Vector2(r.xMax,r.yMin)})
        {v.position=p;vh.AddVert(v);}vh.AddTriangle(0,1,2);vh.AddTriangle(0,2,3);return vh;
    }
    static void CheckClipping()
    {
        foreach(var card in Cards().Where(c=>c.GetComponent<CardInteraction>().isPlaced))
        {
            var effect=card.cardImage.GetComponent<BoardArtworkClip>();Expect(effect!=null,"Board art has no clip effect.");
            using(var vh=Quad(card.cardImage.rectTransform))
            {
                var material=card.cardImage.material;effect.ModifyMesh(vh);
                var verts=new List<UIVertex>();vh.GetUIVertexStream(verts);
                foreach(var v in verts)
                {
                    var p=layout.Viewport.InverseTransformPoint(card.cardImage.transform.TransformPoint(v.position));var r=layout.Viewport.rect;
                    Expect(p.x>=r.xMin-.1f&&p.x<=r.xMax+.1f&&p.y>=r.yMin-.1f&&p.y<=r.yMax+.1f,"Card artwork escapes its viewport after zoom.");
                }
                Expect(card.cardImage.material==material,"Clipping replaced the card material.");
            }
        }
        Expect(hud.localToWorldMatrix==hudMatrix,"Zoom moved the fixed faction/phase HUD.");
    }
    static void Tick()
    {
        try
        {
            if(EditorApplication.timeSinceStartup-started>90)throw new Exception("Presentation timeout at "+phase);
            if(EditorApplication.timeSinceStartup<due)return;
            switch(phase)
            {
                case 0:
                    turns=TurnManager.instance;deck=DeckManager.instance;Expect(turns!=null&&deck!=null,"MainBoard failed to initialize.");
                    turns.enabled=false;deck.enabled=false;
                    if(turns.curBot!=null)turns.curBot.gameObject.SetActive(false);
                    if(turns.villagerBot!=null)turns.villagerBot.gameObject.SetActive(false);
                    var names=new[]{"Villager1","Villager2","Villager3","Villager9","Villager10","Villager11","Villager12","Villager13","Master21"};
                    deck.allCards=names.Select(n=>{
                        var d=Object.Instantiate(AssetDatabase.LoadAssetAtPath<CardData>("Assets/ScriptableObjects/"+n+".asset"));
                        if(d.cardSkill!=null)d.cardSkill=Object.Instantiate(d.cardSkill);return d;}).ToList();
                    Call(deck,"FinishSetupAndGenerateDeck");
                    phase++;due=EditorApplication.timeSinceStartup+.3;return;
                case 1:
                    turns.currentTurn=TurnState.Player1Turn;turns.currentVotingPlayer=DefaultOwner.Player1;turns.allCurs.Clear();turns.allCurs.Add(Card(1));
                    var seated=new[]{1,2,10,21,11,12,3};
                    for(int i=0;i<seated.Length;i++)Card(seated[i]).GetComponent<CardInteraction>().ExecutePlace(turns.allSeats[i].cardPos);
                    Card(10).isInfected=true;turns.UpdateCurVisuals();layout=GameBoardLayout.Active;
                    Expect(layout!=null&&turns.allSeats.Count>7,"Game Master extra-seat view is missing.");
                    layout.ResetView();hud=Object.FindFirstObjectByType<PlayerStageHUD>().GetComponent<RectTransform>();hudMatrix=hud.localToWorldMatrix;
                    phase++;due=EditorApplication.timeSinceStartup+.4;return;
                case 2:
                    var captions=turns.btnCheck.GetComponentsInChildren<Graphic>().Where(g=>g.isActiveAndEnabled&&(g is Text||g is TMPro.TMP_Text)).ToArray();
                    Expect(captions.Length==1,"Purge has overlapping active captions: "+string.Join(",",captions.Select(g=>g.name)));
                    foreach(float z in new[]{1f,2f,5f}){Zoom(z);CheckClipping();Capture("zoom-"+z);}
                    layout.ResetView();Canvas.ForceUpdateCanvases();
                    Expect(turns.cemeteryCardPos.parent is RectTransform,"Cemetery has no visible slot.");
                    var cemetery=Bounds((RectTransform)turns.cemeteryCardPos.parent,layout.Viewport);
                    Expect(cemetery.xMax<=layout.Viewport.rect.xMax+.5f&&cemetery.xMin>=layout.Viewport.rect.xMin-.5f,"Reset view clips the cemetery.");
                    checks.Add("Normal/infected custom artwork clipped at 1x, 2x and 5x; extra seats/cemetery fit; fixed HUD stayed still");
                    Zoom(5f);
                    inspected=Card(10);inspected.GetComponent<CardInteraction>().OnPointerClick(new PointerEventData(EventSystem.current){button=PointerEventData.InputButton.Right});
                    phase++;due=EditorApplication.timeSinceStartup+.5;return;
                case 3:
                    var input=inspected.GetComponent<CardInteraction>();Expect(input.isInspecting&&input.descriptionPanel.activeSelf,"Right-click did not open card inspection.");
                    Capture("inspection");
                    var canvasRect=(RectTransform)inspected.transform.root;
                    var cardBounds=Bounds((RectTransform)inspected.transform,canvasRect);
                    float expectedWidth=((RectTransform)inspected.transform).rect.width*input.PrefabRootScale.x*input.inspectScaleMultiplier;
                    Expect(Mathf.Abs(cardBounds.width-expectedWidth)<1,"Inspection inherits the board zoom instead of keeping a readable screen size. actual="+cardBounds.width+", expected="+expectedWidth);
                    using(var vh=Quad(inspected.cardImage.rectTransform))
                    {
                        inspected.cardImage.GetComponent<BoardArtworkClip>().ModifyMesh(vh);var verts=new List<UIVertex>();vh.GetUIVertexStream(verts);
                        Expect(verts.Count>0&&verts.Max(v=>v.position.y)-verts.Min(v=>v.position.y)>=inspected.cardImage.rectTransform.rect.height-.5f,
                            "The enlarged inspection card is still clipped by the board viewport.");
                    }
                    GameStartUI.Current.ShowRules();GameStartUI.Current.CloseRules();
                    Expect(!input.isInspecting&&CardInteraction.currentlyInspectedCard==null&&!GameStartUI.IsBlockingInput,"Inspect -> Esc/rules -> Continue left input locked.");
                    Call(layout,"LateUpdate");CheckClipping();
                    checks.Add("Enlarged card stays complete; inspection -> rules -> resume restores input");
                    layout.ResetView();
                    phase++;due=EditorApplication.timeSinceStartup+.3;return;
                case 4:
                    CardDialogue.HideAllBubbles();string randomState=JsonUtility.ToJson(UnityEngine.Random.state);int speakers=0;
                    foreach(var card in Cards().Where(c=>c.GetComponent<CardInteraction>().isPlaced))
                    {
                        var dialogue=card.GetComponent<CardDialogue>();dialogue.StopChatting();
                        bool shown=(bool)typeof(CardDialogue).GetMethod("ShowQuote",Private).Invoke(dialogue,null);
                        if(!shown)continue;speakers++;
                        var rect=(RectTransform)dialogue.bubbleObject.transform;var bounds=Bounds(rect,layout.Viewport);var viewport=layout.Viewport.rect;
                        Expect(bounds.xMin>=viewport.xMin-.5f&&bounds.xMax<=viewport.xMax+.5f&&bounds.yMin>=viewport.yMin-.5f&&bounds.yMax<=viewport.yMax+.5f,"New dialogue is clipped at its initial anchor.");
                        Expect(dialogue.bubbleObject.GetComponentsInChildren<Graphic>(true).All(g=>!g.raycastTarget),"Dialogue blocks the pointer.");
                        if(detachedBubble==null)detachedBubble=dialogue.bubbleObject;
                    }
                    Expect(speakers>0&&speakers<=3,"No valid speech or concurrent speech cap was exceeded.");
                    Expect(randomState==JsonUtility.ToJson(UnityEngine.Random.state),"Speech changed gameplay RNG.");
                    Capture("dialogue");
                    checks.Add("Speech uses clipped board overlay, fits on start, allows at most three speakers and does not block input or change gameplay RNG");
                    GameStartUI.Current.ShowRules();Expect(!detachedBubble.activeSelf,"Opening a modal left speech visible.");GameStartUI.Current.CloseRules();
                    checks.Add("Modal opening hides all active dialogue");Finish(true,"Board zoom, artwork/inspection clipping, dialogue and resume checks passed.");return;
            }
        }
        catch(Exception e){Finish(false,e.ToString());}
    }
    static void Finish(bool passed,string message)
    {
        EditorApplication.update-=Tick;SessionState.SetBool(Request,false);Time.timeScale=1;
        File.WriteAllText(Path.Combine(Directory.GetParent(Application.dataPath).FullName,"BoardPresentationValidationResult.json"),JsonUtility.ToJson(new Report{passed=passed,assertions=assertions,message=message,checks=checks.ToArray()},true));
        Debug.Log("PRESENTATION RESULT: "+message);EditorApplication.Exit(passed?0:1);
    }
}

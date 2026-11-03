// Runs in the isolated project only. Render the actual authored scene without saving it.
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public static class CardPresentationPreview
{
    const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    static int captureWidth=1600,captureHeight=900;
    static T Field<T>(object target,string name)=>(T)target.GetType().GetField(name,Private).GetValue(target);
    public static void Run() { Render(false); }
    public static void Cemetery() { Render(true); }
    public static void English() { Render(false,GameLanguage.English); }
    public static void Vietnamese() { Render(false,GameLanguage.Vietnamese); }
    public static void InspectVietnamese() { Render(false,GameLanguage.Vietnamese,true); }
    public static void DraftVietnamese() { Render(false,GameLanguage.Vietnamese,false,true); }
    public static void NotebookVietnamese() { Render(false,GameLanguage.Vietnamese,false,false,true); }
    public static void FeedbackVietnamese() { Render(false,GameLanguage.Vietnamese,feedback:true); }
    public static void FeedbackEnglish() { Render(false,GameLanguage.English,feedback:true); }
    public static void VoteEffectsVietnamese() { Render(false,GameLanguage.Vietnamese,votes:true); }
    public static void VoteEffectsEnglish() { Render(false,GameLanguage.English,votes:true); }
    public static void VoteMarksVietnamese() { Render(false,GameLanguage.Vietnamese,votes:true,marks:true); }
    public static void VoteEffectsCompactVietnamese() { captureWidth=1280;captureHeight=720;Render(false,GameLanguage.Vietnamese,votes:true); }
    public static void TraderFxFrames() { captureWidth=1280;captureHeight=720;Render(false,GameLanguage.Vietnamese,votes:true,showcase:"Trader"); }
    public static void JesterFxFrames() { captureWidth=1280;captureHeight=720;Render(false,GameLanguage.Vietnamese,votes:true,showcase:"Jester"); }
    public static void SnooperFxFrames() { captureWidth=1280;captureHeight=720;Render(false,GameLanguage.Vietnamese,votes:true,showcase:"Snooper"); }
    public static void GamblerFxFrames() { captureWidth=1280;captureHeight=720;Render(false,GameLanguage.Vietnamese,votes:true,showcase:"Gambler"); }
    public static void GamblerLossFxFrames() { captureWidth=1280;captureHeight=720;Render(false,GameLanguage.Vietnamese,votes:true,showcase:"GamblerLoss"); }
    private static void Render(bool cemetery,GameLanguage language=GameLanguage.English,bool inspect=false,bool draft=false,bool notebook=false,bool feedback=false,bool votes=false,bool marks=false,string showcase=null)
    {
        try
        {
            EditorSceneManager.OpenScene("Assets/Scenes/MainBoard.unity");
            var tm=Object.FindFirstObjectByType<TurnManager>();TurnManager.instance=tm;
            var deck=Object.FindFirstObjectByType<DeckManager>();DeckManager.instance=deck;
            var view=Object.FindFirstObjectByType<GameStartUI>();
            GameSettings.currentMode=GameMode.PlayerVsCurBot;
            Localizer.Initialize(view.GetComponent<LanguageController>().catalog,language);Localizer.SetLanguage(language,false);
            var camera=new GameObject("Preview camera").AddComponent<Camera>();
            camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.04f,.02f,.02f);
            camera.orthographic=true;camera.transform.position=new Vector3(0,0,-1000);
            camera.nearClipPlane=.1f;camera.farClipPlane=2000;
            var target=new RenderTexture(captureWidth,captureHeight,24);camera.targetTexture=target;
            foreach(var canvas in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
                if(canvas.isRootCanvas){canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=100;}
            typeof(GameStartUI).GetMethod("Initialize",Private).Invoke(view,null);
            foreach(string key in new[]{"setupPanel","rulesPanel","inputShield","resultPanel","revealControl"})
                Field<GameObject>(view,key)?.SetActive(false);
            deck.draftPanel.SetActive(false);
            foreach(var cursor in Object.FindObjectsByType<CustomCusor>(FindObjectsSortMode.None))cursor.gameObject.SetActive(false);
            tm.currentTurn=TurnState.Player2Turn;tm.p2Hearts=3;tm.UpdateHeartsUI();
            Field<Text>(view,"phaseLabel").text="LƯỢT PHE DÂN LÀNG";
            Field<Text>(view,"instruction").text="Đặt bài mình: 2 tim · Bài đối phương / trung lập: 5 tim · Kỹ năng có chi phí riêng.";
            var hand=Field<RectTransform>(view,"handDock");
            deck.p1Panel.SetActive(false);deck.neutralPanel.SetActive(false);deck.p2Panel.SetActive(true);
            for(int i=0;!draft&&!votes&&i<3;i++)
            {
                var definition=AssetDatabase.LoadAssetAtPath<CardData>("Assets/ScriptableObjects/"+new[]{"Villager10","Villager1","Trader20"}[i]+".asset");
                var card=Object.Instantiate(tm.cardPrefab,tm.allSeats[i].cardPos).GetComponent<CardInstance>();
                card.SetupCard(definition);var rect=card.GetComponent<RectTransform>();
                rect.anchoredPosition=Vector2.zero;rect.localRotation=Quaternion.identity;rect.localScale=Vector3.one;
                card.GetComponent<CardInteraction>().isPlaced=true;
                if(i==1){var dialogue=card.GetComponent<CardDialogue>();dialogue.tmpText.text=definition.quotes[0];dialogue.bubbleObject.SetActive(true);}
            }
            if(!draft)for(int i=0;i<7;i++)
            {
                var card=Object.Instantiate(tm.cardPrefab,hand).GetComponent<CardInstance>();card.SetupCard(AssetDatabase.LoadAssetAtPath<CardData>("Assets/ScriptableObjects/Villager"+(9+i)+".asset"));
                var rect=card.GetComponent<RectTransform>();rect.anchorMin=rect.anchorMax=new Vector2(.5f,.5f);
                float fraction=i/6f-.5f;rect.anchoredPosition=new Vector2(fraction*450,-20-Mathf.Abs(fraction)*25);rect.localScale=Vector3.one*.72f;rect.localRotation=Quaternion.Euler(0,0,-fraction*28);
            }
            if(draft)
            {
                deck.draftPanel.SetActive(true);
                deck.ApplyOnlineDraft(0,8,false,new[]{AssetDatabase.LoadAssetAtPath<CardData>("Assets/ScriptableObjects/Blind19.asset"),AssetDatabase.LoadAssetAtPath<CardData>("Assets/ScriptableObjects/Master21.asset")});
            }
            if(cemetery)
            {
                for(int i=0;i<9;i++)
                {
                    var corpse=Object.Instantiate(tm.cardPrefab,tm.cemeteryCardPos).GetComponent<CardInstance>();
                    corpse.SetupCard(AssetDatabase.LoadAssetAtPath<CardData>("Assets/ScriptableObjects/Villager"+(9+i%4)+".asset"));
                    corpse.isDead=true;corpse.cemeteryOrder=i+1;
                }
                tm.cemeteryViewPanel.SetActive(true);tm.RefreshCemeteryPresentation();tm.cemeteryView.Refresh();
                foreach(var dialogue in Object.FindObjectsByType<CardDialogue>(FindObjectsInactive.Include,FindObjectsSortMode.None))dialogue.bubbleObject.SetActive(false);
            }
            if(inspect)
            {
                var card=Object.Instantiate(tm.cardPrefab,view.transform).GetComponent<CardInstance>();
                card.SetupCard(AssetDatabase.LoadAssetAtPath<CardData>("Assets/ScriptableObjects/Master21.asset"));
                var rect=card.GetComponent<RectTransform>();rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(.5f,.5f);rect.anchoredPosition=Vector2.zero;rect.localScale=Vector3.one*1.8f;
                var interaction=card.GetComponent<CardInteraction>();interaction.isInspecting=true;interaction.descriptionPanel.SetActive(true);interaction.RestoreCardDescription();rect.position=interaction.InspectionPosition();
                var overlay=card.gameObject.AddComponent<Canvas>();overlay.overrideSorting=true;overlay.sortingOrder=1000;
                foreach(var bubble in Object.FindObjectsByType<CardDialogue>(FindObjectsInactive.Include,FindObjectsSortMode.None))bubble.bubbleObject.SetActive(false);
                var dimmer=Object.FindFirstObjectByType<CanvasDimmer>(FindObjectsInactive.Include);
                if(dimmer!=null){dimmer.gameObject.SetActive(true);dimmer.GetComponent<CanvasGroup>().alpha=dimmer.maxDimAlpha;}
            }
            foreach(var owner in Object.FindObjectsByType<CardOwnershipView>(FindObjectsInactive.Include,FindObjectsSortMode.None))owner.Refresh();
            foreach(var legend in Object.FindObjectsByType<OwnershipLegend>(FindObjectsSortMode.None))typeof(OwnershipLegend).GetMethod("LateUpdate",Private).Invoke(legend,null);
            typeof(DeckManager).GetProperty("IsReady").SetValue(deck,!draft);
            if(!draft){tm.btnStartGame.SetActive(false);tm.btnEndTurn.SetActive(true);}
            if(feedback)tm.ShowLocalActionFeedback(string.Format("{0} cannot be moved by hand while its extra seats are occupied. Empty those seats first; card effects can still push it.","GameMaster (số 21)"));
            VoteEffectsView voteView=null;
            if(votes)
            {
                Skill_Gambler.ResetComboChain();GameSettings.currentMode=GameMode.PlayerVsVillBot;
                tm.currentTurn=TurnState.SacrificePhase;tm.currentVotingPlayer=DefaultOwner.Player1;tm.firstPlayerThisRound=TurnState.Player1Turn;
                var definitions=new[]{"Villager10","Trader20","Gambler24","Snooper22","Detactive23","Villager11","Villager1"};
                if(showcase=="Jester")definitions[1]="Jester20";
                if(showcase=="Snooper"){definitions[1]="Snooper22";definitions[3]="Villager12";}
                if(showcase=="Gambler"||showcase=="GamblerLoss"){definitions[1]="Gambler24";definitions[2]="Villager12";}
                var board=definitions.Select((name,index)=>{
                    var card=Object.Instantiate(tm.cardPrefab,tm.allSeats[index].cardPos).GetComponent<CardInstance>();
                    var data=Object.Instantiate(AssetDatabase.LoadAssetAtPath<CardData>("Assets/ScriptableObjects/"+name+".asset"));
                    if(data.cardSkill!=null)data.cardSkill=Object.Instantiate(data.cardSkill);card.SetupCard(data);
                    var input=card.GetComponent<CardInteraction>();input.isPlaced=true;input.originalParent=card.transform.parent;input.lockState=3;
                    card.GetComponent<RectTransform>().anchoredPosition=Vector2.zero;return card;
                }).ToArray();
                Field<GameBoardLayout>(view,"boardLayout").FitBoard();Canvas.ForceUpdateCanvases();
                tm.allCurs.Add(board[6]);
                if(showcase==null)
                {
                    board[3].isInfected=true;board[0].bonusSuspicion=12;
                    board[2].cardData.cardSkill.TryCastActiveSkill(board[2],board[0]);
                    board[3].cardData.cardSkill.TryCastActiveSkill(board[3],board[0]);
                    tm.voteEffects.Detective(board[4],board[0]);
                }
                if(!marks)
                {
                    if(showcase=="Gambler"||showcase=="GamblerLoss")
                    {
                        tm.currentVotingPlayer=showcase=="Gambler"?DefaultOwner.Player1:DefaultOwner.Player2;
                        board[1].cardData.cardSkill.TryCastActiveSkill(board[1],board[0]);
                        if(showcase=="GamblerLoss")
                            ((System.Collections.Generic.Dictionary<CardInstance,int>)typeof(TurnManager).GetField("cardVotes",Private).GetValue(tm))[board[0]]=6;
                    }
                    tm.isTallyingVotes=true;int[] counts={4,5,1,0,0,0,2};tm.voteEffects.BeginTally(counts);
                    var original=(int[])counts.Clone();
                    if(showcase=="Jester")
                    {
                        for(int seed=0;seed<100;seed++)
                        {UnityEngine.Random.InitState(seed);counts=(int[])original.Clone();board[1].cardData.cardSkill.TryOnBeforeVoteCalculate(board[1],counts,1);if(counts.Sum()-original.Sum()>=2)break;}
                        tm.voteEffects.RecordChange(board[1],"Jester","Jester scatters extra votes at random. Only the cards shown in this step receive them.",original,counts);
                    }
                    else if(showcase=="Snooper")
                    {
                        board[1].cardData.cardSkill.TryOnBeforeVoteCalculate(board[1],counts,1);
                        tm.voteEffects.RecordChange(board[1],"Snooper","Snooper adds one vote to its indicated target. If no valid target is available, no vote is added.",original,counts);
                    }
                    else if(showcase=="Gambler"||showcase=="GamblerLoss")
                        board[1].cardData.cardSkill.TryOnBeforeVoteCalculate(board[1],counts,1);
                    else
                    {
                        board[1].cardData.cardSkill.TryOnBeforeVoteCalculate(board[1],counts,1);
                        tm.voteEffects.RecordChange(board[1],"Trader","Trader divides the highest count (5) among 3 occupied seats: 1 votes each. Remainder discarded.",original,counts,new[]{0,1,2});
                    }
                    if(showcase==null)
                    {
                        original=(int[])counts.Clone();board[3].cardData.cardSkill.TryOnBeforeVoteCalculate(board[3],counts,3);
                        tm.voteEffects.RecordChange(board[3],"Snooper","Snooper adds one vote to its indicated target. If no valid target is available, no vote is added.",original,counts);
                        board[2].cardData.cardSkill.TryOnBeforeVoteCalculate(board[2],counts,2);
                    }
                    for(int i=0;i<board.Length;i++){board[i].voteText.text=counts[i].ToString();board[i].voteText.gameObject.SetActive(counts[i]>0);}
                    PlayerInformation.RefreshHostMarks();
                }
                voteView=Object.FindFirstObjectByType<VoteEffectsView>();typeof(VoteEffectsView).GetMethod("Awake",Private).Invoke(voteView,null);
            }
            typeof(GameStartUI).GetMethod("LateUpdate",Private).Invoke(view,null);
            if(votes)
            {
                typeof(VoteEffectsView).GetMethod("LateUpdate",Private).Invoke(voteView,null);
                Canvas.ForceUpdateCanvases();
                if(!marks)typeof(VoteEffectsView).GetMethod("Play",Private).Invoke(voteView,new object[]{tm.voteEffects.Local.Single(r=>r.kind==(showcase=="GamblerLoss"?"Gambler":showcase??"Trader"))});
                foreach(var hud in Object.FindObjectsByType<CardVotePresentation>(FindObjectsSortMode.None))
                {
                    typeof(CardVotePresentation).GetMethod("Awake",Private)?.Invoke(hud,null);
                    if(!marks&&hud.GetComponent<CardInstance>().cardData.cardNumber==20)typeof(CardVotePresentation).GetField("activationAt",Private).SetValue(hud,Time.unscaledTime-.25f);
                    hud.Refresh();
                }
                foreach(var owner in Object.FindObjectsByType<CardOwnershipView>(FindObjectsSortMode.None))owner.Refresh();
            }
            if(notebook)tm.notePanel.SetActive(true);
            foreach(var label in Object.FindObjectsByType<LocalizedLabel>(FindObjectsInactive.Include,FindObjectsSortMode.None))label.Refresh();
            Canvas.ForceUpdateCanvases();
            if(feedback)
            {
                var label=Field<Text>(view,"instruction");var sample=label.text;
                foreach(var entry in view.GetComponent<LanguageController>().catalog.entries.Where(e=>e.key.StartsWith("action.feedback.")))
                {
                    label.text=string.Format(language==GameLanguage.Vietnamese?entry.vietnamese:entry.english,"GameMaster (number 21)",5,3);
                    if(label.preferredHeight>label.rectTransform.rect.height+.5f)
                        throw new Exception("Feedback clips in the saved header: "+entry.key+" / "+language+" ("+label.preferredHeight+" > "+label.rectTransform.rect.height+")");
                }
                label.text=sample;
            }
            Field<GameBoardLayout>(view,"boardLayout").FitBoard();Canvas.ForceUpdateCanvases();camera.Render();
            RenderTexture.active=target;var image=new Texture2D(captureWidth,captureHeight,TextureFormat.RGBA32,false);
            image.ReadPixels(new Rect(0,0,captureWidth,captureHeight),0,0);image.Apply();
            if(showcase!=null)
            {
                var folder=Path.Combine(Directory.GetParent(Application.dataPath).FullName,"SkillFxFrames",showcase);Directory.CreateDirectory(folder);
                int frames=Mathf.CeilToInt(tm.voteEffects.Local.Single(r=>r.kind==(showcase=="GamblerLoss"?"Gambler":showcase)).PresentationSeconds*20)+1;
                for(int frame=0;frame<frames;frame++)
                {
                    voteView.skillFx.EvaluateAt(frame/20f);
                    foreach(var hud in Object.FindObjectsByType<CardVotePresentation>(FindObjectsSortMode.None))hud.Refresh();
                    Canvas.ForceUpdateCanvases();camera.Render();RenderTexture.active=target;
                    image.ReadPixels(new Rect(0,0,captureWidth,captureHeight),0,0);image.Apply();
                    File.WriteAllBytes(Path.Combine(folder,"frame-"+frame.ToString("D3")+".png"),image.EncodeToPNG());
                }
                Debug.Log("SKILL FX FRAMES: "+showcase+" rendered "+frames+" frames using the production effect evaluator.");
                RenderTexture.active=null;EditorApplication.Exit(0);return;
            }
            if(votes)
            {
                if(!marks&&voteView.panel.transform.GetSiblingIndex()!=hand.childCount-1)throw new Exception("The hand can cover the resolution panel.");
                foreach(var hud in Object.FindObjectsByType<CardVotePresentation>(FindObjectsSortMode.None))
                    if(hud.suspicionBadge.activeSelf&&hud.suspicionCount.preferredWidth>hud.suspicionCount.rectTransform.rect.width+.5f)
                        throw new Exception("Suspicion total is clipped: "+hud.suspicionCount.text);
                foreach(var row in Object.FindObjectsByType<VoteEffectRow>(FindObjectsSortMode.None))
                    foreach(var text in new[]{row.source,row.reason,row.changes})
                        if(text.preferredHeight>text.rectTransform.rect.height+.5f)throw new Exception("Vote row clips: "+text.name+" / "+text.text);
            }
            File.WriteAllBytes(Path.Combine(Directory.GetParent(Application.dataPath).FullName,votes?(marks?"VoteMarks-":"VoteEffects-")+language+(captureWidth==1600?"":"-1280x720")+".png":feedback?"ActionFeedback-"+language+".png":notebook?"Notebook-Seven-Seats.png":draft?"Draft-Vietnamese.png":cemetery?"CemeteryPreview.png":inspect?"Inspection-Vietnamese.png":"Ownership-"+language+".png"),image.EncodeToPNG());
            RenderTexture.active=null;
            Debug.Log("PRESENTATION PREVIEW: rendered actual scene and prefab artwork.");EditorApplication.Exit(0);
        }
        catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
    }
}

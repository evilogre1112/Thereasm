using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// Exercise real Capture/Apply and rendering, including a host whose own role is Villager.
[InitializeOnLoad]
public static class OnlineVisibilityValidation
{
    const string Request="Thereasm.VisibilityValidation";
    const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    static GameObject root;
    static OnlineSession session;
    static TurnManager tm;
    static DeckManager deck;
    static OnlineGameBridge bridge;
    static int assertions;
    static readonly List<string> cases=new List<string>();
    static readonly List<string> errors=new List<string>();
    static readonly string[] names={"Villager1","Villager10","Villager11","Snooper22","Gambler24","Blind19","Invoker19"};
    static Dictionary<string,CardData> data;
    [Serializable] class CardView { public string data; public int id,seat,votes; public bool cur,infected,deadInfected,ritualUsed,used,gambler,snooper,blind,blindCur,blindInfected,mustVote; public int voteTarget; }
    [Serializable] class Snapshot { public bool over; public string feedback; public string[] options; public CardView[] cards; public VoteEffectRecord[] voteEffects; public SkillSoundEvent[] skillSounds; }
    [Serializable] class Report { public bool passed;public int assertions;public string message;public string[] cases; }
    static OnlineVisibilityValidation()
    {
        EditorApplication.playModeStateChanged+=state=>{
            if(state==PlayModeStateChange.EnteredPlayMode&&SessionState.GetBool(Request,false))EditorApplication.delayCall+=Check;
        };
    }
    public static void Run(){SessionState.SetBool(Request,true);EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);EditorApplication.isPlaying=true;}
    static void Expect(bool value,string message){assertions++;if(!value)throw new Exception(message);}
    static RectTransform Node(string name,Transform parent=null)
    {var go=new GameObject(name,typeof(RectTransform));go.transform.SetParent(parent,false);return (RectTransform)go.transform;}
    static void Set(object target,string property,object value)=>target.GetType().GetProperty(property).SetValue(target,value);
    static void Field(object target,string field,object value)=>target.GetType().GetField(field,Private).SetValue(target,value);
    static CardInstance Card(string name,int seat=-1)
    {
        var rect=Node(name,seat>=0?tm.allSeats[seat].cardPos:deck.neutralHand);rect.sizeDelta=new Vector2(150,220);
        var card=rect.gameObject.AddComponent<CardInstance>();card.enabled=false;card.cardData=data[name];card.currentOwner=card.cardData.owner;
        card.cardImage=rect.gameObject.AddComponent<Image>();card.infectionEffect=Node("infection",rect).gameObject;card.infectionEffect.SetActive(false);
        card.normalMaterial=new Material(Shader.Find("UI/Default"));card.infectedMaterial=new Material(Shader.Find("UI/Default"));
        card.gamblerMark=Node("bet",rect).gameObject;card.gamblerMark.SetActive(false);
        card.snooperMark=Node("snooper",rect).gameObject;card.snooperMark.SetActive(false);
        var input=rect.gameObject.AddComponent<CardInteraction>();input.enabled=false;input.isPlaced=seat>=0;input.lockState=3;
        input.originalParent=rect.parent;input.handParent=deck.neutralHand;card.FaceUp();return card;
    }
    static void Board(DefaultOwner hostRole,Dictionary<string,CardData> definitions=null)
    {
        if(root!=null)Object.DestroyImmediate(root);
        if(session!=null)Object.DestroyImmediate(session.gameObject);
        Skill_Gambler.ResetComboChain();HoldToReveal.isRevealingAll=false;GameSettings.currentMode=GameMode.PvP;
        session=new GameObject("visibility session").AddComponent<OnlineSession>();
        Set(session,"Connected",true);Set(session,"MatchStarted",true);Set(session,"IsHost",true);Set(session,"MatchId","visibility-test");
        Field(session,"localRole",hostRole);
        root=Node("Visibility canvas").gameObject;root.AddComponent<Canvas>();
        tm=Node("turns",root.transform).gameObject.AddComponent<TurnManager>();tm.enabled=false;tm.currentTurn=TurnState.SacrificePhase;
        tm.p1Hearts=tm.p2Hearts=5;tm.allSeats=new List<SeatDropZone>();
        tm.cemeteryCardPos=Node("Cemetery",root.transform);tm.globalCemetery=tm.cemeteryCardPos;
        tm.cemeteryViewPanel=Node("CemeteryViewPanel",root.transform).gameObject;tm.cemeteryViewPanel.SetActive(false);
        tm.discoverPanel=Node("Discover",root.transform).gameObject;tm.discoverPanel.SetActive(false);
        tm.discoverSlots=Enumerable.Range(0,3).Select(i=>(Transform)Node("option"+i,tm.discoverPanel.transform)).ToArray();
        tm.cardPrefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/CardUI.prefab");
        deck=Node("deck",root.transform).gameObject.AddComponent<DeckManager>();deck.enabled=false;
        deck.p1Panel=Node("P1 hand",root.transform).gameObject;deck.p2Panel=Node("P2 hand",root.transform).gameObject;
        deck.neutralPanel=Node("Neutral hand",root.transform).gameObject;
        deck.draftPanel=Node("draft",root.transform).gameObject;deck.draftPanel.SetActive(false);
        data=definitions??names.ToDictionary(n=>n,n=>{
            var asset=AssetDatabase.LoadAssetAtPath<CardData>("Assets/ScriptableObjects/"+n+".asset");var copy=Object.Instantiate(asset);copy.name=n;
            if(copy.cardSkill!=null)copy.cardSkill=Object.Instantiate(copy.cardSkill);return copy;
        });
        deck.allCards=data.Values.ToList();deck.neutralCardPool=data.Values.Where(d=>d.owner==DefaultOwner.Neutral).ToList();Set(deck,"IsReady",true);
        for(int i=0;i<5;i++)
        {var rect=Node("seat"+i,root.transform);rect.anchoredPosition=new Vector2(i*240,0);rect.sizeDelta=new Vector2(150,220);
            var seat=rect.gameObject.AddComponent<SeatDropZone>();seat.cardPos=Node("CardPos",rect);seat.isNextToCemetery=i==4;tm.allSeats.Add(seat);}
        bridge=tm.gameObject.AddComponent<OnlineGameBridge>();bridge.enabled=false;
    }
    static object Capture()=>typeof(OnlineGameBridge).GetMethod("Capture",Private).Invoke(bridge,null);
    static Snapshot Read(object snapshot)=>JsonUtility.FromJson<Snapshot>(JsonUtility.ToJson(snapshot));
    static CardView View(Snapshot snapshot,string name)=>snapshot.cards.Single(c=>c.data.EndsWith(":"+name));
    static CardInstance GuestCard(string name)=>Object.FindObjectsByType<CardInstance>(FindObjectsInactive.Include,FindObjectsSortMode.None).Single(c=>c.cardData!=null&&c.cardData.name==name);
    static void ApplyGuest(object snapshot,DefaultOwner viewer,Dictionary<string,CardData> definitions)
    {
        Board(viewer,definitions);Set(session,"IsHost",false);
        tm.voteEffects=tm.gameObject.AddComponent<VoteEffectFeed>();
        typeof(OnlineGameBridge).GetMethod("Apply",Private).Invoke(bridge,new[]{snapshot});
    }
    static bool InfectionIs(CardInstance card,bool visible) => card.cardImage.material==(visible?card.infectedMaterial:card.normalMaterial) &&
        (card.infectionEffect==null||card.infectionEffect.activeSelf==visible);
    static void Pass(string name){cases.Add(name);Debug.Log("VISIBILITY CHECK: "+name);}
    static void WireField(object wire,string name,object value)=>wire.GetType().GetField(name).SetValue(wire,value);
    static void ApplyWire(object wire)=>typeof(OnlineGameBridge).GetMethod("Apply",Private).Invoke(bridge,new[]{wire});
    static CemeteryView Cemetery()
    {
        var view=tm.cemeteryViewPanel.AddComponent<CemeteryView>();tm.cemeteryView=view;
        view.content=Node("Cemetery card grid",view.transform);view.content.sizeDelta=new Vector2(1200,560);
        view.grid=view.content.gameObject.AddComponent<GridLayoutGroup>();view.grid.spacing=new Vector2(12,16);
        view.grid.padding=new RectOffset(20,20,20,20);return view;
    }
    static void OnlineFlow()
    {
        Board(DefaultOwner.Player1);tm.currentTurn=TurnState.SacrificePhase;tm.currentVotingPlayer=DefaultOwner.Player2;
        var first=Card("Villager10",0);var second=Card("Villager11",1);var target=Card("Villager1",2);
        Expect(tm.GetMissingVoterNames().Count==2,"Missing-voter list is incomplete.");
        Field(session,"localRole",DefaultOwner.Player2);
        tm.EndTurn();Expect(tm.feedbackMessage!=null&&tm.feedbackMessage.Contains("10")&&tm.feedbackMessage.Contains("11"),"End turn did not name the unvoted cards.");
        Field(session,"localRole",DefaultOwner.Player1);
        var wire=Capture();WireField(wire,"revision",10);var defs=data;ApplyGuest(wire,DefaultOwner.Player2,defs);
        var rows=Read(wire).cards;int firstId=rows.Single(c=>c.data.EndsWith(":Villager10")).id;
        int secondId=rows.Single(c=>c.data.EndsWith(":Villager11")).id,targetId=rows.Single(c=>c.data.EndsWith(":Villager1")).id;
        Expect(!bridge.Send(OnlineAction.EndTurn)&&bridge.LocalFeedback.Contains("10")&&bridge.LocalFeedback.Contains("11"),"Guest did not immediately identify missing votes.");
        Expect(bridge.Send(OnlineAction.Vote,firstId,targetId,2)&&bridge.Send(OnlineAction.Vote,secondId,targetId,2),"Second vote was blocked awaiting the first acknowledgement.");
        Expect(bridge.PendingCount==2&&bridge.Voted==2&&bridge.RequiredVotes==2&&bridge.MissingVoterNames().Count==0,"Queued votes did not update the local progress immediately.");
        Expect(bridge.Send(OnlineAction.EndTurn)&&bridge.PendingCount==3&&bridge.HasPhaseBarrier,"Could not queue End Turn immediately after the last vote.");
        Expect(!bridge.Send(OnlineAction.Vote,firstId,targetId,2),"Input crossed a pending phase boundary.");
        WireField(wire,"acknowledged",1);WireField(wire,"revision",11);
        var cardRows=(Array)wire.GetType().GetField("cards").GetValue(wire);
        foreach(var row in cardRows)if((int)row.GetType().GetField("id").GetValue(row)==firstId)WireField(row,"voteTarget",2);
        ApplyWire(wire);Expect(bridge.PendingCount==2&&bridge.Voted==2,"Partial ACK erased a later pending vote or reset progress.");
        Pass("Nonblocking vote queue, immediate missing-card feedback, projected progress and partial acknowledgements");

        Board(DefaultOwner.Player1);tm.currentTurn=TurnState.SacrificePhase;tm.currentVotingPlayer=DefaultOwner.Player2;
        Card("Villager10",0);Card("Villager11",1);Card("Villager1",2);
        rows=Read(Capture()).cards;firstId=rows.Single(c=>c.data.EndsWith(":Villager10")).id;
        secondId=rows.Single(c=>c.data.EndsWith(":Villager11")).id;targetId=rows.Single(c=>c.data.EndsWith(":Villager1")).id;
        Field(session,"settings",ScriptableObject.CreateInstance<PhotonOnlineConfig>());
        var commands=new[]{new OnlineCommand{match=session.MatchId,sequence=1,action=OnlineAction.Vote,phase=4,voting=(int)DefaultOwner.Player2,source=firstId,target=targetId,seat=2},
            new OnlineCommand{match=session.MatchId,sequence=2,action=OnlineAction.Vote,phase=4,voting=(int)DefaultOwner.Player2,source=secondId,target=targetId,seat=2},
            new OnlineCommand{match=session.MatchId,sequence=3,action=OnlineAction.EndTurn,phase=4,voting=(int)DefaultOwner.Player2}};
        foreach(int index in new[]{2,0,1})typeof(OnlineGameBridge).GetMethod("OnGuestMessage",Private).Invoke(bridge,new object[]{"command",JsonUtility.ToJson(commands[index])});
        typeof(OnlineGameBridge).GetMethod("Update",Private).Invoke(bridge,null);
        Expect((int)typeof(OnlineGameBridge).GetField("receivedSequence",Private).GetValue(bridge)==3,"Host did not process the complete ordered command batch.");
        Expect(tm.GetVoteTarget(Object.FindObjectsByType<CardInstance>(FindObjectsSortMode.None).Single(c=>c.cardData.name=="Villager10"))==2,"Host lost a queued vote.");
        typeof(OnlineGameBridge).GetMethod("OnGuestMessage",Private).Invoke(bridge,new object[]{"command",JsonUtility.ToJson(commands[0])});
        Expect((int)typeof(OnlineGameBridge).GetField("receivedSequence",Private).GetValue(bridge)==3,"Retry reapplied an old command.");
        Pass("Host batches commands in sequence despite arrival reordering and duplicate retries");

        Board(DefaultOwner.Player1);tm.currentTurn=TurnState.Player2Turn;Card("Villager10");Card("Villager11");Card("Villager1",2);
        wire=Capture();WireField(wire,"revision",10);defs=data;ApplyGuest(wire,DefaultOwner.Player2,defs);rows=Read(wire).cards;
        firstId=rows.Single(c=>c.data.EndsWith(":Villager10")).id;secondId=rows.Single(c=>c.data.EndsWith(":Villager11")).id;
        Expect(bridge.Send(OnlineAction.Place,firstId,seat:0)&&bridge.Send(OnlineAction.Place,secondId,seat:1),"Second card placement was blocked by a global pending flag.");
        Expect(tm.p2Hearts==5&&bridge.DisplayHearts(5)==1,"Optimistic heart display mutated authoritative resources or failed to reserve both costs.");
        WireField(wire,"acknowledged",1);WireField(wire,"p2Hearts",3);WireField(wire,"revision",11);ApplyWire(wire);
        Expect(bridge.PendingCount==1&&bridge.DisplayHearts(3)==1,"Partial placement ACK double-counted or released the wrong cost.");
        WireField(wire,"acknowledged",2);WireField(wire,"p2Hearts",1);WireField(wire,"revision",12);ApplyWire(wire);
        Expect(!bridge.CommandPending&&bridge.DisplayHearts(1)==1,"Final ACK left a stale resource reservation.");
        Pass("Independent card commands and immediate resource display reconcile without mutating gameplay state");

        foreach(int count in new[]{1,2,9,28})
        {
            Board(DefaultOwner.Player1);var view=Cemetery();tm.cemeteryViewPanel.SetActive(true);
            for(int i=0;i<count;i++){var card=Object.Instantiate(tm.cardPrefab,tm.cemeteryCardPos).GetComponent<CardInstance>();card.enabled=false;card.SetupCard(data["Villager10"]);card.isDead=true;card.cemeteryOrder=i+1;tm.deadCardsData.Add(card.cardData);}
            tm.RefreshCemeteryPresentation();view.Refresh();Canvas.ForceUpdateCanvases();LayoutRebuilder.ForceRebuildLayoutImmediate(view.content);
            Bounds bounds=new Bounds();bool initial=true;
            foreach(RectTransform child in view.content)
            {
                var corners=new Vector3[4];child.GetWorldCorners(corners);
                foreach(var corner in corners){var local=view.content.InverseTransformPoint(corner);if(initial){bounds=new Bounds(local,Vector3.zero);initial=false;}else bounds.Encapsulate(local);}
            }
            Expect(Vector2.Distance(bounds.center,view.content.rect.center)<1,"Cemetery grid is off-center for "+count+" cards.");
            Expect(bounds.size.x<=view.content.rect.width+1&&bounds.size.y<=view.content.rect.height+1,"Cemetery cards overflow their reserved panel.");
            var positions=view.content.Cast<Transform>().Select(t=>t.localPosition).ToArray();
            wire=Capture();defs=data;ApplyGuest(wire,DefaultOwner.Player2,defs);view=Cemetery();tm.cemeteryViewPanel.SetActive(true);tm.RefreshCemeteryPresentation();view.Refresh();Canvas.ForceUpdateCanvases();LayoutRebuilder.ForceRebuildLayoutImmediate(view.content);
            Expect(tm.deadCardsData.Count==count&&view.content.childCount==count,"Host/client cemetery membership diverged.");
            var expected=view.content.Cast<Transform>().Select(t=>t.localPosition).ToArray();
            ApplyWire(wire);Canvas.ForceUpdateCanvases();
            Expect(view.content.Cast<Transform>().Select((t,i)=>Vector3.Distance(t.localPosition,expected[i])<.1f).All(x=>x),"Snapshot moved cemetery cards into a corner.");
            tm.CloseCemeteryView();Expect(view.content.parent==tm.cemeteryViewPanel.transform&&tm.cemeteryCardPos.childCount==count,"Closing cemetery moved its layout or lost cards.");
            Expect(tm.cemeteryCardPos.Cast<Transform>().All(t=>t.localScale==new Vector3(.6f,.6f,1)),"Closed guest cemetery uses different card scale.");
        }
        Pass("Centered host/client cemetery for 1, 2, 9 and 28 corpses, stable snapshots, closing preserves the saved content grid");
        Board(DefaultOwner.Player1);tm.currentTurn=TurnState.Player2Turn;var cemetery=Cemetery();tm.cemeteryViewPanel.SetActive(true);
        var dying=Object.Instantiate(tm.cardPrefab,tm.allSeats[0].cardPos).GetComponent<CardInstance>();dying.enabled=false;dying.SetupCard(data["Villager10"]);
        tm.BanishCardToCemetery(dying.GetComponent<CardInteraction>());
        Expect(dying.transform.parent==cemetery.content&&dying.transform.localScale==Vector3.one&&dying.GetComponent<CanvasGroup>().blocksRaycasts,"A death while cemetery is open did not enter the live grid correctly.");
        tm.CloseCemeteryView();
        dying=Object.Instantiate(tm.cardPrefab,tm.allSeats[1].cardPos).GetComponent<CardInstance>();dying.enabled=false;dying.SetupCard(data["Villager11"]);
        tm.BanishCardToCemetery(dying.GetComponent<CardInteraction>());tm.ToggleCemeteryView();
        Expect(cemetery.content.childCount==2&&tm.deadCardsData.Count==2&&dying.transform.parent==cemetery.content,"Opening cemetery omitted the card still flying to the pile.");
        Expect(((System.Collections.IDictionary)typeof(TurnManager).GetField("cemeteryFlights",Private).GetValue(tm)).Count==0,"An old flight can still pull a displayed corpse away from the grid.");
        Pass("Real death while cemetery is open and opening before a death animation lands");
        Board(DefaultOwner.Player1);Set(session,"IsHost",false);Field(session,"roleRevision",10);
        session.ConfirmBoardMatch(session.MatchId);
        typeof(OnlineSession).GetMethod("ClientPrepareForSceneTransition",Private).Invoke(session,null);
        string stale="{\"revision\":10,\"hostRole\":1,\"players\":2,\"started\":true,\"match\":\"visibility-test\"}";
        typeof(OnlineSession).GetMethod("Received",Private).Invoke(session,new object[]{"lobby",stale,true});
        Expect(string.IsNullOrEmpty(session.MatchId),"A delayed old lobby rebound the loading board to a retired match.");
        string fresh="{\"revision\":11,\"hostRole\":1,\"players\":2,\"started\":true,\"match\":\"next-match\"}";
        typeof(OnlineSession).GetMethod("Received",Private).Invoke(session,new object[]{"lobby",fresh,true});
        Expect(session.MatchId=="next-match","Valid next-match lobby was rejected after scene preparation.");
        session.ConfirmBoardMatch(session.MatchId);Field(session,"clientScenePrepared",false);
        fresh="{\"revision\":12,\"hostRole\":1,\"players\":2,\"started\":true,\"match\":\"third-match\"}";
        typeof(OnlineSession).GetMethod("Received",Private).Invoke(session,new object[]{"lobby",fresh,true});
        typeof(OnlineSession).GetMethod("ClientPrepareForSceneTransition",Private).Invoke(session,null);
        typeof(OnlineSession).GetMethod("Received",Private).Invoke(session,new object[]{"lobby",fresh,true});
        Expect(session.MatchId=="third-match","Early new-match announcement was accidentally retired instead of the previously rendered match.");
        Pass("Rematch epoch rejects old lobby packets and accepts announcements arriving before or after scene loading");
    }
    static bool ChatRunning(CardDialogue dialogue) => typeof(CardDialogue).GetField("routine",Private).GetValue(dialogue)!=null;
    static bool Speak(CardDialogue dialogue) => (bool)typeof(CardDialogue).GetMethod("ShowQuote",Private).Invoke(dialogue,null);
    static void PresentationViewport()
    {
        // Dialogue now lives in the real board viewport, rather than an
        // independent card Canvas. Model that contract in this isolated fixture.
        var area=Node("Presentation viewport",root.transform);area.sizeDelta=new Vector2(1600,600);
        var board=Node("Presentation board",area);
        foreach(var seat in tm.allSeats)seat.transform.SetParent(board,false);
        var cemetery=Node("Presentation cemetery",board);cemetery.sizeDelta=new Vector2(150,220);cemetery.anchoredPosition=new Vector2(1200,0);
        tm.cemeteryCardPos.SetParent(cemetery,false);
        var layout=area.gameObject.AddComponent<GameBoardLayout>();layout.Initialize(tm,area);
        Canvas.ForceUpdateCanvases();
    }
    static EnergyBarUI HeartView()
    {
        var bar=Node("Hearts",root.transform).gameObject.AddComponent<EnergyBarUI>();
        var sprite=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/Hearts/parchment-heart.png");
        Expect(sprite!=null&&sprite.texture!=null,"Custom heart did not import as an editable Sprite.");
        bar.heartIcons=Enumerable.Range(0,5).Select(i=>{var image=Node("Heart "+i,bar.transform).gameObject.AddComponent<Image>();image.sprite=sprite;return image;}).ToArray();
        bar.countLabel=Node("Count",bar.transform).gameObject.AddComponent<Text>();tm.p1EnergyBar=bar;return bar;
    }
    static void Presentation()
    {
        Board(DefaultOwner.Player1);tm.currentTurn=TurnState.Player1Turn;
        PresentationViewport();
        var hostBar=HeartView();
        for(int count=-1;count<=6;count++)
        {
            hostBar.UpdateEnergyDisplay(count);int value=Mathf.Clamp(count,0,5);
            Expect(hostBar.countLabel.text==value+"/5","Heart count did not clamp correctly.");
            Expect(hostBar.heartIcons.Where((image,i)=>image.color!=(i<value?hostBar.availableColor:hostBar.spentColor)).Count()==0,"Heart artwork state disagrees with numeric resource count.");
        }
        var card=Object.Instantiate(tm.cardPrefab,deck.player1Hand).GetComponent<CardInstance>();
        card.enabled=false;card.SetupCard(data["Villager1"]);var input=card.GetComponent<CardInteraction>();input.enabled=false;input.handParent=deck.player1Hand;
        var dialogue=card.GetComponent<CardDialogue>();
        dialogue.StartChatting();Expect(!ChatRunning(dialogue)&&!dialogue.bubbleObject.activeSelf,"A card still in hand started speaking.");
        input.ExecutePlace(tm.allSeats[0].cardPos);
        Expect(ChatRunning(dialogue),"ExecutePlace did not start host/bot/swap dialogue.");
        string randomBefore=JsonUtility.ToJson(UnityEngine.Random.state);
        bool spoke=Speak(dialogue);
        Expect(spoke&&dialogue.bubbleObject.activeSelf&&!string.IsNullOrEmpty(dialogue.tmpText.text),"Placed host card bubble is missing. canSpeak="+
            typeof(CardDialogue).GetProperty("CanSpeak",Private).GetValue(dialogue)+", card="+card.transform.position+", viewport="+GameBoardLayout.Active.Viewport.rect+
            ", bubble="+((RectTransform)dialogue.bubbleObject.transform).rect+", scale="+GameBoardLayout.Active.BoardVisualScale+", over="+tm.isGameOver);
        Expect(randomBefore==JsonUtility.ToJson(UnityEngine.Random.state),"Cosmetic speech altered gameplay RNG.");
        Expect(dialogue.bubbleObject.GetComponentsInChildren<Graphic>(true).All(g=>!g.raycastTarget),"Speech bubble blocks card input.");
        Expect(dialogue.bubbleObject.transform.IsChildOf(GameBoardLayout.Active.Viewport)&&
            dialogue.bubbleObject.transform.parent.GetSiblingIndex()==GameBoardLayout.Active.Viewport.childCount-1,
            "Speech is not in the last, clipped board-overlay layer.");
        var timer=typeof(CardDialogue).GetField("routine",Private).GetValue(dialogue);dialogue.StartChatting();
        Expect(ReferenceEquals(timer,typeof(CardDialogue).GetField("routine",Private).GetValue(dialogue)),"Repeated placement/snapshot restarted speech timer.");
        card.gameObject.SetActive(false);Expect(!ChatRunning(dialogue)&&!dialogue.bubbleObject.activeSelf,"Disabled card retained a bubble/timer.");
        card.gameObject.SetActive(true);Expect(ChatRunning(dialogue),"Re-enabled board card never resumed speech.");
        input.ExecuteReturnToHand();Expect(!ChatRunning(dialogue)&&!dialogue.bubbleObject.activeSelf,"Returned card kept speaking.");
        input.ExecutePlace(tm.allSeats[0].cardPos);tm.p1Hearts=2;tm.p2Hearts=4;tm.currentTurn=TurnState.Player2Turn;
        tm.UpdateHeartsUI();Expect(hostBar.countLabel.text=="4/5","Host resource header shows wrong turn.");
        var wire=Capture();var definitions=data;ApplyGuest(wire,DefaultOwner.Player2,definitions);
        var guestBar=HeartView();typeof(OnlineGameBridge).GetMethod("Apply",Private).Invoke(bridge,new[]{wire});
        PresentationViewport();
        card=GuestCard("Villager1");dialogue=card.GetComponent<CardDialogue>();
        Expect(ChatRunning(dialogue)&&Speak(dialogue),"Guest snapshot did not restore placed-card speech.");
        Expect(guestBar.countLabel.text=="4/5"&&guestBar.heartIcons[3].color==guestBar.availableColor&&guestBar.heartIcons[4].color==guestBar.spentColor,"Guest snapshot did not update hearts.");
        timer=typeof(CardDialogue).GetField("routine",Private).GetValue(dialogue);
        typeof(OnlineGameBridge).GetMethod("Apply",Private).Invoke(bridge,new[]{wire});
        Expect(ReferenceEquals(timer,typeof(CardDialogue).GetField("routine",Private).GetValue(dialogue)),"Guest snapshot repeatedly resets chatter.");
        card.isDead=true;typeof(CardDialogue).GetMethod("LateUpdate",Private).Invoke(dialogue,null);
        Expect(!ChatRunning(dialogue)&&!dialogue.bubbleObject.activeSelf,"Dead guest card retained speech.");
        Pass("Restored host/client card speech lifecycle and five-heart resource display");
    }
    static void Check()
    {
        Application.logMessageReceived+=(message,stack,type)=>{if(type==LogType.Exception)errors.Add(message);};
        try
        {
            foreach(var hostRole in new[]{DefaultOwner.Player1,DefaultOwner.Player2})
            {
                Board(hostRole);
                var cur=Card("Villager1",0);tm.allCurs.Add(cur);cur.hasUsedSkillInRitual=true;
                var target=Card("Villager10",1);target.isInfected=true;
                var snooper=Card("Snooper22",2);snooper.isInfected=true;
                var gambler=Card("Gambler24",3);tm.currentVotingPlayer=DefaultOwner.Player1;
                Expect(snooper.cardData.cardSkill.TryCastActiveSkill(snooper,target),"Snooper setup failed.");
                Expect(gambler.cardData.cardSkill.TryCastActiveSkill(gambler,target),"Gambler setup failed.");
                tm.StartVoteSelection(target); // wrong physical owner while P1 is voting: must not create a vote.
                tm.StartVoteSelection(cur);tm.ApplyVote(1);
                tm.ShowFeedback("Private controller feedback");tm.UpdateCurVisuals();
                Expect(target.gamblerMark.activeSelf==(hostRole==DefaultOwner.Player1),"Host saw the other player's bet.");
                Expect(target.snooperMark.activeSelf==(hostRole==DefaultOwner.Player1),"Host saw secret Decay target.");
                Expect(cur.cardImage.sprite==(hostRole==DefaultOwner.Player1?cur.cardData.cursemancerArtwork:cur.cardData.defaultArtwork),"Host role artwork leaked or hid its own identity.");
                var wire=Capture();var remote=Read(wire);var viewer=session.RemoteRole;
                bool secret=viewer==DefaultOwner.Player1;
                Expect(View(remote,"Villager1").cur==secret&&View(remote,"Villager1").ritualUsed==secret,"Cur identity/ritual-used leaked on wire.");
                Expect(View(remote,"Villager10").infected==secret&&View(remote,"Snooper22").infected==secret,"Infection leaked on wire.");
                Expect(View(remote,"Villager10").snooper==secret&&View(remote,"Snooper22").used==secret,"Snooper secret target/usage leaked on wire.");
                Expect(View(remote,"Villager10").gambler==secret&&View(remote,"Gambler24").used==secret,"Bet target/usage leaked on wire.");
                Expect(secret?remote.feedback=="Private controller feedback":string.IsNullOrEmpty(remote.feedback),"Opponent received private controller feedback.");
                Expect(View(remote,"Villager10").votes==(secret?1:0),"Opponent received private votes.");
                Expect(View(remote,"Villager1").mustVote==secret&&View(remote,"Villager1").voteTarget==(secret?1:-1),"Opponent received another faction's individual vote.");
                var definitions=data;ApplyGuest(wire,viewer,definitions);
                var guestCur=GuestCard("Villager1");var guestTarget=GuestCard("Villager10");
                Expect(guestCur.cardImage.sprite==(secret?guestCur.cardData.cursemancerArtwork:guestCur.cardData.defaultArtwork),"Guest artwork does not match its role.");
                Expect(InfectionIs(guestTarget,secret)&&guestTarget.gamblerMark.activeSelf==secret&&guestTarget.snooperMark.activeSelf==secret,"Guest effects leaked/hid private state.");
                Pass("Pre-tally packet and host/client UI privacy: host "+hostRole);
            }
            Board(DefaultOwner.Player1);
            var bet=Card("Gambler24",0);var a=Card("Villager1",1);var b=Card("Villager10",2);
            tm.currentVotingPlayer=DefaultOwner.Player1;Expect(bet.cardData.cardSkill.TryCastActiveSkill(bet,a),"First bet failed.");
            tm.currentVotingPlayer=DefaultOwner.Player2;Expect(bet.cardData.cardSkill.TryCastActiveSkill(bet,b),"Second bet failed.");
            tm.UpdateCurVisuals();var both=Read(Capture());
            Expect(a.gamblerMark.activeSelf&&!b.gamblerMark.activeSelf,"Host bet mark was replaced by opponent bet.");
            Expect(!View(both,"Villager1").gambler&&View(both,"Villager10").gambler&&View(both,"Gambler24").used,"Guest did not get exactly its own bet.");
            Pass("Both players' bets stay independent, including waiting for the other voter");
            foreach(bool infected in new[]{false,true})
            {
                Board(DefaultOwner.Player2);var snoop=Card("Snooper22",0);a=Card("Villager1",1);tm.allCurs.Add(a);
                snoop.isInfected=infected;tm.currentVotingPlayer=DefaultOwner.Player1;
                if(infected)Expect(snoop.cardData.cardSkill.TryCastActiveSkill(snoop,a),"Decay target failed.");
                tm.isTallyingVotes=true;var counts=new int[5];snoop.cardData.cardSkill.OnBeforeVoteCalculate(snoop,counts,0);tm.UpdateCurVisuals();
                Expect(counts[1]==1&&a.snooperMark.activeSelf&&View(Read(Capture()),"Villager1").snooper,"Resolved Snooper target was not public.");
                ((Skill_Snooper)snoop.cardData.cardSkill).ResetRound();tm.isTallyingVotes=false;tm.UpdateCurVisuals();
                Expect(!a.snooperMark.activeSelf&&!View(Read(Capture()),"Villager1").snooper,"Old Snooper result survived reset.");
                Pass("Public Snooper tally and round cleanup, infected="+infected);
            }
            foreach(var hostRole in new[]{DefaultOwner.Player1,DefaultOwner.Player2})
            foreach(bool infectionTruth in new[]{false,true})
            {
                Board(hostRole);var blind=Card("Blind19",4);blind.isInfected=true;
                var corpse=Card("Villager11");corpse.isDead=true;corpse.cemeteryOrder=1;corpse.wasInfectedWhenDied=infectionTruth;corpse.transform.SetParent(tm.cemeteryCardPos,false);
                if(infectionTruth)tm.allCurs.Add(corpse);tm.deadCardsData.Add(corpse.cardData);
                blind.cardData.cardSkill.ApplyAura(blind,4);tm.UpdateCurVisuals();
                bool hostTruth=hostRole==DefaultOwner.Player1;
                Expect(corpse.isRevealedByBlind&&InfectionIs(corpse,hostTruth?infectionTruth:!infectionTruth),"Host Blind report ignored perspective.");
                Expect(corpse.cardImage.sprite==(hostTruth&&infectionTruth?corpse.cardData.cursemancerArtwork:corpse.cardData.defaultArtwork),"Host saw fake Cur information as truth.");
                var wire=Capture();var viewer=session.RemoteRole;var row=View(Read(wire),"Villager11");
                Expect(row.deadInfected==(viewer==DefaultOwner.Player1&&infectionTruth)&&row.blindInfected==!infectionTruth,"Packet confused real corpse infection with Blind report.");
                var definitions=data;ApplyGuest(wire,viewer,definitions);corpse=GuestCard("Villager11");
                Expect(InfectionIs(corpse,viewer==DefaultOwner.Player1?infectionTruth:!infectionTruth),"Guest sees wrong Blind information.");
                corpse.FaceUp();Expect(InfectionIs(corpse,viewer==DefaultOwner.Player1?infectionTruth:!infectionTruth),"FaceUp bypassed Blind information policy.");
                corpse.transform.SetParent(root.transform,false);tm.UpdateCurVisuals();
                Expect(InfectionIs(corpse,viewer==DefaultOwner.Player1?infectionTruth:!infectionTruth),"Inspect/in-flight reparent bypassed Blind policy.");
                Pass("Blind false report vs automatic Cur truth, host="+hostRole+", real infection="+infectionTruth);
            }
            foreach(var hostRole in new[]{DefaultOwner.Player1,DefaultOwner.Player2})
            {
                Board(hostRole);var invoker=Card("Invoker19",4);invoker.lastPlacedBy=session.RemoteRole;tm.currentVotingPlayer=session.RemoteRole;
                var corpse=Card("Villager11");corpse.isDead=true;corpse.transform.SetParent(tm.cemeteryCardPos,false);tm.deadCardsData.Add(corpse.cardData);
                Expect(invoker.cardData.cardSkill.TryCastActiveSkill(invoker,null),"Invoker private choice setup failed.");
                Expect(!tm.discoverPanel.activeSelf&&tm.InvokerDiscoverOpenFor(session.RemoteRole),"Host can see guest's candidate UI or lost the private model.");
                var wire=Capture();Expect(Read(wire).options.Length==1,"Guest candidates were not sent from the hidden host panel.");
                var definitions=data;var viewer=session.RemoteRole;ApplyGuest(wire,viewer,definitions);
                Expect(tm.discoverPanel.activeSelf,"Controlling guest cannot see its Invoker candidates.");
                Pass("Invoker candidates are private to their controller, host="+hostRole);
            }
            Board(DefaultOwner.Player1);a=Card("Villager1",0);a.isInfected=true;a.hasUsedSkillInRitual=true;tm.allCurs.Add(a);tm.isGameOver=true;
            var ended=Capture();Expect(View(Read(ended),"Villager1").cur&&View(Read(ended),"Villager1").infected,"Match end did not disclose roles.");
            var finalDefinitions=data;ApplyGuest(ended,DefaultOwner.Player2,finalDefinitions);a=GuestCard("Villager1");
            Expect(a.cardImage.sprite==a.cardData.cursemancerArtwork&&InfectionIs(a,true),"End-of-match guest reveal failed.");
            Pass("Roles and infection reveal correctly after match end");
            SkillSoundFlow();
            FeedbackFlow();
            VoteAuditVisibility();
            Presentation();
            OnlineFlow();
            Finish(errors.Count==0,errors.Count==0?"Actual snapshot capture/apply and host/client visibility checks passed.":string.Join("; ",errors));
        }
        catch(Exception exception){Finish(false,exception.ToString());}
    }
    static void VoteAuditVisibility()
    {
        foreach(var hostRole in new[]{DefaultOwner.Player1,DefaultOwner.Player2})
        {
            Board(hostRole);tm.voteEffects=tm.gameObject.AddComponent<VoteEffectFeed>();tm.currentTurn=TurnState.SacrificePhase;tm.currentVotingPlayer=DefaultOwner.Player1;
            var snooper=Card("Snooper22",0);var gambler=Card("Gambler24",2);var target=Card("Villager10",1);snooper.isInfected=true;
            Expect(snooper.cardData.cardSkill.TryCastActiveSkill(snooper,target),"Secret Snooper cue setup failed.");
            Expect(gambler.cardData.cardSkill.TryCastActiveSkill(gambler,target),"Private Gambler cue setup failed.");
            var privateWire=Capture();var secret=Read(privateWire).voteEffects;
            Expect(secret.Length==(hostRole==DefaultOwner.Player1?0:2),"Private skill activation feed reached the wrong player.");
            if(hostRole==DefaultOwner.Player2)
            {
                WireField(privateWire,"revision",10);var definitions=data;ApplyGuest(privateWire,DefaultOwner.Player1,definitions);
                Expect(tm.voteEffects.Local.Length==2&&tm.voteEffects.Local.Any(r=>r.kind=="Snooper")&&tm.voteEffects.Local.Any(r=>r.kind=="Gambler"),"Controlling guest lost private skill cues.");
                string once=JsonUtility.ToJson(Read(privateWire));ApplyWire(privateWire);
                Expect(tm.voteEffects.Local.Length==2,"Repeated snapshot duplicated skill activation records.");
                continue;
            }
            tm.isTallyingVotes=true;int[] before={0,2,0,0,0};int[] after=(int[])before.Clone();tm.voteEffects.BeginTally(before);
            snooper.cardData.cardSkill.TryOnBeforeVoteCalculate(snooper,after,0);
            tm.voteEffects.RecordChange(snooper,"Snooper","Snooper adds one vote to its indicated target. If no valid target is available, no vote is added.",before,after);
            var publicWire=Capture();var records=Read(publicWire).voteEffects;
            Expect(records.Length==2&&records[0].id==1&&records[1].changes.Single().number==10,"Public tally leaked hidden event gaps or lost its real target delta.");
            Expect(records[1].changes[0].before==2&&records[1].changes[0].after==3,"Public Snooper explanation contains the wrong vote change.");
            WireField(publicWire,"revision",11);var defs=data;ApplyGuest(publicWire,DefaultOwner.Player2,defs);
            Expect(tm.voteEffects.Local.Last().kind=="Snooper"&&tm.voteEffects.Local.Last().changes[0].after==3,"Guest lost the public tally explanation.");
        }
        Pass("Vote audit snapshots preserve private cues, independent visible IDs, exact public deltas and idempotent guest replay");
    }
    static SkillAudio AttachSkillAudio()
    {
        var node=Node("Skill audio",root.transform).gameObject;node.SetActive(false);
        var sound=node.AddComponent<SkillAudio>();sound.bank=AssetDatabase.LoadAssetAtPath<SkillSoundBank>("Assets/AudioClip/Skills/CardSkillSounds.asset");
        node.SetActive(true);return sound;
    }
    static void SkillSoundFlow()
    {
        foreach(var hostRole in new[]{DefaultOwner.Player1,DefaultOwner.Player2})
        {
            Board(hostRole);Card("Villager1",0);var hostSound=AttachSkillAudio();var initial=Capture();
            SkillAudio.Emit("Snooper",DefaultOwner.Player1);SkillAudio.Emit("Cursermancer",DefaultOwner.Player1);
            SkillAudio.Emit("Gambler",DefaultOwner.Player2);SkillAudio.Emit("Hunter");
            var wire=Capture();var stream=Read(wire).skillSounds;var viewer=session.RemoteRole;
            Expect(stream.Length==(viewer==DefaultOwner.Player1?3:2),"Snapshot includes the wrong number of perspective-filtered skill sounds.");
            Expect(stream.Select(e=>e.id).SequenceEqual(Enumerable.Range(1,stream.Length)),"Private sound events leak through sequence gaps.");
            Expect(stream.Any(e=>e.key=="Hunter")&&stream.Any(e=>e.key=="Gambler")== (viewer==DefaultOwner.Player2),"Public Hunter/private Gambler sound filtering failed.");
            Expect(stream.Any(e=>e.key=="Snooper")== (viewer==DefaultOwner.Player1)&&stream.Any(e=>e.key=="Cursermancer")== (viewer==DefaultOwner.Player1),"Cursermancer secret sounds leaked to the other player.");
            var definitions=data;ApplyGuest(initial,viewer,definitions);var guestSound=AttachSkillAudio();ApplyWire(initial);
            Expect(guestSound.PlayedCues==0,"Initial snapshot makes historical skill sounds.");
            ApplyWire(wire);Expect(guestSound.PlayedCues==stream.Length,"Guest did not hear exactly its new permitted sounds.");
            ApplyWire(wire);Expect(guestSound.PlayedCues==stream.Length,"Repeated actual snapshot replays skill sounds.");
            Pass("Actual audio Capture/Apply filters private actions and deduplicates playback, host="+hostRole);
        }
    }
    static void FeedbackFlow()
    {
        Board(DefaultOwner.Player1);tm.currentTurn=TurnState.Player2Turn;tm.p2Hearts=0;
        var own=Card("Villager10",0);var before=Capture();WireField(before,"revision",10);
        int id=View(Read(before),"Villager10").id;
        var command=new OnlineCommand{match=session.MatchId,sequence=1,action=OnlineAction.Return,phase=(int)TurnState.Player2Turn,source=id};
        typeof(OnlineGameBridge).GetMethod("ApplyCommand",Private).Invoke(bridge,new object[]{command});
        Expect(!own.GetComponent<CardInteraction>().isPlaced&&tm.p2Hearts==2,"Host refuses a guest's refunding retrieval at zero hearts.");
        Expect(typeof(OnlineGameBridge).GetField("commandError",Private).GetValue(bridge)==null,"Successful own retrieval received a rejection.");
        var defs=data;ApplyGuest(before,DefaultOwner.Player2,defs);
        own=GuestCard("Villager10");
        Expect(own.GetComponent<CardInteraction>().ReturnPaymentCost==0,"Guest projects a fee for its own retrieval.");
        Expect(bridge.Send(OnlineAction.Return,id),"Guest cannot submit a zero-heart retrieval.");
        var reservations=(Dictionary<int,int>)typeof(OnlineGameBridge).GetField("resourceReservations",Private).GetValue(bridge);
        Expect(reservations.Values.Single()==0,"Guest reserves hearts that its own retrieval should refund.");
        Pass("Host/client zero-heart retrieval uses the real charge rather than the refund amount");

        Board(DefaultOwner.Player1);tm.currentTurn=TurnState.Player2Turn;
        own=Card("Villager10",0);own.GetComponent<CardInteraction>().lockState=1;
        id=View(Read(Capture()),"Villager10").id;
        command=new OnlineCommand{match=session.MatchId,sequence=1,action=OnlineAction.Return,phase=(int)TurnState.Player2Turn,source=id};
        typeof(OnlineGameBridge).GetMethod("ApplyCommand",Private).Invoke(bridge,new object[]{command});
        string rejection=(string)typeof(OnlineGameBridge).GetField("commandError",Private).GetValue(bridge);
        Expect(rejection!=null&&rejection.Contains("next action turn")&&rejection.Contains("10"),"Guest does not receive the named card's actual lock reason.");
        Expect(tm.GetOnlineFeedback(DefaultOwner.Player1)==null,"Guest command failure appears in the host's feedback.");
        tm.ShowLocalActionFeedback("private local explanation");
        string wire=JsonUtility.ToJson(Capture());
        Expect(!wire.Contains("private local explanation"),"Local action feedback is included in the opponent's snapshot.");
        Expect(wire.Contains("next action turn"),"The rejection receipt does not preserve the reason.");
        Pass("Rejection receipts explain card locks while local notices remain private");
    }
    static void Finish(bool passed,string message)
    {
        SessionState.SetBool(Request,false);
        File.WriteAllText(Path.Combine(Directory.GetParent(Application.dataPath).FullName,"OnlineVisibilityValidationResult.json"),JsonUtility.ToJson(new Report{passed=passed,assertions=assertions,message=message,cases=cases.ToArray()},true));
        Debug.Log("VISIBILITY RESULT: "+message);EditorApplication.Exit(passed?0:1);
    }
}

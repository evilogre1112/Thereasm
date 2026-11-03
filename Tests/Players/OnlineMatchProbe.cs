#if !THEREASM_RELEASE
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Collections;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

// Copied only into the isolated validation project. Runs only with explicit probe arguments.
public class OnlineMatchProbe : MonoBehaviour
{
    string folder,mode,room,episode;
    bool host,started,acted,wrongDraftChecked;
    bool comparedDeck, comparedBoard;
    bool dragInProgress, pointerFollowVerified, snapshotDuringDragVerified, instantDropVerified, invalidDropVerified, rollbackVerified;
    int stage,assertions,actedDraft=-1,turn=-1,actionStep;
    TurnState observed=(TurnState)(-1);
    float began,stageBegan,due,nextReport;
    string firstMatch,actionKey;
    OnlineSession watchedSession;
    int privacyPackets;
    bool cemeteryVerified, cemeteryOpened, disconnectMenuVerified, queuedVotesVerified;
    [Serializable] class WireCard { public string data;public bool cur,infected,deadInfected,ritualUsed,used; }
    [Serializable] class WireSnapshot { public string match;public bool over;public WireCard[] cards; }
    readonly List<string> checks=new List<string>(),errors=new List<string>();
    [Serializable] class Report
    {
        public bool passed;public int stage,assertions,role,phase,draft;public string message,match;public string[] checks,deck,board;
        public bool pointerFollowVerified, snapshotDuringDragVerified, instantDropVerified, invalidDropVerified, rollbackVerified;
        public int privacyPackets;
        public bool cemeteryVerified, disconnectMenuVerified, queuedVotesVerified;
        public string language;
    }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Launch()
    {
        if(!Environment.GetCommandLineArgs().Contains("-thereasm-probe"))return;
        new GameObject("Online match probe").AddComponent<OnlineMatchProbe>();
    }
    string Arg(string key)
    {var args=Environment.GetCommandLineArgs();int i=Array.IndexOf(args,key);return i>=0&&i+1<args.Length?args[i+1]:"";}
    void Awake()
    {
        DontDestroyOnLoad(gameObject);folder=Arg("-probe-folder");mode=Arg("-probe-mode");host=mode=="host";
        room=Arg("-probe-room");episode=Arg("-probe-episode");Directory.CreateDirectory(folder);
        began=stageBegan=Time.realtimeSinceStartup;
        Application.logMessageReceived+=(message,stack,type)=>{if(type==LogType.Exception||message.Contains("không áp dụng được snapshot"))errors.Add(message);};
    }
    void Expect(bool value,string message){assertions++;if(!value)throw new Exception(message);}
    void Mark(string name){File.WriteAllText(Path.Combine(folder,mode+"."+name),"ok");}
    bool Other(string name)=>File.Exists(Path.Combine(folder,(host?"client":"host")+"."+name));
    void Next(string name){checks.Add(name);Debug.Log("ONLINE PROBE "+mode+": "+name);stage++;stageBegan=Time.realtimeSinceStartup;acted=false;due=Time.realtimeSinceStartup+.3f;}
    CardInstance Card(int number)
    {
        var tm=TurnManager.instance;
        return FindObjectsByType<CardInstance>(FindObjectsInactive.Include,FindObjectsSortMode.None).FirstOrDefault(c=>
            c.cardData!=null&&c.cardData.owner!=DefaultOwner.Neutral&&c.cardData.cardNumber==number&&
            (tm==null||tm.discoverPanel==null||!c.transform.IsChildOf(tm.discoverPanel.transform)));
    }
    Vector2 Point(GameObject target)
    {
        Canvas.ForceUpdateCanvases();var rect=target.GetComponent<RectTransform>();var corners=new Vector3[4];rect.GetWorldCorners(corners);
        foreach(float x in new[]{.12f,.5f,.88f})foreach(float y in new[]{.5f,.2f,.8f})
        {
            var point=RectTransformUtility.WorldToScreenPoint(null,Vector3.Lerp(Vector3.Lerp(corners[0],corners[3],x),Vector3.Lerp(corners[1],corners[2],x),y));
            var data=new PointerEventData(EventSystem.current){position=point};var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(data,hits);
            if(hits.Count>0&&(hits[0].gameObject==target||hits[0].gameObject.transform.IsChildOf(target.transform)))return point;
        }
        throw new Exception("No raycastable UI surface: "+target.name);
    }
    void Click(GameObject target)
    {
        Vector2 point=Point(target);var data=new PointerEventData(EventSystem.current){position=point,button=PointerEventData.InputButton.Left};
        var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(data,hits);data.pointerCurrentRaycast=hits[0];data.pointerPressRaycast=hits[0];
        ExecuteEvents.ExecuteHierarchy(hits[0].gameObject,data,ExecuteEvents.pointerClickHandler);
    }
    bool Button(string method)
    {
        var button=FindObjectsByType<Button>(FindObjectsInactive.Exclude,FindObjectsSortMode.None).FirstOrDefault(b=>
            b.IsInteractable()&&Enumerable.Range(0,b.onClick.GetPersistentEventCount()).Any(i=>b.onClick.GetPersistentMethodName(i)==method));
        if(button==null)return false; // The saved UI refreshes its button state in LateUpdate.
        Expect(true,"Enabled button "+method);Click(button.gameObject);return true;
    }
    void Drag(CardInstance card,SeatDropZone seat,bool right=false,bool expectRejection=false)
    {
        Vector2 start=Point(card.gameObject),end=Point(seat.gameObject);
        var data=new PointerEventData(EventSystem.current){position=start,pressPosition=start,button=right?PointerEventData.InputButton.Right:PointerEventData.InputButton.Left,
            pointerDrag=card.gameObject,dragging=true,eligibleForClick=false};
        var tm=TurnManager.instance;
        bool physical=!right&&(tm.currentTurn==TurnState.Player1Turn||tm.currentTurn==TurnState.Player2Turn);
        var origin=card.transform.position;
        var parent=card.transform.parent;
        int p1=tm.p1Hearts,p2=tm.p2Hearts;
        if(OnlineSession.Guest&&physical&&!invalidDropVerified)
        {
            ExecuteEvents.Execute(card.gameObject,data,ExecuteEvents.beginDragHandler);
            data.position=new Vector2(-100,-100);data.delta=data.position-start;
            ExecuteEvents.Execute(card.gameObject,data,ExecuteEvents.dragHandler);
            ExecuteEvents.Execute(card.gameObject,data,ExecuteEvents.endDragHandler);
            Expect(card.transform.parent==parent&&Vector3.Distance(card.transform.position,origin)<.1f&&!Pending&&
                !card.GetComponent<OnlineCardInput>().IsPresenting&&!CardInteraction.isAnyCardDragging,
                "Invalid drop did not restore the client card.");
            Expect(tm.p1Hearts==p1&&tm.p2Hearts==p2,"Invalid visual drop spent hearts.");
            invalidDropVerified=true;checks.Add("Client invalid drop restores its original position without sending a command");
            data.position=start;data.delta=Vector2.zero;
        }
        ExecuteEvents.Execute(card.gameObject,data,ExecuteEvents.beginDragHandler);
        data.position=end;data.delta=end-start;ExecuteEvents.Execute(card.gameObject,data,ExecuteEvents.dragHandler);
        if(OnlineSession.Guest)
        {
            var input=card.GetComponent<OnlineCardInput>();
            if(physical)
            {
                Vector2 displacement=RectTransformUtility.WorldToScreenPoint(null,card.transform.position)-RectTransformUtility.WorldToScreenPoint(null,origin);
                Expect(input.IsMovingCard&&Vector2.Distance(displacement,end-start)<2,"Guest card did not follow the pointer immediately.");
                Expect(TargetingArrow.instance==null||!TargetingArrow.instance.gameObject.activeSelf,"Physical card drag displayed a target marker.");
                pointerFollowVerified=true;
                if(!snapshotDuringDragVerified)
                {dragInProgress=true;StartCoroutine(HoldDrag(card,seat,data,p1,p2));return;}
            }
            else
                Expect(!input.IsMovingCard&&Vector3.Distance(card.transform.position,origin)<.1f&&
                    TargetingArrow.instance!=null&&TargetingArrow.instance.gameObject.activeSelf,"Skill/ritual targeting moved the card instead of its target marker.");
        }
        FinishDrag(card,seat,data,p1,p2,physical,expectRejection);
    }
    int AppliedRevision()=> (int)typeof(OnlineGameBridge).GetField("appliedRevision",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(OnlineGameBridge.Instance);
    IEnumerator HoldDrag(CardInstance card,SeatDropZone seat,PointerEventData data,int p1,int p2)
    {
        var held=card.transform.position;int before=AppliedRevision();float deadline=Time.realtimeSinceStartup+6;
        OnlineSession.Instance.RequestSnapshot();
        while(AppliedRevision()<=before&&Time.realtimeSinceStartup<deadline)
        {
            yield return null;
            if(Vector3.Distance(card.transform.position,held)>.1f)
            {dragInProgress=false;Finish(false,"Incoming snapshot pulled the client card away from the pointer.");yield break;}
        }
        try
        {
            Expect(AppliedRevision()>before,"No real Photon snapshot arrived during the held drag.");
            snapshotDuringDragVerified=true;checks.Add("Client card follows the pointer and survives incoming Photon snapshots during drag");
            FinishDrag(card,seat,data,p1,p2,true);
        }
        catch(Exception exception){Finish(false,exception.ToString());}
        dragInProgress=false;
    }
    void FinishDrag(CardInstance card,SeatDropZone seat,PointerEventData data,int p1,int p2,bool physical,bool expectRejection=false)
    {
        var tm=TurnManager.instance;
        var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(data,hits);Expect(hits.Count>0,"No drop surface.");data.pointerCurrentRaycast=hits[0];
        ExecuteEvents.ExecuteHierarchy(hits[0].gameObject,data,ExecuteEvents.dropHandler);
        if(OnlineSession.Guest)Expect(tm.p1Hearts==p1&&tm.p2Hearts==p2,"Guest mutated hearts locally during OnDrop.");
        ExecuteEvents.Execute(card.gameObject,data,ExecuteEvents.endDragHandler);
        if(OnlineSession.Guest&&physical&&!expectRejection)
        {
            Expect(Pending&&card.GetComponent<OnlineCardInput>().IsPresenting&&Vector3.Distance(card.transform.position,seat.cardPos.position)<.1f,
                "Guest drop waited for the host instead of appearing immediately at the seat.");
            Expect(tm.p1Hearts==p1&&tm.p2Hearts==p2,"Visual prediction spent hearts before host acknowledgement.");
            instantDropVerified=true;
        }
    }
    void Show(DefaultOwner owner)
    { if(owner==DefaultOwner.Player1)GameStartUI.Current.ShowCurHand();else GameStartUI.Current.ShowVillageHand(); }
    bool Pending=>OnlineSession.Guest&&OnlineGameBridge.Instance!=null&&OnlineGameBridge.Instance.CommandPending;
    bool CemeteryCheck()
    {
        if(cemeteryVerified)return true;
        var tm=TurnManager.instance;
        var dead=FindObjectsByType<CardInstance>(FindObjectsInactive.Include,FindObjectsSortMode.None).Where(c=>c.isDead).OrderBy(c=>c.cemeteryOrder).ToArray();
        if(dead.Length==0)return true;
        if(!cemeteryOpened){tm.ToggleCemeteryView();cemeteryOpened=true;due=Time.realtimeSinceStartup+.25f;return false;}
        if(!File.Exists(Path.Combine(folder,mode+".cemetery")))
        {
        Canvas.ForceUpdateCanvases();
        Expect(tm.CemeteryContent!=null&&dead.All(c=>c.transform.parent==tm.CemeteryContent),"Cemetery omitted an in-flight or newly dead card.");
        Expect(tm.deadCardsData.Count==dead.Length,"Cemetery model count differs from visible corpses.");
        Expect(dead.All(c=>c.transform.localScale==Vector3.one&&(c.GetComponent<UnityEngine.UI.LayoutElement>()==null||!c.GetComponent<UnityEngine.UI.LayoutElement>().ignoreLayout)),"Open cemetery did not normalize card layout/scale.");
        var rect=(RectTransform)tm.CemeteryContent;var corners=new Vector3[4];var bounds=new Bounds();bool initial=true;
        foreach(var card in dead){card.GetComponent<RectTransform>().GetWorldCorners(corners);foreach(var corner in corners){var local=rect.InverseTransformPoint(corner);if(initial){bounds=new Bounds(local,Vector3.zero);initial=false;}else bounds.Encapsulate(local);}}
        Expect(Vector2.Distance(bounds.center,rect.rect.center)<2,"Cemetery cards are not centered.");
        File.WriteAllText(Path.Combine(folder,mode+".cemetery-cards"),string.Join("|",dead.Select(c=>c.cemeteryOrder+":"+c.cardData.name)));
        Mark("cemetery");
        }
        if(!Other("cemetery")){due=Time.realtimeSinceStartup+.05f;return false;}
        Expect(File.ReadAllText(Path.Combine(folder,"host.cemetery-cards"))==File.ReadAllText(Path.Combine(folder,"client.cemetery-cards")),"Host/client cemetery contents or order diverged.");
        Expect(Button("CloseCemeteryView"),"Saved cemetery Close button is not clickable.");
        cemeteryVerified=true;checks.Add("Matching cemetery order, centered grid and saved Close button on both peers");return true;
    }
    bool Ready=>OnlineSession.Instance!=null&&OnlineSession.Instance.Connected&&OnlineSession.Instance.PlayerCount==2&&OnlineSession.Instance.RolesSynchronized;
    void InspectPacket(string kind,string payload)
    {
        if(kind!="snapshot"||OnlineSession.LocalRole!=DefaultOwner.Player2)return;
        var wire=JsonUtility.FromJson<WireSnapshot>(OnlineSnapshotCodec.Decode(payload));
        if(wire==null||wire.over||wire.match!=OnlineSession.Instance.MatchId||wire.cards==null)return;
        foreach(var card in wire.cards)
            if(card.cur||card.infected||card.deadInfected||card.ritualUsed||card.data.Contains(":Snooper22")&&card.used)
            {Finish(false,"Villager received private identity/infection/ritual/Decay state in a real Photon packet.");return;}
        privacyPackets++;
    }
    void Update()
    {
        try
        {
            float now=Time.realtimeSinceStartup;
            if(now-began>300||now-stageBegan>75)throw new Exception("Timed out at stage "+stage+", turn="+turn+", action="+actionStep);
            if(now>=nextReport){Write(false,"Running");nextReport=now+.5f;}
            if(now<due||dragInProgress)return;
            var session=OnlineSession.Instance;var tm=TurnManager.instance;var deck=DeckManager.instance;
            if(!host&&session!=null&&watchedSession!=session){watchedSession=session;session.HostMessage+=InspectPacket;}
            switch(stage)
            {
                case 0:
                    if(session==null)return;
                    FindFirstObjectByType<OnlineLobbyView>(FindObjectsInactive.Include).Open();
                    if(!acted){if(host)session.CreateRoom(room);else if(Other("room"))session.JoinRoom(room);else return;acted=true;}
                    if(host&&session.Connected)Mark("room");
                    if(!Ready)return;
                    Expect(host?OnlineSession.LocalRole==DefaultOwner.Player1:OnlineSession.LocalRole==DefaultOwner.Player2,"Automatic opposite role assignment failed.");
                    Next("Connected with opposite automatic roles");break;
                case 1:
                    if(!host&&!acted){if(!Button("ChooseCur"))return;acted=true;}
                    if(OnlineSession.LocalRole!=(host?DefaultOwner.Player2:DefaultOwner.Player1)||!Ready)return;
                    Mark("swapped");if(!Other("swapped"))return;Next("Guest role selection swapped both roles");break;
                case 2:
                    if(episode=="A")
                    {
                        if(!host&&!acted){if(!Button("ChooseVillage"))return;acted=true;}
                        if(OnlineSession.LocalRole!=(host?DefaultOwner.Player1:DefaultOwner.Player2)||!Ready)return;
                    }
                    Mark("roles");if(!Other("roles"))return;
                    if(host)session.StartMatch();Next("Roles confirmed before synchronized scene transition");break;
                case 3:
                    if(SceneManager.GetActiveScene().name!="MainBoard"||tm==null||deck==null||OnlineGameBridge.Instance==null||OnlineSession.Guest&&!OnlineGameBridge.Instance.HasSnapshot)return;
                    if(deck.DraftTotal==0)return;
                    Expect(deck.DraftTotal==8,"Expected exactly eight neutral groups, got "+deck.DraftTotal);
                    firstMatch=session.MatchId;Expect(!string.IsNullOrEmpty(firstMatch),"Missing authoritative match id.");
                    if(!acted){File.WriteAllText(Path.Combine(folder,mode+".match"),firstMatch);Mark("loaded");acted=true;}if(!Other("loaded"))return;
                    Expect(File.ReadAllText(Path.Combine(folder,(host?"client":"host")+".match"))==firstMatch,"Peers entered different match epochs.");
                    Next("Both processes loaded the same board and match id");break;
                case 4:
                    if(deck.IsReady){Mark("drafted");if(Other("drafted"))Next("All eight draft picks synchronized: first four Player1, last four Player2");return;}
                    if(deck.IsPicking||Pending)return;
                    if(deck.DraftChooser!=OnlineSession.LocalRole)
                    {
                        if(!wrongDraftChecked&&deck.DraftOptions().Length>0){int step=deck.DraftStep;deck.PickNeutralCard(deck.DraftOptions()[0],true);Expect(deck.DraftStep==step&&!Pending,"Wrong player submitted a neutral pick.");wrongDraftChecked=true;}
                        return;
                    }
                    if(actedDraft==deck.DraftStep)return;
                    var offers=deck.DraftOptions();if(offers.Length<2)return;
                    string[] preferred={"Elder17","Preacher18","Invoker19","Trader20","Sacrifier21","Controller22","Witch23","Gambler24"};
                    int side=Array.FindIndex(offers,c=>preferred.Contains(c.name));if(side<0)side=0;
                    if(!Button(side==0?"PickDraftLeft":"PickDraftRight"))return;actedDraft=deck.DraftStep;due=now+.6f;break;
                case 5:
                    if(!deck.IsReady||Pending)return;
                    if(!acted){File.WriteAllText(Path.Combine(folder,mode+".deck"),string.Join("|",deck.allCards.Select(c=>c.name)));Mark("deckready");acted=true;}
                    if(!Other("deckready"))return;
                    if(!comparedDeck)
                    {
                        Expect(File.ReadAllText(Path.Combine(folder,"host.deck"))==File.ReadAllText(Path.Combine(folder,"client.deck")),"Selected deck/order diverged.");
                        string requested=Arg("-thereasm-language");
                        if(!string.IsNullOrEmpty(requested))Expect(Localizer.Language==(requested=="vi"?GameLanguage.Vietnamese:GameLanguage.English),"Local language selection was overwritten by peer state.");
                        foreach(int number in new[]{1,10})
                        {
                            var owned=Card(number);var ownership=owned.GetComponent<CardOwnershipView>();ownership.Refresh();
                            Expect(ownership.ownerLabel.text==Localizer.T(owned.cardData.owner==OnlineSession.LocalRole?"CỦA BẠN":"ĐỐI THỦ"),"Ownership label does not match local perspective/language.");
                        }
                        checks.Add("Local language and public ownership labels remain independent of the other peer");comparedDeck=true;
                    }
                    if(OnlineSession.LocalRole!=DefaultOwner.Player1){if(tm.currentTurn!=TurnState.SetupPhase)Next("Cursermancer setup synchronized");return;}
                    if(!tm.IsCur(Card(1))){Show(DefaultOwner.Player1);if(actionKey!="cur1"){actionKey="cur1";due=now+.9f;return;}Click(Card(1).gameObject);due=now+.5f;return;}
                    if(!tm.IsCur(Card(11))){Show(DefaultOwner.Player2);if(actionKey!="cur11"){actionKey="cur11";due=now+.9f;return;}Click(Card(11).gameObject);due=now+.5f;return;}
                    if(!(OnlineSession.Guest?OnlineGameBridge.Instance.CanStartGame:tm.CanStartGame))return;
                    if(!tm.btnStartGame.GetComponent<Button>().IsInteractable())return;
                    Click(tm.btnStartGame);Next("Cursermancer setup synchronized");break;
                case 6:
                    if(tm.currentTurn==TurnState.SetupPhase||Pending)return;
                    if(tm.currentTurn==TurnState.CursePhase)
                    {File.WriteAllText(Path.Combine(folder,mode+".board"),Board());Mark("boardready");actionStep=0;Next("Both players placed/moved/swapped cards on the shared board");return;}
                    if(tm.currentTurn!=observed){observed=tm.currentTurn;turn++;actionStep=0;actionKey=null;due=now+.4f;return;}
                    if(!OnlineSession.LocalTurn)return;
                    if(actionStep==0){Show(tm.currentTurn==TurnState.Player1Turn?DefaultOwner.Player1:DefaultOwner.Player2);actionStep=1;due=now+.9f;return;}
                    if(actionStep==1)
                    {
                        if(turn==0)Drag(Card(10),tm.allSeats[0]);
                        else if(turn==1)Drag(Card(1),tm.allSeats[1]);
                        else if(turn==2)Drag(Card(10),tm.allSeats[2]);
                        else if(turn==3)Drag(Card(10),tm.allSeats[3]);
                        else if(turn==4)Drag(Card(10),tm.allSeats[1]);
                        else
                        {
                            int empty=tm.allSeats.FindIndex(s=>s.cardPos.GetComponentInChildren<CardInstance>()==null);
                            int[] numbers=tm.currentTurn==TurnState.Player1Turn?new[]{2,3,4,5,6,7,8}:new[]{11,12,13,14,15,16,9};
                            var candidate=numbers.Select(Card).FirstOrDefault(c=>c!=null&&!c.isDead&&!c.GetComponent<CardInteraction>().isPlaced);
                            if(empty>=0&&candidate!=null)Drag(candidate,tm.allSeats[empty]);
                        }
                        actionStep=2;due=now+.6f;return;
                    }
                    if(actionStep==2)
                    {
                        Expect(Card(10).GetComponent<CardInteraction>().isPlaced,"Shared card 10 disappeared.");
                        if(turn==0)Expect(tm.GetSeatIndex(Card(10).GetComponent<CardInteraction>())==0&&tm.p2Hearts==3,"First guest/host placement did not synchronize.");
                        if(turn==2)Expect(tm.GetSeatIndex(Card(10).GetComponent<CardInteraction>())==2,"Move failed.");
                        if(turn==3)Expect(tm.GetSeatIndex(Card(10).GetComponent<CardInteraction>())==3,"Moving an opponent card failed.");
                        if(turn==4)Expect(tm.GetSeatIndex(Card(10).GetComponent<CardInteraction>())==1&&tm.GetSeatIndex(Card(1).GetComponent<CardInteraction>())==3,"Board swap failed: "+Board()+", pending="+Pending+", feedback="+tm.feedbackMessage);
                        if(turn==4&&OnlineSession.Guest&&!rollbackVerified)
                        {
                            // The board swap left only 2 hearts; this hand-to-board swap costs 3.
                            Expect(tm.p2Hearts==2,"Unexpected budget for rejection test.");
                            Drag(Card(11),tm.allSeats[1],expectRejection:true);actionStep=4;due=now+.6f;return;
                        }
                        Click(tm.btnEndTurn);actionStep=3;due=now+.5f;
                    }
                    if(actionStep==4)
                    {
                        var returned=Card(11);var restored=Card(10);
                        Expect(!returned.GetComponent<CardInteraction>().isPlaced&&returned.transform.parent==returned.GetComponent<CardInteraction>().handParent&&
                            tm.GetSeatIndex(restored.GetComponent<CardInteraction>())==1&&tm.p2Hearts==2&&
                            !returned.GetComponent<OnlineCardInput>().IsPresenting&&!restored.GetComponent<OnlineCardInput>().IsPresenting&&
                            returned.GetComponent<CanvasGroup>().blocksRaycasts&&restored.GetComponent<CanvasGroup>().blocksRaycasts,
                            "Rejected swap did not restore both client cards and authoritative hearts.");
                        rollbackVerified=true;checks.Add("Unaffordable swap is rejected locally or rolled back by the host, with both cards and hearts preserved");
                        Click(tm.btnEndTurn);actionStep=3;due=now+.5f;
                    }
                    break;
                case 7:
                    if(!Other("boardready"))return;
                    if(!comparedBoard){Expect(File.ReadAllText(Path.Combine(folder,"host.board"))==File.ReadAllText(Path.Combine(folder,"client.board")),"Board occupancy/hearts diverged before Curse Phase.");comparedBoard=true;}
                    if(tm.currentTurn==TurnState.SacrificePhase){Next("Cursermancer infection command synchronized");return;}
                    if(!OnlineSession.LocalTurn||Pending)return;
                    if(actionStep==0){Drag(Card(11),tm.allSeats[tm.GetSeatIndex(Card(10).GetComponent<CardInteraction>())]);actionStep=1;due=now+.5f;return;}
                    Expect(Card(10).isInfected,"Infection failed on acting Cursermancer.");Click(tm.btnEndTurn);due=now+.5f;break;
                case 8:
                    if(tm.isGameOver){Next("Vote commands and tally produced the same result");return;}
                    if(tm.currentTurn!=TurnState.SacrificePhase){Next("Vote commands and tally produced the same result");return;}
                    if(tm.isTallyingVotes||!OnlineSession.LocalTurn)return;
                    var voters=tm.allSeats.Select(s=>s.cardPos.GetComponentInChildren<CardInstance>()).Where(c=>c!=null&&!c.isDead&&c.cardData.owner==OnlineSession.LocalRole).ToArray();
                    var unvoted=voters.Where(c=>!File.Exists(Path.Combine(folder,mode+".vote."+c.cardData.cardNumber))).ToArray();
                    if(unvoted.Length>0)
                    {
                        var victim=Card(OnlineSession.LocalRole==DefaultOwner.Player1?11:1);
                        foreach(var voter in unvoted){Drag(voter,tm.allSeats[tm.GetSeatIndex(victim.GetComponent<CardInteraction>())]);Mark("vote."+voter.cardData.cardNumber);}
                        if(OnlineSession.Guest)
                        {
                            Expect(OnlineGameBridge.Instance.PendingCount>=unvoted.Length,"Could not queue all votes in the same frame without an ACK.");
                            Expect(OnlineGameBridge.Instance.Voted==OnlineGameBridge.Instance.RequiredVotes,"Vote progress did not respond before network confirmation.");
                            queuedVotesVerified=true;checks.Add("Queued all own votes and End Turn in one frame without waiting for network ACKs");
                        }
                    }
                    Click(tm.btnEndTurn);due=now+.5f;break;
                case 9:
                    if(!CemeteryCheck())return;
                    if(tm.isGameOver){Next("Match completed");return;}
                    if(Pending||!OnlineSession.LocalTurn||tm.isTallyingVotes)return;
                    if(actionKey!=tm.currentTurn.ToString()){actionKey=tm.currentTurn.ToString();actionStep=0;}
                    if(actionStep==0){Show(OnlineSession.LocalRole);actionStep=1;due=now+.9f;return;}
                    if(OnlineSession.LocalRole==DefaultOwner.Player1)
                    {
                        var cur=new[]{Card(1),Card(11)}.FirstOrDefault(c=>c!=null&&!c.isDead);
                        if(cur!=null&&!cur.GetComponent<CardInteraction>().isPlaced){Show(cur.cardData.owner);if(actionStep==1){actionStep=2;due=now+.9f;return;}Drag(cur,tm.allSeats[1]);due=now+.5f;return;}
                        Click(tm.btnEndTurn);due=now+.6f;
                    }
                    else
                    {
                        var hunter=Card(9);var cur=new[]{Card(1),Card(11)}.FirstOrDefault(c=>c!=null&&!c.isDead);
                        if(!hunter.GetComponent<CardInteraction>().isPlaced){Drag(hunter,tm.allSeats[0]);actionStep=2;due=now+.5f;return;}
                        if(tm.p2Hearts<5){Click(tm.btnEndTurn);due=now+.5f;return;}
                        if(cur!=null&&cur.GetComponent<CardInteraction>().isPlaced){Drag(hunter,tm.allSeats[tm.GetSeatIndex(cur.GetComponent<CardInteraction>())],true);due=now+.7f;}
                    }
                    break;
                case 10:
                    if(!CemeteryCheck())return;
                    if(!tm.isGameOver)return;
                    Mark("over");if(!Other("over"))return;
                    if(!host&&!acted){session.SwapRoles();acted=true;}
                    var expected=episode=="A"?(host?DefaultOwner.Player2:DefaultOwner.Player1):(host?DefaultOwner.Player1:DefaultOwner.Player2);
                    if(OnlineSession.LocalRole!=expected||!Ready)return;
                    if(host&&!acted){session.StartMatch();acted=true;}
                    if(session.MatchId==firstMatch)return;
                    Next("Roles swapped for rematch with a fresh authoritative match id");break;
                case 11:
                    if(tm==null||deck==null||tm.isGameOver||OnlineSession.Guest&&!OnlineGameBridge.Instance.HasSnapshot)return;
                    if(deck.DraftTotal!=8)return;
                    if(deck.DraftStep==0)
                    {
                        if(OnlineSession.LocalRole==DefaultOwner.Player1&&!acted&&Button("PickDraftLeft")){acted=true;due=now+.7f;}
                        return;
                    }
                    Mark("rematch");if(!Other("rematch"))return;
                    Expect(errors.Count==0,"Runtime errors: "+string.Join("; ",errors));
                    if(!host)Expect(pointerFollowVerified&&snapshotDuringDragVerified&&instantDropVerified&&invalidDropVerified,"Client drag presentation coverage was incomplete.");
                    if(!host&&episode=="A")Expect(rollbackVerified,"Client prediction rejection coverage was incomplete.");
                    if(!host)Expect(privacyPackets>0,"No actual Villager snapshots inspected for information leakage.");
                    if(!host)Expect(queuedVotesVerified,"Continuous client input coverage was incomplete.");
                    Next("Full match and rematch passed; checking disconnection navigation");break;
                case 12:
                    bool quitting=episode=="A"?host:!host;
                    if(quitting)
                    {
                        if(!Other("exitReady"))return;
                        session.Leave();Finish(true,"Match, cemetery and rematch passed; peer left deliberately for navigation validation.");return;
                    }
                    if(!acted){tm.ToggleCemeteryView();Mark("exitReady");acted=true;return;}
                    if(!session.PeerLost)return;
                    if(!Button("ReturnToMenu"))return;
                    Next("Menu remains clickable after the other peer leaves, including over cemetery/shield");due=now+2;break;
                case 13:
                    if(SceneManager.GetActiveScene().name!="MenuScenes")return;
                    Expect(!OnlineSession.Active,"Old online match is still active after returning to menu.");
                    disconnectMenuVerified=true;
                    Finish(true,"Online match/rematch, cemetery synchronization and return to menu after peer exit passed.");break;
            }
        }
        catch(Exception exception){Finish(false,exception.ToString());}
    }
    void Write(bool passed,string message)
    {
        var tm=TurnManager.instance;var deck=DeckManager.instance;
        var report=new Report{passed=passed,stage=stage,assertions=assertions,message=message,role=(int)OnlineSession.LocalRole,
            language=Localizer.Language.ToString(),pointerFollowVerified=pointerFollowVerified,snapshotDuringDragVerified=snapshotDuringDragVerified,
            instantDropVerified=instantDropVerified,invalidDropVerified=invalidDropVerified,
            rollbackVerified=rollbackVerified,
            privacyPackets=privacyPackets,
            cemeteryVerified=cemeteryVerified,disconnectMenuVerified=disconnectMenuVerified,queuedVotesVerified=queuedVotesVerified,
            match=OnlineSession.Instance!=null?OnlineSession.Instance.MatchId:null,phase=tm!=null?(int)tm.currentTurn:-1,draft=deck!=null?deck.DraftStep:-1,checks=checks.ToArray(),
            deck=deck!=null&&deck.allCards!=null?deck.allCards.Where(c=>c!=null).Select(c=>c.name).ToArray():new string[0],
            board=tm!=null&&tm.allSeats!=null?new[]{Board()}:new string[0]};
        File.WriteAllText(Path.Combine(folder,mode+".json"),JsonUtility.ToJson(report,true));
    }
    string Board()
    {
        var tm=TurnManager.instance;
        return tm.p1Hearts+":"+tm.p2Hearts+"|"+string.Join("|",tm.allSeats.Select(s=>{
            var card=s.cardPos.GetComponentInChildren<CardInstance>();return card!=null?card.cardData.name+":"+card.GetComponent<CardInteraction>().lockState:"empty";
        }));
    }
    void Finish(bool passed,string message){Write(passed,message);Debug.Log("ONLINE PROBE RESULT: "+message);Application.Quit(passed?0:1);enabled=false;}
}
#endif

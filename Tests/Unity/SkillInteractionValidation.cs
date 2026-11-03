using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Object = UnityEngine.Object;

// Real skill assets and MonoBehaviours in an isolated Play Mode scene; no networking.
[InitializeOnLoad]
public static class SkillInteractionValidation
{
    const string Request="Thereasm.SkillValidation";
    static TurnManager tm;
    static DeckManager deck;
    static GameObject root;
    static int assertions;
    static readonly List<string> cases=new List<string>();
    static readonly List<string> errors=new List<string>();
    static readonly BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    [Serializable] class Report { public bool passed; public int assertions; public string message; public string[] cases; }
    static SkillInteractionValidation()
    {
        EditorApplication.playModeStateChanged+=state=>{
            if(state==PlayModeStateChange.EnteredPlayMode&&SessionState.GetBool(Request,false)) EditorApplication.delayCall+=Check;
        };
    }
    public static void Run()
    {
        SessionState.SetBool(Request,true);
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        EditorApplication.isPlaying=true;
    }
    static void Expect(bool value,string message){assertions++;if(!value)throw new Exception(message);}
    static RectTransform Node(string name,Transform parent)
    { var go=new GameObject(name,typeof(RectTransform));go.transform.SetParent(parent,false);return (RectTransform)go.transform; }
    static CardInteraction I(CardInstance card)=>card.GetComponent<CardInteraction>();
    static CardData Data(string asset)=>AssetDatabase.LoadAssetAtPath<CardData>("Assets/ScriptableObjects/"+asset+".asset");
    static void Reset(int seats=5)
    {
        if(root!=null)Object.DestroyImmediate(root);
        GameSettings.currentMode=GameMode.PvP;Time.timeScale=1;
        Skill_Gambler.ResetComboChain();
        root=new GameObject("Skill validation",typeof(RectTransform),typeof(Canvas));
        tm=Node("Turns",root.transform).gameObject.AddComponent<TurnManager>();tm.enabled=false;
        tm.allSeats=new List<SeatDropZone>();tm.currentTurn=TurnState.Player2Turn;tm.p1Hearts=tm.p2Hearts=5;
        tm.cemeteryCardPos=Node("Cemetery",root.transform);tm.globalCemetery=tm.cemeteryCardPos;
        tm.cemeteryViewPanel=Node("CemeteryViewPanel",root.transform).gameObject;tm.cemeteryViewPanel.SetActive(false);
        tm.discoverPanel=Node("Discover",root.transform).gameObject;tm.discoverPanel.SetActive(false);
        tm.discoverSlots=Enumerable.Range(0,3).Select(n=>(Transform)Node("Option"+n,tm.discoverPanel.transform)).ToArray();
        tm.cardPrefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/CardUI.prefab");
        deck=Node("Deck",root.transform).gameObject.AddComponent<DeckManager>();deck.enabled=false;
        deck.p1Panel=Node("P1 Hand",root.transform).gameObject;deck.p2Panel=Node("P2 Hand",root.transform).gameObject;
        deck.neutralPanel=Node("Neutral Hand",root.transform).gameObject;deck.allCards=new List<CardData>();
        for(int n=0;n<seats;n++)AddSeat(n*240,0);
    }
    static SeatDropZone AddSeat(float x,float y)
    {
        var rect=Node("Seat"+tm.allSeats.Count,root.transform);rect.anchoredPosition=new Vector2(x,y);
        var seat=rect.gameObject.AddComponent<SeatDropZone>();seat.cardPos=Node("CardPos",rect);tm.allSeats.Add(seat);return seat;
    }
    static CardInstance Card(string asset,int seat=-1)
    {
        var data=Object.Instantiate(Data(asset));Expect(data!=null,"Missing card asset "+asset);
        if(data.cardSkill!=null)data.cardSkill=Object.Instantiate(data.cardSkill);
        deck.allCards.Add(data);
        var rect=Node(asset,deck.neutralHand);rect.sizeDelta=new Vector2(150,220);
        var card=rect.gameObject.AddComponent<CardInstance>();card.enabled=false;card.cardData=data;
        card.cardImage=rect.gameObject.AddComponent<Image>();card.currentOwner=data.owner;card.lastPlacedBy=DefaultOwner.Player2;
        var interaction=rect.gameObject.AddComponent<CardInteraction>();interaction.enabled=false;
        interaction.handParent=deck.neutralHand;interaction.originalParent=deck.neutralHand;
        card.voteText=Node("Votes",rect).gameObject.AddComponent<TextMeshProUGUI>();card.voteText.gameObject.SetActive(false);
        card.gamblerMark=Node("Gambler mark",rect).gameObject;card.gamblerMark.SetActive(false);
        card.snooperMark=Node("Snooper mark",rect).gameObject;card.snooperMark.SetActive(false);
        if(seat>=0)Put(card,seat);
        return card;
    }
    static void Put(CardInstance card,int seat)
    {card.transform.SetParent(tm.allSeats[seat].cardPos,false);I(card).originalParent=card.transform.parent;I(card).isPlaced=true;I(card).lockState=3;}
    static void Vote(CardInstance voter,int seat)
    {tm.currentVotingPlayer=voter.cardData.owner;tm.StartVoteSelection(voter);tm.ApplyVote(seat);}
    static void Pass(string name){cases.Add(name);Debug.Log("SKILL CHECK: "+name);}
    static void VotePresentationChecks()
    {
        Reset();tm.voteEffects=tm.gameObject.AddComponent<VoteEffectFeed>();
        var left=Card("Villager10",0);var trader=Card("Trader20",1);var right=Card("Villager1",2);
        int[] counts={5,3,1,0,0};var original=(int[])counts.Clone();tm.voteEffects.BeginTally(counts);
        trader.cardData.cardSkill.TryOnBeforeVoteCalculate(trader,counts,1);
        string state=JsonUtility.ToJson(UnityEngine.Random.state);
        tm.voteEffects.RecordChange(trader,"Trader","Trader divides the highest count (5) among 3 occupied seats: 1 votes each. Remainder discarded.",original,counts);
        Expect(counts.Take(3).All(n=>n==1),"Trader changed its rounding/group rule.");
        Expect(state==JsonUtility.ToJson(UnityEngine.Random.state),"Vote presentation consumed randomness.");
        var step=tm.voteEffects.For(DefaultOwner.Player1).Last();
        Expect(step.sourceNumber==20&&step.changes.Length==2&&step.changes.Single(c=>c.number==10).before==5&&step.changes.Single(c=>c.number==10).after==1,"Vote audit does not describe the actual Trader changes.");
        Expect(tm.voteEffects.For(DefaultOwner.Player2).Last().reason==step.reason,"Public tally explanation differs between players.");
        Pass("Vote audit records real highest-count Trader division without replaying or randomizing skills");

        Reset();tm.voteEffects=tm.gameObject.AddComponent<VoteEffectFeed>();var gambler=Card("Gambler24",1);left=Card("Villager1",0);right=Card("Villager10",2);
        tm.currentTurn=TurnState.SacrificePhase;tm.currentVotingPlayer=DefaultOwner.Player1;tm.firstPlayerThisRound=TurnState.Player1Turn;
        Expect(gambler.cardData.cardSkill.TryCastActiveSkill(gambler,right),"First Gambler setup failed.");
        Expect(tm.voteEffects.For(DefaultOwner.Player1).Length==1&&tm.voteEffects.For(DefaultOwner.Player2).Length==0,"Private Gambler activation leaked through the feed.");
        tm.currentVotingPlayer=DefaultOwner.Player2;Expect(gambler.cardData.cardSkill.TryCastActiveSkill(gambler,left),"Second Gambler setup failed.");
        tm.StartVoteSelection(left);tm.currentVotingPlayer=DefaultOwner.Player1;Vote(left,2);Vote(right,0);
        counts=new[]{2,0,3,0,0};tm.isTallyingVotes=true;tm.voteEffects.BeginTally(counts);
        gambler.cardData.cardSkill.TryOnBeforeVoteCalculate(gambler,counts,1);
        var records=tm.voteEffects.For(DefaultOwner.Player2).Where(r=>r.kind=="Gambler"&&r.tally).ToArray();
        Expect(records.Length==2&&records[0].reason.Contains("first bettor loses")&&records[1].reason.Contains("second bettor wins"),"Gambler hides the two separate bet outcomes.");
        Expect(records[0].changes.Length==1&&records[0].changes[0].after==records[0].changes[0].before-1,"Gambler loss explanation does not match the removed ballot.");
        Expect(records[1].changes.Length==1&&records[1].changes[0].after==records[1].changes[0].before+1,"Gambler win explanation does not match its added vote.");
        Pass("Gambler keeps private selection cues private and explains both real bet outcomes separately");

        Reset();tm.voteEffects=tm.gameObject.AddComponent<VoteEffectFeed>();var snooper=Card("Snooper22",0);right=Card("Villager10",1);
        tm.currentTurn=TurnState.SacrificePhase;tm.currentVotingPlayer=DefaultOwner.Player1;snooper.isInfected=true;
        Expect(snooper.cardData.cardSkill.TryCastActiveSkill(snooper,right),"Snooper private selection setup failed.");
        var detective=Card("Detactive23",3);right.bonusSuspicion=1;tm.voteEffects.Detective(detective,right);
        var publicFeed=tm.voteEffects.For(DefaultOwner.Player2);
        Expect(publicFeed.Length==1&&publicFeed[0].id==1&&publicFeed[0].kind=="Detective","Hidden Snooper activity leaks through event IDs.");
        Expect(!JsonUtility.ToJson(publicFeed[0]).Contains("selected"),"Hidden target selection leaked into a public reason.");
        Pass("Independent event sequences do not reveal secret Snooper activity to Villagers");

        Reset();var hudCard=Object.Instantiate(tm.cardPrefab,tm.allSeats[0].cardPos).GetComponent<CardInstance>();hudCard.SetupCard(Data("Villager10"));
        var hud=hudCard.GetComponent<CardVotePresentation>();Expect(hud!=null,"Card prefab has no saved vote presentation.");
        I(hudCard).isPlaced=true;I(hudCard).originalParent=hudCard.transform.parent;I(hudCard).lockState=3;
        var savedPositions=new[]{hud.voteMotion.anchoredPosition,hud.suspicionMotion.anchoredPosition,hud.snooperMotion.anchoredPosition,hud.gamblerMotion.anchoredPosition};
        var savedSizes=new[]{hud.voteMotion.sizeDelta,hud.suspicionMotion.sizeDelta,hud.snooperMotion.sizeDelta,hud.gamblerMotion.sizeDelta};
        hudCard.bonusSuspicion=14;hudCard.voteText.text="4";hudCard.voteText.gameObject.SetActive(true);hud.Refresh();
        Expect(hud.suspicionCount.text=="+14"&&hudCard.hatContainer.childCount==0,"Detective still spawns stacked hats or loses suspicion totals.");
        Expect(hud.voteBadge.activeSelf&&hud.DisplayedVotes==4&&!hudCard.voteText.enabled,"New vote badge did not replace the naked number renderer.");
        tm.isTallyingVotes=true;hud.PresentCount(0);hud.Refresh();
        Expect(hud.voteBadge.activeSelf&&hud.DisplayedVotes==0&&hudCard.voteText.text=="4","Zero-vote presentation is hidden or mutates authoritative votes.");
        Expect(savedPositions.SequenceEqual(new[]{hud.voteMotion.anchoredPosition,hud.suspicionMotion.anchoredPosition,hud.snooperMotion.anchoredPosition,hud.gamblerMotion.anchoredPosition}),"Runtime overwrites the user's authored badge positions.");
        Expect(savedSizes.SequenceEqual(new[]{hud.voteMotion.sizeDelta,hud.suspicionMotion.sizeDelta,hud.snooperMotion.sizeDelta,hud.gamblerMotion.sizeDelta}),"Runtime overwrites the user's authored badge sizes.");
        foreach(var label in new[]{hud.voteCount,hud.suspicionCount})Expect(label.raycastTarget,"The count cannot open its explanation.");
        foreach(var symbol in new[]{"Ballot","Evidence"})
        {var image=hud.GetComponentsInChildren<Image>(true).Single(i=>i.name==symbol);Expect(image.enabled&&image.raycastTarget&&image.GetComponentInParent<CardVoteHint>()!=null,"Icon explanation is unreachable: "+symbol);}
        hud.ActivateEffect("Detective");typeof(CardVotePresentation).GetField("activationAt",Private).SetValue(hud,Time.unscaledTime-.25f);hud.Refresh();
        Expect(hud.activationFrame.alpha>0,"Activated skill does not light its source card.");
        Pass("Saved card HUD keeps 14 suspicion in one badge, displays zero votes, preserves raw state and bounds icon coverage");
    }
    static void FeedbackChecks()
    {
        Reset();var mine=Card("Villager10",0);var input=I(mine);
        input.lockState=1;
        Expect(ActionFeedback.BeginDragBlock(input,false).Contains("next action turn"),"A fresh card does not explain its movement lock.");
        input.lockState=3;input.isLockedByElder=true;
        Expect(ActionFeedback.BeginDragBlock(input,false).Contains("Elder"),"Elder lock reason is missing.");
        input.isLockedByElder=false;input.isLockedBySkill=true;
        Expect(ActionFeedback.BeginDragBlock(input,false).Contains("extra seats"),"GameMaster lock reason is missing.");
        input.isLockedBySkill=false;tm.p2Hearts=0;
        Expect(ActionFeedback.BeginDragBlock(input,false)==null,"Zero hearts incorrectly prevent a refunding retrieval.");
        Expect(input.ReturnPaymentCost==0,"Own retrieval incorrectly charges hearts.");
        var pointer=new UnityEngine.EventSystems.PointerEventData(null){button=UnityEngine.EventSystems.PointerEventData.InputButton.Left,position=Vector2.zero};
        input.OnBeginDrag(pointer);Expect(input.canDrag,"The actual drag path blocks retrieval at zero hearts.");
        input.OnEndDrag(pointer);
        Expect(!input.isPlaced&&tm.p2Hearts==2,"Retrieving at zero hearts did not return the card/refund.");
        Pass("Actual zero-heart retrieval, movement lock and aura explanations");

        Reset();mine=Card("Villager10");tm.p2Hearts=1;
        I(mine).PlaceOnSeat(tm.allSeats[0].cardPos);
        Expect(tm.LocalActionFeedback!=null&&tm.LocalActionFeedback.Contains("needs 2 hearts")&&tm.LocalActionFeedback.Contains("have 1"),"Placement refusal omits required/available hearts.");
        Expect(tm.p2Hearts==1&&!I(mine).isPlaced,"Rejected placement changed resources or location.");
        Pass("Insufficient placement cost is explained without spending hearts");

        Reset();var hunter=Card("Villager9",0);var target=Card("Villager1",1);
        tm.p2Hearts=3;
        Expect(!hunter.cardData.cardSkill.TryCastActiveSkill(hunter,target)&&tm.LocalActionFeedback.Contains("needs 5 hearts"),"Hunter cost feedback is missing.");
        tm.p2Hearts=5;hunter.isSilenced=true;
        Expect(!hunter.cardData.cardSkill.TryCastActiveSkill(hunter,target)&&tm.LocalActionFeedback.Contains("Saboteur"),"Silenced skill feedback is missing.");
        hunter.isSilenced=false;hunter.hasUsedSkill=true;
        Expect(!hunter.cardData.cardSkill.TryCastActiveSkill(hunter,target)&&tm.LocalActionFeedback.Contains("after the Sacrifice"),"Skill cooldown feedback is missing.");
        hunter.hasUsedSkill=false;
        Expect(!hunter.cardData.cardSkill.TryCastActiveSkill(hunter,null)&&tm.LocalActionFeedback.Contains("right mouse"),"Hunter click does not explain the targeting gesture.");
        Expect(tm.p2Hearts==5&&!target.isDead,"Rejected skills changed gameplay.");
        Pass("Skill reasons cover cost, silence, cooldown and the correct mouse gesture");

        Reset();var snooper=Card("Snooper22",0);target=Card("Villager1",1);tm.currentTurn=TurnState.SacrificePhase;tm.currentVotingPlayer=DefaultOwner.Player2;
        snooper.isInfected=false;string clean=ActionFeedback.SkillFailure(snooper.cardData.cardSkill,snooper,target);
        snooper.isInfected=true;string infected=ActionFeedback.SkillFailure(snooper.cardData.cardSkill,snooper,target);
        Expect(clean==infected,"Skill feedback exposes Snooper's private infection.");
        var disabled=Node("Disabled purge",root.transform).gameObject;disabled.AddComponent<Image>();disabled.AddComponent<Button>().interactable=false;
        var explanation=disabled.AddComponent<BlockedActionFeedback>();explanation.control=BlockedActionFeedback.Control.Purge;
        explanation.OnPointerClick(new UnityEngine.EventSystems.PointerEventData(null));
        Expect(tm.LocalActionFeedback.Contains("only during an action turn"),"A disabled Purge gives no explanation.");
        typeof(TurnManager).GetField("localActionFeedbackUntil",Private).SetValue(tm,Time.unscaledTime-1);
        Expect(tm.LocalActionFeedback==null,"Local explanations never expire.");
        Pass("Private feedback does not disclose Snooper infection; disabled controls explain and notices expire");
    }
    static void Check()
    {
        Application.logMessageReceived+=(message,stack,type)=>{if(type==LogType.Exception)errors.Add(message);};
        try
        {
            FeedbackChecks();
            VotePresentationChecks();
            Reset();
            foreach(var asset in new[]{"Blind","Controller","Detective","Elder","GameMaster","Jester","Prankster","Preacher","Reeve","Saboteur","Sacrifier","Totem","Trader"})
                Expect(AssetDatabase.LoadAssetAtPath<CardSkill>("Assets/Skill_Neutral/Skill_"+asset+".asset").skillType==SkillType.Passive,"Wrong passive classification: "+asset);
            Expect(Data("Snooper22").cardSkill.skillType==SkillType.Hybrid,"Snooper cannot expose its active targeting.");
            Expect(((Skill_Blind)Data("Blind19").cardSkill).mustSitNearCemetery,"Blind ignores its cemetery-seat rule.");
            Pass("Real asset classifications and Blind seat requirement");

            var detective=Card("Detactive23",1);var a=Card("Villager1",0);var sab=Card("Saboteur18",2);
            tm.RefreshAllAuras();Expect(detective.isSilenced&&!detective.hasUsedSkill&&a.bonusSuspicion==0&&sab.bonusSuspicion==0,"Silenced Detective still adds suspicion.");
            sab.transform.SetParent(deck.neutralHand);I(sab).isPlaced=false;
            var b=Card("Villager10",2);tm.RefreshAllAuras();Expect(detective.hasUsedSkill,"Detective does not recover after silence ends.");
            I(detective).ExecuteReturnToHand();Expect(!detective.hasUsedSkill,"Detective Once does not reset on return.");
            Put(detective,1);tm.RefreshAllAuras();Expect(a.bonusSuspicion+b.bonusSuspicion==2,"Replayed Detective failed its second placement.");
            Pass("Saboteur/Detective silence and Once replay");

            Reset(4);a=Card("Villager1",0);var prank=Card("Prankster24",1);prank.hasUsedSkill=true;
            AddSeat(-240,0).name="ExtraSeat_Left";var elder=Card("Elder17",4);
            tm.RefreshAllAuras();Expect(tm.GetSeatIndex(I(a))==0&&I(a).isLockedByElder,"Prankster bypassed an Elder processed later in the seat list.");
            Expect(tm.HorizontalNeighbor(3,1)==-1,"Right edge wrapped into an appended extra seat.");
            Pass("Geometric aura range and protection before reactive movement");

            Reset(4);a=Card("Villager1",0);prank=Card("Prankster24",1);prank.hasUsedSkill=true;
            var prank2=Card("Prankster24",3);prank2.hasUsedSkill=true;tm.RefreshAllAuras();
            Expect(tm.IsLivingBoardCard(a)&&tm.IsLivingBoardCard(prank)&&tm.IsLivingBoardCard(prank2),"Prankster chain lost a live card.");
            Expect(tm.GetSeatIndex(I(a))==0||tm.GetSeatIndex(I(a))==2,"Prankster moved outside a legal empty seat.");
            Pass("Prankster area-trigger chain terminates without recursive overflow");

            Reset();sab=Card("Saboteur18",0);prank=Card("Prankster24",1);prank.hasUsedSkill=true;b=Card("Villager10",2);tm.RefreshAllAuras();
            I(sab).SwapWith(I(b),tm.allSeats[2].cardPos);
            Expect(tm.GetSeatIndex(I(sab))==2&&tm.GetSeatIndex(I(b))==0&&prank.isSilenced,"Prankster reacted to an unfinished swap.");
            Expect(tm.allSeats.All(s=>s.cardPos.GetComponentsInChildren<CardInstance>().Length<=1),"Swap stacked two cards on one seat.");
            Pass("Atomic swap does not expose transient unsilenced Prankster state");

            Reset();var hunter=Card("Villager9",0);a=Card("Villager1",1);var preacher=Card("Preacher18",2);tm.RefreshAllAuras();
            Expect(hunter.cardData.cardSkill.TryCastActiveSkill(hunter,a)&&!a.isDead&&tm.p2Hearts==0,"Preacher failed to block a paid Hunter shot.");
            Expect(!hunter.cardData.cardSkill.TryCastActiveSkill(hunter,a),"Hunter fired twice in one round.");
            var offBoard=Card("Villager2");hunter.hasUsedSkill=false;tm.p2Hearts=5;
            Expect(!hunter.cardData.cardSkill.TryCastActiveSkill(hunter,offBoard)&&tm.p2Hearts==5,"Invalid Hunter target spent resources.");
            Pass("Hunter shield, cooldown and invalid-target resource safety");

            Reset();var snooper=Card("Snooper22",1);snooper.isInfected=true;a=Card("Villager10",0);tm.allCurs.Add(a);
            tm.currentTurn=TurnState.SacrificePhase;tm.currentVotingPlayer=DefaultOwner.Player2;
            Expect(!snooper.cardData.cardSkill.TryCastActiveSkill(snooper,a),"Physical ownership of a hidden Cursermancer granted Villagers control of infected Snooper.");
            tm.currentVotingPlayer=DefaultOwner.Player1;Expect(snooper.cardData.cardSkill.TryCastActiveSkill(snooper,a),"Cursermancer cannot control infected Snooper.");
            var votes=new int[tm.allSeats.Count];I(a).isPlaced=false;a.transform.SetParent(deck.player2Hand);
            snooper.cardData.cardSkill.OnBeforeVoteCalculate(snooper,votes,1);Expect(votes.Sum()==0,"Snooper voted for a removed target.");
            Pass("Infected Snooper role permission and stale-target safety");

            Reset();var gambler=Card("Gambler24",1);a=Card("Villager1",0);offBoard=Card("Villager10");
            tm.currentTurn=TurnState.SacrificePhase;tm.currentVotingPlayer=DefaultOwner.Player1;
            Expect(!gambler.cardData.cardSkill.TryCastActiveSkill(gambler,offBoard)&&Skill_Gambler.playersWhoGambled.Count==0,"Invalid bet registered a gambler.");
            Expect(gambler.cardData.cardSkill.TryCastActiveSkill(gambler,a),"Valid bet failed.");
            Expect(Skill_Gambler.IsMarkedFor(a,DefaultOwner.Player1)&&!Skill_Gambler.IsMarkedFor(a,DefaultOwner.Player2),"Secret bet is visible to the opposing role.");
            I(a).isPlaced=false;a.transform.SetParent(deck.player1Hand);votes=new int[tm.allSeats.Count];
            gambler.cardData.cardSkill.OnBeforeVoteCalculate(gambler,votes,1);Expect(votes.Sum()==0,"Gambler voted for a vacated seat.");
            Pass("Gambler validates before charging/registering and retains target identity");

            Reset();a=Card("Villager1",0);var trader=Card("Trader20",1);b=Card("Villager10",2);snooper=Card("Snooper22",3);var c=Card("Villager11",4);
            tm.allCurs.Add(a);tm.currentTurn=TurnState.SacrificePhase;Vote(b,0);Vote(c,0);
            var tally=(IEnumerator)typeof(TurnManager).GetMethod("SacrificeRoutine",Private).Invoke(tm,null);
            tally.MoveNext();tally.MoveNext();
            Expect(a.voteText.text=="1"&&trader.voteText.text=="1"&&b.voteText.text=="1","Trader ran before Snooper's extra vote.");
            Pass("Actual tally adds votes before Trader sharing");

            Reset(3);trader=Card("Trader20",1);a=Card("Villager1",0);votes=new[]{5,0,0};
            trader.cardData.cardSkill.OnBeforeVoteCalculate(trader,votes,1);Expect(votes[0]==2&&votes[1]==2&&votes[2]==0,"Trader counted an empty seat or retained a remainder.");
            Pass("Trader shares only occupied neighbors and discards remainder");

            Reset();a=Card("Villager1",0);b=Card("Villager10",1);c=Card("Villager11",2);var d=Card("Villager12",3);var e=Card("Villager2",4);
            tm.currentTurn=TurnState.SacrificePhase;Vote(b,0);Vote(c,4);Vote(d,4);
            Expect(tm.GetVoteTargetOfPlayer(DefaultOwner.Player2)==4,"Gambler penalty uses the first vote instead of the most-voted target.");
            tm.BanishCardToCemetery(I(e));Expect(tm.GetVoteTarget(c)==-1&&tm.GetVoteTarget(d)==-1,"Votes targeting a dead card were retained.");
            tm.BanishCardToCemetery(I(b));Expect(tm.GetVoteTarget(b)==-1,"A dead voter kept its vote.");
            Pass("Gambler majority penalty and cancellation of votes involving dead cards");

            Reset();var gm=Card("Master21",1);tm.RefreshAllAuras();Expect(tm.allSeats.Count==7,"Game Master did not add seats.");
            a=Card("Villager1",5);tm.RefreshAllAuras();Expect(I(gm).isLockedBySkill,"Game Master anchor has a timing gap.");
            I(a).isProtectedByPreacher=true;tm.BanishCardToCemetery(I(gm));
            Expect(a.isDead&&gm.isDead&&tm.allSeats.Count==5,"Preacher rescued a card from Game Master's death collapse.");
            Expect(a.cemeteryOrder<gm.cemeteryOrder&&tm.deadCardsData[0]==a.cardData&&tm.deadCardsData[1]==gm.cardData,"Nested Game Master deaths disagree with the authoritative cemetery history.");
            Pass("Game Master immediate anchor and collapse bypasses Preacher");

            Reset();gm=Card("Master21",1);tm.RefreshAllAuras();a=Card("Villager1",5);sab=Card("Saboteur18",2);tm.RefreshAllAuras();
            Expect(gm.isSilenced&&!a.isDead&&!I(a).isPlaced&&tm.allSeats.Count==5,"Silencing Game Master did not safely return extra-seat occupants.");
            Pass("Saboteur/Game Master collapse returns living occupants without recursion");

            Reset();gm=Card("Master21",1);tm.RefreshAllAuras();a=Card("Villager1",5);tm.RefreshAllAuras();
            int hearts=tm.p2Hearts;I(gm).PlaceOnSeat(tm.allSeats[2].cardPos);
            Expect(tm.GetSeatIndex(I(gm))==1&&tm.p2Hearts==hearts,"Manual move bypassed Game Master's occupied-seat anchor.");
            I(gm).ForceReturnToHand();
            Expect(!gm.isDead&&!a.isDead&&!I(gm).isPlaced&&!I(a).isPlaced&&tm.allSeats.Count==5,"Forced Game Master return did not return its extra-seat occupants or recreated seats during cleanup.");
            Pass("Forced Game Master return clears occupants and seats despite manual anchor");

            Reset();gm=Card("Master21",1);tm.RefreshAllAuras();a=Card("Villager1",5);tm.RefreshAllAuras();
            var occupiedExtra=tm.allSeats[5];prank=Card("Prankster24",2);prank.hasUsedSkill=true;tm.RefreshAllAuras();
            int movedSeat=tm.GetSeatIndex(I(gm));
            Expect(movedSeat!=1&&tm.IsLivingBoardCard(gm)&&tm.IsLivingBoardCard(a)&&I(gm).isLockedBySkill,"Prankster could not push an anchored Game Master with its occupants.");
            Expect(Mathf.Approximately(occupiedExtra.GetComponent<RectTransform>().anchoredPosition.x,tm.allSeats[movedSeat].GetComponent<RectTransform>().anchoredPosition.x),"Extra seat did not follow forced Game Master movement.");
            I(gm).ForceReturnToHand();Expect(!I(a).isPlaced&&!a.isDead&&tm.allSeats.Count==5,"Returning a pushed Game Master did not clear its occupied seats.");
            Pass("Prankster moves occupied Game Master seats; forced return returns their cards");

            Reset();gm=Card("Master21",1);tm.RefreshAllAuras();a=Card("Villager1",5);tm.RefreshAllAuras();prank=Card("Prankster24",2);
            int jackpotSeed=0;for(;jackpotSeed<1000;jackpotSeed++){UnityEngine.Random.InitState(jackpotSeed);if(UnityEngine.Random.value<=.1f)break;}
            UnityEngine.Random.InitState(jackpotSeed);tm.RefreshAllAuras();
            Expect(!I(gm).isPlaced&&!I(a).isPlaced&&!gm.isDead&&!a.isDead&&tm.allSeats.Count==5,"Prankster jackpot failed to return an anchored Game Master and its occupants.");
            Pass("Prankster jackpot bypasses Game Master manual anchor but does not kill occupants");

            Reset();var blind=Card("Blind19",4);tm.allSeats[4].isNextToCemetery=true;a=Card("Villager1",0);a.isInfected=true;
            tm.BanishCardToCemetery(I(a));Expect(a.isRevealedByBlind&&a.blindRevealAsInfected,"Blind failed to read the latest corpse during flight.");
            blind.isInfected=true;tm.RefreshAllAuras();Expect(a.isRevealedByBlind&&a.blindRevealAsInfected,"A redundant aura refresh rewrote Blind's completed report.");
            blind.hasUsedSkill=false;tm.RefreshAllAuras();Expect(!blind.hasUsedSkill&&a.blindRevealAsInfected,"Blind re-inspected an already recorded death.");
            b=Card("Villager10",2);b.isInfected=true;tm.BanishCardToCemetery(I(b));
            Expect(b.isRevealedByBlind&&!b.blindRevealAsCur&&!b.blindRevealAsInfected,"Infected Blind did not falsify its next uninspected corpse.");
            sab=Card("Saboteur18",3);tm.RefreshAllAuras();Expect(a.isRevealedByBlind&&b.isRevealedByBlind,"Silencing Blind erased completed public reports.");
            Pass("Blind inspects each death once and preserves completed reports across aura refreshes and silence");

            Reset();var totem=Card("Totem1",1);sab=Card("Saboteur18",2);tm.RefreshAllAuras();
            var cost=(int)typeof(CardInteraction).GetMethod("GetReturnToHandCost",Private).Invoke(I(totem),new object[]{I(totem)});
            Expect(totem.isSilenced&&cost==2,"Silenced Totem retained its printed discount.");
            Pass("Saboteur disables Totem discount");

            Reset();var witch=Card("Witch23",1);witch.isInfected=true;a=Card("Villager1",0);a.isInfected=true;
            var infectedHand=Card("Villager10");infectedHand.isInfected=true;
            Expect(witch.cardData.cardSkill.TryCastActiveSkill(witch,null)&&a.isDead&&!witch.isDead&&!infectedHand.isDead,"Witch sweep killed itself or a card in hand, or missed an infected board card.");
            Expect(witch.hasUsedSkill&&tm.p2Hearts==0,"Witch did not consume its round use and hearts.");
            Pass("Witch board-only sweep excludes itself and preserves round cooldown");

            Reset();a=Card("Villager1",0);b=Card("Villager10",1);var controller=Card("Controller22",2);var sacrifier=Card("Sacrifier21",3);
            tm.allCurs.Add(a);tm.currentTurn=TurnState.SacrificePhase;Vote(b,0);
            tally=(IEnumerator)typeof(TurnManager).GetMethod("SacrificeRoutine",Private).Invoke(tm,null);
            tally.MoveNext();tally.MoveNext();tally.MoveNext();
            Expect(sacrifier.isDead&&!a.isDead&&!controller.isDead,"Controller took priority over Sacrifier.");
            Pass("Actual tally respects Sacrifier priority over Controller");

            Reset();var jester=Card("Jester20",1);a=Card("Villager1",0);b=Card("Villager10",2);
            for(int seed=0;seed<32;seed++)
            {
                UnityEngine.Random.InitState(seed);votes=new int[tm.allSeats.Count];jester.cardData.cardSkill.OnBeforeVoteCalculate(jester,votes,1);
                Expect(votes[1]==0&&votes[3]==0&&votes[4]==0&&votes.Sum()<=3,"Jester targeted itself/an empty seat or exceeded three extra votes.");
            }
            Pass("Jester random extra votes use living occupied targets");

            Reset();var reeve=Card("Reeve17",1);a=Card("Villager10",0);
            var pending=(IEnumerator)typeof(Skill_VillageChief).GetMethod("DelayedForceCurse",Private).Invoke(reeve.cardData.cardSkill,new object[]{reeve});
            pending.MoveNext();I(reeve).isPlaced=false;reeve.transform.SetParent(deck.neutralHand);pending.MoveNext();
            Expect(tm.currentTurn==TurnState.Player2Turn,"A removed Reeve forced a delayed Curse Phase.");
            Pass("Reeve revalidates itself before delayed phase change");

            Reset();a=Card("Villager1",1);sab=Card("Saboteur18",0);b=Card("Villager10",2);tm.allCurs.Add(a);
            tm.currentTurn=TurnState.CursePhase;tm.RefreshAllAuras();tm.StartCurseSelection(a);tm.ApplyCurse(2);
            Expect(a.isSilenced&&b.isInfected,"Saboteur incorrectly blocked Cursermancer infection.");
            Pass("Saboteur leaves Cursermancer infection available");

            Reset();var invoker=Card("Invoker19",4);tm.allSeats[4].isNextToCemetery=true;invoker.lastPlacedBy=DefaultOwner.Player2;
            a=Card("Villager1",0);tm.allCurs.Add(a);tm.BanishCardToCemetery(I(a));
            tm.currentTurn=TurnState.SacrificePhase;tm.currentVotingPlayer=DefaultOwner.Player2;
            Expect(invoker.cardData.cardSkill.TryCastActiveSkill(invoker,null),"Invoker failed to open valid options.");
            tm.ResolveInvokerSelection(0);Expect(tm.deadCardsData.Count==0&&invoker.hasUsedSkill,"Invoker did not consume the selected corpse and skill.");
            tm.ResolveInvokerSelection(0);
            var living=Object.FindObjectsByType<CardInstance>(FindObjectsInactive.Include,FindObjectsSortMode.None).Where(ci=>!ci.isDead&&ci.cardData==a.cardData&&!ci.transform.IsChildOf(tm.discoverPanel.transform)).ToArray();
            Expect(living.Length==1&&tm.IsCur(living[0])&&!living[0].isInfected,"Invoker duplicated a card or lost its hidden identity.");
            Pass("Invoker resurrects exactly once during death flight and preserves Cursermancer identity");
            // Leave the last scene alive briefly to catch stale death-animation exceptions.
            var until=EditorApplication.timeSinceStartup+2;
            EditorApplication.CallbackFunction finish=null;finish=()=>{
                if(EditorApplication.timeSinceStartup<until)return;
                EditorApplication.update-=finish;
                Finish(errors.Count==0,errors.Count==0?"Skill interaction regressions passed in real Unity Play Mode.":string.Join("; ",errors));
            };EditorApplication.update+=finish;
        }
        catch(Exception exception){Finish(false,exception.ToString());}
    }
    static void Finish(bool passed,string message)
    {
        SessionState.SetBool(Request,false);
        File.WriteAllText(Path.Combine(Directory.GetParent(Application.dataPath).FullName,"SkillInteractionValidationResult.json"),
            JsonUtility.ToJson(new Report{passed=passed,assertions=assertions,message=message,cases=cases.ToArray()},true));
        Debug.Log("SKILL VALIDATION: "+message);EditorApplication.Exit(passed?0:1);
    }
}

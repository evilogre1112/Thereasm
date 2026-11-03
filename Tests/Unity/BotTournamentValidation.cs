// Editor-only referee, copied into the isolated validation project. No bot/rule overrides.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class BotTournamentValidation
{
    const string Request="Thereasm.BotTournament";
    const int Level=2, SeedBase=20261030;
    static int MatchCount=>SessionState.GetInt(Request+".Count",100);
    static readonly List<Match> matches=new List<Match>();
    static readonly List<string> errors=new List<string>();
    static readonly List<string> trace=new List<string>();
    static TurnManager manager;
    static DeckManager deck;
    static bool attached,draftPicked,playing;
    static int sceneHandle=-1,frameAttached,transitions,actionTurns,lastPhase=-1,lastVoter=-1;
    static double batchStarted,matchStarted,lastProgress,lastScan,lastHeartbeat;
    static string lastFingerprint="";
    static string[] initialDeck=new string[0],initialCurs=new string[0];
    static int maximumSeats;

    [Serializable] public class Match
    {
        public int match,seed,level,actionTurns,phaseTransitions,maximumSeats;
        public string firstPlayer,outcome,reason,lastPhase,feedback;
        public double seconds;
        public int livingCurs,livingVillagers,p1Checks,p2Checks;
        public string[] deck,curs,trace,errors;
    }
    [Serializable] public class Report
    {
        public string timestampUtc,method,settings;
        public bool complete;
        public int requested,recorded,level,cursermancerWins,villagerWins,unresolved,errors;
        public double seconds;
        public Match[] matches;
    }
    static BotTournamentValidation()
    {
        EditorApplication.playModeStateChanged+=state=>
        {
            if(state==PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Request,false))
            {
                matches.Clear();batchStarted=EditorApplication.timeSinceStartup;
                Application.logMessageReceived+=OnLog;
                SceneManager.sceneLoaded+=OnLoaded;
                Time.timeScale=100;Time.fixedDeltaTime=2;Time.maximumDeltaTime=10;
                QualitySettings.vSyncCount=0;Application.targetFrameRate=240;
                Application.SetStackTraceLogType(LogType.Log,StackTraceLogType.None);
                Application.SetStackTraceLogType(LogType.Warning,StackTraceLogType.None);
                AudioListener.pause=true;
                BeginMatch();EditorApplication.update+=Tick;
            }
        };
    }
    public static void Run()
    {
        SessionState.SetBool(Request,true);
        GameSettings.currentMode=GameMode.PlayerVsCurBot;GameSettings.currentLevel=Level;
        // A clean first load follows after entering Play Mode, just like all later matches.
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        EditorApplication.isPlaying=true;
    }
    public static void RunSmoke(){SessionState.SetInt(Request+".Count",4);Run();}
    static string Root=>Directory.GetParent(Application.dataPath).FullName;
    static void BeginMatch()
    {
        errors.Clear();trace.Clear();manager=null;deck=null;attached=draftPicked=playing=false;
        initialDeck=initialCurs=new string[0];transitions=actionTurns=maximumSeats=0;
        lastPhase=lastVoter=-1;lastFingerprint="";lastScan=0;
        matchStarted=lastProgress=lastHeartbeat=EditorApplication.timeSinceStartup;
        GameSettings.currentMode=GameMode.PlayerVsCurBot;GameSettings.currentLevel=Level;
        Time.timeScale=100;UnityEngine.Random.InitState(SeedBase+matches.Count);
        SceneManager.LoadScene("MainBoard",LoadSceneMode.Single);
    }
    static void OnLoaded(Scene scene,LoadSceneMode mode)
    {
        if(scene.name!="MainBoard")return;
        sceneHandle=scene.handle;
        UnityEngine.Random.InitState(SeedBase+matches.Count);
        manager=Object.FindFirstObjectByType<TurnManager>();
        deck=Object.FindFirstObjectByType<DeckManager>();
        if(manager!=null) manager.firstPlayerThisRound=matches.Count%2==0?TurnState.Player1Turn:TurnState.Player2Turn;
        frameAttached=Time.frameCount;
    }
    static void OnLog(string message,string stack,LogType type)
    {
        if(type==LogType.Exception || type==LogType.Error || type==LogType.Assert)
            if(errors.Count<8)errors.Add(message+"\n"+stack);
    }
    static CardInstance[] RealCards()
    {
        return Object.FindObjectsByType<CardInstance>(FindObjectsInactive.Include,FindObjectsSortMode.None)
            .Where(c=>c.cardData!=null && c.gameObject.scene.handle==sceneHandle &&
                (deck.draftPanel==null || !c.transform.IsChildOf(deck.draftPanel.transform)) &&
                (manager.discoverPanel==null || !c.transform.IsChildOf(manager.discoverPanel.transform)))
            .OrderBy(c=>c.cardData.cardNumber).ThenBy(c=>c.cardData.name).ToArray();
    }
    static (int curs,int villagers) Living(CardInstance[] cards)
    {
        int curs=0,villagers=0;
        foreach(var c in cards)
        {
            if(c.isDead || c.transform.parent==manager.cemeteryCardPos || c.transform.parent==manager.globalCemetery)continue;
            if(manager.allCurs.Contains(c))curs++;
            else if(c.cardData.owner==DefaultOwner.Player2)villagers++;
        }
        return(curs,villagers);
    }
    static void Tick()
    {
        try
        {
            double now=EditorApplication.timeSinceStartup;
            if(errors.Count>0){Record("Error","Unity error/exception");return;}
            if(manager==null||deck==null)
            {
                if(now-matchStarted>30)Finish(false,"MainBoard initialization did not finish.");
                return;
            }
            if(!attached && Time.frameCount>frameAttached+1)
            {
                if(manager.curBot==null||manager.villagerBot==null)throw new Exception("MainBoard has missing bot references.");
                manager.curBot.tm=manager;
                manager.curBot.gameObject.SetActive(true);manager.curBot.enabled=true;
                manager.villagerBot.gameObject.SetActive(true);manager.villagerBot.enabled=true;
                attached=true;
                Debug.Log("BOT TOURNAMENT START "+(matches.Count+1)+"/100 seed="+(SeedBase+matches.Count)+" level=2 first="+manager.firstPlayerThisRound);
            }
            if(!deck.IsReady && !deck.IsPicking && !draftPicked && Time.frameCount>frameAttached+3)
            {
                bool left=UnityEngine.Random.value<.5f;
                var slot=left?deck.draftSlotLeft:deck.draftSlotRight;
                var offer=slot!=null?slot.GetComponentInChildren<CardInstance>():null;
                if(offer==null){left=!left;slot=left?deck.draftSlotLeft:deck.draftSlotRight;offer=slot!=null?slot.GetComponentInChildren<CardInstance>():null;}
                if(offer!=null){draftPicked=true;deck.PickNeutralCard(offer.cardData,left);}
            }
            if(!playing && manager.currentTurn!=TurnState.SetupPhase)
            {
                var initial=RealCards();initialDeck=initial.Select(c=>c.cardData.name).ToArray();
                initialCurs=manager.allCurs.Where(c=>c!=null).Select(c=>c.cardData.name).OrderBy(s=>s).ToArray();
                if(initialDeck.Length!=24 || initialCurs.Length!=2)throw new Exception("Unexpected level-2 setup: cards="+initialDeck.Length+", Curs="+initialCurs.Length);
                playing=true;
            }
            maximumSeats=Math.Max(maximumSeats,manager.allSeats.Count);
            int phase=(int)manager.currentTurn,voter=(int)manager.currentVotingPlayer;
            if(lastPhase!=phase || (manager.currentTurn==TurnState.SacrificePhase && lastVoter!=voter))
            {
                transitions++;if(phase==(int)TurnState.Player1Turn||phase==(int)TurnState.Player2Turn)actionTurns++;
                trace.Add(manager.currentTurn+(manager.currentTurn==TurnState.SacrificePhase?"/"+manager.currentVotingPlayer:"")+" hearts="+manager.p1Hearts+"/"+manager.p2Hearts);
                if(trace.Count>30)trace.RemoveAt(0);
                lastPhase=phase;lastVoter=voter;lastProgress=now;
            }
            if(manager.isGameOver)
            {
                var alive=Living(RealCards());var outcome=GameRules.EvaluateWinner(alive.curs,alive.villagers);
                if(outcome==GameOutcome.InProgress)throw new Exception("Game over without a matching rule outcome: "+manager.winText?.text);
                Record(outcome==GameOutcome.CursWin?"Cursermancer":"Villager","Declared by real TurnManager");return;
            }
            if(now-lastScan>=.15)
            {
                lastScan=now;
                string fingerprint=phase+":"+voter+":"+manager.p1Hearts+":"+manager.p2Hearts+":"+manager.p1Checks+":"+manager.p2Checks+":"+manager.isTallyingVotes+":"+
                    string.Join("|",RealCards().Select(c=>c.cardData.name+":"+c.isDead+":"+c.transform.parent?.GetInstanceID()+":"+c.isInfected+":"+c.hasUsedSkill+":"+manager.GetVoteTarget(c)));
                if(fingerprint!=lastFingerprint){lastProgress=now;lastFingerprint=fingerprint;}
            }
            if(now-lastProgress>30){Record("Unresolved","No rule-state progress for 30 real seconds");return;}
            if(actionTurns>500){Record("Unresolved","More than 500 action turns without a winner");return;}
            if(now-matchStarted>180){Record("Unresolved","180 real seconds per-match limit");return;}
            if(now-lastHeartbeat>10)
            {
                lastHeartbeat=now;
                File.WriteAllText(Path.Combine(Root,"BotTournament-Level2-status.txt"),"Match "+(matches.Count+1)+"/"+MatchCount+"; "+manager.currentTurn+"; action turns="+actionTurns+"; real seconds="+(now-matchStarted).ToString("F1"));
            }
        }
        catch(Exception e){errors.Add(e.ToString());Record("Error","Referee exception");}
    }
    static void Record(string outcome,string reason)
    {
        var alive=manager!=null&&deck!=null?Living(RealCards()):(0,0);
        var record=new Match{match=matches.Count+1,seed=SeedBase+matches.Count,level=Level,
            firstPlayer=matches.Count%2==0?"Cursermancer":"Villager",outcome=outcome,reason=reason,
            actionTurns=actionTurns,phaseTransitions=transitions,maximumSeats=maximumSeats,
            seconds=EditorApplication.timeSinceStartup-matchStarted,livingCurs=alive.Item1,livingVillagers=alive.Item2,
            p1Checks=manager!=null?manager.p1Checks:0,p2Checks=manager!=null?manager.p2Checks:0,
            lastPhase=manager!=null?manager.currentTurn.ToString():"Loading",feedback=manager!=null?manager.feedbackMessage:null,
            deck=initialDeck,curs=initialCurs,trace=trace.ToArray(),errors=errors.ToArray()};
        matches.Add(record);Save(false);
        Debug.Log("BOT TOURNAMENT RESULT "+record.match+"/"+MatchCount+" "+outcome+" turns="+actionTurns+" seconds="+record.seconds.ToString("F2"));
        if(matches.Count==MatchCount){Finish(true,"Completed "+MatchCount+" match attempts.");return;}
        if(matches.Count>=3 && matches.Skip(matches.Count-3).All(m=>m.outcome=="Error")){Finish(false,"Three consecutive runtime errors; stopped rather than generate invalid matches.");return;}
        BeginMatch();
    }
    static void Save(bool complete)
    {
        var report=new Report{timestampUtc=DateTime.UtcNow.ToString("O"),complete=complete,requested=MatchCount,recorded=matches.Count,level=Level,
            cursermancerWins=matches.Count(m=>m.outcome=="Cursermancer"),villagerWins=matches.Count(m=>m.outcome=="Villager"),
            unresolved=matches.Count(m=>m.outcome=="Unresolved"),errors=matches.Count(m=>m.outcome=="Error"),seconds=EditorApplication.timeSinceStartup-batchStarted,
            method="Actual MainBoard scene, unmodified CurBotAI and VillagerBotAI MonoBehaviours/coroutines, authoritative TurnManager outcomes.",
            settings="Level 2 only; 24 cards; 8 neutral choices selected by normal bot-mode random draft (first offer uniformly selected by referee); 2 Curs selected by Cur bot; alternating starting factions; timeScale=100; serialized bot search/pacing retained.",matches=matches.ToArray()};
        File.WriteAllText(Path.Combine(Root,"BotTournament-Level2.json"),JsonUtility.ToJson(report,true));
        string csv="match,seed,level,first_player,outcome,action_turns,seconds,living_curs,living_villagers,maximum_seats,reason\n"+
            string.Join("\n",matches.Select(m=>m.match+","+m.seed+","+m.level+","+m.firstPlayer+","+m.outcome+","+m.actionTurns+","+m.seconds.ToString("F3",System.Globalization.CultureInfo.InvariantCulture)+","+m.livingCurs+","+m.livingVillagers+","+m.maximumSeats+",\""+m.reason.Replace("\"","\"\"")+"\""));
        File.WriteAllText(Path.Combine(Root,"BotTournament-Level2.csv"),csv);
    }
    static void Finish(bool complete,string message)
    {
        EditorApplication.update-=Tick;Application.logMessageReceived-=OnLog;SceneManager.sceneLoaded-=OnLoaded;
        SessionState.SetBool(Request,false);Save(complete);Debug.Log("BOT TOURNAMENT FINISH: "+message);
        EditorApplication.Exit(complete?0:1);
    }
}

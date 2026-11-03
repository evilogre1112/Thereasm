using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Only the isolated benchmark copy replaces WaitForSeconds construction with this adapter.
// The default path retains the original delay, including all normal Editor validations.
public static class BotBatchPacing
{
    public static bool FastEnabled;
    public static WaitForSeconds Wait(float seconds) => new WaitForSeconds(FastEnabled ? 0 : seconds);
}

#if THEREASM_BOT_BENCHMARK
[DefaultExecutionOrder(10000)]
public class NeutralTournamentRunner : MonoBehaviour
{
    [Serializable] public class Group { public int number; public string[] assets,names; }
    [Serializable] public class Game
    {
        public int id,pair,seed,mask,level,frames,actionTurns,rituals,maximumSeats,livingCurs,livingVillagers;
        public string firstPlayer,winner,reason,phase;
        public double seconds;
        public int p1Checks,p2Checks;
        public string[] neutral,curs,errors;
    }
    [Serializable] public class Result
    {
        public bool complete,fast;
        public string utc,method;
        public int requested,recorded,level,curWins,villageWins,unresolved,errorMatches;
        public double seconds;
        public Group[] groups;
        public Game[] games;
    }
    class Entry { public int mask,seed,pair;public bool curFirst; }
    readonly List<Entry> schedule=new List<Entry>();
    readonly List<Game> games=new List<Game>();
    readonly List<string> errors=new List<string>();
    readonly Dictionary<string,CardData> definitions=new Dictionary<string,CardData>();
    readonly System.Diagnostics.Stopwatch batch=new System.Diagnostics.Stopwatch();
    readonly System.Diagnostics.Stopwatch clock=new System.Diagnostics.Stopwatch();
    TurnManager tm; DeckManager deck; Group[] groups;
    int count,index,loadFrame,startFrame,turns,rituals,maxSeats,lastPhase=-1,lastVoting=-1,lastProgressFrame;
    bool attached,started,reload,exiting,fast;
    string output,lastState="";string[] selectedNeutral,selectedCurs=new string[0];
    double heartbeat;
    StreamWriter journal;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Boot()
    {
        if(!Environment.GetCommandLineArgs().Contains("-neutral-tournament"))return;
        var go=new GameObject("Neutral tournament referee");DontDestroyOnLoad(go);go.AddComponent<NeutralTournamentRunner>();
    }
    string Arg(string key,string fallback)
    {var a=Environment.GetCommandLineArgs();int i=Array.IndexOf(a,key);return i>=0&&i+1<a.Length?a[i+1]:fallback;}
    void Awake()
    {
        count=int.Parse(Arg("-matches","1000"));fast=Arg("-profile","fast")=="fast";
        output=Path.GetFullPath(Arg("-results",Path.Combine(Application.persistentDataPath,"NeutralTournament")));
        Directory.CreateDirectory(Path.GetDirectoryName(output));
        journal=new StreamWriter(output+".jsonl",false,new System.Text.UTF8Encoding(false));
        BotBatchPacing.FastEnabled=fast;Application.runInBackground=true;
        Application.targetFrameRate=fast?-1:240;QualitySettings.vSyncCount=0;
        Time.timeScale=100;Time.fixedDeltaTime=fast?10:2;Time.maximumDeltaTime=10;
        Time.captureDeltaTime=fast?.01f:0;AudioListener.pause=true;
        Debug.unityLogger.filterLogType=LogType.Error;
        Application.logMessageReceived+=Log;SceneManager.sceneLoaded+=Loaded;
        BuildSchedule();batch.Start();clock.Start();
        GameSettings.currentMode=GameMode.PlayerVsCurBot;GameSettings.currentLevel=2;
    }
    void BuildSchedule()
    {
        var random=new System.Random(20261030);
        var halves=Enumerable.Range(0,128).OrderBy(_=>random.Next()).ToArray();
        var excluded=new HashSet<int>();foreach(int m in halves.Take(6)){excluded.Add(m);excluded.Add(m^255);}
        int pair=0;
        for(int cycle=0;cycle<2;cycle++)foreach(int mask in Enumerable.Range(0,256).OrderBy(_=>random.Next()))
        {
            if(cycle==1&&excluded.Contains(mask))continue;
            int seed=20261130+pair;
            bool curFirst=random.Next(2)==0;
            schedule.Add(new Entry{mask=mask,seed=seed,pair=pair,curFirst=curFirst});
            schedule.Add(new Entry{mask=mask,seed=seed,pair=pair,curFirst=!curFirst});pair++;
        }
        if(count<1||count>schedule.Count)throw new Exception("Match count must be 1..1000");
        schedule.RemoveRange(count,schedule.Count-count);
    }
    void Loaded(Scene scene,LoadSceneMode mode)
    {
        if(scene.name!="MainBoard")return;
        try
        {
            tm=FindFirstObjectByType<TurnManager>();deck=FindFirstObjectByType<DeckManager>();
            var pairs=deck.neutralCardPool.GroupBy(c=>c.cardNumber).OrderBy(g=>g.Key).ToArray();
            if(pairs.Length!=8||pairs.Any(g=>g.Count()!=2))throw new Exception("Expected exactly 8 binary Neutral groups.");
            groups=pairs.Select(g=>new Group{number=g.Key,assets=g.OrderBy(c=>c.name,StringComparer.Ordinal).Select(c=>c.name).ToArray(),
                names=g.OrderBy(c=>c.name,StringComparer.Ordinal).Select(c=>c.cardName).ToArray()}).ToArray();
            definitions.Clear();foreach(var c in deck.allCards.Concat(deck.neutralCardPool))if(c!=null)definitions[c.name]=c;
            var entry=schedule[index];UnityEngine.Random.InitState(entry.seed);
            selectedNeutral=groups.Select((g,i)=>g.assets[(entry.mask>>i)&1]).ToArray();
            // Exhaustive, preselected draft only. GenerateDeck and every gameplay action are original.
            deck.allCards=deck.allCards.Where(c=>c.owner!=DefaultOwner.Neutral).Concat(selectedNeutral.Select(n=>definitions[n])).ToList();
            deck.neutralCardPool=new List<CardData>();
            tm.firstPlayerThisRound=entry.curFirst?TurnState.Player1Turn:TurnState.Player2Turn;
            attached=started=false;reload=false;errors.Clear();selectedCurs=new string[0];
            turns=rituals=maxSeats=0;lastPhase=lastVoting=-1;lastState="";
            loadFrame=startFrame=lastProgressFrame=Time.frameCount;clock.Restart();
        }
        catch(Exception e){errors.Add(e.ToString());Stop(false,"Loading failure");}
    }
    void Log(string message,string stack,LogType type)
    {if((type==LogType.Error||type==LogType.Exception||type==LogType.Assert)&&errors.Count<8)errors.Add(message+"\n"+stack);}
    void LateUpdate()
    {
        if(exiting)return;
        try
        {
            if(reload)
            {
                reload=false;GameSettings.currentMode=GameMode.PlayerVsCurBot;GameSettings.currentLevel=2;
                SceneManager.LoadScene("MainBoard");return;
            }
            if(tm==null||deck==null)return;
            if(errors.Count>0){Record("Error","Runtime error");return;}
            if(!attached&&Time.frameCount>loadFrame+1)
            {
                Canvas.ForceUpdateCanvases();
                tm.curBot.tm=tm;tm.curBot.gameObject.SetActive(true);tm.curBot.enabled=true;
                tm.villagerBot.gameObject.SetActive(true);tm.villagerBot.enabled=true;
                attached=true;
                if(fast)
                {
                    // These are display-only components. Keep active GameObjects, real Button
                    // callbacks, board geometry, card/rule components and all bot settings.
                    foreach(var c in FindObjectsByType<Canvas>(FindObjectsInactive.Include,FindObjectsSortMode.None))c.enabled=false;
                    foreach(var c in FindObjectsByType<LocalizedLabel>(FindObjectsInactive.Include,FindObjectsSortMode.None))c.enabled=false;
                    foreach(var c in FindObjectsByType<FloatingUI>(FindObjectsInactive.Include,FindObjectsSortMode.None))c.enabled=false;
                    foreach(var c in FindObjectsByType<CardDialogue>(FindObjectsInactive.Include,FindObjectsSortMode.None))c.enabled=false;
                    foreach(var c in FindObjectsByType<CardOwnershipView>(FindObjectsInactive.Include,FindObjectsSortMode.None))c.enabled=false;
                    foreach(var c in FindObjectsByType<OwnershipLegend>(FindObjectsInactive.Include,FindObjectsSortMode.None))c.enabled=false;
                }
            }
            if(!started&&tm.currentTurn!=TurnState.SetupPhase)
            {
                var cards=Cards();selectedCurs=tm.allCurs.Where(c=>c!=null).Select(c=>c.cardData.name).OrderBy(n=>n).ToArray();
                if(cards.Length!=24||selectedCurs.Length!=2)throw new Exception("Invalid level-2 setup.");
                if(tm.currentTurn!=(schedule[index].curFirst?TurnState.Player1Turn:TurnState.Player2Turn))throw new Exception("First-player mismatch.");
                started=true;
            }
            if(tm.allSeats!=null)maxSeats=Math.Max(maxSeats,tm.allSeats.Count);
            if((int)tm.currentTurn!=lastPhase || (tm.currentTurn==TurnState.SacrificePhase&&(int)tm.currentVotingPlayer!=lastVoting))
            {
                if(tm.currentTurn==TurnState.Player1Turn||tm.currentTurn==TurnState.Player2Turn)turns++;
                if(tm.currentTurn==TurnState.CursePhase)rituals++;
                lastPhase=(int)tm.currentTurn;lastVoting=(int)tm.currentVotingPlayer;lastProgressFrame=Time.frameCount;
            }
            if(tm.isGameOver)
            {
                var alive=Living(Cards());var result=GameRules.EvaluateWinner(alive.Item1,alive.Item2);
                if(result==GameOutcome.InProgress)throw new Exception("Winner does not satisfy real victory rules.");
                Record(result==GameOutcome.CursWin?"Cursermancer":"Villager","TurnManager declared winner");return;
            }
            if(Time.frameCount%12==0)
            {
                string state=tm.currentTurn+":"+tm.currentVotingPlayer+":"+tm.p1Hearts+":"+tm.p2Hearts+":"+tm.p1Checks+":"+tm.p2Checks+":"+tm.isTallyingVotes+":"+
                    string.Join("|",Cards().Select(c=>c.cardData.name+":"+c.isDead+":"+c.transform.parent?.GetInstanceID()+":"+c.isInfected+":"+c.hasUsedSkill+":"+tm.GetVoteTarget(c)));
                if(state!=lastState){lastState=state;lastProgressFrame=Time.frameCount;}
            }
            if(turns>500){Record("Unresolved","500-action-turn limit");return;}
            if(Time.frameCount-lastProgressFrame>4000||clock.Elapsed.TotalSeconds>90){Record("Unresolved","No progress / real-time safety limit");return;}
            if(batch.Elapsed.TotalSeconds-heartbeat>3){heartbeat=batch.Elapsed.TotalSeconds;Progress();}
        }
        catch(Exception e){errors.Add(e.ToString());Record("Error","Referee exception");}
    }
    CardInstance[] Cards()=>FindObjectsByType<CardInstance>(FindObjectsInactive.Include,FindObjectsSortMode.None)
        .Where(c=>c.cardData!=null&&c.gameObject.scene.name=="MainBoard"&&
            (deck.draftPanel==null||!c.transform.IsChildOf(deck.draftPanel.transform))&&
            (tm.discoverPanel==null||!c.transform.IsChildOf(tm.discoverPanel.transform))).ToArray();
    (int,int) Living(CardInstance[] cards)
    {
        int cur=0,village=0;foreach(var c in cards)
        {
            if(c.isDead||c.transform.parent==tm.cemeteryCardPos||c.transform.parent==tm.globalCemetery)continue;
            if(tm.allCurs.Contains(c))cur++;else if(c.cardData.owner==DefaultOwner.Player2)village++;
        }
        return(cur,village);
    }
    void Record(string winner,string reason)
    {
        var entry=schedule[index];var alive=Living(Cards());
        var game=new Game{id=index+1,pair=entry.pair,seed=entry.seed,mask=entry.mask,level=2,firstPlayer=entry.curFirst?"Cursermancer":"Villager",
            winner=winner,reason=reason,seconds=clock.Elapsed.TotalSeconds,frames=Time.frameCount-startFrame,actionTurns=turns,rituals=rituals,
            maximumSeats=maxSeats,livingCurs=alive.Item1,livingVillagers=alive.Item2,p1Checks=tm.p1Checks,p2Checks=tm.p2Checks,
            neutral=selectedNeutral,curs=selectedCurs,phase=tm.currentTurn.ToString(),errors=errors.ToArray()};
        games.Add(game);journal.WriteLine(JsonUtility.ToJson(game));index++;
        if(index%10==0){journal.Flush();Progress();}
        if(index>=count){Stop(true,"Complete");return;}
        if(games.Count>=3&&games.Skip(games.Count-3).All(g=>g.winner=="Error")){Stop(false,"Three consecutive errors");return;}
        reload=true;
    }
    Result Summary(bool complete)=>new Result{complete=complete,fast=fast,utc=DateTime.UtcNow.ToString("O"),level=2,requested=count,recorded=games.Count,
        curWins=games.Count(g=>g.winner=="Cursermancer"),villageWins=games.Count(g=>g.winner=="Villager"),
        unresolved=games.Count(g=>g.winner=="Unresolved"),errorMatches=games.Count(g=>g.winner=="Error"),seconds=batch.Elapsed.TotalSeconds,
        method="Original MainBoard/TurnManager and serialized bots; exhaustive preselected Neutral draft; paired starting sides and seeds. Fast path replaces presentation waits with one-frame yields and disables display-only rendering, not decisions or rules.",
        groups=groups,games=complete?games.ToArray():null};
    void Progress()=>File.WriteAllText(output+"-progress.json",JsonUtility.ToJson(Summary(false),true));
    void Stop(bool complete,string reason)
    {
        if(exiting)return;exiting=true;journal?.Flush();journal?.Dispose();
        File.WriteAllText(output+".json",JsonUtility.ToJson(Summary(complete),true));Progress();
        File.WriteAllText(output+"-finish.txt",reason);Application.Quit(complete?0:1);
    }
}
#endif

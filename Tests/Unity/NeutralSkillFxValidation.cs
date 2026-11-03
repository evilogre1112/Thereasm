using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object=UnityEngine.Object;

[InitializeOnLoad]
public static class NeutralSkillFxValidation
{
    const string Request="Thereasm.NeutralSkillFxValidation";
    static GameObject root;
    static TurnManager turns;
    static NeutralSkillFx fx;
    static CardInstance[] cards;
    static int assertions;
    static readonly List<string> cases=new List<string>();
    [Serializable] class Report {public bool passed;public int assertions;public string message;public string[] cases;}
    static NeutralSkillFxValidation(){EditorApplication.playModeStateChanged+=s=>{if(s==PlayModeStateChange.EnteredPlayMode&&SessionState.GetBool(Request,false))EditorApplication.delayCall+=Check;};}
    public static void Run(){SessionState.SetBool(Request,true);EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);EditorApplication.isPlaying=true;}
    static void Expect(bool value,string message){assertions++;if(!value)throw new Exception(message);}
    static RectTransform Node(string name,Transform parent=null){var go=new GameObject(name,typeof(RectTransform));go.transform.SetParent(parent,false);return (RectTransform)go.transform;}
    static void Setup(string sourceName)
    {
        if(root!=null)Object.DestroyImmediate(root);GameSettings.currentMode=GameMode.PvP;Skill_Gambler.ResetComboChain();
        root=Node("FX validation").gameObject;root.AddComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;
        turns=Node("Turns",root.transform).gameObject.AddComponent<TurnManager>();turns.enabled=false;turns.allSeats=new List<SeatDropZone>();turns.currentTurn=TurnState.SacrificePhase;turns.isTallyingVotes=true;
        turns.voteEffects=turns.gameObject.AddComponent<VoteEffectFeed>();
        var cardPrefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/CardUI.prefab");cards=new CardInstance[3];
        for(int i=0;i<3;i++)
        {
            var seat=Node("Seat "+i,root.transform).gameObject.AddComponent<SeatDropZone>();seat.GetComponent<RectTransform>().anchoredPosition=new Vector2((i-1)*220,0);seat.cardPos=Node("CardPos",seat.transform);turns.allSeats.Add(seat);
            var name=i==1?sourceName:i==0?"Villager10":"Villager1";
            var data=Object.Instantiate(AssetDatabase.LoadAssetAtPath<CardData>("Assets/ScriptableObjects/"+name+".asset"));
            if(data.cardSkill!=null)data.cardSkill=Object.Instantiate(data.cardSkill);
            var card=Object.Instantiate(cardPrefab,seat.cardPos).GetComponent<CardInstance>();card.enabled=false;card.SetupCard(data);cards[i]=card;
            var input=card.GetComponent<CardInteraction>();input.enabled=false;input.isPlaced=true;input.originalParent=card.transform.parent;input.lockState=3;
            card.GetComponent<CardVotePresentation>().enabled=false;
        }
        var layer=Node("Effect layer",root.transform);layer.anchorMin=Vector2.zero;layer.anchorMax=Vector2.one;layer.sizeDelta=Vector2.zero;
        var group=layer.gameObject.AddComponent<CanvasGroup>();group.blocksRaycasts=false;group.interactable=false;
        fx=root.AddComponent<NeutralSkillFx>();fx.layer=layer;
        fx.ballotPrefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Effects/SkillBallotPaper.prefab").GetComponent<SkillFxGraphic>();
        fx.sparkPrefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Effects/SkillComicSpark.prefab").GetComponent<SkillFxGraphic>();
        fx.ringPrefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Effects/SkillVoteImpact.prefab").GetComponent<SkillFxGraphic>();
        fx.burstPrefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Effects/SkillComicBurst.prefab").GetComponent<SkillFxBurst>();
        foreach(var graphic in new[]{fx.ballotPrefab,fx.sparkPrefab,fx.ringPrefab,fx.burstPrefab.graphic})
            Expect(graphic.traderSprite!=null&&graphic.jesterSprite!=null&&graphic.sprite!=null,"Painted FX sprite reference is missing: "+graphic.name);
        Expect(fx.ballotPrefab.traderSprite!=fx.ballotPrefab.jesterSprite&&fx.burstPrefab.graphic.traderSprite!=fx.burstPrefab.graphic.jesterSprite,"Trader and Jester do not have distinct artwork.");
        Canvas.ForceUpdateCanvases();
    }
    static void Raw(int[] counts){for(int i=0;i<3;i++){cards[i].voteText.text=counts[i].ToString();cards[i].voteText.gameObject.SetActive(counts[i]>0);}}
    static void Refresh(){foreach(var card in cards)card.GetComponent<CardVotePresentation>().Refresh();}
    static void Pass(string text){cases.Add(text);Debug.Log("NEUTRAL FX CHECK: "+text);}
    static VoteEffectRecord Record(int[] before,int[] after,string kind)
    {
        turns.voteEffects.BeginTally(before);turns.voteEffects.RecordChange(cards[1],kind,"FX test",before,after,kind=="Trader"?new[]{0,1,2}:null);
        Raw(after);return turns.voteEffects.Local.Last();
    }
    static void CheckTargetedRecord(VoteEffectRecord record,int[] before,int[] after)
    {
        Raw(after);var wire=JsonUtility.FromJson<VoteEffectRecord>(JsonUtility.ToJson(record));
        string random=JsonUtility.ToJson(UnityEngine.Random.state);
        var actor=cards[1].GetComponent<CardInteraction>().visualChild;var scale=actor.localScale;var rotation=actor.localRotation;
        var positions=cards.Select(c=>c.GetComponent<CardVotePresentation>().voteMotion.anchoredPosition).ToArray();
        Expect(fx.TryPlay(wire),record.kind+" refused its serialized public tally.");
        fx.EvaluateAt(.35f);Refresh();
        foreach(var change in wire.changes)
            Expect(cards.Single(c=>c.cardData.cardNumber==change.number).GetComponent<CardVotePresentation>().DisplayedVotes==change.before,"Count changed before ballot arrival.");
        var papers=fx.layer.GetComponentsInChildren<SkillFxGraphic>(true).Where(g=>g.glyph==SkillFxGraphic.Glyph.Ballot).ToArray();
        int expected=wire.changes.Sum(c=>Math.Abs(c.after-c.before));
        Expect(papers.Length==expected,"Targeted FX invented or omitted a ballot.");
        Vector3? launch=papers.Length>0?papers[0].rectTransform.position:(Vector3?)null;
        fx.EvaluateAt(.6f);
        if(launch.HasValue)Expect(Vector3.Distance(launch.Value,papers[0].rectTransform.position)>1,"Targeted ballot is not moving.");
        fx.EvaluateAt(1.2f);Refresh();
        for(int i=0;i<3;i++)
        {
            Expect(cards[i].GetComponent<CardVotePresentation>().DisplayedVotes==after[i],"Targeted arrival differs from the skill result.");
            Expect(cards[i].voteText.text==after[i].ToString(),"Targeted FX changed authoritative counts.");
        }
        Expect(random==JsonUtility.ToJson(UnityEngine.Random.state),"Targeted FX consumed gameplay randomness.");
        fx.EvaluateAt(record.PresentationSeconds+.1f);
        Expect(!fx.IsPlaying&&actor.localScale==scale&&actor.localRotation==rotation,"Targeted FX leaves a transform offset.");
        Expect(positions.SequenceEqual(cards.Select(c=>c.GetComponent<CardVotePresentation>().voteMotion.anchoredPosition)),"Targeted FX moved authored icons.");
        Expect(fx.layer.GetComponentsInChildren<Graphic>(true).All(g=>!g.raycastTarget),"Targeted FX blocks input.");
    }
    static void TargetedChecks()
    {
        foreach(bool infected in new[]{false,true})
        {
            Setup("Snooper22");cards[1].isInfected=infected;turns.currentVotingPlayer=DefaultOwner.Player1;
            if(infected)
            {
                turns.isTallyingVotes=false;
                Expect(cards[1].cardData.cardSkill.TryCastActiveSkill(cards[1],cards[0]),"Decay Snooper selection failed.");
                var selection=turns.voteEffects.For(DefaultOwner.Player1).Last();
                Expect(!fx.TryPlay(selection)&&fx.ActiveParticles==0,"Private Snooper selection launched public ballots.");
                Expect(turns.voteEffects.For(DefaultOwner.Player2).Length==0,"Private Snooper selection leaked to Villager.");
                turns.isTallyingVotes=true;
            }
            else turns.allCurs.Add(cards[2]);
            int[] before={1,1,1},after=(int[])before.Clone();
            cards[1].cardData.cardSkill.TryOnBeforeVoteCalculate(cards[1],after,1);
            var record=Record(before,after,"Snooper");
            CheckTargetedRecord(record,before,after);
            Expect(turns.voteEffects.For(DefaultOwner.Player2).Last().changes.Single().after==2,"Public Snooper tally differs by viewer.");
            Pass("Snooper public ballot flight and private selection boundary, Decay="+infected);
        }
        Setup("Snooper22");var empty=Record(new[]{1,1,1},new[]{1,1,1},"Snooper");
        CheckTargetedRecord(empty,new[]{1,1,1},new[]{1,1,1});
        Pass("Snooper without a valid target throws no fake ballot");

        foreach(int mode in new[]{0,1,2,3})
        {
            Setup("Gambler24");turns.firstPlayerThisRound=TurnState.Player1Turn;turns.isTallyingVotes=false;
            cards[0].cardData.owner=DefaultOwner.Player1;cards[2].cardData.owner=DefaultOwner.Player2;
            var votes=(Dictionary<CardInstance,int>)typeof(TurnManager).GetField("cardVotes",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(turns);
            votes[cards[0]]=2;votes[cards[2]]=0;
            if(mode==0||mode==2)
            {
                turns.currentVotingPlayer=DefaultOwner.Player1;
                Expect(cards[1].cardData.cardSkill.TryCastActiveSkill(cards[1],cards[0]),"First bet setup failed.");
                Expect(!fx.TryPlay(turns.voteEffects.For(DefaultOwner.Player1).Last()),"A private bet launched public ballots.");
                Expect(turns.voteEffects.For(DefaultOwner.Player2).Length==0,"First bet leaked to second player.");
            }
            if(mode!=0)
            {
                turns.currentVotingPlayer=DefaultOwner.Player2;
                Expect(cards[1].cardData.cardSkill.TryCastActiveSkill(cards[1],cards[2]),"Second bet setup failed.");
            }
            int[] before=mode==3?new[]{0,1,1}:new[]{2,1,3},after=(int[])before.Clone();
            turns.isTallyingVotes=true;turns.voteEffects.BeginTally(before);
            cards[1].cardData.cardSkill.TryOnBeforeVoteCalculate(cards[1],after,1);
            var records=turns.voteEffects.Local.Where(r=>r.kind=="Gambler"&&r.tally).ToArray();
            Expect(records.Length==(mode==2?2:1),"Gambler lost a real per-player outcome.");
            var running=(int[])before.Clone();
            foreach(var record in records)
            {
                var next=(int[])running.Clone();foreach(var change in record.changes)next[Array.FindIndex(cards,c=>c.cardData.cardNumber==change.number)]=change.after;
                CheckTargetedRecord(record,running,next);running=next;
            }
            Expect(running.SequenceEqual(after),"Gambler sequence did not end at the actual total.");
            Pass("Gambler real win/loss/dual-bet/zero-removal outcomes, mode="+mode);
        }
    }
    static void Check()
    {
        try
        {
            Localizer.Initialize(AssetDatabase.LoadAssetAtPath<GameLocalization>("Assets/UI/Localization/GameLocalization.asset"),GameLanguage.English);Localizer.SetLanguage(GameLanguage.English,false);
            Setup("Trader20");int[] before={5,3,1};var after=(int[])before.Clone();cards[1].cardData.cardSkill.TryOnBeforeVoteCalculate(cards[1],after,1);
            var record=Record(before,after,"Trader");Expect(record.participants.Length==3&&record.participants[2].before==record.participants[2].after,"Trader omitted the unchanged occupied participant.");
            var wire=JsonUtility.FromJson<VoteEffectRecord>(JsonUtility.ToJson(record));Expect(wire.participants.Length==3&&wire.participants[0].before==5,"Trader participants do not survive snapshot serialization.");
            string random=JsonUtility.ToJson(UnityEngine.Random.state);var visual=cards[1].GetComponent<CardInteraction>().visualChild;var scale=visual.localScale;var rotation=visual.localRotation;
            var positions=cards.Select(c=>c.GetComponent<CardVotePresentation>().voteMotion.anchoredPosition).ToArray();
            Expect(fx.TryPlay(record),"Trader VFX refused a real tally event.");fx.EvaluateAt(.18f);Refresh();
            Expect(visual.localScale.x>scale.x*1.05f&&visual.localScale.x<scale.x*1.23f,"Trader does not punch or grows too much.");
            Expect(cards[0].GetComponent<CardVotePresentation>().DisplayedVotes==5,"Trader changes the displayed vote count before delivery.");
            fx.EvaluateAt(.55f);Expect(fx.ActiveParticles>0,"Trader has no moving ballot papers.");
            Expect(!fx.GetComponentInChildren<SkillFxBurst>().formula.gameObject.activeSelf,"Trader still renders the removed floating equation.");
            fx.EvaluateAt(1.4f);Refresh();Expect(cards.All(c=>c.GetComponent<CardVotePresentation>().DisplayedVotes==1),"Trader arrivals did not set every occupied share.");
            Expect(cards.All(c=>c.voteText.text=="1"),"VFX mutated the authoritative vote label.");
            fx.EvaluateAt(1.8f);Expect(!fx.IsPlaying&&visual.localScale==scale&&visual.localRotation==rotation,"Trader leaves the source enlarged or tilted.");
            Expect(positions.SequenceEqual(cards.Select(c=>c.GetComponent<CardVotePresentation>().voteMotion.anchoredPosition)),"FX moved the user's authored vote icons.");
            Expect(random==JsonUtility.ToJson(UnityEngine.Random.state),"Trader VFX consumed gameplay randomness.");Pass("Trader punch, real occupied group, delayed arrivals, no floating equation, untouched state and authored icon positions");

            Setup("Trader20");before=new[]{2,0,0};after=(int[])before.Clone();cards[1].cardData.cardSkill.TryOnBeforeVoteCalculate(cards[1],after,1);record=Record(before,after,"Trader");
            Expect(fx.TryPlay(record),"Zero-share Trader cue is missing.");fx.EvaluateAt(1.4f);Refresh();
            Expect(cards.All(c=>c.GetComponent<CardVotePresentation>().DisplayedVotes==0)&&!fx.GetComponentInChildren<SkillFxBurst>().formula.gameObject.activeSelf,"Trader zero-share animation invents extra votes or shows an equation.");fx.Stop();Pass("Trader zero shares and discarded remainder remain faithful to the real outcome");

            foreach(bool wantVotes in new[]{true,false})
            {
                Setup("Jester20");before=new[]{1,1,1};after=null;
                for(int seed=0;seed<100;seed++)
                {UnityEngine.Random.InitState(seed);var candidate=(int[])before.Clone();cards[1].cardData.cardSkill.TryOnBeforeVoteCalculate(cards[1],candidate,1);if((candidate.Sum()>before.Sum())==wantVotes){after=candidate;break;}}
                Expect(after!=null,"Could not obtain a real Jester outcome for FX validation.");record=Record(before,after,"Jester");random=JsonUtility.ToJson(UnityEngine.Random.state);visual=cards[1].GetComponent<CardInteraction>().visualChild;scale=visual.localScale;
                Expect(fx.TryPlay(record),"Jester VFX refused its actual outcome.");fx.EvaluateAt(.32f);Refresh();
                Expect(cards.Where(c=>c!=cards[1]).All(c=>c.GetComponent<CardVotePresentation>().DisplayedVotes==1),"Jester vote increments happen before papers arrive.");
                if(wantVotes)Expect(fx.ActiveParticles>0,"Jester does not throw any ballots.");
                else Expect(!fx.layer.GetComponentsInChildren<SkillFxGraphic>(true).Any(g=>g.glyph==SkillFxGraphic.Glyph.Ballot),"A no-vote Jester outcome throws fake votes.");
                fx.EvaluateAt(1.35f);Refresh();for(int i=0;i<3;i++)Expect(cards[i].GetComponent<CardVotePresentation>().DisplayedVotes==after[i],"Jester landing count differs from the real result.");
                Expect(random==JsonUtility.ToJson(UnityEngine.Random.state),"Jester VFX rerolled or consumed gameplay randomness.");
                fx.Stop();Expect(visual.localScale==scale&&fx.layer.GetComponentsInChildren<Graphic>(true).All(g=>!g.raycastTarget),"Stopped Jester leaves a transform offset or blocks input.");
                Pass(wantVotes?"Jester throws exactly its generated votes, applies increments on arrival and never rerolls":"Jester's no-vote outcome has a distinct cue without false ballots");
            }
            Setup("Trader20");record=Record(new[]{5,3,1},new[]{1,1,1},"Trader");Expect(fx.TryPlay(record),"Interruption setup failed.");fx.EvaluateAt(.2f);fx.enabled=true;turns.isTallyingVotes=false;
            typeof(NeutralSkillFx).GetMethod("LateUpdate",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(fx,null);
            Expect(!fx.IsPlaying,"A phase change did not clean up the skill effect.");Expect(!fx.TryPlay(record),"A historical/private non-tally cue plays as a fresh live skill.");
            turns.isTallyingVotes=true;Expect(fx.TryPlay(record),"Disable cleanup setup failed.");fx.EvaluateAt(.3f);fx.enabled=false;Refresh();
            Expect(!fx.IsPlaying&&!fx.TryPlay(record)&&cards.All(c=>c.GetComponent<CardVotePresentation>().DisplayedVotes==1),"Disabling VFX stalls the queue or leaves displayed counts unfinished.");
            Pass("Phase changes clean up safely; non-tally/history events cannot launch live board effects");
            TargetedChecks();
            Finish(true,"Trader, Jester, Snooper and Gambler native Play Mode FX checks passed.");
        }
        catch(Exception e){Finish(false,e.ToString());}
    }
    static void Finish(bool passed,string message)
    {
        SessionState.SetBool(Request,false);File.WriteAllText(Path.Combine(Directory.GetParent(Application.dataPath).FullName,"NeutralSkillFxValidationResult.json"),JsonUtility.ToJson(new Report{passed=passed,assertions=assertions,message=message,cases=cases.ToArray()},true));
        Debug.Log("NEUTRAL FX RESULT: "+message);EditorApplication.Exit(passed?0:1);
    }
}

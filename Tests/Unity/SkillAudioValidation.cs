using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

[InitializeOnLoad]
public static class SkillAudioValidation
{
    const string Request="Thereasm.SkillAudioValidation";
    static GameObject root;
    static SkillAudio audio;
    static TurnManager turns;
    static CardInstance[] cards;
    static int assertions;
    static List<string> cases=new List<string>();
    [Serializable] class Report{public bool passed;public int assertions;public string message;public string[] cases;}
    static SkillAudioValidation(){EditorApplication.playModeStateChanged+=s=>{if(s==PlayModeStateChange.EnteredPlayMode&&SessionState.GetBool(Request,false))EditorApplication.delayCall+=Check;};}
    static void Expect(bool value,string message){assertions++;if(!value)throw new Exception(message);}
    public static void Run()
    {
        try
        {
            foreach(string scene in new[]{"MainBoard","TutorialBoard"})
            {
                EditorSceneManager.OpenScene("Assets/Scenes/"+scene+".unity");
                var saved=Object.FindFirstObjectByType<SkillAudio>();
                if(saved==null||saved.bank==null||saved.source==null||saved.source.clip!=null||saved.source.playOnAwake)
                    throw new Exception("Saved skill audio references are missing or play on scene load: "+scene);
                if(Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None).Length!=1)throw new Exception("AudioListener count changed in "+scene);
            }
            SessionState.SetBool(Request,true);EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);EditorApplication.isPlaying=true;
        }
        catch(Exception e){Finish(false,e.ToString());}
    }
    static RectTransform Node(string name,Transform parent)
    {var go=new GameObject(name,typeof(RectTransform));go.transform.SetParent(parent,false);return (RectTransform)go.transform;}
    static SkillAudio NewAudio()
    {
        var node=Node("Audio",root.transform).gameObject;node.SetActive(false);
        var value=node.AddComponent<SkillAudio>();value.bank=AssetDatabase.LoadAssetAtPath<SkillSoundBank>("Assets/AudioClip/Skills/CardSkillSounds.asset");
        value.masterVolume=.55f;node.SetActive(true);return value;
    }
    static void Setup(string skill="Gambler24")
    {
        if(root!=null)Object.DestroyImmediate(root);Skill_Gambler.ResetComboChain();
        GameSettings.currentMode=GameMode.PlayerVsCurBot;
        root=new GameObject("Audio checks",typeof(RectTransform),typeof(Canvas),typeof(AudioListener));
        turns=Node("Turns",root.transform).gameObject.AddComponent<TurnManager>();turns.enabled=false;
        turns.allSeats=new List<SeatDropZone>();turns.currentTurn=TurnState.SacrificePhase;turns.currentVotingPlayer=DefaultOwner.Player2;
        turns.voteEffects=turns.gameObject.AddComponent<VoteEffectFeed>();turns.p1Hearts=turns.p2Hearts=5;
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/CardUI.prefab");cards=new CardInstance[3];
        for(int i=0;i<3;i++)
        {
            var seat=Node("Seat "+i,root.transform).gameObject.AddComponent<SeatDropZone>();
            seat.GetComponent<RectTransform>().anchoredPosition=new Vector2((i-1)*220,0);
            seat.cardPos=Node("CardPos",seat.transform);turns.allSeats.Add(seat);
            var card=Object.Instantiate(prefab,seat.cardPos).GetComponent<CardInstance>();card.enabled=false;
            var data=Object.Instantiate(AssetDatabase.LoadAssetAtPath<CardData>("Assets/ScriptableObjects/"+(i==1?skill:i==0?"Villager10":"Villager1")+".asset"));
            if(data.cardSkill!=null)data.cardSkill=Object.Instantiate(data.cardSkill);card.SetupCard(data);cards[i]=card;
            var input=card.GetComponent<CardInteraction>();input.enabled=false;input.isPlaced=true;input.originalParent=card.transform.parent;
        }
        audio=NewAudio();
    }
    static void ClearThrottle()=>((Dictionary<string,float>)typeof(SkillAudio).GetField("lastPlayed",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(audio)).Clear();
    static void Pass(string text){cases.Add(text);Debug.Log("SKILL AUDIO CHECK: "+text);}
    static void Check()
    {
        try
        {
            Setup();Expect(audio.bank!=null&&audio.bank.cues.Length==23,"The authored sound bank is incomplete.");
            foreach(var type in TypeCache.GetTypesDerivedFrom<CardSkill>().Where(t=>!t.IsAbstract&&t.Name.StartsWith("Skill_")))
                Expect(audio.bank.Find(type.Name.Replace("Skill_",""))?.clip!=null,"Missing card sound: "+type.Name);
            foreach(var cue in audio.bank.cues)
            {
                Expect(cue.clip!=null&&cue.clip.length>.1f&&cue.clip.length<1&&cue.clip.channels==1,"Invalid audio asset: "+cue.key);
                cue.clip.LoadAudioData();var samples=new float[cue.clip.samples];
                Expect(cue.clip.GetData(samples,0),"Cannot decode imported audio: "+cue.key);
                Expect(samples.Max(v=>Mathf.Abs(v))<.95f&&samples.Max(v=>Mathf.Abs(v))>.1f,"Clipped or silent sound: "+cue.key);
            }
            Expect(audio.GetComponents<AudioSource>().Length==8&&audio.GetComponents<AudioSource>().All(v=>v.spatialBlend==0&&!v.loop&&!v.playOnAwake),"Skill audio is not a bounded 2D voice pool.");
            string rng=JsonUtility.ToJson(UnityEngine.Random.state);
            Expect(audio.PlayCue("Hunter"),"Hunter audio does not play.");
            Expect(!audio.PlayCue("Hunter"),"Same-frame duplicate sounds are stacked.");
            Expect(rng==JsonUtility.ToJson(UnityEngine.Random.state),"Skill sounds consume gameplay RNG.");
            audio.soundEnabled=false;Expect(!audio.PlayCue("Witch"),"Sound toggle is ignored.");audio.soundEnabled=true;
            audio.masterVolume=0;Expect(!audio.PlayCue("Witch"),"Master mute is ignored.");audio.masterVolume=.55f;
            Pass("All 18 card skills have directly referenced short, decoded, unclipped audio; bounded 2D playback, mute and independent RNG");

            Setup();int count=audio.PlayedCues;
            Expect(!cards[1].cardData.cardSkill.TryCastActiveSkill(cards[1],null),"Invalid Gambler selection succeeded.");
            Expect(audio.PlayedCues==count&&audio.For(DefaultOwner.Player1).Length==0&&audio.For(DefaultOwner.Player2).Length==0,"Rejected skill emitted audio.");
            Expect(cards[1].cardData.cardSkill.TryCastActiveSkill(cards[1],cards[0]),"Real Gambler selection failed.");
            Expect(audio.LastCue=="Gambler"&&audio.For(DefaultOwner.Player2).Single().key=="Gambler"&&audio.For(DefaultOwner.Player1).Length==0,"Private bet sound leaked or did not play for its owner.");
            Pass("Only successful active skills sound; real Gambler selections stay private");

            Setup("Snooper22");cards[1].isInfected=true;turns.currentVotingPlayer=DefaultOwner.Player1;count=audio.PlayedCues;
            Expect(cards[1].cardData.cardSkill.TryCastActiveSkill(cards[1],cards[0]),"Decay Snooper selection failed.");
            SkillAudio.Emit("Cursermancer",DefaultOwner.Player1);
            Expect(audio.PlayedCues==count&&audio.For(DefaultOwner.Player2).Length==0&&audio.For(DefaultOwner.Player1).Length==2,"Private curse or Decay sound leaked to the Villager client.");
            SkillAudio.Emit("Detective");
            Expect(audio.For(DefaultOwner.Player2).Single().id==1,"Private audio leaks through public sequence gaps.");
            var remote=NewAudio();remote.ApplyRemote(audio.For(DefaultOwner.Player2));
            Expect(remote.PlayedCues==0,"First snapshot replays old sounds.");
            remote.ApplyRemote(new[]{new SkillSoundEvent{id=1,key="Detective"},new SkillSoundEvent{id=2,key="Hunter"}});
            Expect(remote.PlayedCues==1&&remote.LastCue=="Hunter","New remote skill does not play exactly once.");
            remote.ApplyRemote(new[]{new SkillSoundEvent{id=2,key="Hunter"}});
            Expect(remote.PlayedCues==1,"Repeated snapshots replay skill audio.");
            Pass("Perspective-filtered audio has independent event IDs; remote initialization and repeated snapshots never replay history");

            Setup("Preacher18");audio.RefreshAuraSounds(turns);int events=audio.For(DefaultOwner.Player2).Length;
            Expect(events==1,"A protecting aura did not sound.");
            for(int i=0;i<10;i++)audio.RefreshAuraSounds(turns);
            Expect(audio.For(DefaultOwner.Player2).Length==events,"Unchanged aura refreshes repeatedly make sound.");
            cards[0].GetComponent<CardInteraction>().isPlaced=false;cards[0].transform.SetParent(root.transform,false);audio.RefreshAuraSounds(turns);
            Expect(audio.For(DefaultOwner.Player2).Length==events+1,"Changed aura target set did not produce a new event.");
            Pass("Passive aura feedback changes with target membership, never with redundant refresh calls");

            Setup();turns.isTallyingVotes=true;
            var record=new VoteEffectRecord{id=20,round=1,kind="Gambler",tally=true,changes=new[]{new VoteChange{before=2,after=1}}};
            Expect(audio.PlayTally(record)&&audio.LastCue=="GamblerLoss","Losing Gambler sounds like a win.");
            Expect(!audio.PlayTally(record),"Review/repeated tally replays the activation sound.");
            record=new VoteEffectRecord{id=21,round=1,kind="Snooper",tally=true};
            Expect(audio.PlayTally(record)&&audio.LastCue=="NoEffect","No-target effect sounds like a successful vote.");
            ClearThrottle();record=new VoteEffectRecord{id=22,round=1,kind="Snooper",tally=true,changes=new[]{new VoteChange{before=1,after=2}}};
            turns.isTallyingVotes=false;Expect(!audio.PlayTally(record),"Historical tally makes sound outside tally playback.");
            audio.enabled=false;Expect(!audio.PlayCue("Witch")&&audio.GetComponents<AudioSource>().All(v=>!v.isPlaying),"Disabling skill audio does not stop/disable playback.");
            Pass("Tally sounds distinguish real loss and empty results, reject history and stop cleanly");

            Setup("Snooper22");turns.allCurs.Add(cards[0]);turns.isTallyingVotes=true;
            int[] before={1,1,1},after=(int[])before.Clone();turns.voteEffects.BeginTally(before);
            cards[1].cardData.cardSkill.TryOnBeforeVoteCalculate(cards[1],after,1);
            turns.voteEffects.RecordChange(cards[1],"Snooper","Actual audio/FX timing",before,after);
            var fx=root.AddComponent<NeutralSkillFx>();fx.layer=Node("FX layer",root.transform);
            fx.ballotPrefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Effects/SkillBallotPaper.prefab").GetComponent<SkillFxGraphic>();
            fx.sparkPrefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Effects/SkillComicSpark.prefab").GetComponent<SkillFxGraphic>();
            fx.ringPrefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Effects/SkillVoteImpact.prefab").GetComponent<SkillFxGraphic>();
            fx.burstPrefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Effects/SkillComicBurst.prefab").GetComponent<SkillFxBurst>();
            rng=JsonUtility.ToJson(UnityEngine.Random.state);
            Expect(fx.TryPlay(turns.voteEffects.Local.Last())&&audio.LastCue=="Snooper","Activation sound does not start with the real skill animation.");
            fx.EvaluateAt(.3f);Expect(audio.LastCue=="PaperThrow","Paper launch does not have its synchronized whoosh.");
            count=audio.PlayedCues;fx.EvaluateAt(.6f);Expect(audio.PlayedCues==count,"A paper makes repeated sounds every animation frame.");
            fx.EvaluateAt(.95f);Expect(audio.LastCue=="PaperLand","The vote arrival does not sound when it lands.");
            Expect(rng==JsonUtility.ToJson(UnityEngine.Random.state),"Combined audio/FX consumes gameplay RNG.");
            fx.Stop();Pass("Production skill animation, paper launch and ballot arrival play their sounds at the matching motion events");
            Finish(true,"Skill audio assets, activation, privacy, snapshots, aura deduplication and tally checks passed.");
        }
        catch(Exception e){Finish(false,e.ToString());}
    }
    static void Finish(bool passed,string message)
    {
        SessionState.SetBool(Request,false);
        File.WriteAllText(Path.Combine(Directory.GetParent(Application.dataPath).FullName,"SkillAudioValidationResult.json"),JsonUtility.ToJson(new Report{passed=passed,assertions=assertions,message=message,cases=cases.ToArray()},true));
        Debug.Log("SKILL AUDIO RESULT: "+message);EditorApplication.Exit(passed?0:1);
    }
}

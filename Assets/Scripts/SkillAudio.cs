using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[Serializable]
public class SkillSoundEvent
{
    public int id;
    public string key;
}

// Cosmetic events only. Private actions have separate per-viewer sequences.
public class SkillAudio : MonoBehaviour
{
    public static SkillAudio Current { get; private set; }
    public SkillSoundBank bank;
    public AudioSource source;
    [Range(0,1)] public float masterVolume=.7f;
    [Range(2,12)] public int maxVoices=8;
    public bool soundEnabled=true;
    public int PlayedCues { get; private set; }
    public string LastCue { get; private set; }
    private readonly List<SkillSoundEvent> p1=new List<SkillSoundEvent>(),p2=new List<SkillSoundEvent>();
    private readonly List<AudioSource> voices=new List<AudioSource>();
    private readonly Dictionary<string,float> lastPlayed=new Dictionary<string,float>();
    private readonly Dictionary<int,string> auraState=new Dictionary<int,string>();
    private readonly HashSet<string> tallies=new HashSet<string>();
    private int p1Sequence,p2Sequence,remoteSeen,nextVoice;
    private bool remoteInitialized;

    void Awake()
    {
        Current=this;
        if(source==null)source=gameObject.AddComponent<AudioSource>();
        voices.Add(source);
        for(int i=1;i<maxVoices;i++)voices.Add(gameObject.AddComponent<AudioSource>());
        foreach(var voice in voices)
        {
            voice.playOnAwake=false;voice.loop=false;voice.spatialBlend=0;
            voice.dopplerLevel=0;voice.priority=100;voice.pitch=1;voice.volume=1;
            voice.outputAudioMixerGroup=source.outputAudioMixerGroup;
        }
    }
    void OnDestroy(){if(Current==this)Current=null;}
    void OnDisable(){foreach(var voice in voices)if(voice!=null)voice.Stop();}
    public static string Key(CardInstance card) => card!=null&&card.cardData!=null&&card.cardData.cardSkill!=null?
        card.cardData.cardSkill.GetType().Name.Replace("Skill_",""):null;
    public static void Emit(CardInstance card,DefaultOwner visibility=DefaultOwner.Neutral)
    {var key=Key(card);if(key!=null)Emit(key,visibility);}
    public static void Emit(string key,DefaultOwner visibility=DefaultOwner.Neutral)
    {if(Current!=null&&Current.isActiveAndEnabled&&!OnlineSession.Guest)Current.Publish(key,visibility);}
    private void Publish(string key,DefaultOwner visibility)
    {
        if(bank==null||bank.Find(key)==null)return;
        void Push(List<SkillSoundEvent> list,ref int sequence)
        {list.Add(new SkillSoundEvent{id=++sequence,key=key});if(list.Count>32)list.RemoveAt(0);}
        if(visibility!=DefaultOwner.Player2)Push(p1,ref p1Sequence);
        if(visibility!=DefaultOwner.Player1)Push(p2,ref p2Sequence);
        if(visibility==DefaultOwner.Neutral||visibility==CardOwnershipView.Perspective)PlayCue(key);
    }
    public SkillSoundEvent[] For(DefaultOwner viewer) => (viewer==DefaultOwner.Player1?p1:p2).ToArray();
    public void ApplyRemote(SkillSoundEvent[] events)
    {
        var rows=(events??Array.Empty<SkillSoundEvent>()).Where(e=>e!=null&&e.id>0).OrderBy(e=>e.id).Take(32).ToArray();
        // Joining/reconnecting must not play the complete sound history.
        if(!remoteInitialized){remoteInitialized=true;remoteSeen=rows.LastOrDefault()?.id??0;return;}
        foreach(var row in rows)if(row.id>remoteSeen){remoteSeen=row.id;PlayCue(row.key);}
    }
    public bool PlayCue(string key)
    {
        if(!isActiveAndEnabled||!soundEnabled||masterVolume<=0||bank==null||key==null)return false;
        var cue=bank.Find(key);if(cue==null||cue.clip==null||cue.volume<=0)return false;
        // Merge simultaneous impacts instead of stacking identical loud sounds.
        if(lastPlayed.TryGetValue(key,out var at)&&Time.unscaledTime-at<.045f)return false;
        lastPlayed[key]=Time.unscaledTime;
        var voice=voices.FirstOrDefault(v=>v!=null&&!v.isPlaying);
        if(voice==null&&voices.Count>0)voice=voices[nextVoice++%voices.Count];
        if(voice==null)return false;
        voice.Stop();voice.clip=cue.clip;voice.volume=masterVolume*cue.volume;voice.pitch=1;voice.Play();
        PlayedCues++;LastCue=key;return true;
    }
    public bool PlayTally(VoteEffectRecord record)
    {
        var turns=TurnManager.instance;
        if(record==null||!record.tally||turns==null||!turns.isTallyingVotes||turns.currentTurn!=TurnState.SacrificePhase||bank==null||bank.Find(record.kind)==null)return false;
        string token=CardOwnershipView.Perspective+":"+record.round+":"+record.id+":"+record.kind;
        if(!tallies.Add(token))return false;
        if(tallies.Count>256){tallies.Clear();tallies.Add(token);}
        bool unchanged=record.kind!="Controller"&&record.kind!="Sacrifier"&&
            !(record.changes??Array.Empty<VoteChange>()).Any(c=>c.before!=c.after);
        return PlayCue(unchanged?"NoEffect":record.kind=="Gambler"&&record.changes.Any(c=>c.after<c.before)?"GamblerLoss":record.kind);
    }
    public void RefreshAuraSounds(TurnManager turns)
    {
        if(OnlineSession.Guest||turns==null)return;
        var active=new HashSet<int>();
        foreach(var card in turns.allSeats.Select(s=>s!=null&&s.cardPos!=null?s.cardPos.GetComponentInChildren<CardInstance>():null).Where(c=>turns.IsLivingBoardCard(c)&&!c.isFaceDown&&!c.isSilenced))
        {
            var skill=card.cardData.cardSkill;
            if(!(skill is Skill_Preacher)&&!(skill is Skill_Elder)&&!(skill is Skill_Saboteur)&&!(skill is Skill_Totem))continue;
            int id=card.GetInstanceID();active.Add(id);
            int seat=turns.GetSeatIndex(card.GetComponent<CardInteraction>());
            string signature=skill is Skill_Totem?"active":string.Join(",",new[]{turns.HorizontalNeighbor(seat,-1),turns.HorizontalNeighbor(seat,1)}
                .Where(i=>i>=0).Select(turns.GetBoardCard).Where(c=>turns.IsLivingBoardCard(c)).Select(c=>c.GetInstanceID()).OrderBy(i=>i));
            if((!auraState.TryGetValue(id,out var previous)||previous!=signature)&&signature.Length>0)Emit(card);
            auraState[id]=signature;
        }
        foreach(int id in auraState.Keys.Where(id=>!active.Contains(id)).ToArray())auraState.Remove(id);
    }
}

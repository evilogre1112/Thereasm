using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// Presentation only. Uses the public tally's real results, never the skill RNG or gameplay state.
[DefaultExecutionOrder(800)]
public class NeutralSkillFx : MonoBehaviour
{
    public RectTransform layer;
    public SkillFxGraphic ballotPrefab, sparkPrefab, ringPrefab;
    public SkillFxBurst burstPrefab;
    public Color traderInk = new Color(.71f,.4f,.09f), jesterInk = new Color(.67f,.08f,.2f);
    public Color snooperInk = new Color(.15f,.65f,.57f), gamblerInk = new Color(.86f,.57f,.15f), gamblerLossInk = new Color(.78f,.15f,.2f);
    [Range(1.05f,1.22f)] public float traderPunch = 1.14f, jesterPunch = 1.18f;
    [Range(1.05f,1.22f)] public float snooperPunch = 1.12f, gamblerPunch = 1.18f;
    [Range(24,80)] public int maxParticles = 48;
    public bool IsPlaying { get; private set; }
    public int ActiveParticles => motions.Count(m => !m.landed && m.graphic != null && m.graphic.gameObject.activeSelf);
    private class Motion
    {
        public SkillFxGraphic graphic;
        public Vector2 start, end, size;
        public float delay, duration, arc, spin;
        public bool landed, spark, retract, started;
        public Action arrival;
    }
    private readonly List<SkillFxGraphic> pool = new List<SkillFxGraphic>();
    private readonly List<Motion> motions = new List<Motion>();
    private RectTransform actor;
    private Vector3 actorScale;
    private Quaternion actorRotation;
    private SkillFxBurst burst;
    private Vector2 burstPosition;
    private VoteEffectRecord current;
    private float elapsed;
    private bool jester;
    private Color effectInk;
    private Camera CameraForLayer
    {
        get { var canvas=layer!=null?layer.GetComponentInParent<Canvas>()?.rootCanvas:null;
            return canvas!=null&&canvas.renderMode!=RenderMode.ScreenSpaceOverlay?canvas.worldCamera:null; }
    }
    private CardInstance BoardCard(int number)
    {
        var t=TurnManager.instance;if(t==null)return null;
        foreach(var seat in t.allSeats)
        {var card=seat!=null&&seat.cardPos!=null?seat.cardPos.GetComponentInChildren<CardInstance>():null;
            if(card!=null&&card.cardData!=null&&card.cardData.cardNumber==number&&t.IsLivingBoardCard(card))return card;}
        return null;
    }
    private Vector2 Point(RectTransform rect, bool top = false)
    {
        Vector3 world=rect.TransformPoint(top?new Vector3(rect.rect.center.x,rect.rect.yMax):new Vector3(rect.rect.center.x,rect.rect.center.y));
        var screen=RectTransformUtility.WorldToScreenPoint(CameraForLayer,world);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(layer,screen,CameraForLayer,out var result);return result;
    }
    private Vector2 BadgePoint(CardInstance card)
    {var hud=card.GetComponent<CardVotePresentation>();return Point(hud!=null&&hud.voteMotion!=null?hud.voteMotion:(RectTransform)card.transform);}
    private void Count(CardInstance card,int value)
    {if(card!=null&&TurnManager.instance!=null&&TurnManager.instance.IsLivingBoardCard(card))card.GetComponent<CardVotePresentation>()?.PresentCount(value);}
    private SkillFxGraphic Borrow(SkillFxGraphic prefab)
    {
        if(prefab==null)return null;
        var graphic=pool.Find(p=>p!=null&&!p.gameObject.activeSelf&&p.glyph==prefab.glyph&&!motions.Any(m=>m.graphic==p&&!m.landed));
        if(graphic==null)
        {if(pool.Count>=maxParticles)return null;graphic=Instantiate(prefab,layer);pool.Add(graphic);}
        graphic.gameObject.SetActive(true);graphic.rectTransform.localScale=Vector3.one;graphic.rectTransform.localRotation=Quaternion.identity;
        graphic.raycastTarget=false;graphic.Style(jester,effectInk);return graphic;
    }
    private void Fly(Vector2 start,Vector2 end,float delay,float duration,float arc,float spin,Vector2 size,Action arrival=null,bool spark=false,bool ring=false,bool retract=false)
    {
        var graphic=Borrow(ring?ringPrefab:spark?sparkPrefab:ballotPrefab);
        if(graphic==null){arrival?.Invoke();return;}
        graphic.gameObject.SetActive(false);
        motions.Add(new Motion{graphic=graphic,start=start,end=end,delay=delay,duration=duration,arc=arc,spin=spin,size=size,arrival=arrival,spark=spark||ring,retract=retract});
    }
    private void Impact(CardInstance card,string kind)
    {
        if(card==null)return;
        SkillAudio.Current?.PlayCue("PaperLand");
        card.GetComponent<CardVotePresentation>()?.ActivateEffect(kind);
        Vector2 point=BadgePoint(card);
        Fly(point,point,elapsed,.25f,0,0,new Vector2(52,52),spark:true,ring:true);
        for(int i=0;i<4;i++)
        {float angle=(i*90+25)*Mathf.Deg2Rad;Fly(point,point+new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*30,elapsed,.27f,8,i*35,new Vector2(10,14),spark:true);}
    }
    public bool TryPlay(VoteEffectRecord record)
    {
        var t=TurnManager.instance;
        if(!isActiveAndEnabled||record==null||!record.tally||t==null||!t.isTallyingVotes||
            t.currentTurn!=TurnState.SacrificePhase||layer==null||ballotPrefab==null||burstPrefab==null)return false;
        var source=BoardCard(record.sourceNumber);
        if(source==null||!MatchesSkill(record.kind,source.cardData.cardSkill))return false;
        Stop();current=record;jester=record.kind=="Jester";elapsed=0;
        effectInk=record.kind=="Snooper"?snooperInk:record.kind=="Gambler"?
            ((record.changes??new VoteChange[0]).Any(c=>c.after<c.before)?gamblerLossInk:gamblerInk):jester?jesterInk:traderInk;
        var input=source.GetComponent<CardInteraction>();actor=(input!=null&&input.visualChild!=null?input.visualChild:source.transform) as RectTransform;
        actorScale=actor.localScale;actorRotation=actor.localRotation;
        Vector2 center=Point(actor),top=Point(actor,true);
        if(burst==null)burst=Instantiate(burstPrefab,layer);
        burst.gameObject.SetActive(true);burst.graphic.Style(jester,effectInk);
        burstPosition=new Vector2(Mathf.Clamp(top.x,layer.rect.xMin+88,layer.rect.xMax-88),Mathf.Min(top.y+76,layer.rect.yMax-120));
        ((RectTransform)burst.transform).anchoredPosition=burstPosition;
        if(Localizer.Catalog!=null&&Localizer.Catalog.readableFont!=null)burst.title.font=Localizer.Catalog.readableFont;
        if(burst.formula!=null){burst.formula.text="";burst.formula.gameObject.SetActive(false);}
        if(jester)BuildJester(record,center,top);
        else if(record.kind=="Trader")BuildTrader(record,center);
        else BuildTargetedBallots(record,source,top);
        IsPlaying=true;SkillAudio.Current?.PlayTally(record);EvaluateAt(0);return true;
    }
    private static bool MatchesSkill(string kind,CardSkill skill) =>
        kind=="Trader"&&skill is Skill_Trader||kind=="Jester"&&skill is Skill_Jester||
        kind=="Snooper"&&skill is Skill_Snooper||kind=="Gambler"&&skill is Skill_Gambler;
    private void BuildTargetedBallots(VoteEffectRecord record,CardInstance source,Vector2 top)
    {
        bool snooper=record.kind=="Snooper";int launched=0;
        foreach(var change in record.changes??new VoteChange[0])
        {
            var target=BoardCard(change.number);if(target==null)continue;
            Count(target,change.before);int landed=change.before;
            int direction=change.after>change.before?1:-1;
            // These two skills change one ballot per resolution. Use only the recorded delta.
            for(int vote=0;vote<Mathf.Min(3,Mathf.Abs(change.after-change.before));vote++)
            {
                int index=launched++;
                Vector2 start=direction>0?top+new Vector2(index*7,-18):BadgePoint(target);
                Vector2 end=direction>0?BadgePoint(target):top+new Vector2(index*7,-18);
                Fly(start,end,.25f+index*.1f,.62f,snooper?48:100,
                    snooper?35:direction>0?220:-220,new Vector2(34,43),()=>
                    {
                        Count(target,landed+=direction);Impact(target,record.kind);
                        if(direction<0)Impact(source,record.kind);
                    },retract:direction<0);
            }
        }
        burst.title.text=record.kind+(launched>0?"!":"…");
        if(launched==0)return;
        for(int i=0;i<(snooper?3:6);i++)
        {
            float angle=(25+i*57)*Mathf.Deg2Rad;
            Fly(top,top+new Vector2(Mathf.Cos(angle)*44,Mathf.Sin(angle)*32),.12f+i*.03f,.4f,6,i*35,new Vector2(9,13),spark:true);
        }
    }
    private void BuildJester(VoteEffectRecord record,Vector2 center,Vector2 top)
    {
        int launched=0;
        foreach(var change in record.changes??new VoteChange[0])
        {
            var target=BoardCard(change.number);if(target==null)continue;
            Count(target,change.before);int landed=change.before;
            for(int vote=0;vote<Mathf.Min(3,Mathf.Max(0,change.after-change.before));vote++)
            {
                int index=launched++;Vector2 start=top+new Vector2((index-1)*9,-18);
                Fly(start,BadgePoint(target),.24f+index*.11f,.62f,80+(index%2)*24,index%2==0?165:-145,new Vector2(31,39),()=>{Count(target,++landed);Impact(target,"Jester");});
            }
        }
        burst.title.text=launched>0?"HA HA!":"…";
        for(int i=0;i<(launched>0?7:2);i++)
        {float angle=(35+i*43)*Mathf.Deg2Rad;Fly(top,top+new Vector2(Mathf.Cos(angle)*52,Mathf.Sin(angle)*36),.12f+i*.025f,.42f,8,i*40,new Vector2(8,14),spark:true);}
    }
    private void BuildTrader(VoteEffectRecord record,Vector2 center)
    {
        var group=record.participants!=null&&record.participants.Length>0?record.participants:record.changes??new VoteChange[0];
        foreach(var member in group)Count(BoardCard(member.number),member.before);
        var donor=group.OrderByDescending(c=>c.before).FirstOrDefault();int highest=donor?.before??0;
        burst.title.text=Localizer.T(highest>0?"DEAL!":"NO BALLOTS");
        var donorCard=donor!=null?BoardCard(donor.number):null;
        if(donorCard!=null)
            for(int i=0;i<Mathf.Min(5,highest);i++)
                Fly(BadgePoint(donorCard)+new Vector2(i*3,0),center+new Vector2((i-2)*3,i*2),.13f+i*.035f,.38f,42,12-i*5,new Vector2(27,34));
        for(int i=0;i<group.Length;i++)
        {
            var change=group[i];var target=BoardCard(change.number);if(target==null)continue;
            if(change.after>0)
                Fly(center,BadgePoint(target),.68f+i*.065f,.43f,48,12-i*12,new Vector2(32,40),()=>{Count(target,change.after);Impact(target,"Trader");});
            else Fly(center,BadgePoint(target),.7f+i*.06f,.25f,12,0,new Vector2(3,3),()=>{Count(target,0);Impact(target,"Trader");},spark:true);
        }
        int remainder=record.participants!=null&&record.participants.Length>0?highest-group[0].after*group.Length:0;
        for(int i=0;i<Mathf.Min(3,remainder);i++)Fly(center,center+new Vector2(-30+i*25,-48),.84f+i*.045f,.38f,8,i*55,new Vector2(9,13),spark:true);
    }
    public void EvaluateAt(float seconds)
    {
        if(!IsPlaying)return;elapsed=Mathf.Max(0,seconds);
        if(actor==null){Stop();return;}
        float punch=current.kind=="Jester"?jesterPunch:current.kind=="Snooper"?snooperPunch:current.kind=="Gambler"?gamblerPunch:traderPunch;
        float beat=Mathf.Clamp01(elapsed/.6f);float bump=Mathf.Sin(beat*Mathf.PI)*Mathf.Exp(-beat*1.4f);
        float amplitude=(current.changes?.Length??0)>0?1:.5f;
        actor.localScale=actorScale*(1+(punch-1)*bump*amplitude*1.7f);
        actor.localRotation=actorRotation*Quaternion.Euler(0,0,(jester?7:-3)*Mathf.Sin(beat*Mathf.PI*2)*(1-beat));
        float appear=Mathf.Clamp01(elapsed/.14f),fade=Mathf.Clamp01((current.PresentationSeconds-elapsed)/.26f);
        burst.group.alpha=appear*fade;burst.transform.localScale=Vector3.one*(.88f+.12f*appear+Mathf.Sin(appear*Mathf.PI)*.12f);
        for(int i=0;i<motions.Count;i++)
        {
            var m=motions[i];if(m.landed||m.graphic==null)continue;float t=(elapsed-m.delay)/m.duration;
            if(t<0)continue;
            if(!m.started){m.started=true;if(!m.spark)SkillAudio.Current?.PlayCue("PaperThrow");}
            if(t>=1)
            {m.landed=true;m.graphic.gameObject.SetActive(false);m.arrival?.Invoke();continue;}
            m.graphic.gameObject.SetActive(true);var rect=m.graphic.rectTransform;
            float ease=m.spark?t:t*t*(3-2*t);
            rect.anchoredPosition=Vector2.Lerp(m.start,m.end,ease)+Vector2.up*(Mathf.Sin(t*Mathf.PI)*m.arc);
            rect.localRotation=Quaternion.Euler(0,0,m.spin*(m.spark?t:t-.5f));
            rect.sizeDelta=m.size*(m.spark?Mathf.Lerp(.35f,1.2f,t):1);
            m.graphic.SetOpacity(m.spark?1-t:Mathf.Min(1,t*9)*(m.retract?Mathf.Clamp01((1-t)*4):1));
        }
        if(elapsed>=current.PresentationSeconds)Stop();
    }
    void LateUpdate()
    {
        if(!IsPlaying||GameStartUI.IsBlockingInput)return;
        if(TurnManager.instance==null||!TurnManager.instance.isTallyingVotes){Stop();return;}
        EvaluateAt(elapsed+Time.unscaledDeltaTime);
    }
    public void Stop()
    {
        if(IsPlaying&&current!=null&&TurnManager.instance!=null&&TurnManager.instance.isTallyingVotes)
            foreach(var change in current.participants!=null&&current.participants.Length>0?current.participants:current.changes??new VoteChange[0])
                Count(BoardCard(change.number),change.after);
        if(actor!=null){actor.localScale=actorScale;actor.localRotation=actorRotation;}
        foreach(var graphic in pool)if(graphic!=null)graphic.gameObject.SetActive(false);
        if(burst!=null)burst.gameObject.SetActive(false);
        motions.Clear();actor=null;IsPlaying=false;
    }
    void OnDisable(){Stop();}
}

using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

[DefaultExecutionOrder(700)]
public class VoteEffectsView : MonoBehaviour
{
    public static VoteEffectsView Current { get; private set; }
    public GameObject panel;
    public Transform rows;
    public VoteEffectRow rowPrefab;
    public Text heading, subtitle, empty, reviewLabel;
    public ScrollRect scroll;
    public Button reviewButton;
    [Header("Neutral ballot skills — scene-authored VFX")]
    public NeutralSkillFx skillFx;
    private readonly List<VoteEffectRow> rendered = new List<VoteEffectRow>();
    private readonly Queue<VoteEffectRecord> pending = new Queue<VoteEffectRecord>();
    private int seen, revision = -1, renderedId = -1;
    private DefaultOwner viewer;
    private bool initialized, wasTally, manual;
    private float nextEffect;
    private int filterNumber;
    private string detailKind;
    void Awake() { Current = this; if(panel!=null)panel.SetActive(false); }
    void OnDestroy() { if(Current==this)Current=null; }
    public void Toggle()
    {
        if(panel==null)return;
        if(panel.activeSelf) { Close();return; }
        manual=true;filterNumber=0;detailKind=null;panel.SetActive(true);Rebuild();
    }
    public void Close() { manual=false; if(panel!=null)panel.SetActive(false); }
    public void ShowCard(CardInstance card,string kind)
    { manual=true;filterNumber=card.cardData.cardNumber;detailKind=kind;panel.SetActive(true);Rebuild(); }
    public void Replay()
    {
        skillFx?.Stop();
        pending.Clear();var feed=VoteEffectFeed.Current;
        if(feed!=null)foreach(var row in feed.Local.Where(r=>r.tally))pending.Enqueue(row);
        nextEffect=0;filterNumber=0;detailKind=null;manual=true;panel.SetActive(true);Rebuild();
    }
    private void Rebuild()
    {
        var feed=VoteEffectFeed.Current;
        if(feed==null||rowPrefab==null)return;
        foreach(var row in rendered)if(row!=null){row.gameObject.SetActive(false);Destroy(row.gameObject);}rendered.Clear();
        bool liveVoting=TurnManager.instance!=null&&TurnManager.instance.currentTurn==TurnState.SacrificePhase&&!TurnManager.instance.isTallyingVotes;
        var records=feed.LocalRecords.Where(r=>filterNumber==0 || r.sourceNumber==filterNumber || r.changes.Any(c=>c.number==filterNumber)).ToArray();
        if(filterNumber==0)records=records.Where(r=>r.tally).ToArray();
        else if(liveVoting&&detailKind=="Votes")records=new VoteEffectRecord[0];
        else if(liveVoting&&(detailKind=="Gambler"||detailKind=="Snooper"))records=records.Where(r=>!r.tally&&r.kind==detailKind).ToArray();
        foreach(var record in records)
        {var row=Instantiate(rowPrefab,rows);row.gameObject.SetActive(true);row.Bind(record,rendered.Count);rendered.Add(row);}
        if(heading!=null)heading.text=Localizer.T(filterNumber==0?(TurnManager.instance.isTallyingVotes?"VOTE RESOLUTION":"LAST TALLY"):"CARD EFFECT DETAILS");
        if(subtitle!=null)subtitle.text=Localizer.T(detailKind=="Detective" ? "Suspicion is not a vote. Detective uses it when comparing neighbours; it does not directly increase this card's ballots." :
            detailKind=="Gambler" ? "Only your wager is shown before the tally. Counter-bets are resolved together when both players finish voting." :
            detailKind=="Snooper" ? "Selections stay private before the tally. Snooper's actual vote target is public when its effect resolves." :
            TurnManager.instance!=null&&TurnManager.instance.currentTurn==TurnState.SacrificePhase&&!TurnManager.instance.isTallyingVotes ?
            "During voting, only your own ballots are visible. Both sides and Neutral effects are revealed at the tally." :
            "Original ballots → Neutral effects → Verdict. Click a vote badge to inspect that card.");
        if(empty!=null){empty.gameObject.SetActive(records.Length==0);empty.text=Localizer.T("No public vote changes yet. Cast your votes; the tally will explain every Neutral effect here.");}
        renderedId=feed.LocalRecords.LastOrDefault()?.id??0;revision=Localizer.Revision;
    }
    private void Play(VoteEffectRecord record)
    {
        bool animated=skillFx!=null&&skillFx.TryPlay(record);
        if(!animated&&record.tally)SkillAudio.Current?.PlayTally(record);
        var cards=Object.FindObjectsByType<CardInstance>(FindObjectsInactive.Include,FindObjectsSortMode.None);
        bool verdict=record.kind=="Controller"||record.kind=="Sacrifier";
        foreach(var card in cards)
        {
            if(card.cardData==null)continue;
            var presentation=card.GetComponent<CardVotePresentation>();if(presentation==null)continue;
            if(card.cardData.cardNumber==record.sourceNumber)presentation.ActivateEffect(record.kind);
            foreach(var delta in record.changes)
                if(card.cardData.cardNumber==delta.number)
                {
                    if(record.tally&&!verdict&&!animated)presentation.PresentCount(delta.after);
                    else if(!record.tally||verdict)presentation.ActivateEffect(record.kind);
                }
        }
        foreach(var row in rendered)if(row!=null)row.Emphasize(row.RecordId==record.id);
        if(panel.activeSelf&&!manual&&rendered.Count>1)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)rows);
            var selected=rendered.Find(r=>r.RecordId==record.id);
            float range=((RectTransform)rows).rect.height-scroll.viewport.rect.height;
            if(selected!=null)scroll.verticalNormalizedPosition=range>0?Mathf.Clamp01(1-Mathf.Abs(selected.GetComponent<RectTransform>().anchoredPosition.y)/range):1;
        }
    }
    void LateUpdate()
    {
        var feed=VoteEffectFeed.Current;var turns=TurnManager.instance;
        if(feed==null||turns==null||GameStartUI.IsBlockingInput)return;
        var local=feed.LocalRecords;
        int lastId=local.Count>0?local[local.Count-1].id:0;
        var perspective=CardOwnershipView.Perspective;
        if(initialized&&viewer!=perspective){pending.Clear();seen=lastId;renderedId=-1;}
        initialized=true;viewer=perspective;
        bool tally=turns.isTallyingVotes;
        if(tally&&!wasTally){pending.Clear();nextEffect=0;manual=false;filterNumber=0;detailKind=null;panel.SetActive(true);renderedId=-1;}
        if(!tally&&wasTally&&!manual)panel.SetActive(false);
        wasTally=tally;
        if(panel.activeSelf)panel.transform.SetAsLastSibling();
        if(lastId>seen)foreach(var record in local)if(record.id>seen){pending.Enqueue(record);seen=record.id;}
        if(panel.activeSelf&&(revision!=Localizer.Revision||renderedId!=lastId))Rebuild();
        if(pending.Count>0&&Time.unscaledTime>=nextEffect&&(skillFx==null||!skillFx.IsPlaying))
        {var record=pending.Dequeue();Play(record);nextEffect=Time.unscaledTime+record.PresentationSeconds;}
        if(reviewLabel!=null)reviewLabel.text=Localizer.T("Vote details");
        if(reviewButton!=null)reviewButton.interactable=turns.currentTurn!=TurnState.SetupPhase;
    }
}

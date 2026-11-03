using UnityEngine;
using UnityEngine.UI;

// Saved card HUD. The existing numeric voteText remains the network/logic state.
public class CardVotePresentation : MonoBehaviour
{
    public GameObject voteBadge, suspicionBadge;
    public Text voteCount, voteWord, suspicionCount;
    public RectTransform voteMotion, suspicionMotion, snooperMotion, gamblerMotion;
    public CanvasGroup activationFrame;
    public Image suspicionIcon;
    [Header("Status rail — board marks sit outside the artwork")]
    public float statusIconSize = 48, inCardIconSize = 40, counterWidth = 40, statusGap = 4, statusOffset = 6, voteOffset = 64;
    [Header("Prefab-first positioning")]
    [Tooltip("Off = use the positions/anchors/sizes authored in CardUI prefab. Turn on only for the old dynamic status rail layout.")]
    [SerializeField] private bool autoLayoutStatusRail = false;
    public int suspicionFontSize = 20;
    private Vector3 votePrefabScale, suspicionPrefabScale, snooperPrefabScale, gamblerPrefabScale, activationPrefabScale;
    private void Awake()
    {
        votePrefabScale = voteMotion != null ? voteMotion.localScale : Vector3.one;
        suspicionPrefabScale = suspicionMotion != null ? suspicionMotion.localScale : Vector3.one;
        snooperPrefabScale = snooperMotion != null ? snooperMotion.localScale : Vector3.one;
        gamblerPrefabScale = gamblerMotion != null ? gamblerMotion.localScale : Vector3.one;
        activationPrefabScale = activationFrame != null ? activationFrame.transform.localScale : Vector3.one;
    }
    private CardInstance card;
    private int lastSuspicion, lastCount = -1, presentationCount = -1;
    private int languageRevision = -1;
    private int layoutMask = -1;
    private bool layoutOutside;
    private bool lastSnooper, lastGambler;
    private int pulseDelta;
    private float voteAt = -10, suspicionAt = -10, snooperAt = -10, gamblerAt = -10, activationAt = -10;
    public int DisplayedVotes { get; private set; }
    public void PresentCount(int value) { pulseDelta = value-DisplayedVotes; presentationCount = value; voteAt = Time.unscaledTime; }
    public void ClearPresentation() { presentationCount = -1; }
    public void ActivateEffect(string kind)
    {
        activationAt = Time.unscaledTime;
        if (kind == "Detective") suspicionAt = activationAt;
        if (kind == "Snooper") snooperAt = activationAt;
        if (kind == "Gambler") gamblerAt = activationAt;
    }
    public void RefreshSuspicion()
    {
        if (card == null) card = GetComponent<CardInstance>();
        if (card != null && card.bonusSuspicion != lastSuspicion)
        { lastSuspicion = card.bonusSuspicion; suspicionAt = Time.unscaledTime; if(suspicionCount!=null)suspicionCount.text="+"+lastSuspicion; }
    }
    private static void Bounce(RectTransform rect, float at, Vector3 prefabScale)
    {
        if (rect == null) return;
        float t = (Time.unscaledTime-at)/.7f;
        rect.localScale = prefabScale * (t >= 0 && t < 1 ? 1 + Mathf.Sin(t*Mathf.PI*2)*.13f*(1-t) : 1);
    }
    private void LayoutRail(bool outside, bool suspect, bool snooper, bool gambler)
    {
        // By default, the prefab owns all badge geometry. The rail is optional
        // for projects still relying on an automatically rearranged status row.
        if (!autoLayoutStatusRail) return;
        int mask=(suspect?1:0)+(snooper?2:0)+(gambler?4:0);
        if(mask==layoutMask&&outside==layoutOutside)return;
        layoutMask=mask;layoutOutside=outside;
        float size=outside?statusIconSize:inCardIconSize;
        float counter=counterWidth*size/statusIconSize;
        void Position(RectTransform rect, float x, float y, float width)
        {
            if(rect==null)return;
            rect.anchorMin=rect.anchorMax=outside?new Vector2(.5f,0):new Vector2(1,1);
            rect.pivot=outside?new Vector2(.5f,1):Vector2.one;
            rect.sizeDelta=new Vector2(width,size);rect.anchoredPosition=new Vector2(x,y);
        }
        if(outside)
        {
            int count=(suspect?1:0)+(snooper?1:0)+(gambler?1:0);
            float total=(suspect?size+counter:0)+(snooper?size:0)+(gambler?size:0)+Mathf.Max(0,count-1)*statusGap;
            float cursor=-total*.5f;
            if(suspect){Position(suspicionMotion,cursor+(size+counter)*.5f,-statusOffset,size+counter);cursor+=size+counter+statusGap;}
            if(snooper){Position(snooperMotion,cursor+size*.5f,-statusOffset,size);cursor+=size+statusGap;}
            if(gambler)Position(gamblerMotion,cursor+size*.5f,-statusOffset,size);
        }
        else
        {
            Position(suspicionMotion,-3,-22,size+counter);
            if(suspicionMotion!=null){suspicionMotion.anchorMin=suspicionMotion.anchorMax=new Vector2(0,1);suspicionMotion.pivot=new Vector2(0,1);suspicionMotion.anchoredPosition=new Vector2(3,-22);}
            Position(snooperMotion,-3,-80,size);Position(gamblerMotion,-3,-126,size);
        }
        if(suspicionIcon!=null)suspicionIcon.rectTransform.sizeDelta=new Vector2(size,size);
        if(suspicionCount!=null)
        {
            suspicionCount.rectTransform.anchoredPosition=new Vector2(size,0);
            suspicionCount.rectTransform.sizeDelta=new Vector2(counter-2,size);
            suspicionCount.fontSize=Mathf.RoundToInt(suspicionFontSize*size/statusIconSize);
            suspicionCount.resizeTextMaxSize=suspicionCount.fontSize;
        }
        // Badge geometry is controlled by the CardUI prefab.
    }
    void LateUpdate() { Refresh(); }
    public void Refresh()
    {
        if (card == null) card = GetComponent<CardInstance>();
        if (card == null || card.cardData == null) return;
        var input = GetComponent<CardInteraction>();var turns = TurnManager.instance;
        bool onBoard = input != null && input.isPlaced && !card.isDead && !card.isFaceDown;
        bool tally = turns != null && turns.isTallyingVotes;
        int actual = 0;
        bool hasVotes = card.voteText != null && card.voteText.gameObject.activeSelf && int.TryParse(card.voteText.text,out actual);
        // Disable only the old glyph renderer, never its state object or color.
        if (card.voteText != null) card.voteText.enabled = false;
        if (!tally) presentationCount = -1;
        int count = tally && presentationCount >= 0 ? presentationCount : actual;
        bool visible = onBoard && (tally && presentationCount >= 0 || hasVotes);
        if (voteBadge != null) voteBadge.SetActive(visible);
        if (count != lastCount) { voteAt = Time.unscaledTime;lastCount = count; if(voteCount!=null)voteCount.text=count.ToString(); }
        DisplayedVotes = count;
        if (voteCount != null) voteCount.color = Time.unscaledTime-voteAt < .85f && pulseDelta != 0 ?
            (pulseDelta > 0 ? new Color(.57f,.87f,.74f) : new Color(.94f,.68f,.56f)) : new Color(.96f,.88f,.7f);
        if (voteWord != null && languageRevision != Localizer.Revision) { voteWord.text = Localizer.T("votes");languageRevision=Localizer.Revision; }
        RefreshSuspicion();
        bool showSuspicion = !card.isFaceDown && card.bonusSuspicion > 0;
        if (suspicionBadge != null) suspicionBadge.SetActive(showSuspicion);
        bool snooper = card.snooperMark != null && card.snooperMark.activeSelf;
        bool gambler = card.gamblerMark != null && card.gamblerMark.activeSelf;
        LayoutRail(onBoard && !input.isInspecting,showSuspicion,snooper,gambler);
        if (snooper && !lastSnooper) snooperAt = Time.unscaledTime;
        if (gambler && !lastGambler) gamblerAt = Time.unscaledTime;
        lastSnooper = snooper;lastGambler = gambler;
        Bounce(voteMotion,voteAt,votePrefabScale);
        Bounce(suspicionMotion,suspicionAt,suspicionPrefabScale);
        Bounce(snooperMotion,snooperAt,snooperPrefabScale);
        Bounce(gamblerMotion,gamblerAt,gamblerPrefabScale);
        if (activationFrame != null)
        {
            float t = (Time.unscaledTime-activationAt)/.95f;
            activationFrame.alpha = onBoard && t >= 0 && t < 1 ? Mathf.Sin(t*Mathf.PI)*.9f : 0;
            activationFrame.transform.localScale = activationPrefabScale*(1+Mathf.Max(0,1-t)*.018f);
        }
    }
}

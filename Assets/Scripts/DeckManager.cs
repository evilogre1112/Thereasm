using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.UI;
using UnityEngine.EventSystems; 
using TMPro;

public class DeckManager : MonoBehaviour
{
    public static DeckManager instance;
    [Header("UI theme — direct asset reference")]
    public GameUITheme uiTheme;

    [Header("Cấu hình khuôn mẫu")]
    public GameObject cardPrefab;
    [Tooltip("Dedicated editable prefab for neutral draft offers. Its panel layout is preserved at runtime.")]
    public GameObject draftCardPrefab;

    [Header("Khu vực hiển thị")]
    public GameObject p1Panel;    
    public GameObject p2Panel;    
    public GameObject neutralPanel; 

    public Transform player1Hand => p1Panel != null ? p1Panel.transform : null;
    public Transform player2Hand => p2Panel != null ? p2Panel.transform : null;
    public Transform neutralHand => neutralPanel != null ? neutralPanel.transform : null ;

    [Header("Dữ liệu 16 lá bài gốc (KHÔNG BỎ NEUTRAL VÀO ĐÂY)")]
    public List<CardData> allCards;

    [Header("Hệ thống Draft Bài Neutral")]
    public GameObject draftPanel;       
    public Transform draftSlotLeft;     
    public Transform draftSlotRight;    
    
    [Tooltip("Kéo 8 lá bài Neutral (từ 17-24) vào đây")]
    public List<CardData> neutralCardPool; 
    
    private Dictionary<int, List<CardData>> draftPairs = new Dictionary<int, List<CardData>>();
    private List<int> draftKeys = new List<int>();
    private int currentDraftStep = 0;
     private bool isRoguelikeDraft = false;
     private GameObject leftCardObj;
    private GameObject rightCardObj;
    private bool isAnimatingPick = false; 
    public bool IsReady { get; private set; }
    [SerializeField] private bool useDockNavigation;
    public bool UseDockNavigation { get => useDockNavigation; set => useDockNavigation = value; }
    public bool IsPicking => isAnimatingPick;
    private bool onlineMirror;
    private int remoteDraftTotal;
    private CardData[] remoteDraftOptions = new CardData[0];
    private string remoteDraftSignature;
    public int DraftStep => currentDraftStep;
    public int DraftTotal => onlineMirror ? remoteDraftTotal : draftKeys.Count;
    public DefaultOwner DraftChooser => OnlineRules.DraftRole(DraftStep, DraftTotal);
    public bool CanPickDraft => !IsReady && !IsPicking && (!OnlineSession.Active ||
        OnlineRules.CanDraft(OnlineSession.LocalRole, DraftStep, DraftTotal, IsPicking) && (!OnlineSession.Guest || OnlineGameBridge.Instance != null &&
            OnlineGameBridge.Instance.HasSnapshot && !OnlineGameBridge.Instance.CommandPending));
    public CardData[] DraftOptions()
    {
        if (onlineMirror) return remoteDraftOptions;
        if (IsReady || currentDraftStep >= draftKeys.Count) return new CardData[0];
        return draftPairs[draftKeys[currentDraftStep]].ToArray();
    }
    public string DraftProgress => isRoguelikeDraft ? "Chọn 1 lá; các nhóm còn lại được chọn ngẫu nhiên" :
        DraftTotal == 0 ? "Đang đồng bộ bộ bài trung lập…" :
        $"Lượt {Mathf.Min(DraftStep + 1, DraftTotal)}/{DraftTotal} · Người chơi {(DraftChooser == DefaultOwner.Player1 ? 1 : 2)} chọn · " +
        (DraftChooser == DefaultOwner.Player1 ? "Cursermancer" : "Dân làng");

    [Header("Hệ thống Điều hướng (Arrows)")]
    [Tooltip("Kéo nút Mũi Tên Trái vào đây")]
    public Button leftArrowBtn;
    [Tooltip("Kéo nút Mũi Tên Phải vào đây")]
    public Button rightArrowBtn;
    [Tooltip("Kéo component TextMeshProUGUI hiển thị tên vào đây")]
    public TextMeshProUGUI handDescriptionText;
    [Tooltip("Kéo nút 'Mở Bài' vào đây (nút này chỉ hiện khi bài bị ẩn)")]
    public Button openHandBtn;

    private GameObject[] handPanels;
    public bool SuppressHandEntranceAnimation { get; private set; }
    public void BeginGuestHandSync() { SuppressHandEntranceAnimation = true; }
    public void FinishGuestHandSync()
    {
        if (handPanels != null)
            foreach (var panel in handPanels)
            {
                if (panel == null) continue;
                SortHand(panel.transform);
                var animator = panel.GetComponent<HandPanelAnimator>();
                if (animator != null) animator.StopAndSnap();
                else ArrangeFanLayout(panel.transform);
            }
        SuppressHandEntranceAnimation = false;
    }
    private string[] handDescriptions = { "YOUR HAND", "NEUTRAL HAND", "OPPONENT's HAND" };
    
    // Chỉ mục hiện tại (0=P1, 1=Neutral, 2=P2)
    private int currentHandIndex = 0; 
    public DefaultOwner SelectedHand => currentHandIndex==2?DefaultOwner.Player2:currentHandIndex==1?DefaultOwner.Neutral:DefaultOwner.Player1;

    [Header("Cấu hình Xòe Quạt (Fan Layout)")]
    public float cardSpacing = 80f; 
    public float curveAmount = 15f;  
    public float angleSpread = 45f;  
    [Tooltip("Show separate upright cards instead of an overlapping fan. Useful for guided practice.")]
    [SerializeField] private bool separateHandCards;
    [SerializeField, Min(0)] private float separatedCardGap = 24f;

    [Header("Cấu hình Animation Bung Bài")]
    public float drawDuration = 0.25f; 
    public float delayBetweenCards = 0.05f; 
    
    [Tooltip("Kéo file âm thanh chia bài vào đây!")]
    public AudioClip dealCardSFX; 

    void Awake() 
    {
        instance = this;
    }

    void Start()
    {
        allCards = allCards == null ? new List<CardData>() : new List<CardData>(allCards);
        allCards.RemoveAll(card => card == null);
        if (neutralCardPool != null) neutralCardPool = neutralCardPool.FindAll(card => card != null);
        if (neutralCardPool != null && neutralCardPool.Count > 0 && draftPanel != null &&
            draftSlotLeft != null && draftSlotRight != null)
        {
            allCards.RemoveAll(card => card.owner == DefaultOwner.Neutral);
            StartDraftPhase();
        }
        else
        {
            FinishSetupAndGenerateDeck();
        }
    }

    void Update()
    {
        if (UseDockNavigation)
        {
            SetNavigationUIActive(false);
            if (openHandBtn != null) openHandBtn.gameObject.SetActive(false);
            return;
        }
        // Chờ draft xong mới khởi tạo đủ các panel tay bài.
        if (handPanels == null || handPanels.Length == 0)
        {
            SetNavigationUIActive(false);
            return;
        }

        if (openHandBtn != null && !openHandBtn.gameObject.activeSelf)
        {
            openHandBtn.gameObject.SetActive(true);
        }

        bool isHandVisible = handPanels[currentHandIndex].activeInHierarchy;
        SetNavigationUIActive(isHandVisible);
    }

    private void SetNavigationUIActive(bool active)
    {
        if (handDescriptionText != null) handDescriptionText.gameObject.SetActive(active);
        if (leftArrowBtn != null) leftArrowBtn.gameObject.SetActive(active);
        if (rightArrowBtn != null) rightArrowBtn.gameObject.SetActive(active);
    }

    private void StartDraftPhase()
    {
        draftPanel.SetActive(true);
        isAnimatingPick = false;

        if (GameSettings.currentMode == GameMode.PlayerVsCurBot || GameSettings.currentMode == GameMode.PlayerVsVillBot)
        {
            isRoguelikeDraft = true;
            Debug.Log("<color=cyan>Bắt đầu Draft Mode: ROGUELIKE (Đánh với Bot)</color>");
            SetupBotDraft();
        }
        else
        {
            isRoguelikeDraft = false;
            Debug.Log("<color=cyan>Bắt đầu Draft Mode: CLASSIC (PvP 2 Người)</color>");
            SetupPvPDraft();
        }
    }

    private void SetupPvPDraft()
    {
        draftPairs.Clear();
        foreach (CardData card in neutralCardPool)
        {
            if (!draftPairs.ContainsKey(card.cardNumber)) draftPairs[card.cardNumber] = new List<CardData>();
            draftPairs[card.cardNumber].Add(card);
        }

        draftKeys = new List<int>(draftPairs.Keys);
        for (int i = 0; i < draftKeys.Count; i++)
        {
            int temp = draftKeys[i];
            int randomIndex = Random.Range(i, draftKeys.Count);
            draftKeys[i] = draftKeys[randomIndex];
            draftKeys[randomIndex] = temp;
        }

        currentDraftStep = 0;
        ShowDraftPair();
    }

    private void ShowDraftPair()
    {
        isAnimatingPick = false; 

        foreach (Transform child in draftSlotLeft) Destroy(child.gameObject);
        foreach (Transform child in draftSlotRight) Destroy(child.gameObject);

        int currentCardNumber = draftKeys[currentDraftStep];
        List<CardData> pair = draftPairs[currentCardNumber];

        if (pair.Count >= 2)
        {
            leftCardObj = SpawnDraftCard(pair[0], draftSlotLeft, true); 
            rightCardObj = SpawnDraftCard(pair[1], draftSlotRight, false);
        }
        else if (pair.Count == 1)
        {
            leftCardObj = SpawnDraftCard(pair[0], draftSlotLeft, true); 
            rightCardObj = null;
            PickNeutralCard(pair[0], true); 
        }
    }

    private void SetupBotDraft()
    {
        foreach (Transform child in draftSlotLeft) Destroy(child.gameObject);
        foreach (Transform child in draftSlotRight) Destroy(child.gameObject);

        draftPairs.Clear();
        foreach (CardData card in neutralCardPool)
        {
            if (!draftPairs.ContainsKey(card.cardNumber)) draftPairs[card.cardNumber] = new List<CardData>();
            draftPairs[card.cardNumber].Add(card);
        }

        draftKeys = new List<int>(draftPairs.Keys);

        if (draftKeys.Count > 0)
        {
            int randomKeyIndex = Random.Range(0, draftKeys.Count);
            int selectedNumber = draftKeys[randomKeyIndex];
            
            List<CardData> pair = draftPairs[selectedNumber];

            if (pair.Count >= 2)
            {
                bool swap = Random.value > 0.5f; 
                leftCardObj = SpawnDraftCard(pair[swap ? 1 : 0], draftSlotLeft, true); 
                rightCardObj = SpawnDraftCard(pair[swap ? 0 : 1], draftSlotRight, false);
            }
            else if (pair.Count == 1)
            {
                leftCardObj = SpawnDraftCard(pair[0], draftSlotLeft, true); 
                rightCardObj = null;
                PickNeutralCard(pair[0], true); 
            }
        }
        else 
        {
            FinishSetupAndGenerateDeck();
        }
    }
    public void PickNeutralCard(CardData chosenCard, bool isLeft)
    {
        if (isAnimatingPick) return; 
        if (OnlineSession.Active)
        {
            var options = DraftOptions(); int side = isLeft ? 0 : 1;
            if (IsReady || side >= options.Length || options[side] != chosenCard) return;
            if (!OnlineGameBridge.ApplyingCommand && !CanPickDraft) return;
            if (OnlineSession.Guest && !OnlineGameBridge.ApplyingCommand)
            {
                // Dùng Send() để phân biệt yêu cầu được chấp nhận với yêu cầu bị từ chối.
                var bridge = OnlineGameBridge.Instance;
                if (bridge != null && CardActionRouter.SendGuest(OnlineAction.Draft, option: side))
                {
                    if (AudioManager.instance != null && dealCardSFX != null)
                        AudioManager.instance.PlaySFX(dealCardSFX, false);
                    bridge.StartCoroutine(PlayGuestDraftFeedback(isLeft ? leftCardObj : rightCardObj,
                        isLeft ? rightCardObj : leftCardObj));
                }
                return;
            }
        }
        
        if (AudioManager.instance != null && dealCardSFX != null)
            AudioManager.instance.PlaySFX(dealCardSFX, false);

        if (isRoguelikeDraft) 
            StartCoroutine(AnimatePickAndAutoFill(chosenCard, isLeft));
        else 
            StartCoroutine(AnimatePickAndNextPvP(chosenCard, isLeft));
    }

    // Same animation on host and guest. Guest resets if the authoritative pair remains.
    private IEnumerator PlayGuestDraftFeedback(GameObject chosen, GameObject unchosen)
    {
        yield return CardDraftVisualFeedback.Play(chosen, unchosen, restoreAfterwards: true);
    }

    private IEnumerator AnimatePickAndNextPvP(CardData chosenCard, bool isLeft)
    {
        isAnimatingPick = true; 
        yield return StartCoroutine(PlayPickAnimation(isLeft));

        allCards.Add(chosenCard); 
        currentDraftStep++;
        
        if (currentDraftStep < draftKeys.Count) ShowDraftPair(); 
        else
        {
            draftPanel.SetActive(false);
            FinishSetupAndGenerateDeck(); 
        }
    }

    // Người chơi chọn lá đầu; bảy số còn lại được bốc tự động.
    private IEnumerator AnimatePickAndAutoFill(CardData chosenCard, bool isLeft)
    {
        isAnimatingPick = true; 
        yield return StartCoroutine(PlayPickAnimation(isLeft));

        allCards.Add(chosenCard); 

        int chosenNumber = chosenCard.cardNumber;
        draftKeys.Remove(chosenNumber);

        foreach (int key in draftKeys)
        {
            List<CardData> pair = draftPairs[key];
            
            if (pair.Count > 0)
            {
                // Mỗi số chỉ lấy một biến thể để tránh trùng bài.
                CardData randomCardInPair = pair[Random.Range(0, pair.Count)];
                allCards.Add(randomCardInPair);
                
                Debug.Log($"<color=white>Auto-fill: Đã thêm {randomCardInPair.cardName} (Số {key})</color>");
            }
        }

        draftPanel.SetActive(false);
        FinishSetupAndGenerateDeck(); 
    }

    // Shared with the online guest; only the host advances the actual draft state.
    private IEnumerator PlayPickAnimation(bool isLeft)
    {
        yield return CardDraftVisualFeedback.Play(isLeft ? leftCardObj : rightCardObj,
            isLeft ? rightCardObj : leftCardObj, restoreAfterwards: false);
    }

    private GameObject SpawnDraftCard(CardData data, Transform slot, bool isLeftSide)
    {
        GameObject displayObj = Instantiate(draftCardPrefab != null ? draftCardPrefab : cardPrefab, slot);
        
        RectTransform rect = displayObj.GetComponent<RectTransform>();
        // Draft slots define the position. The card must be centered within its slot,
        // rather than reusing CardUI's scene/seat offset.
        rect.anchoredPosition = Vector2.zero;
        rect.localScale = Vector3.one;
        CardInstance displayCi = displayObj.GetComponent<CardInstance>();
        displayCi.cardData = data;
        displayCi.cardImage.sprite = data.defaultArtwork;

        CardInteraction cardInteract = displayObj.GetComponent<CardInteraction>();
        if (cardInteract != null) 
        {
            cardInteract.enabled = false; 

            if (cardInteract.descriptionPanel != null && cardInteract.descriptionText != null)
            {
                if (draftCardPrefab == null) LayoutDraftDescription(cardInteract);
                cardInteract.SetDescription(data.description);
                cardInteract.descriptionPanel.SetActive(true);
            }
        }

        EventTrigger trigger = displayObj.AddComponent<EventTrigger>();
        EventTrigger.Entry onClick = new EventTrigger.Entry();
        onClick.eventID = EventTriggerType.PointerClick;
        onClick.callback.AddListener((eventData) => {
            PointerEventData pointerData = (PointerEventData)eventData;

            GameObject clickedObj = pointerData.pointerCurrentRaycast.gameObject;
            if (clickedObj != null && cardInteract != null && cardInteract.descriptionPanel != null)
            {
                if (clickedObj == cardInteract.descriptionPanel || clickedObj.transform.IsChildOf(cardInteract.descriptionPanel.transform))
                {
                    return; 
                }
            }
            
            if (isAnimatingPick) return;
            if (OnlineSession.Active && !CanPickDraft) return;

            if (pointerData.button == PointerEventData.InputButton.Left)
            {
                if (cardInteract != null && cardInteract.descriptionPanel != null)
                {
                    cardInteract.descriptionPanel.SetActive(false);
                }
                PickNeutralCard(data, isLeftSide);
            }
            else
            {
                Debug.Log("<color=orange>DeckManager: Hãy bấm CHUỘT TRÁI để chọn lá bài!</color>");
            }
        });
        trigger.triggers.Add(onClick);

        return displayObj;
    }

    public void ApplyOnlineDraft(int step, int total, bool picking, CardData[] options)
    {
        onlineMirror = true; currentDraftStep = step; remoteDraftTotal = total;
        remoteDraftOptions = options; isAnimatingPick = picking;
        if (draftPanel != null) draftPanel.SetActive(true);
        string signature = step + ":" + string.Join("|", System.Array.ConvertAll(options, data => data.name));
        if (signature == remoteDraftSignature) return;
        remoteDraftSignature = signature;
        foreach (var slot in new[] { draftSlotLeft, draftSlotRight })
            if (slot != null) foreach (Transform child in slot) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
        leftCardObj = options.Length > 0 ? SpawnDraftCard(options[0], draftSlotLeft, true) : null;
        rightCardObj = options.Length > 1 ? SpawnDraftCard(options[1], draftSlotRight, false) : null;
    }

    public static void LayoutDraftDescription(CardInteraction card)
    {
        if (card == null || card.descriptionPanel == null || card.descriptionText == null) return;
        // CardUI's regular description layout does not fit the Draft popup.
        // This fallback is only called when a dedicated draftCardPrefab is NOT used.
        card.transform.localScale = Vector3.one;
        var panel = card.descriptionPanel.GetComponent<RectTransform>();
        panel.anchorMin = panel.anchorMax = new Vector2(.5f, 0);
        panel.pivot = new Vector2(.5f, 1);
        panel.anchoredPosition = new Vector2(0, -24);
        panel.sizeDelta = new Vector2(500, 260);
        panel.localScale = Vector3.one;
        var text = card.descriptionText;
        text.rectTransform.anchorMin = Vector2.zero;
        text.rectTransform.anchorMax = Vector2.one;
        text.rectTransform.offsetMin = new Vector2(20, 16);
        text.rectTransform.offsetMax = new Vector2(-20, -16);
        text.enableAutoSizing = true;
        text.fontSizeMin = 16;
        text.fontSizeMax = 20;
        text.overflowMode = TextOverflowModes.Ellipsis;
        text.color = new Color32(248, 236, 213, 255);
        if (panel.TryGetComponent<Image>(out var background)) GameUIAssets.Panel(background);
    }


    private IEnumerator AnimatePickAndNext(CardData chosenCard, bool isLeft)
    {
        isAnimatingPick = true; 

        GameObject chosenObj = isLeft ? leftCardObj : rightCardObj;
        GameObject unchosenObj = isLeft ? rightCardObj : leftCardObj;

        float duration = 0.35f; 
        float elapsed = 0f;

        Vector3 chosenStartScale = chosenObj != null ? chosenObj.transform.localScale : Vector3.one;
        Vector3 chosenEndScale = chosenStartScale * 1.15f; 

        Vector3 unchosenStartScale = unchosenObj != null ? unchosenObj.transform.localScale : Vector3.one;
        Vector3 unchosenEndScale = Vector3.zero; 

        CanvasGroup unchosenCg = unchosenObj != null ? unchosenObj.GetComponent<CanvasGroup>() : null;
        if (unchosenCg == null && unchosenObj != null) unchosenCg = unchosenObj.AddComponent<CanvasGroup>();

        if (unchosenObj != null)
        {
            var interact = unchosenObj.GetComponent<CardInteraction>();
            if (interact != null && interact.descriptionPanel != null) interact.descriptionPanel.SetActive(false);
        }

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            float smoothT = Mathf.SmoothStep(0, 1, t);

            if (chosenObj != null)
                chosenObj.transform.localScale = Vector3.Lerp(chosenStartScale, chosenEndScale, smoothT);

            if (unchosenObj != null)
            {
                unchosenObj.transform.localScale = Vector3.Lerp(unchosenStartScale, unchosenEndScale, smoothT);
                if (unchosenCg != null) unchosenCg.alpha = Mathf.Lerp(1f, 0f, smoothT); 
            }

            yield return null;
        }

        yield return new WaitForSeconds(0.1f);

        allCards.Add(chosenCard); 
        currentDraftStep++;
        
        if (currentDraftStep < draftKeys.Count)
        {
            ShowDraftPair(); 
        }
        else
        {
            draftPanel.SetActive(false);
            FinishSetupAndGenerateDeck(); 
        }
    }

    private void FinishSetupAndGenerateDeck()
    {
        if (IsReady) return;
        GenerateDeck();
        IsReady = true;
        if (draftPanel != null) draftPanel.SetActive(false);
        
        InitializeHandNavigation();
    }

    public void InitializeOnlineView(int completedStep = -1, int total = -1)
    {
        if (completedStep >= 0) { currentDraftStep = completedStep; remoteDraftTotal = total; }
        if (IsReady) return;
        IsReady = true;
        if (draftPanel != null) draftPanel.SetActive(false);
        InitializeHandNavigation();
    }
    public void BuildTutorialDeck(CardData[] exercise)
    {
        if (!TutorialController.Active || IsReady) return;
        allCards = new List<CardData>(exercise);
        neutralCardPool = new List<CardData>();
        FinishSetupAndGenerateDeck();
    }

    private void InitializeHandNavigation()
    {
        if (p1Panel == null || p2Panel == null || neutralPanel == null) return;
        
        handPanels = new GameObject[] { p1Panel, neutralPanel, p2Panel };

        if (leftArrowBtn != null)
        {
            leftArrowBtn.onClick.RemoveAllListeners();
            leftArrowBtn.onClick.AddListener(() => CycleHand(-1));
        }
        if (rightArrowBtn != null)
        {
            rightArrowBtn.onClick.RemoveAllListeners();
            rightArrowBtn.onClick.AddListener(() => CycleHand(1));
        }

        if (openHandBtn != null)
        {
            openHandBtn.onClick.RemoveAllListeners();
            openHandBtn.onClick.AddListener(ToggleCurrentHand);
        }

        currentHandIndex = GameSettings.currentMode == GameMode.PlayerVsCurBot ? 2 : 0;
        UpdateHandView();

        foreach (GameObject panel in handPanels)
        {
            ArrangeFanLayout(panel.transform);
            AttachAutoAnimator(panel);
        }
    }

    public void ToggleCurrentHand()
    {
        if (handPanels != null && handPanels.Length > currentHandIndex)
        {
            GameObject currentHand = handPanels[currentHandIndex];
            
            bool isCurrentlyVisible = currentHand.activeSelf;

            currentHand.SetActive(!isCurrentlyVisible);
            
            if (!isCurrentlyVisible) 
            {
                ArrangeFanLayout(currentHand.transform);
                if (AudioManager.instance != null && dealCardSFX != null)
                {
                    AudioManager.instance.PlaySFX(dealCardSFX, false);
                }
            }
        }
    }

    private void CycleHand(int direction)
    {
        currentHandIndex += direction;

        if (currentHandIndex < 0) currentHandIndex = handPanels.Length - 1;
        else if (currentHandIndex >= handPanels.Length) currentHandIndex = 0;

        UpdateHandView();

        if (AudioManager.instance != null && AudioManager.instance.defaultClickSFX != null)
            AudioManager.instance.PlaySFX(AudioManager.instance.defaultClickSFX, true);
    }

    private void UpdateHandView()
    {
        if (handPanels == null) return;

        for (int i = 0; i < handPanels.Length; i++)
        {
            handPanels[i].SetActive(i == currentHandIndex);
        }

        if (handDescriptionText != null)
        {
            if (currentHandIndex == 1) 
            {
                handDescriptionText.text = "BÀI TRUNG LẬP";
                return;
            }

            // Nhãn mô tả bộ bài đang xem, không mô tả vai trò bí mật.
            if (currentHandIndex == 0)
            {
                handDescriptionText.text = "BÀI PHE CURSERMANCER";
            }
            else if (currentHandIndex == 2)
            {
                handDescriptionText.text = "BÀI PHE DÂN LÀNG";
            }
        }
    }

    private void AttachAutoAnimator(GameObject panel)
    {
        if (panel != null)
        {
            HandPanelAnimator animator = panel.AddComponent<HandPanelAnimator>();
            animator.deckManager = this;
        }
    }
    
    void GenerateDeck()
    {
        foreach (CardData data in allCards)
        {
            Transform targetParent = (data.owner == DefaultOwner.Player1) ? p1Panel.transform : 
                                     (data.owner == DefaultOwner.Player2) ? p2Panel.transform : neutralPanel.transform;

            GameObject newCard = Instantiate(cardPrefab, targetParent);
            CardInstance ci = newCard.GetComponent<CardInstance>();
            
            if(ci != null) {
                ci.cardData = data; 
                ci.currentOwner = data.owner;
                ci.UpdateCardUI();  
            }
        }
    }

    public void ArrangeFanLayout(Transform panel)
    {
        if (panel == null) return;
        int count = panel.childCount;
        if (count == 0) return;

        if (separateHandCards)
        {
            float width = 0;
            foreach (Transform child in panel)
                if (child is RectTransform card) width = Mathf.Max(width, card.rect.width);
            for (int i = 0; i < count; i++)
            {
                var card = panel.GetChild(i) as RectTransform;
                if (card == null) continue;
                card.anchoredPosition = new Vector2((i - (count - 1) * .5f) * (width + separatedCardGap), 0);
                card.localRotation = Quaternion.identity;
            }
            return;
        }

        float span = cardSpacing * count;
        float curve = curveAmount * count;
        if (UseDockNavigation && panel.parent is RectTransform area)
        {
            float width = (area.rect.width - 40) / Mathf.Max(.1f, panel.localScale.x);
            span = Mathf.Min(span, Mathf.Max(0, width - 200));
            curve = Mathf.Min(curve, 100);
        }

        for (int i = 0; i < count; i++)
        {
            RectTransform cardRect = panel.GetChild(i).GetComponent<RectTransform>();
            if (cardRect == null) continue;
            
            float normalizedPosition = (count > 1) ? (float)i / (count - 1) : 0.5f;
            float centeredIndex = normalizedPosition - 0.5f; 

            float xPos = centeredIndex * span;
            float yPos = -Mathf.Abs(centeredIndex * centeredIndex) * curve;
            float angle = centeredIndex * -angleSpread;

            cardRect.anchoredPosition = new Vector2(xPos, yPos);
            cardRect.localRotation = Quaternion.Euler(0, 0, angle);
        }
    }

    public void SortHand(Transform hand)
    {
        if (hand == null || hand.childCount <= 1) return;

        List<CardInstance> cardsInHand = new List<CardInstance>();
        foreach (Transform child in hand)
        {
            CardInstance ci = child.GetComponent<CardInstance>();
            if (ci != null) cardsInHand.Add(ci);
        }

        cardsInHand.Sort((a, b) => 
        {
            int indexA = allCards.IndexOf(a.cardData);
            int indexB = allCards.IndexOf(b.cardData);
            return indexA.CompareTo(indexB);
        });

        for (int i = 0; i < cardsInHand.Count; i++)
        {
            cardsInHand[i].transform.SetSiblingIndex(i);
        }
    }
    public void SwitchToHand(DefaultOwner owner)
    {
        if (handPanels == null || handPanels.Length == 0) return;
        
        if (owner == DefaultOwner.Player1) currentHandIndex = 0;
        else if (owner == DefaultOwner.Player2) currentHandIndex = 2;
        else if (owner == DefaultOwner.Neutral) currentHandIndex = 1;
        
        UpdateHandView(); 
    }
    public void HideAllHands()
    {
        if (handPanels == null) return;
        foreach (GameObject panel in handPanels)
        {
            if (panel != null) panel.SetActive(false);
        }
    }
}

public class HandPanelAnimator : MonoBehaviour
{
    public DeckManager deckManager;
    private Coroutine animCoroutine;

    // Cancel staged animations and restore a consistent fan after a network
    // bootstrap. Safe to invoke even if the panel was enabled before cards arrived.
    public void StopAndSnap()
    {
        StopAllCoroutines();
        animCoroutine = null;
        if (deckManager != null) deckManager.ArrangeFanLayout(transform);
        foreach (Transform child in transform)
        {
            if (child.TryGetComponent<CardInteraction>(out var cardInput))
                child.localScale = cardInput.PrefabRootScale;
            else child.localScale = Vector3.one;
            if (child.TryGetComponent<CanvasGroup>(out var group)) group.blocksRaycasts = true;
        }
    }

    void OnDisable() { StopAndSnap(); }

    void OnEnable()
    {
        if (deckManager == null) return;
        if (deckManager.SuppressHandEntranceAnimation)
        {
            StopAndSnap();
            return;
        }
        if (animCoroutine != null) StopCoroutine(animCoroutine);
        animCoroutine = StartCoroutine(PlayFanningAnimation());
    }
    

    IEnumerator PlayFanningAnimation()
    {
        int count = transform.childCount;
        if (count == 0) yield break;

        deckManager.ArrangeFanLayout(transform);

        List<RectTransform> cards = new List<RectTransform>();
        List<Vector2> targetPos = new List<Vector2>();
        List<Quaternion> targetRot = new List<Quaternion>();
        List<Vector3> targetScale = new List<Vector3>();

        for (int i = 0; i < count; i++)
        {
            RectTransform rect = transform.GetChild(i).GetComponent<RectTransform>();
            cards.Add(rect);
            targetPos.Add(rect.anchoredPosition);
            targetRot.Add(rect.localRotation);
            if (rect.TryGetComponent<CardInteraction>(out var cardInput))
                targetScale.Add(cardInput.PrefabRootScale);
            else targetScale.Add(rect.localScale);

            rect.anchoredPosition = Vector2.zero;
            rect.localRotation = Quaternion.identity;
            rect.localScale = Vector3.zero; 
            
            CanvasGroup cg = rect.GetComponent<CanvasGroup>();
            if (cg != null) cg.blocksRaycasts = false;
        }

        for (int i = 0; i < count; i++)
        {
            StartCoroutine(AnimateSingleCard(cards[i], targetPos[i], targetRot[i], targetScale[i]));
            
            if (AudioManager.instance != null)
            {
                AudioClip sfxToPlay = deckManager.dealCardSFX != null ? deckManager.dealCardSFX : AudioManager.instance.defaultClickSFX;
                if (sfxToPlay != null)
                {
                    AudioManager.instance.PlaySFX(sfxToPlay, true); 
                }
            }

            yield return new WaitForSeconds(deckManager.delayBetweenCards);
        }
    }

    IEnumerator AnimateSingleCard(RectTransform card, Vector2 tPos, Quaternion tRot, Vector3 prefabScale)
    {
        float elapsed = 0f;
        float duration = deckManager.drawDuration;
        
        // Nhớ panel gốc để nhận biết lá đã rời tay trong lúc chia bài.
        Transform originalParent = card != null ? card.parent : null;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            
            float backT = t - 1f;
            backT = backT * backT * ((1.5f + 1f) * backT + 1.5f) + 1f;

            if (card != null)
            {
                // Dừng hiệu ứng chia bài nếu lá đã được kéo ra khỏi tay.
                if (card.parent != originalParent) yield break;

                card.anchoredPosition = Vector2.Lerp(Vector2.zero, tPos, backT);
                card.localRotation = Quaternion.Slerp(Quaternion.identity, tRot, backT);
                card.localScale = Vector3.Lerp(Vector3.zero, prefabScale, backT);
            }

            yield return null;
        }

        if (card != null && card.parent == originalParent)
        {
            card.anchoredPosition = tPos;
            card.localRotation = tRot;
            card.localScale = prefabScale;
            
            CanvasGroup cg = card.GetComponent<CanvasGroup>();
            if (cg != null) cg.blocksRaycasts = true;
        }
    }
}

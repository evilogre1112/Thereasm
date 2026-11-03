using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Collections;
using UnityEngine.EventSystems;

public enum TurnState 
{ 
    SetupPhase, 
    Player1Turn,
    Player2Turn,
    CursePhase,  // Giai đoạn lây nhiễm trong Nghi thức (Ritual Infection)
    SacrificePhase // Giai đoạn hiến tế trong Nghi thức
}

public class TurnManager : MonoBehaviour
{
    public static TurnManager instance; 

    
    [Header("Trạng thái Lượt")]
    public TurnState firstPlayerThisRound = TurnState.Player2Turn; 
    public DefaultOwner currentVotingPlayer; 
    [Header("Nghĩa trang chung")]
    public Transform globalCemetery;
    private Dictionary<CardInstance, int> cardVotes = new Dictionary<CardInstance, int>();
    private CardInstance activeVoter;
    private int nextCemeteryOrder;
    private readonly Dictionary<CardInstance, Coroutine> cemeteryFlights = new Dictionary<CardInstance, Coroutine>();
    public TurnState currentTurn; 
    [Header("Bảo mật UI")]
    public bool isTallyingVotes = false; // Ẩn thông tin riêng trong lúc tổng kết phiếu.
    public TextMeshProUGUI turnText;

    [Header("Giai đoạn Setup (Danh sách Cursermancer)")]
    public List<CardInstance> allCurs = new List<CardInstance>(); 
    [HideInInspector] public CardInstance allyCur;
    [HideInInspector] public CardInstance enemyCur;
    
    [Header("Các nút bấm UI")]
    public GameObject btnStartGame;  
    public GameObject btnEndTurn;    

    [Header("--- THANH NĂNG LƯỢNG MỚI ---")]
    public EnergyBarUI p1EnergyBar;

  [Header("Hệ thống Tim (Heart System)")]
    public int p1Hearts = 5;
    public int p2Hearts = 5;
    public const int MAX_HEARTS = 5;   

    [Header("Cấu hình Bàn chơi (Meeting)")]
    public List<SeatDropZone> allSeats; 

    [Header("Nghĩa trang")]
    public Transform cemeteryCardPos;

    public int validLeftTarget = -1;
    public int validRightTarget = -1;
    public int validTopTarget = -1;
    public int validBottomTarget = -1;
    public CardInstance activeCurInRitual;

    [Header("Chế độ Ngụy Trang (Curse)")]
    public GameObject btnDisguiseToggle;
    public bool isDisguiseModeActive = false;

    [Header("Hệ thống Check")]
    public GameObject btnCheck; 
    public TextMeshProUGUI checkBtnText;
    public int p1Checks = 2;
    public int p2Checks = 2;
    public const int MAX_CHECKS = 2;

    [Header("Invoker - Sacrifice Version")]
    public List<CardData> deadCardsData = new List<CardData>();
    public GameObject discoverPanel;
    public Transform[] discoverSlots;
    public GameObject cardPrefab;
    [Header("Vote explanations — saved presentation feed")]
    public VoteEffectFeed voteEffects;

    private CardInstance currentInvokerInstance; // Giữ chủ sở hữu của lá Invoker đang mở bảng.

    private CardInteraction currentInvoker;
    private List<CardData> currentOptions = new List<CardData>();

    [Header("Hệ thống Kết thúc")]
    public GameObject winPanel;
    public TextMeshProUGUI winText;
    [Header("Phe Bot (AI)")]
    public VillagerBotAI villagerBot;
    
    public CurBotAI curBot;

    public string feedbackMessage { get; private set; }
    public int FeedbackRevision { get; private set; }
    private string localActionFeedback;
    private float localActionFeedbackUntil;
    public string LocalActionFeedback => Time.unscaledTime < localActionFeedbackUntil ? localActionFeedback : null;
    public void ShowLocalActionFeedback(string message)
    { localActionFeedback = message; localActionFeedbackUntil = Time.unscaledTime + 8; }
    public void ClearLocalActionFeedback() { localActionFeedback = null; }
    public void ResetWorkshopVotes()
    {
        if(!TutorialWorkshop.Active)return;
        cardVotes.Clear();activeVoter=null;activeCurInRitual=null;
    }
    public void RefreshWorkshopViewer()
    {
        if(!TutorialWorkshop.Active)return;
        UpdateCurVisuals();UpdateVoteDisplay();
    }
    private DefaultOwner feedbackOwner = DefaultOwner.Neutral;
    public string GetOnlineFeedback(DefaultOwner viewer) => !OnlineSession.Active || isGameOver ||
        feedbackOwner == DefaultOwner.Neutral || feedbackOwner == viewer ? feedbackMessage : null;
    private bool deathCheckPending;
    private bool refreshingAuras, aurasDirty;
    private int boardMutationDepth;
    public void BeginBoardMutation() { boardMutationDepth++; }
    public void EndBoardMutation() { if (--boardMutationDepth == 0) RefreshAllAuras(); }
    private readonly HashSet<(CardInstance, CardInstance)> pranksterReactions = new HashSet<(CardInstance, CardInstance)>();

    public void ShowFeedback(string message)
    {
        FeedbackRevision++;
        feedbackMessage = message;
        feedbackOwner = currentTurn == TurnState.SetupPhase || currentTurn == TurnState.CursePhase || currentTurn == TurnState.Player1Turn ?
            DefaultOwner.Player1 : currentTurn == TurnState.SacrificePhase ? currentVotingPlayer : DefaultOwner.Player2;
        if (!OnlineSession.Active || feedbackOwner == OnlineSession.LocalRole) Debug.Log(message);
    }

    public bool IsBotTurn =>
        (GameSettings.currentMode == GameMode.PlayerVsCurBot &&
            (currentTurn == TurnState.SetupPhase || currentTurn == TurnState.Player1Turn ||
             currentTurn == TurnState.CursePhase ||
             (currentTurn == TurnState.SacrificePhase && currentVotingPlayer == DefaultOwner.Player1))) ||
        (GameSettings.currentMode == GameMode.PlayerVsVillBot &&
            (currentTurn == TurnState.Player2Turn ||
             (currentTurn == TurnState.SacrificePhase && currentVotingPlayer == DefaultOwner.Player2)));

    void Awake()
    {
        instance = this;
        // Inspector previews must never become an open modal at the start of a match.
        if (cemeteryViewPanel != null) cemeteryViewPanel.SetActive(false);
        if (discoverPanel != null) discoverPanel.SetActive(false);
        if (notePanel != null) notePanel.SetActive(false);
    }

    void Start()
    {
        GameSettings.currentLevel = GameRules.ClampLevel(GameSettings.currentLevel);
        HoldToReveal.isRevealingAll = false;
        CardInteraction.isAnyCardDragging = false;
        Skill_Gambler.ResetComboChain();
        isGameOver = false;
        if (winPanel != null) winPanel.SetActive(false);
        GameStartUI.CreateBoard(this);
        if (villagerBot != null) villagerBot.gameObject.SetActive(false);
        if (curBot != null) curBot.gameObject.SetActive(false);

        if (GameSettings.currentMode == GameMode.PlayerVsVillBot && villagerBot != null)
        {
            Debug.Log("<color=green>Chế độ: Đánh với Villager Bot!</color>");
            villagerBot.gameObject.SetActive(true);
        }
        else if (GameSettings.currentMode == GameMode.PlayerVsCurBot && curBot != null)
        {
            Debug.Log("<color=purple>Chế độ: Đánh với Curse Bot!</color>");
            curBot.gameObject.SetActive(true);
        }
        else 
        {
            Debug.Log("<color=cyan>Chế độ: Solo 2 Người (PvP)!</color>");
        }
        currentTurn = TurnState.SetupPhase;
        ResetHearts();
        UpdateTurnUI();
        if (btnStartGame != null) 
        {
            btnStartGame.SetActive(true);
            btnStartGame.GetComponent<Button>().interactable = false;
        }
        if(btnEndTurn != null) btnEndTurn.SetActive(false); 
    }
    void Update()
    {
        if (deathCheckPending && !isTallyingVotes)
        {
            deathCheckPending = false;
            CheckGameOver();
        }
        if (EventSystem.current == null || GameStartUI.IsBlockingInput ||
            OnlineSession.Active && !OnlineSession.LocalTurn) return;
        if (discoverPanel != null && discoverPanel.activeSelf && (Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1)))
        {
            PointerEventData pointerData = new PointerEventData(EventSystem.current) { position = Input.mousePosition };
            List<RaycastResult> results = new List<RaycastResult>();
            EventSystem.current.RaycastAll(pointerData, results);

            bool clickedOnDiscoverCard = false;

            foreach (RaycastResult result in results)
            {
                if (result.gameObject.transform.IsChildOf(discoverPanel.transform))
                {
                    if (result.gameObject.GetComponentInParent<CardInstance>() != null)
                    {
                        clickedOnDiscoverCard = true;
                        break;
                    }
                }
            }

            if (!clickedOnDiscoverCard)
            {
                CloseInvokerDiscoverUI();
            }
        }

        if (notePanel != null && notePanel.activeSelf && (Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1)))
        {
            PointerEventData pointerData = new PointerEventData(EventSystem.current) { position = Input.mousePosition };
            List<RaycastResult> results = new List<RaycastResult>();
            EventSystem.current.RaycastAll(pointerData, results);

            bool clickedInsideNote = false;

            foreach (RaycastResult result in results)
            {
                if (result.gameObject.transform.IsChildOf(notePanel.transform))
                {
                    if (result.gameObject.name != "NotePanel") 
                    {
                        clickedInsideNote = true;
                        break;
                    }
                }
            }

            if (!clickedInsideNote)
            {
                notePanel.SetActive(false);
                Debug.Log("<color=gray>Đã đóng Sổ tay do click ra ngoài.</color>");
            }
        }
    }
    [Header("Note Button")]
    public GameObject notePanel;

    public void ToggleNote() {
        if (notePanel != null) notePanel.SetActive(!notePanel.activeInHierarchy);
    }

    public void CloseInvokerDiscoverUI()
    {
        if (discoverPanel != null)
        {
            discoverPanel.SetActive(false);
            currentInvokerInstance = null;
            UpdateCurVisuals();
            Debug.Log("<color=gray>Đã đóng bảng Discover do click ra ngoài lá bài.</color>");
        }
    }

    public void EndTurn()
    {
        if (!TutorialController.Allow(TutorialAction.EndTurn)) return;
        if (OnlineGameBridge.Forward(OnlineAction.EndTurn)) return;
        if (OnlineSession.Active && !OnlineSession.LocalTurn && !OnlineGameBridge.ApplyingCommand) return;
        if (isGameOver || isTallyingVotes || currentTurn == TurnState.SetupPhase) return;
        if (currentTurn == TurnState.SacrificePhase) 
        { 
            GetVotingProgress(out int voted, out int required);
            if (voted < required)
            {
                ShowFeedback("Chưa bỏ phiếu: " + string.Join(", ", GetMissingVoterNames()) + ". Kéo từ các lá này tới lá muốn hiến tế.");
                return;
            }
        }

        feedbackMessage = null;
        activeVoter = null;
        ClearLocalActionFeedback();
        CloseInvokerDiscoverUI();

        CardInteraction.ForceUnzoom();
        if (TabManager.instance != null) TabManager.instance.CloseAllTabs();
        ClearCurseTargets(); 

        if (currentTurn == TurnState.CursePhase) { StartSacrificePhase(); return; }
        
        if (currentTurn == TurnState.SacrificePhase) 
        { 
            NextSacrificeStep(); 
            return; 
        }

        // Start the ritual once the board is full OR every remaining living card
        // is seated. A bot passing because of Tim/strategy is NOT enough.
        if ((currentTurn == TurnState.Player1Turn || currentTurn == TurnState.Player2Turn) &&
            (IsMeetingFull() || AreAllLivingCardsPlaced()))
        {
            StartCursePhase();
            return; 
        }

        ProcessNormalTurnSwap();
    }

    public void ToggleDisguiseMode()
    {
        isDisguiseModeActive = !isDisguiseModeActive;
        
        if (btnDisguiseToggle != null)
        {
            TextMeshProUGUI btnText = btnDisguiseToggle.GetComponentInChildren<TextMeshProUGUI>();
            if (btnText != null)
            {
                btnText.text = isDisguiseModeActive ? "Show" : "Unshow";
            }
        }

        Debug.Log($"<color=cyan>Chế độ Ngụy trang: {isDisguiseModeActive}</color>");
        UpdateCurVisuals(); 
    }

    private void ProcessNormalTurnSwap()
    {
        CardInteraction[] allCards = FindObjectsByType<CardInteraction>(FindObjectsSortMode.None);
        foreach (CardInteraction card in allCards) card.ResetTurnState();

        currentTurn = (currentTurn == TurnState.Player1Turn) ? TurnState.Player2Turn : TurnState.Player1Turn;
        if (currentTurn == TurnState.Player1Turn) p1Hearts = MAX_HEARTS;
        else if (currentTurn == TurnState.Player2Turn) p2Hearts = MAX_HEARTS;

        UpdateTurnUI();
    }

    private void StartCursePhase()
    {
        currentTurn = TurnState.CursePhase;
        UpdateTurnUI();
        Debug.Log("<color=purple>!!! NGHI THỨC: BẮT ĐẦU NGUYỀN RỦA !!!</color>");
        foreach(var cur in allCurs) { 
            if (cur != null) { cur.MarkSkillAsUsed(false); cur.currentVictim = null; cur.hasUsedSkillInRitual = false; }
        }
        ClearCurseTargets();
    }

    public void ForceCursePhase()
    {
        if (isGameOver || isTallyingVotes || currentTurn == TurnState.SetupPhase) return;
        if (currentTurn == TurnState.CursePhase || currentTurn == TurnState.SacrificePhase) return;

        Debug.Log("<color=yellow>!!! TRƯỞNG LÀNG GÕ BÚA - ÉP BUỘC VÀO ĐÊM !!!</color>");
        
        CardInteraction.ForceUnzoom();
        if (TabManager.instance != null) TabManager.instance.CloseAllTabs();
        ClearCurseTargets();
        
        StartCursePhase();
    }

    private void StartSacrificePhase()
    {
        currentTurn = TurnState.SacrificePhase;
        currentVotingPlayer = (firstPlayerThisRound == TurnState.Player1Turn) ? DefaultOwner.Player1 : DefaultOwner.Player2;
        cardVotes.Clear();
        UpdateVoteDisplay(); 
        if (isDisguiseModeActive) 
        {
            RevealSeatCards(); 
        }
        UpdateTurnUI();
    }

    private void NextSacrificeStep()
    {
        activeVoter = null;
        DefaultOwner secondPlayer = (firstPlayerThisRound == TurnState.Player1Turn) ? DefaultOwner.Player2 : DefaultOwner.Player1;
        ClearAllGamblerMarks();
        ClearAllSnooperMarks();
        if (currentVotingPlayer != secondPlayer)
        {
            currentVotingPlayer = secondPlayer;
            UpdateVoteDisplay(); 
            if (isDisguiseModeActive) 
            {
                RevealSeatCards(); 
            }
            UpdateTurnUI();
        }
        else
        {
            StartCoroutine(SacrificeRoutine());
        }
    }

    public bool IsMyVoteTurn(CardInstance card) => card != null && card.cardData != null &&
        !card.isDead && card.cardData.owner == currentVotingPlayer;

    public void StartVoteSelection(CardInstance voterCard)
    {
        if (!TutorialController.CanChooseSource(TutorialAction.Vote, voterCard)) return;
        activeVoter = null;
        if (isGameOver || isTallyingVotes || currentTurn != TurnState.SacrificePhase ||
            !IsMyVoteTurn(voterCard)) return;
        if (GetSeatIndex(voterCard.GetComponent<CardInteraction>()) >= 0) activeVoter = voterCard;
    }

    public void GetVotingProgress(out int voted, out int required)
    {
        voted = 0;
        required = 0;
        foreach (var seat in allSeats)
        {
            if (seat == null || seat.cardPos == null) continue;
            CardInstance voter = seat.cardPos.GetComponentInChildren<CardInstance>();
            if (!IsMyVoteTurn(voter) || FindLegalVoteTarget(voter, -1) < 0) continue;
            required++;
            if (cardVotes.TryGetValue(voter, out int target) && target >= 0 && target < allSeats.Count)
            {
                CardInstance card = allSeats[target].cardPos.GetComponentInChildren<CardInstance>();
                if (card != null && !card.isDead && card != voter) voted++;
            }
        }
    }

    public static string CardLabel(CardInstance card) => card != null && card.cardData != null ?
        $"{card.cardData.cardName} (số {card.cardData.cardNumber})" : "lá bài";

    public List<string> GetMissingVoterNames()
    {
        var names = new List<string>();
        foreach (var seat in allSeats)
        {
            var voter = seat != null && seat.cardPos != null ? seat.cardPos.GetComponentInChildren<CardInstance>() : null;
            if (!IsMyVoteTurn(voter) || FindLegalVoteTarget(voter, -1) < 0) continue;
            int target = GetVoteTarget(voter);
            if (target < 0 || target >= allSeats.Count || GetBoardCard(target) == null || GetBoardCard(target).isDead || GetBoardCard(target) == voter)
                names.Add(CardLabel(voter));
        }
        return names;
    }

    public int FindLegalVoteTarget(CardInstance voter, int preferred)
    {
        if (preferred >= 0 && preferred < allSeats.Count)
        {
            var target = allSeats[preferred].cardPos.GetComponentInChildren<CardInstance>();
            if (target != null && !target.isDead && target != voter) return preferred;
        }
        for (int i = 0; i < allSeats.Count; i++)
        {
            var target = allSeats[i].cardPos.GetComponentInChildren<CardInstance>();
            if (target != null && !target.isDead && target != voter) return i;
        }
        return -1;
    }

    public void ApplyVote(int targetSeatIndex)
    {
        if (!TutorialController.Allow(TutorialAction.Vote, activeVoter, seat: targetSeatIndex)) return;
        if (isGameOver || isTallyingVotes || currentTurn != TurnState.SacrificePhase || !IsMyVoteTurn(activeVoter)) return;
        if (activeVoter == null || targetSeatIndex < 0 || targetSeatIndex >= allSeats.Count) return;
        CardInstance targetCard = allSeats[targetSeatIndex].cardPos.GetComponentInChildren<CardInstance>();
        
        if (targetCard == activeVoter)
        {
            ShowFeedback(CardLabel(activeVoter) + " không thể tự bỏ phiếu cho chính mình.");
            return; 
        }

        if (targetCard != null && !targetCard.isDead)
        {
            cardVotes[activeVoter] = targetSeatIndex;
            feedbackMessage = null;
            UpdateVoteDisplay();
            // Do not leak the other player's secret ballot into the Unity Console.
            if (CanLocalViewerSeeCurrentVotes)
                Debug.Log($"<color=orange>{activeVoter.name} đã Vote cho ghế số {targetSeatIndex}</color>");
        }
        else ShowFeedback("Hãy bỏ phiếu vào một lá còn sống trên bàn.");
    }
    
    // The local viewer is NOT necessarily the current voter: in bot matches and
    // on the online host, the other side may be voting on this very screen.
    public DefaultOwner LocalBallotViewer
    {
        get
        {
            if (OnlineSession.Active) return OnlineSession.LocalRole;
            if (TutorialWorkshop.Active) return TutorialController.Instance.workshop.Viewer;
            if (GameSettings.currentMode == GameMode.PlayerVsVillBot) return DefaultOwner.Player1;
            if (GameSettings.currentMode == GameMode.PlayerVsCurBot) return DefaultOwner.Player2;
            // Local hot-seat PvP has one screen, so its active player is the viewer.
            return currentVotingPlayer;
        }
    }

    public bool CanLocalViewerSeeCurrentVotes =>
        currentTurn == TurnState.SacrificePhase && !isTallyingVotes && !isGameOver &&
        currentVotingPlayer == LocalBallotViewer;

    private void UpdateVoteDisplay(bool showAll = false)
    {
        // Always clear the previous voter's private counts before switching roles.
        foreach (var seat in allSeats)
        {
            if (seat == null || seat.cardPos == null) continue;
            CardInstance card = seat.cardPos.GetComponentInChildren<CardInstance>();
            if (card == null || card.voteText == null) continue;
            card.voteText.text = "";
            card.voteText.color = Color.white;
            card.voteText.gameObject.SetActive(false);
        }

        // Only the actual tally phase is allowed to reveal both players' ballots.
        // showAll by itself is not a privilege escalation.
        bool publicTally = showAll && isTallyingVotes;
        if (!publicTally && !CanLocalViewerSeeCurrentVotes) return;

        DefaultOwner viewer = LocalBallotViewer;
        int[] visibleCounts = new int[allSeats.Count];
        foreach (var vote in cardVotes)
        {
            CardInstance voter = vote.Key;
            int targetSeat = vote.Value;
            if (voter == null || voter.cardData == null || targetSeat < 0 || targetSeat >= visibleCounts.Length)
                continue;
            if (publicTally || voter.cardData.owner == viewer)
                visibleCounts[targetSeat]++;
        }

        for (int i = 0; i < visibleCounts.Length; i++)
        {
            if (visibleCounts[i] == 0) continue;
            var seat = allSeats[i];
            if (seat == null || seat.cardPos == null) continue;
            CardInstance card = seat.cardPos.GetComponentInChildren<CardInstance>();
            if (card == null || card.voteText == null) continue;
            card.voteText.text = visibleCounts[i].ToString();
            card.voteText.gameObject.SetActive(true);
        }
    }

    private void ExecuteSacrificeAndReset()
    {
        StartCoroutine(SacrificeRoutine());
    }

    private IEnumerator SacrificeRoutine()
    {
        if (isGameOver || isTallyingVotes) yield break;
        Debug.Log("<color=black>ĐANG TỔNG KẾT PHIẾU BẦU...</color>");
        isTallyingVotes = true;
        RevealSeatCards(); 
        ForceDisguiseAllCards();
        
        UpdateVoteDisplay(true); 
        int[] initialPresentation = new int[allSeats.Count];
        foreach (var ballot in cardVotes)
            if (ballot.Value >= 0 && ballot.Value < initialPresentation.Length) initialPresentation[ballot.Value]++;
        voteEffects?.BeginTally(initialPresentation);
        Debug.Log("<color=white>Đang hiển thị phiếu bầu gốc từ người chơi...</color>");
        yield return new WaitForSeconds(2.0f);

        int[] originalCounts = new int[allSeats.Count];
        int[] voteCounts = new int[allSeats.Count];

        foreach (var kvp in cardVotes) 
        {
            if (kvp.Value >= 0 && kvp.Value < allSeats.Count)
            {
                voteCounts[kvp.Value]++;
                originalCounts[kvp.Value]++; // Giữ lại một bản để so sánh màu sắc
            }
        }

        // Add/subtract votes first. Trader shares the resulting counts afterwards.
        // Preserve seat order within each stage.
        for (int stage = 0; stage < 2; stage++)
        for (int i = 0; i < allSeats.Count; i++)
        {
            CardInstance ci = allSeats[i].cardPos.GetComponentInChildren<CardInstance>();
            if (IsLivingBoardCard(ci) && ci.cardData.cardSkill != null && !ci.isSilenced &&
                (ci.cardData.cardSkill is Skill_Trader) == (stage == 1))
            {
                int[] before = (int[])voteCounts.Clone();
                int max = 0, occupied = 0;
                if (ci.cardData.cardSkill is Skill_Trader)
                {
                    foreach (int member in new[]{HorizontalNeighbor(i,-1),i,HorizontalNeighbor(i,1)})
                        if (member >= 0 && GetBoardCard(member) != null) { occupied++;max = Mathf.Max(max,before[member]); }
                }
                ci.cardData.cardSkill.TryOnBeforeVoteCalculate(ci, voteCounts, i);
                if (ci.cardData.cardSkill is Skill_Trader)
                    voteEffects?.RecordChange(ci,"Trader",string.Format("Trader divides the highest count ({0}) among {1} occupied seats: {2} votes each. Remainder discarded.",max,occupied,occupied > 0 ? max/occupied : 0),before,voteCounts,new[]{HorizontalNeighbor(i,-1),i,HorizontalNeighbor(i,1)});
                else if (ci.cardData.cardSkill is Skill_Jester)
                    voteEffects?.RecordChange(ci,"Jester","Jester scatters extra votes at random. Only the cards shown in this step receive them.",before,voteCounts);
                else if (ci.cardData.cardSkill is Skill_Snooper)
                    voteEffects?.RecordChange(ci,"Snooper","Snooper adds one vote to its indicated target. If no valid target is available, no vote is added.",before,voteCounts);
            }
        }

        ApplyFinalVotesToUI(voteCounts, originalCounts); 
        
        yield return new WaitForSeconds(Mathf.Max(1.5f, (voteEffects != null ? voteEffects.TallyPresentationSeconds : 0) + .4f));

        // Giải quyết các kỹ năng thay đổi danh sách tử hình theo độ ưu tiên.
        List<int> targetSeats = new List<int>();
        bool isManipulated = false;

        List<CardInstance> activeCards = new List<CardInstance>();
        for (int i = 0; i < allSeats.Count; i++)
        {
            CardInstance ci = allSeats[i].cardPos.GetComponentInChildren<CardInstance>();
            if (ci != null && ci.cardData != null && ci.cardData.cardSkill != null && ci.GetComponent<CardInteraction>().isPlaced && !ci.isSilenced)
            {
                activeCards.Add(ci);
            }
        }
        activeCards.Sort((a, b) => b.cardData.cardSkill.OverridePriority.CompareTo(a.cardData.cardSkill.OverridePriority));

        foreach (CardInstance ci in activeCards)
        {
            int seatIdx = TurnManager.instance.GetSeatIndex(ci.GetComponent<CardInteraction>());
            if (ci.cardData.cardSkill.OnOverrideVoteResult(ci, voteCounts, ref targetSeats, seatIdx))
            {
                isManipulated = true;
                voteEffects?.RecordOverride(ci,targetSeats);
                break;
            }
        }

        // Không có kỹ năng ghi đè thì dùng luật số đông.
        if (!isManipulated)
        {
            int maxVotes = 0;
            for (int i = 0; i < voteCounts.Length; i++) 
            {
                if (voteCounts[i] > maxVotes) 
                { 
                    maxVotes = voteCounts[i]; 
                    targetSeats.Clear(); 
                    targetSeats.Add(i); 
                }
                else if (voteCounts[i] == maxVotes && maxVotes > 0) 
                { 
                    targetSeats.Add(i);
                }
            }
        }

        
        // Chốt danh sách object trước khi cái chết của Game Master làm đổi chỉ số ghế.
        if (isManipulated && voteEffects != null) yield return new WaitForSeconds(.9f);
        voteEffects?.EndCapture();
        List<CardInteraction> cardsToBanish = new List<CardInteraction>();
        foreach (int seatIdx in targetSeats) 
        {
            CardInteraction doomedCard = allSeats[seatIdx].cardPos.GetComponentInChildren<CardInteraction>();
            if (doomedCard != null) 
            {
                cardsToBanish.Add(doomedCard);
            }
        }

        foreach (CardInteraction doomedCard in cardsToBanish)
        {
            BanishCardToCemetery(doomedCard);
        }

        CheckGameOver();
        if (isGameOver) { isTallyingVotes = false; yield break; }

        yield return new WaitForSeconds(1.5f);

        for (int i = allSeats.Count - 1; i >= 0; i--) 
        {
            SeatDropZone seat = allSeats[i];
            
            // Bảo vệ: Nếu ghế này là ghế phụ và vừa bị sụp đổ (Destroy) thì bỏ qua
            if (seat == null) continue; 

            CardInteraction survivor = seat.cardPos.GetComponentInChildren<CardInteraction>();
            if (survivor != null) 
            {
                CardInstance ci = survivor.GetComponent<CardInstance>();
                if (ci != null && ci.voteText != null) 
                {
                    ci.voteText.text = ""; 
                    ci.voteText.color = Color.white; 
                    ci.voteText.gameObject.SetActive(false);
                }
                
                // Duyệt ngược vì trả Game Master về tay có thể xóa các ghế phụ.
                survivor.ForceReturnToHand(); 
            }
        }

        yield return new WaitForSeconds(0.5f);

        firstPlayerThisRound = (firstPlayerThisRound == TurnState.Player1Turn) ? TurnState.Player2Turn : TurnState.Player1Turn;
        
        Skill_Gambler.ResetComboChain();
        ClearAllGamblerMarks();
        ClearAllSnooperMarks();

        CardInstance[] allCardsInGame = FindObjectsByType<CardInstance>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (CardInstance ci in allCardsInGame)
        {
            if (ci != null)
            {
                ci.hasUsedSkill = false;
                if (ci.cardData != null && ci.cardData.cardSkill is Skill_Snooper snooper) snooper.ResetRound();
            }
        }
        
        currentTurn = firstPlayerThisRound;
        // Chỉ nạp đầy tim cho người đi đầu tiên của vòng mới (Bắt đầu Action Phase)
        if (currentTurn == TurnState.Player1Turn) p1Hearts = MAX_HEARTS;
        else if (currentTurn == TurnState.Player2Turn) p2Hearts = MAX_HEARTS;
        CheckGameOver();
        isTallyingVotes = false;
        if (isGameOver) yield break;
        UpdateTurnUI();
        RefreshAllAuras();
    }
    private void ApplyFinalVotesToUI(int[] finalVotes, int[] originalVotes)
    {
        for (int i = 0; i < allSeats.Count; i++)
        {
            CardInstance ci = allSeats[i].cardPos.GetComponentInChildren<CardInstance>();
            if (ci != null && ci.voteText != null)
            {
                if (finalVotes[i] > 0)
                {
                    ci.voteText.gameObject.SetActive(true);
                    ci.voteText.text = finalVotes[i].ToString();

                    ci.voteText.color = (finalVotes[i] != originalVotes[i]) ? Color.yellow : Color.white;
                }
                else
                {
                    ci.voteText.gameObject.SetActive(false);
                }
            }
        }
    }


    private IEnumerator SmoothFlyToCemetery(Transform card, Vector3 targetWorldPos, float duration, float targetScale)
    {
        float elapsed = 0;
        Vector3 startPos = card.position;
        Vector3 startScale = card.localScale;
        Vector3 endScale = new Vector3(targetScale, targetScale, 1f);
        
        Quaternion startRot = card.rotation;
        Quaternion endRot = Quaternion.identity;

        while (elapsed < duration)
        {
            if (card == null) yield break; // Invoker may resurrect it during this flight.
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            
            t = t * t * (3f - 2f * t); 

            card.position = Vector3.Lerp(startPos, targetWorldPos, t);
            card.localScale = Vector3.Lerp(startScale, endScale, t);
            card.rotation = Quaternion.Slerp(startRot, endRot, t);
            
            yield return null;
        }
        
        if (card == null) yield break;
        cemeteryFlights.Remove(card.GetComponent<CardInstance>());
        PlaceCemeteryCard(card.GetComponent<CardInstance>());
        ActivateCemeterySkill(card.GetComponent<CardInstance>());
        RefreshAllAuras();
    }
    private void ActivateCemeterySkill(CardInstance cardInstance)
    {
        // Cemetery skills still execute once, on authoritative landing.
        if (cardInstance != null && cardInstance.cardData != null && cardInstance.cardData.cardSkill != null && 
            cardInstance.cardData.cardSkill.skillType == SkillType.Passive && 
            cardInstance.cardData.cardSkill.triggerTime == PassiveTriggerTime.InCemetery)
        {
            cardInstance.cardData.cardSkill.Activate(cardInstance);
        }
    }

    private void ForceDisguiseAllCards()
    {
        foreach (var seat in allSeats)
        {
            var card = seat.cardPos.GetComponentInChildren<CardInstance>();
            if (card != null) PlayerInformation.Render(card);
        }
    }

    public void StartCurseSelection(CardInstance curInstance)
    {
        if (!TutorialController.CanChooseSource(TutorialAction.Curse, curInstance)) return;
        if (isGameOver || isTallyingVotes || currentTurn != TurnState.CursePhase ||
            curInstance == null || curInstance.isDead || !allCurs.Contains(curInstance)) return;
        ClearCurseTargets();
        RevealSeatCards();
        activeCurInRitual = curInstance; 

        int curIndex = GetSeatIndex(curInstance.GetComponent<CardInteraction>());
        if (curIndex == -1) return;

        validLeftTarget = CalculateInfectionTarget(curIndex, Vector2.left);
        validRightTarget = CalculateInfectionTarget(curIndex, Vector2.right);
        validTopTarget = CalculateInfectionTarget(curIndex, Vector2.up);
        validBottomTarget = CalculateInfectionTarget(curIndex, Vector2.down);
    }
   
    
   private int CalculateInfectionTarget(int currentIndex, Vector2 direction)
    {
        RectTransform originRect = allSeats[currentIndex].GetComponent<RectTransform>();
        Vector2 originPos = originRect.anchoredPosition;

        List<int> seatsInDirection = new List<int>();
        for (int i = 0; i < allSeats.Count; i++)
        {
            if (i == currentIndex) continue;
            RectTransform checkRect = allSeats[i].GetComponent<RectTransform>();
            Vector2 checkPos = checkRect.anchoredPosition;

            // Sai số UI layout khi kiểm tra các ghế cùng hàng hoặc cột.
            if (direction == Vector2.up && Mathf.Abs(checkPos.x - originPos.x) < 5f && checkPos.y > originPos.y + 5f) seatsInDirection.Add(i);
            else if (direction == Vector2.down && Mathf.Abs(checkPos.x - originPos.x) < 5f && checkPos.y < originPos.y - 5f) seatsInDirection.Add(i);
            else if (direction == Vector2.left && Mathf.Abs(checkPos.y - originPos.y) < 5f && checkPos.x < originPos.x - 5f) seatsInDirection.Add(i);
            else if (direction == Vector2.right && Mathf.Abs(checkPos.y - originPos.y) < 5f && checkPos.x > originPos.x + 5f) seatsInDirection.Add(i);
        }

        seatsInDirection.Sort((a, b) => {
            float distA = Vector2.Distance(originPos, allSeats[a].GetComponent<RectTransform>().anchoredPosition);
            float distB = Vector2.Distance(originPos, allSeats[b].GetComponent<RectTransform>().anchoredPosition);
            return distA.CompareTo(distB);
        });

        foreach (int seatIdx in seatsInDirection)
        {
            CardInstance card = allSeats[seatIdx].cardPos.GetComponentInChildren<CardInstance>();
            
            if (card == null) break; // Ghế trống chặn đứng mầm bệnh

            bool isMyVictim = (activeCurInRitual != null && card == activeCurInRitual.currentVictim);
            
            if (card.isInfected && !isMyVictim) continue; // Bỏ qua nếu đã nhiễm để tìm lá tiếp theo
            
            return seatIdx;
        }

        return -1;
    }

    public void ApplyCurse(int seatIndex)
    {
        if (!TutorialController.Allow(TutorialAction.Curse, activeCurInRitual, seat: seatIndex)) return;
        if (isGameOver || isTallyingVotes || currentTurn != TurnState.CursePhase ||
            seatIndex < 0 || seatIndex >= allSeats.Count) return;
        bool isValid = (seatIndex == validLeftTarget || seatIndex == validRightTarget || 
                        seatIndex == validTopTarget || seatIndex == validBottomTarget);

        if (!isValid || activeCurInRitual == null)
        { ActionFeedback.Reject("The curse must follow a valid row or column from your Cursermancer. Empty seats break the path; choose a highlighted target."); return; }

        CardInstance targetCard = allSeats[seatIndex].cardPos.GetComponentInChildren<CardInstance>();
        if (targetCard == null) { ActionFeedback.Reject("Choose a living card, not an empty seat."); return; }

        CardInteraction targetInteraction = targetCard.GetComponent<CardInteraction>();
        if (targetInteraction != null && targetInteraction.isProtectedByPreacher)
        {
            ActionFeedback.Reject("Preacher's shield blocks the curse on this target. Choose an unprotected card.");
            return; 
        }

        // Mỗi Cursermancer chỉ duy trì một mục tiêu; đổi mục tiêu sẽ gỡ bệnh cũ.
        if (activeCurInRitual.currentVictim != null && activeCurInRitual.currentVictim != targetCard)
        {
            activeCurInRitual.currentVictim.isInfected = false;
            activeCurInRitual.currentVictim.FaceUp();
            if (activeCurInRitual.currentVictim.infectionEffect != null) 
                activeCurInRitual.currentVictim.infectionEffect.SetActive(false);
        }

        targetCard.isInfected = true;
        targetCard.FaceUp();
        if (targetCard.infectionEffect != null) 
            targetCard.infectionEffect.SetActive(true);

        activeCurInRitual.currentVictim = targetCard;
        activeCurInRitual.MarkSkillAsUsed(true);
        SkillAudio.Emit("Cursermancer",DefaultOwner.Player1);
        UpdateCurVisuals();
        
        if (!OnlineSession.Active || OnlineSession.LocalCur) Debug.Log("Đã cập nhật lây nhiễm.");
    }
    private void ClearCurseTargets()
    {
        validLeftTarget = -1;
        validRightTarget = -1;
        validTopTarget = -1;
        validBottomTarget = -1;
        activeCurInRitual = null;
    }


    public void ResetHearts()
    {
        p1Hearts = MAX_HEARTS; p2Hearts = MAX_HEARTS;
        UpdateHeartsUI();
    }

  public void ConsumeHearts(int amount)
    {
        ClearLocalActionFeedback();
        if (currentTurn == TurnState.Player1Turn || (currentTurn == TurnState.SacrificePhase && currentVotingPlayer == DefaultOwner.Player1)) 
        {
            p1Hearts = Mathf.Clamp(p1Hearts - amount, 0, MAX_HEARTS);
        }
        else if (currentTurn == TurnState.Player2Turn || (currentTurn == TurnState.SacrificePhase && currentVotingPlayer == DefaultOwner.Player2))
        {
            p2Hearts = Mathf.Clamp(p2Hearts - amount, 0, MAX_HEARTS);
        }
        
        UpdateHeartsUI();
    }

    public bool HasEnoughHearts(int amount)
    {
        if (isGameOver || isTallyingVotes || amount < 0) return false;
        if (currentTurn == TurnState.Player1Turn) return p1Hearts >= amount;
        if (currentTurn == TurnState.Player2Turn) return p2Hearts >= amount;

        // Trong pha hiến tế, trừ tim của currentVotingPlayer.
        if (currentTurn == TurnState.SacrificePhase)
        {
            if (currentVotingPlayer == DefaultOwner.Player1) return p1Hearts >= amount;
            if (currentVotingPlayer == DefaultOwner.Player2) return p2Hearts >= amount;
        }

        return false;
    }

    public void UpdateHeartsUI()
    {
        if (p1EnergyBar != null && !p1EnergyBar.gameObject.activeSelf) 
        {
            p1EnergyBar.gameObject.SetActive(true);
        }

        int currentDisplayHearts = p1Hearts; 

        if (currentTurn == TurnState.Player2Turn || 
           (currentTurn == TurnState.SacrificePhase && currentVotingPlayer == DefaultOwner.Player2))
        {
            currentDisplayHearts = p2Hearts; 
        }

        if (p1EnergyBar != null)
        {
            p1EnergyBar.UpdateEnergyDisplay(OnlineSession.Guest && OnlineGameBridge.Instance != null ? OnlineGameBridge.Instance.DisplayHearts(currentDisplayHearts) : currentDisplayHearts);
        }
    }


    public bool CanStartGame
    {
        get
        {
            if (isGameOver || currentTurn != TurnState.SetupPhase || DeckManager.instance == null ||
                !DeckManager.instance.IsReady) return false;
            foreach (DefaultOwner owner in System.Enum.GetValues(typeof(DefaultOwner)))
            {
                int selected = allCurs.FindAll(c => c != null && !c.isDead && c.cardData != null &&
                    c.cardData.owner == owner).Count;
                if (selected != GameRules.RequiredCursForOwner(GameSettings.currentMode, GameSettings.currentLevel, owner)) return false;
            }
            return true;
        }
    }

    public void SelectCurCard(CardInstance clickedCard)
    {
        if (!TutorialController.Allow(TutorialAction.SelectCur, clickedCard)) return;
        if (OnlineGameBridge.Forward(OnlineAction.SelectCur, clickedCard)) return;
        if (OnlineSession.Active && !OnlineSession.LocalCur && !OnlineGameBridge.ApplyingCommand) return;
        if (isGameOver || currentTurn != TurnState.SetupPhase || clickedCard == null ||
            clickedCard.cardData == null || clickedCard.isDead || DeckManager.instance == null ||
            !DeckManager.instance.IsReady) return;
        if (clickedCard.cardData.cardNumber == 9 || clickedCard.cardData.cardType == CardType.Hunter)
        {
            ShowFeedback("Thợ săn số 9 không thể trở thành Cursermancer.");
            return;
        }

        if (GameSettings.currentMode == GameMode.PvP)
        {
            DefaultOwner cardOwner = clickedCard.cardData.owner;
            if (cardOwner == DefaultOwner.Player1) {
                if (allyCur != null) allyCur.cardImage.sprite = allyCur.cardData.defaultArtwork; 
                allyCur = clickedCard;
            } else if (cardOwner == DefaultOwner.Player2) {
                if (enemyCur != null) enemyCur.cardImage.sprite = enemyCur.cardData.defaultArtwork;
                enemyCur = clickedCard;
            }

            allCurs.Clear();
            if (allyCur != null) allCurs.Add(allyCur);
            if (enemyCur != null) allCurs.Add(enemyCur);

            if (allyCur != null && enemyCur != null && btnStartGame != null) 
            {
                btnStartGame.GetComponent<Button>().interactable = true; 
            }
        }
        else 
        {
            if (allCurs.Contains(clickedCard))
            {
                allCurs.Remove(clickedCard);
            } 
            else
            {
                DefaultOwner owner = clickedCard.cardData.owner;
                int required = GameRules.RequiredCursForOwner(GameSettings.currentMode, GameSettings.currentLevel, owner);
                int selected = allCurs.FindAll(c => c != null && c.cardData.owner == owner).Count;
                if (selected >= required)
                {
                    ShowFeedback($"Nhóm bài này cần {required} Cursermancer. Bấm lại lá đã chọn để bỏ chọn trước khi đổi.");
                    return;
                }
                allCurs.Add(clickedCard);
            }
        }
        feedbackMessage = null;
        if (btnStartGame != null && btnStartGame.TryGetComponent<Button>(out var startButton))
            startButton.interactable = CanStartGame;
        UpdateCurVisuals();
    }

    public void StartRealGame()
    {
        if (!TutorialController.Allow(TutorialAction.StartMatch)) return;
        if (OnlineGameBridge.Forward(OnlineAction.StartGame)) return;
        if (OnlineSession.Active && !OnlineSession.LocalCur && !OnlineGameBridge.ApplyingCommand) return;
        if (!CanStartGame)
        {
            ShowFeedback("Hãy hoàn tất chọn Cursermancer theo từng nhóm bài trước khi bắt đầu.");
            return;
        }
        CardInteraction.ForceUnzoom();
        if (TabManager.instance != null) TabManager.instance.CloseAllTabs();
        if (btnStartGame != null) btnStartGame.SetActive(false);
        if (btnEndTurn != null) btnEndTurn.SetActive(true);
        feedbackMessage = null;
        currentTurn = firstPlayerThisRound; 
        ResetHearts(); UpdateTurnUI();
    }


   public void UpdateCurVisuals()
    {
        if (isGameOver) { RevealAllRolesAtEnd(); return; }
        PlayerInformation.RefreshHostMarks();
        foreach (var card in FindObjectsByType<CardInstance>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            PlayerInformation.Render(card);
    }

    void UpdateTurnUI()
    {
        if (isGameOver) return;
        if (turnText != null) switch (currentTurn) {
            case TurnState.SetupPhase: turnText.text = "SET UP PHASE"; break;
            case TurnState.Player2Turn: turnText.text = "VIL TURN"; break;
            case TurnState.Player1Turn: turnText.text = "CURSERMANCER TURN"; break;
            case TurnState.CursePhase: turnText.text = "CURSERMANCER PHASE"; break;
            case TurnState.SacrificePhase: 
                string roleName = (currentVotingPlayer == DefaultOwner.Player1) ? "CURSERMANCER" : "VILL";
                turnText.text = $"SACRIFI PHASE: {roleName} TURN"; 
                break;
        }
        
        if (btnCheck != null)
        {
            btnCheck.SetActive(true);

            bool isMainActionPhase = (currentTurn == TurnState.Player1Turn || currentTurn == TurnState.Player2Turn);
            int remaining = 0;
            if (currentTurn == TurnState.Player1Turn) remaining = p1Checks;
            else if (currentTurn == TurnState.Player2Turn) remaining = p2Checks;
            bool canUseCheck = isMainActionPhase && (remaining > 0);
            btnCheck.GetComponent<UnityEngine.UI.Button>().interactable = canUseCheck;
            if (checkBtnText != null)
            {
                checkBtnText.text = $"Check ({remaining}/{MAX_CHECKS})";
                checkBtnText.color = canUseCheck ? Color.white : new Color(1f, 1f, 1f, 0.5f);
            }
        }

        UpdateHeartsUI(); 
        UpdateCurVisuals();

        if (DeckManager.instance != null)
        {
            if (currentTurn == TurnState.SetupPhase)
            {
                // Chỉ đổi panel bài; giữ các nút điều khiển bên ngoài panel.
                DeckManager.instance.HideAllHands();
            }
            else if (currentTurn == TurnState.Player1Turn || currentTurn == TurnState.CursePhase)
            {
                DeckManager.instance.SwitchToHand(DefaultOwner.Player1);
            }
            else if (currentTurn == TurnState.Player2Turn)
            {
                DeckManager.instance.SwitchToHand(DefaultOwner.Player2);
            }
            else if (currentTurn == TurnState.SacrificePhase)
            {
                DeckManager.instance.SwitchToHand(currentVotingPlayer);
            }
        }
    }

    [Header("Cemetery View UI")]
    public GameObject cemeteryViewPanel;
    public CemeteryView cemeteryView;
    public Transform CemeteryContent => cemeteryView != null && cemeteryView.content != null ? cemeteryView.content : cemeteryViewPanel != null ? cemeteryViewPanel.transform : null;
    public GameObject canvasDimmer;

    public void ToggleCemeteryView()
    {
        if (!TutorialController.Allow(TutorialAction.Cemetery)) return;
        if (cemeteryViewPanel.activeSelf)
        {
            CloseCemeteryView();
        }
        else
        {
            OpenCemeteryView();
        }
    }

    private void OpenCemeteryView()
    {
        if (TabManager.instance != null) TabManager.instance.CloseAllTabs();
        CardInteraction.ForceUnzoom();

        if (CanvasDimmer.instance != null) 
        {
            CanvasDimmer.instance.Dim();
        }
        
        cemeteryViewPanel.SetActive(true);

        RefreshCemeteryPresentation(true);
        UpdateCurVisuals();
    }

    public void CloseCemeteryView()
    {
        CardInteraction.ForceUnzoom();
        cemeteryViewPanel.SetActive(false);
        
        if (CanvasDimmer.instance != null) 
        {
            CanvasDimmer.instance.Undim(); 
        }

        // Giữ CanvasDimmer hoạt động để coroutine Undim có thể chạy.

        RefreshCemeteryPresentation(true);
        UpdateCurVisuals();
    }
    public void PlaceCemeteryCard(CardInstance card)
    {
        if (card == null || !card.isDead) return;
        var input = card.GetComponent<CardInteraction>();
        if (input != null && input.isInspecting) return;
        bool open = cemeteryViewPanel != null && cemeteryViewPanel.activeSelf;
        Transform destination = open ? CemeteryContent : cemeteryCardPos;
        if (destination == null) return;
        bool changed = card.transform.parent != destination;
        if (changed) card.transform.SetParent(destination, false);
        var rect = card.GetComponent<RectTransform>();
        if (input != null) { input.originalParent = destination; input.enabled = !OnlineSession.Guest; }
        if (card.TryGetComponent<CanvasGroup>(out var group)) { group.interactable = open; group.blocksRaycasts = open; group.alpha = 1; }
        if (card.TryGetComponent<LayoutElement>(out var layout)) layout.ignoreLayout = !open;
        Vector3 prefabCardScale = input != null ? input.PrefabRootScale : Vector3.one;
        rect.localScale = open ? prefabCardScale : Vector3.Scale(prefabCardScale, new Vector3(.6f, .6f, 1));
        rect.localRotation = Quaternion.identity;
        // A visible grid owns anchors and position. Never overwrite them on a snapshot.
        if (!open)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = Vector2.zero;
        }
        cemeteryView?.Refresh();
    }
    public void RefreshCemeteryPresentation(bool finishFlights = false)
    {
        var dead = new List<CardInstance>();
        foreach (var card in FindObjectsByType<CardInstance>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (card.gameObject.scene == gameObject.scene && card.isDead && card.cardData != null &&
                !(discoverPanel != null && card.transform.IsChildOf(discoverPanel.transform))) dead.Add(card);
        dead.Sort((a,b) => a.cemeteryOrder != b.cemeteryOrder ? a.cemeteryOrder.CompareTo(b.cemeteryOrder) :
            a.cardData.cardNumber != b.cardData.cardNumber ? a.cardData.cardNumber.CompareTo(b.cardData.cardNumber) : a.GetInstanceID().CompareTo(b.GetInstanceID()));
        int sibling = 0;
        foreach (var card in dead)
        {
            if (cemeteryFlights.TryGetValue(card, out var flight))
            {
                if (!finishFlights) continue;
                StopCoroutine(flight); cemeteryFlights.Remove(card);
                ActivateCemeterySkill(card);
            }
            PlaceCemeteryCard(card);
            if (!card.GetComponent<CardInteraction>().isInspecting && card.transform.GetSiblingIndex() != sibling)
                card.transform.SetSiblingIndex(sibling);
            sibling++;
        }
        cemeteryView?.Refresh();
    }
    public void HideSeatCards()
    {
        foreach (var seat in allSeats)
        {
            CardInstance ci = seat.cardPos.GetComponentInChildren<CardInstance>();
            if (ci != null)
            {
                ci.FaceDown();
                // Tắt luôn hiệu ứng nhiễm bệnh để không lộ "hào quang"
                if (ci.infectionEffect != null) ci.infectionEffect.SetActive(false);
            }
        }
    }
    public void RevealSeatCards() 
    { 
        foreach (var s in allSeats) 
        {
            CardInstance ci = s.cardPos.GetComponentInChildren<CardInstance>();
            if (ci != null) 
            {
                ci.FaceUp(); 
            }
        }
        UpdateCurVisuals(); 
    }
    public bool IsMeetingFull() { if (allSeats == null || allSeats.Count == 0) return false; foreach (var s in allSeats) if (s.cardPos.childCount == 0) return false; return true; }

    // Nghi thức bắt đầu khi mọi lá còn sống đã ngồi, kể cả khi còn ghế trống.
    // Kiểm tra cả ba tay bài và bỏ qua lá đã chết.
    public bool AreAllLivingCardsPlaced()
    {
        if (allSeats == null || allSeats.Count == 0 || DeckManager.instance == null) return false;
        bool anyLivingOnBoard = false;
        foreach (var seat in allSeats)
        {
            if (seat != null && seat.cardPos != null &&
                seat.cardPos.GetComponentInChildren<CardInstance>() is CardInstance card &&
                !card.isDead && card.cardData != null)
            {
                anyLivingOnBoard = true;
                break;
            }
        }
        if (!anyLivingOnBoard) return false;
        Transform[] hands = { DeckManager.instance.player1Hand,
            DeckManager.instance.player2Hand, DeckManager.instance.neutralHand };
        foreach (Transform hand in hands)
        {
            if (hand == null) return false; // Do not trigger from incomplete setup.
            foreach (Transform child in hand)
            {
                CardInstance card = child.GetComponent<CardInstance>();
                CardInteraction interaction = child.GetComponent<CardInteraction>();
                if (card != null && interaction != null && card.cardData != null &&
                    !card.isDead && !interaction.isPlaced)
                    return false;
            }
        }
        return true;
    }
    public int GetSeatIndex(CardInteraction c) { if (c == null || allSeats == null) return -1; for (int i=0; i<allSeats.Count; i++) if (allSeats[i] != null && allSeats[i].cardPos != null && allSeats[i].cardPos.GetComponentInChildren<CardInteraction>()==c) return i; return -1; }
    public bool IsLivingBoardCard(CardInstance card) => card != null && card.cardData != null && !card.isDead &&
        card.TryGetComponent<CardInteraction>(out var interaction) && interaction.isPlaced && GetSeatIndex(interaction) >= 0;
    public CardInstance GetBoardCard(int index)
    {
        if (allSeats == null || index < 0 || index >= allSeats.Count || allSeats[index] == null || allSeats[index].cardPos == null) return null;
        var card = allSeats[index].cardPos.GetComponentInChildren<CardInstance>();
        return IsLivingBoardCard(card) ? card : null;
    }
    public int HorizontalNeighbor(int index, int direction)
    {
        if (allSeats == null || index < 0 || index >= allSeats.Count || allSeats[index] == null) return -1;
        Vector2 origin = allSeats[index].GetComponent<RectTransform>().anchoredPosition;
        int nearest = -1; float distance = float.MaxValue;
        for (int i = 0; i < allSeats.Count; i++)
        {
            if (i == index || allSeats[i] == null) continue;
            Vector2 delta = allSeats[i].GetComponent<RectTransform>().anchoredPosition - origin;
            float along = delta.x * direction;
            if (Mathf.Abs(delta.y) < 5f && along > 5f && along < distance) { distance = along; nearest = i; }
        }
        return nearest;
    }
    public bool RegisterPranksterReaction(CardInstance source, CardInstance target) =>
        !refreshingAuras || pranksterReactions.Add((source, target));
    private List<CardInstance> BoardCards()
    {
        var cards = new List<CardInstance>();
        for (int i = 0; allSeats != null && i < allSeats.Count; i++) { var card = GetBoardCard(i); if (card != null) cards.Add(card); }
        return cards;
    }
    public CardInstance LatestDeadCard()
    {
        if (deadCardsData.Count == 0) return null;
        var data = deadCardsData[deadCardsData.Count - 1];
        foreach (var card in FindObjectsByType<CardInstance>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (card.isDead && card.cardData == data) return card;
        return null;
    }
    public void RemoveSeat(SeatDropZone seat)
    {
        int index = allSeats.IndexOf(seat);
        if (index < 0) return;
        allSeats.RemoveAt(index);
        foreach (var voter in new List<CardInstance>(cardVotes.Keys))
        {
            if (cardVotes[voter] == index) cardVotes.Remove(voter);
            else if (cardVotes[voter] > index) cardVotes[voter]--;
        }
    }
    private void HighlightPotentialTargets(int l, int r) { }
    public void BanishCardToCemetery(CardInteraction doomedCard)
    {
        if (doomedCard == null) return;
        CardInstance ci = doomedCard.GetComponent<CardInstance>();
        if (ci == null || ci.isDead) return;
        ci.isDead = true; // The card dies now, before its flight animation finishes.
        int deadSeat = GetSeatIndex(doomedCard);
        foreach (var voter in new List<CardInstance>(cardVotes.Keys))
            if (voter == ci || deadSeat >= 0 && cardVotes[voter] == deadSeat) cardVotes.Remove(voter);
        if (activeVoter == ci) activeVoter = null;
        deathCheckPending = true;
        if (ci != null && ci.cardData != null && ci.cardData.cardSkill != null)
        {
            int seatIdx = GetSeatIndex(doomedCard);
            ci.cardData.cardSkill.OnDeath(ci, seatIdx); 
        }
        CardDialogue dialogue = doomedCard.GetComponent<CardDialogue>();
        if (dialogue != null) dialogue.StopChatting();
        
        if (ci != null)
        {
            ci.wasInfectedWhenDied = ci.isInfected;
            ci.isInfected = false;
            
            if (ci.cardData != null)
            {   
                ci.cemeteryOrder = ++nextCemeteryOrder;
                deadCardsData.Add(ci.cardData);
                Debug.Log($"<color=gray>Nghĩa trang ghi nhận: {ci.cardData.cardName} (Đã thanh tẩy mầm bệnh)</color>");
            }
        }

        doomedCard.isPlaced = false;
        doomedCard.lockState = 0;

        Vector3 startWorldPos = doomedCard.transform.position;
        Canvas rootCanvas = doomedCard.GetComponentInParent<Canvas>().rootCanvas;
        doomedCard.transform.SetParent(rootCanvas.transform, true); 
        doomedCard.transform.localScale = doomedCard.PrefabRootScale;
        doomedCard.transform.position = startWorldPos;
        doomedCard.transform.SetAsLastSibling();

        if (cemeteryViewPanel != null && cemeteryViewPanel.activeSelf)
        { PlaceCemeteryCard(ci); ActivateCemeterySkill(ci); RefreshCemeteryPresentation(); }
        else cemeteryFlights[ci] = StartCoroutine(SmoothFlyToCemetery(doomedCard.transform, cemeteryCardPos.position, .35f, 0.6f));
        
        if (ci != null && ci.voteText != null) ci.voteText.gameObject.SetActive(false);
        
        RefreshAllAuras();
        if (currentTurn == TurnState.SacrificePhase && !isTallyingVotes) UpdateVoteDisplay();
        // Resolve once after all simultaneous deaths, including collapsed extra seats.
        // Update checks the pending result after this call stack has finished.
    }

    public void RefreshAllAuras()
    {
        // Movements and seat collapse can request another refresh from inside a skill.
        // Resolve those requests iteratively; never recurse through ExecutePlace/Return.
        aurasDirty = true;
        if (refreshingAuras || boardMutationDepth > 0) return;
        refreshingAuras = true;
        pranksterReactions.Clear();
        try
        {
            while (aurasDirty)
            {
                aurasDirty = false;
                foreach (var interaction in FindObjectsByType<CardInteraction>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                    interaction.ResetAllAuraStates();
                var cards = BoardCards();
                foreach (var card in cards)
                    if (!card.isFaceDown && card.cardData.cardSkill is Skill_Saboteur saboteur)
                        saboteur.ApplyAura(card, GetSeatIndex(card.GetComponent<CardInteraction>()));
                // All shields/locks must be ready before a reactive skill can move anything.
                foreach (var card in cards)
                {
                    if (!IsLivingBoardCard(card) || card.isFaceDown) continue;
                    var skill = card.cardData.cardSkill;
                    if (skill != null && !(skill is Skill_Saboteur) && (!card.isSilenced || skill is Skill_GameMaster))
                        skill.ApplyAura(card, GetSeatIndex(card.GetComponent<CardInteraction>()));
                }
                if (aurasDirty) continue;
                foreach (var card in cards)
                {
                    if (!IsLivingBoardCard(card) || card.isFaceDown || card.isSilenced) continue;
                    var skill = card.cardData.cardSkill;
                    if (skill is Skill_Detective && !card.hasUsedSkill || skill is Skill_Prankster) skill.Activate(card);
                    if (aurasDirty) break; // Rebuild protection after every board mutation.
                }
            }
        }
        finally { refreshingAuras = false; pranksterReactions.Clear(); }
        SkillAudio.Current?.RefreshAuraSounds(this);
        UpdateCurVisuals();
    }
    
    
    private void SetElderProtection(int index)
    {
        CardInteraction neighbor = allSeats[index].cardPos.GetComponentInChildren<CardInteraction>();
        if (neighbor != null) 
        {
            neighbor.isLockedByElder = true;
        }
    }

    public void ExecuteCheck()
    {   
        if (!TutorialController.Allow(TutorialAction.Purge)) return;
        if (OnlineGameBridge.Forward(OnlineAction.Check)) return;
        if (OnlineSession.Active && !OnlineSession.LocalTurn && !OnlineGameBridge.ApplyingCommand) return;
        if (isGameOver || isTallyingVotes) return;
        if (currentTurn != TurnState.Player1Turn && currentTurn != TurnState.Player2Turn) 
        {
            ActionFeedback.Reject("Purge is available only during an action turn. Finish the ritual first.");
            return;
        }
        int currentChecks = (currentTurn == TurnState.Player1Turn || (currentTurn == TurnState.SacrificePhase && currentVotingPlayer == DefaultOwner.Player1)) ? p1Checks : p2Checks;
        if (currentChecks <= 0)
        { ActionFeedback.Reject("You have used both Purges for this match. No Purges remain."); return; }

        if (currentTurn == TurnState.Player1Turn || (currentTurn == TurnState.SacrificePhase && currentVotingPlayer == DefaultOwner.Player1)) p1Checks--;
        else p2Checks--;

        Debug.Log("<color=green>!!! KÍCH HOẠT QUÉT XUYÊN THẤU (CẢ PANEL ẨN) !!!</color>");

        // Thanh trừng tính cả lá nhiễm còn sống trong các tay bài đang ẩn.
        CardInstance[] allCards = FindObjectsByType<CardInstance>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        
        List<CardInstance> infectedList = new List<CardInstance>();

        foreach (CardInstance ci in allCards)
        {
            if (ci != null && ci.isInfected && !ci.isDead &&
                (discoverPanel == null || !ci.transform.IsChildOf(discoverPanel.transform)))
            {
                infectedList.Add(ci);
            }
        }

        bool foundAny = false;
        foreach (CardInstance ci in infectedList)
        {
            PublicPurgeResult?.Invoke(ci);
            foundAny = true;
            Debug.Log($"<color=red>Thanh trừng mục tiêu ẩn: {ci.name} tại {ci.transform.parent.name}</color>");

            // Bật lá ẩn trước khi chạy coroutine bay vào nghĩa trang.
            ci.gameObject.SetActive(true);

            Transform oldParent = ci.transform.parent;
            
            BanishCardToCemetery(ci.GetComponent<CardInteraction>());

            if (oldParent != null && oldParent.name.Contains("Hand") && DeckManager.instance != null)
            {
                DeckManager.instance.ArrangeFanLayout(oldParent);
            }
        }

        if (!foundAny) Debug.Log("Không tìm thấy mầm bệnh nào ẩn nấp!");

        CheckGameOver();
        if (!isGameOver) UpdateTurnUI();
    }

    public bool TryManualActivateInvoker(CardInstance invokerCi)
    {
        if (!IsLivingBoardCard(invokerCi) || invokerCi.isSilenced || invokerCi.isFaceDown ||
            !(invokerCi.cardData.cardSkill is Skill_Invoker) ||
            isGameOver || isTallyingVotes || discoverPanel == null || discoverSlots == null || discoverSlots.Length == 0 ||
            !allSeats[GetSeatIndex(invokerCi.GetComponent<CardInteraction>())].isNextToCemetery) return false;
        if (invokerCi.hasUsedSkill)
        {
            Debug.Log("<color=red>Kỹ năng thất bại: Invoker đã gọi hồn trong lượt này rồi!</color>");
            return false;
        }
        Debug.Log($"<color=white>Lượt bầu chọn: {currentVotingPlayer} | Chủ sở hữu bài: {invokerCi.currentOwner}</color>");

        if (currentTurn != TurnState.SacrificePhase) return false;

        // Quyền dùng Invoker thuộc người đặt lá gần nhất.
        if (invokerCi.lastPlacedBy != currentVotingPlayer)
        {
            Debug.Log("<color=red>Kỹ năng thất bại: Bạn không phải người đã đặt lá bài này!</color>");
            return false;
        }

        if (deadCardsData.Count > 0)
        {
            currentInvokerInstance = invokerCi;
            OpenInvokerDiscoverUI();
            return true;
        }
        return false;
    }
    
    private void OpenInvokerDiscoverUI()
    {
        if (discoverPanel == null || deadCardsData.Count == 0) return;

        discoverPanel.SetActive(!OnlineSession.Active || currentVotingPlayer == OnlineSession.LocalRole);
        
        foreach (Transform slot in discoverSlots)
        {
            foreach (Transform child in slot) Destroy(child.gameObject);
        }

        // Giữ currentOptions khi mở lại bảng để không thể bốc lại lựa chọn miễn phí.
        currentOptions.RemoveAll(data => !deadCardsData.Contains(data));
        if (currentOptions.Count == 0)
        {
            List<CardData> pool = new List<CardData>(deadCardsData);
            int count = Mathf.Min(Mathf.Min(3, discoverSlots.Length), pool.Count);

            for (int i = 0; i < count; i++)
            {
                int randomIndex = Random.Range(0, pool.Count);
                currentOptions.Add(pool[randomIndex]);
                pool.RemoveAt(randomIndex);
            }
        }

        for (int i = 0; i < currentOptions.Count; i++)
        {
            CardData data = currentOptions[i];
            GameObject displayObj = Instantiate(cardPrefab, discoverSlots[i]);
            
            // Keep prefab offset. The discover pop-up enlarges the prefab
            // proportionally instead of overwriting its original scale.
            RectTransform rect = displayObj.GetComponent<RectTransform>();
            rect.localScale = Vector3.Scale(rect.localScale, new Vector3(1.5f, 1.5f, 1f));

            CardInstance displayCi = displayObj.GetComponent<CardInstance>();
            displayCi.cardData = data;

            PlayerInformation.Render(displayCi);

            if (displayObj.TryGetComponent<CardInteraction>(out var cardInteract)) cardInteract.enabled = false;

            int indexInGrave = deadCardsData.IndexOf(data);
            displayObj.AddComponent<UnityEngine.UI.Button>().onClick.AddListener(() => ResolveInvokerSelection(indexInGrave));
        }
    }
    public void ResolveInvokerSelection(int indexInGrave)
    {
        if (OnlineGameBridge.Forward(OnlineAction.Discover, option: indexInGrave)) return;
        if (OnlineSession.Active && !OnlineGameBridge.ApplyingCommand &&
            (!OnlineSession.LocalTurn || !CanChooseOnlineDiscover(indexInGrave, OnlineSession.LocalRole))) return;
        if (!CanChooseOnlineDiscover(indexInGrave, currentVotingPlayer)) return;

        CardData selectedData = deadCardsData[indexInGrave];

        bool isCurCard = false;
        CardInstance savedVictim = null;
        CardInstance oldCur = null;

        foreach (var c in allCurs) 
        {
            if (c != null && c.cardData == selectedData) 
            {
                isCurCard = true;
                savedVictim = c.currentVictim;
                oldCur = c;
                break;
            }
        }

        deadCardsData.RemoveAt(indexInGrave);

        // The original can be in the pile, the expanded cemetery, or still flying.
        foreach (var corpse in FindObjectsByType<CardInstance>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (corpse.isDead && corpse.cardData == selectedData)
            {
                corpse.gameObject.SetActive(false);
                Destroy(corpse.gameObject);
            }
        }

        CardInstance newCi = SpawnCardToHand(selectedData, currentTurn);

        if (isCurCard) 
        {
            if (oldCur != null) allCurs.Remove(oldCur);
            allCurs.Add(newCi);
            newCi.currentVictim = savedVictim;

            if (GameSettings.currentMode == GameMode.PvP)
            {
                if (newCi.cardData.owner == DefaultOwner.Player1) allyCur = newCi;
                else if (newCi.cardData.owner == DefaultOwner.Player2) enemyCur = newCi;
            }
        }

        currentOptions.Clear(); 
        Debug.Log($"<color=cyan>Kẻ gọi hồn đã đưa {selectedData.cardName} từ mộ về tay!</color>");
        
        if (currentInvokerInstance != null) currentInvokerInstance.hasUsedSkill = true; 

        discoverPanel.SetActive(false);
        currentInvokerInstance = null;
        UpdateCurVisuals();
    }
    public CardInstance SpawnCardToHand(CardData data, TurnState turn)
    {
        Transform targetHand;

        if (data.owner == DefaultOwner.Player1)
            targetHand = DeckManager.instance.player1Hand;
        else if (data.owner == DefaultOwner.Player2)
            targetHand = DeckManager.instance.player2Hand;
        else 
            targetHand = DeckManager.instance.neutralHand; 

        GameObject newCard = Instantiate(cardPrefab, targetHand);
        CardInstance ci = newCard.GetComponent<CardInstance>();
        
        ci.cardData = data;
        ci.currentOwner = data.owner; 

        ci.isInfected = false; 
        if (ci.infectionEffect != null) ci.infectionEffect.SetActive(false);
        ci.cardImage.material = ci.normalMaterial; 
        ci.hasUsedSkill = false; 

        ci.UpdateCardUI();
        
        if (DeckManager.instance != null && targetHand != null) 
        {
            DeckManager.instance.SortHand(targetHand); 
            DeckManager.instance.ArrangeFanLayout(targetHand); 
        }

        Debug.Log($"<color=green>Hồi sinh: {data.cardName} đã được thanh tẩy hoàn toàn.</color>");
        return ci;
    }
   public bool IsCur(CardInstance ci)
    {
        if (ci == null || ci.cardData == null) return false;
        foreach (var c in allCurs) {
            if (c != null && ci.cardData == c.cardData) return true;
        }
        return false;
    }
    public int GetVoteTargetOfPlayer(DefaultOwner player)
    {
        var counts = new Dictionary<int, int>();
        int best = -1, most = 0;
        foreach (var kvp in cardVotes)
        {
            if (kvp.Key == null || kvp.Key.cardData.owner != player) continue;
            counts[kvp.Value] = counts.TryGetValue(kvp.Value, out int count) ? count + 1 : 1;
        }
        // Choose among the most-voted seats, retaining the first-vote tie break.
        foreach (var kvp in counts) if (kvp.Value > most) { best = kvp.Key; most = kvp.Value; }
        return best;
    }
    public int GetVoteTarget(CardInstance card) => card != null && cardVotes.TryGetValue(card, out int seat) ? seat : -1;
    public void RefreshTutorialView() { if (TutorialController.Active) UpdateTurnUI(); }

    public void SetOnlineFeedback(string message) { feedbackMessage = message; }
    public CardData[] GetOnlineDiscoverOptions() => currentOptions.ToArray();
    public event System.Action<CardInstance> PublicPurgeResult;
    public bool InvokerDiscoverOpenFor(DefaultOwner viewer) => currentInvokerInstance != null &&
        currentOptions.Count > 0 && currentTurn == TurnState.SacrificePhase && !isGameOver && !isTallyingVotes &&
        currentVotingPlayer == viewer && currentInvokerInstance.lastPlacedBy == viewer && IsLivingBoardCard(currentInvokerInstance);
    public bool CanChooseOnlineDiscover(int index, DefaultOwner role) => currentInvokerInstance != null &&
        currentTurn == TurnState.SacrificePhase && !isGameOver && !isTallyingVotes &&
        currentVotingPlayer == role && currentInvokerInstance.lastPlacedBy == role &&
        IsLivingBoardCard(currentInvokerInstance) && !currentInvokerInstance.isSilenced && !currentInvokerInstance.hasUsedSkill &&
        allSeats[GetSeatIndex(currentInvokerInstance.GetComponent<CardInteraction>())].isNextToCemetery &&
        index >= 0 && index < deadCardsData.Count && currentOptions.Contains(deadCardsData[index]);
    public int[] GetOnlineVoteCounts(DefaultOwner perspective)
    {
        var counts = new int[allSeats.Count];
        foreach (var vote in cardVotes)
            if (vote.Key != null && (isTallyingVotes || currentVotingPlayer == perspective && vote.Key.cardData.owner == perspective) &&
                vote.Value >= 0 && vote.Value < counts.Length) counts[vote.Value]++;
        return counts;
    }

    public void ClearAllGamblerMarks()
    {
        CardInstance[] allCards = FindObjectsByType<CardInstance>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (CardInstance ci in allCards)
        {
            if (ci != null && ci.gamblerMark != null) ci.gamblerMark.SetActive(false);
        }
    }

    public void ClearAllSnooperMarks()
    {
        CardInstance[] allCards = FindObjectsByType<CardInstance>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (CardInstance ci in allCards)
        {
            if (ci != null && ci.snooperMark != null) 
            {
                ci.snooperMark.SetActive(false);
            }
        }
    }

    public void CheckGameOver()
    {
        // A short prepared exercise ends at its own learning goal, not at match victory.
        if (TutorialWorkshop.Active) return;
        if (isGameOver || currentTurn == TurnState.SetupPhase) return;
        CardInstance[] allCards = FindObjectsByType<CardInstance>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        int curTeamPower = 0;       
        int villageTeamCount = 0;   

        foreach (CardInstance ci in allCards)
        {
            if (ci == null || ci.isDead) continue;
            if (ci.gameObject.scene.buildIndex == -1) continue;

            // Bản sao trong bảng Invoker không được tính vào số lá còn sống.
            if (discoverPanel != null && ci.transform.IsChildOf(discoverPanel.transform)) continue;

            if (ci.transform.parent == cemeteryCardPos || ci.transform.parent == globalCemetery) continue;

            // Dựa vào owner để nhận diện bài trung lập, tránh so khớp tên.
            if (ci.cardData == null) continue;

            
            if (allCurs.Contains(ci))
            {
                curTeamPower++;
            }
            else if (ci.cardData.owner == DefaultOwner.Player2)
            {
                villageTeamCount++;
            }
        }


        GameOutcome outcome = GameRules.EvaluateWinner(curTeamPower, villageTeamCount);
        if (outcome == GameOutcome.VillagersWin)
        {
            DeclareWinner("PHE DÂN LÀNG THẮNG! (Lời nguyền đã bị dập tắt)");
        }
        else if (outcome == GameOutcome.CursWin)
        {
            DeclareWinner("PHE CURSERMANCER THẮNG! (Bóng tối bao trùm ngôi làng)");
        }
    }
    public bool isGameOver = false;

    private void DeclareWinner(string message)
    {
        if (isGameOver) return;
        isGameOver = true; 
        if (winPanel != null) winPanel.SetActive(true);
        if (winText != null) winText.text = message;
        
        if (btnEndTurn != null) btnEndTurn.SetActive(false);
        if (btnCheck != null) btnCheck.SetActive(false);
        if (villagerBot != null) villagerBot.StopAllCoroutines();
        if (curBot != null) curBot.StopAllCoroutines();
        RevealAllRolesAtEnd();
        Debug.Log($"<color=gold>TRẬN ĐẤU KẾT THÚC: {message}</color>");

        bool wonAgainstBot =
            (message.Contains("DÂN LÀNG THẮNG") && GameSettings.currentMode == GameMode.PlayerVsCurBot) ||
            (message.Contains("CURSERMANCER THẮNG") && GameSettings.currentMode == GameMode.PlayerVsVillBot);
        if (wonAgainstBot)
        {
            GameSettings.currentLevel = GameRules.ClampLevel(GameSettings.currentLevel + 1);
            Debug.Log($"<color=green>LÊN CẤP! BẠN ĐÃ ĐẠT LEVEL {GameSettings.currentLevel}</color>");
            
            // The result screen lets the player choose when to continue.
        }
    }

    public void RevealAllRolesAtEnd()
    {
        CardInstance[] allCards = FindObjectsByType<CardInstance>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (CardInstance ci in allCards)
        {
            if (ci == null || ci.cardData == null || ci.cardImage == null) continue;
            
            ci.FaceUp();
            if (allCurs.Contains(ci))
            {
                ci.cardImage.sprite = ci.cardData.cursemancerArtwork;
            }
        }
    }
}

using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

// Controls the saved menu and board UI.
[DisallowMultipleComponent]
public class GameStartUI : MonoBehaviour
{
    [Header("Scene and theme")]
    [SerializeField] private GameUITheme theme;
    [SerializeField] private MainMenu menuController;
    [SerializeField] private TurnManager turns;
    [SerializeField] private DeckManager deck;
    [SerializeField] private RectTransform canvasRoot;
    [SerializeField] private RectTransform safeFrame;
    [SerializeField] private Image background;
    [SerializeField] private GameBoardLayout boardLayout;
    [SerializeField] private RectTransform boardArea;

    [Header("Live status labels — edit their RectTransform / Text in the scene")]
    [SerializeField] private Text phaseLabel;
    [SerializeField] private Text instruction;
    [SerializeField] private Text setupProgress;
    [SerializeField] private Text hint;
    [SerializeField] private Text endLabel;
    [SerializeField] private Text checkLabel;
    [SerializeField] private Text revealLabel;
    [SerializeField] private Text resultLabel;
    [SerializeField] private Text levelLabel;

    [Header("Panels — all are saved objects in the Hierarchy")]
    [SerializeField] private GameObject rulesPanel;
    [SerializeField] private GameObject setupPanel;
    [SerializeField] private GameObject inputShield;
    [SerializeField] private GameObject resultPanel;
    [SerializeField] private GameObject helpButton;
    [SerializeField] private GameObject revealControl;
    [SerializeField] private Button[] levelButtons;
    [SerializeField] private Button[] draftButtons;
    [Header("Online — saved scene references")]
    [SerializeField] private OnlineLobbyView onlineLobby;
    [SerializeField] private GameObject menuButton;
    [SerializeField] private GameObject swapRoleButton;
    [SerializeField] private Text rulesHint;
    [SerializeField] private RectTransform handDock;

    [Header("Optional theme application — only changed by Apply Theme")]
    [SerializeField] private Image[] panelImages;
    [SerializeField] private Image[] buttonImages;
    [SerializeField] private Image[] portraitImages;
    [SerializeField] private bool adaptToSafeArea = true;

    private bool initialized;
    [Header("Action feedback")]
    [SerializeField] private Color actionFeedbackColor = new Color(1f, .83f, .5f, 1f);
    private Color instructionBaseColor;
    private bool rulesWasOpen;
    private bool menuWasVisibleBeforeRules;
    private float previousTimeScale = 1;
    private int screenWidth, screenHeight;
    private Rect lastSafeArea;
    private Vector2 prefabSafeFramePosition;
    private Vector3 prefabSafeFrameScale = Vector3.one;
    public static GameStartUI Current { get; private set; }
    public static GameUITheme CurrentTheme => Current != null ? Current.theme : null;
    public static bool IsBlockingInput => Current != null && Current.rulesPanel != null && Current.rulesPanel.activeInHierarchy;
    public GameUITheme Theme => theme;
    public GameObject RulesPanel => rulesPanel;
    public GameObject ResultPanel => resultPanel;
    public GameObject DraftPanel => deck != null ? deck.draftPanel : null;
    public GameObject OnlinePanel => onlineLobby != null ? onlineLobby.gameObject : null;
    public Image BackgroundImage => background;

    void Awake()
    {
        Current = this;
        if (safeFrame != null)
        {
            prefabSafeFramePosition = safeFrame.anchoredPosition;
            prefabSafeFrameScale = safeFrame.localScale;
        }
    }
    void Start() { Initialize(); }

    public static void CreateMenu(MainMenu menu)
    {
        var view = FindView(menu.gameObject.scene);
        if (view == null) return;
        view.menuController = menu;
        view.Initialize();
    }

    public static void CreateBoard(TurnManager manager)
    {
        var view = FindView(manager.gameObject.scene);
        if (view == null) return;
        view.turns = manager;
        view.Initialize();
    }

    private static GameStartUI FindView(Scene scene)
    {
        foreach (var view in FindObjectsByType<GameStartUI>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (view.gameObject.scene == scene) return view;
        Debug.LogError("Thereasm: the scene is missing its saved UI Controller. Open MenuScenes or MainBoard with the authored UI Canvas.");
        return null;
    }

    private void Initialize()
    {
        if (initialized) return;
        initialized = true;
        if (instruction != null) instructionBaseColor = instruction.color;
        Current = this;
        SetVisible(rulesPanel, false);
        SetVisible(resultPanel, false);
        SetVisible(inputShield, false);
        if (deck != null)
        {
            deck.UseDockNavigation = true;
            deck.uiTheme = theme;
        }
        if (boardLayout != null && turns != null) boardLayout.Initialize(turns, boardArea);
        if (menuController != null) UpdateLevel();
    }

    void LateUpdate()
    {
        if (adaptToSafeArea) FitSafeFrame();
        if (turns == null) return;
        if (menuButton != null && menuButton.TryGetComponent<Canvas>(out var navigationCanvas))
            navigationCanvas.overrideSorting = !rulesWasOpen && OnlineSession.Active &&
                OnlineSession.Instance != null && OnlineSession.Instance.PeerLost;
        bool draft = deck != null && !deck.IsReady;
        if (instruction != null) instruction.color = instructionBaseColor;
        bool setup = turns.currentTurn == TurnState.SetupPhase;
        bool bot = turns.IsBotTurn && !turns.isGameOver;
        bool onlineBlocked = OnlineSession.Active && !turns.isGameOver &&
            (OnlineSession.Instance.PeerLost || (draft ? OnlineSession.LocalRole != deck.DraftChooser : !OnlineSession.LocalTurn));
        if (CanvasDimmer.instance != null) CanvasDimmer.instance.transform.SetAsLastSibling();
        RaiseOverlay(deck != null ? deck.draftPanel : null);
        RaiseOverlay(turns.discoverPanel);
        RaiseOverlay(turns.cemeteryViewPanel);
        RaiseOverlay(turns.notePanel);
        bool syncing = OnlineSession.Guest && (OnlineGameBridge.Instance == null || !OnlineGameBridge.Instance.HasSnapshot);
        SetVisible(inputShield, bot && !draft || syncing || OnlineSession.Active && OnlineSession.Instance.PeerLost);
        if (bot && !draft || syncing || OnlineSession.Active && OnlineSession.Instance.PeerLost)
        {
            if (inputShield != null) inputShield.transform.SetAsLastSibling();
            if (helpButton != null) helpButton.transform.SetAsLastSibling();
            if (!rulesWasOpen && menuButton != null) menuButton.transform.SetAsLastSibling();
        }
        SetVisible(setupPanel, setup && !draft && !bot && !turns.isGameOver && (!OnlineSession.Active || OnlineSession.LocalCur));
        SetVisible(revealControl, false);
        SetVisible(swapRoleButton, OnlineSession.Active && turns.isGameOver);
        SetVisible(resultPanel, turns.isGameOver);
        if (rulesWasOpen)
        {
            // Menu has its own high-priority Canvas during online disconnects.
            // A modal rulebook must never have an active Menu canvas over it.
            SetVisible(menuButton, false);
            RaiseOverlay(rulesPanel);
        }
        // Start Match uses the same first row during setup; action controls take
        // that row once play begins, with no overlapping buttons or empty slot.
        SetVisible(turns.btnCheck, !setup && !draft && !turns.isGameOver);
        if (turns.isGameOver)
        {
            RaiseOverlay(resultPanel);
            if (rulesWasOpen) RaiseOverlay(rulesPanel);
            SetText(phaseLabel, "VÁN CHƠI ĐÃ KẾT THÚC");
            SetText(instruction, "Vai trò đã được công khai. Chọn Chơi tiếp khi bạn sẵn sàng.");
            SetText(resultLabel, (turns.winText != null ? turns.winText.text : "Ván chơi kết thúc") +
                (OnlineSession.Active ? "\n\nPhe ván sau: " + (OnlineSession.LocalCur ? "Cursermancer" : "Dân làng") :
                GameSettings.currentMode == GameMode.PvP ? "" : $"\n\nChơi tiếp ở cấp {GameSettings.currentLevel}"));
            SetText(hint, "");
            return;
        }
        if (draft)
        {
            SetText(phaseLabel, "01  /  CHỌN BÀI TRUNG LẬP");
            SetText(instruction, deck.DraftProgress);
            if (draftButtons != null) foreach (var button in draftButtons)
                if (button != null) button.interactable = deck.CanPickDraft;
        }
        else if (setup)
        {
            bool canStart = OnlineSession.Guest && OnlineGameBridge.Instance != null ? OnlineGameBridge.Instance.CanStartGame : turns.CanStartGame;
            SetText(phaseLabel, bot ? "02  /  BOT ĐANG CHỌN CURSERMANCER BÍ MẬT" : "02  /  CHUẨN BỊ VAI TRÒ");
            SetText(instruction, bot ? "Bạn điều khiển Dân làng. Vai trò Cursermancer được giữ kín trong trận." :
                canStart ? "Đã đủ Cursermancer. Bấm Bắt đầu ván để vào lượt hành động." :
                "Chọn đủ Cursermancer ở từng nhóm bài, theo danh sách bên trái.");
            SetText(setupProgress, SetupChecklist());
            if (turns.btnStartGame != null && turns.btnStartGame.TryGetComponent<Button>(out var start))
                start.interactable = canStart && (!OnlineSession.Active || OnlineSession.LocalTurn) &&
                    (!OnlineSession.Guest || OnlineGameBridge.Instance != null && !OnlineGameBridge.Instance.CommandPending);
        }
        else if (turns.isTallyingVotes)
        {
            SetText(phaseLabel, "ĐANG TỔNG KẾT PHIẾU");
            SetText(instruction, "Áp dụng kỹ năng, hiến tế và chuẩn bị vòng tiếp theo…");
        }
        else if (turns.currentTurn == TurnState.SacrificePhase)
        {
            turns.GetVotingProgress(out int voted, out int required);
            if (OnlineSession.Guest && OnlineGameBridge.Instance != null)
            { voted = OnlineGameBridge.Instance.Voted; required = OnlineGameBridge.Instance.RequiredVotes; }
            SetText(phaseLabel, "HIẾN TẾ · " + (turns.currentVotingPlayer == DefaultOwner.Player1 ? "PHE CURSERMANCER" : "PHE DÂN LÀNG"));
            // A human must not see the bot/opponent's voting progress or votes.
            SetText(instruction, turns.CanLocalViewerSeeCurrentVotes ?
                $"Đã bỏ phiếu {voted}/{required} · Kéo từ mỗi lá của bạn tới một lá khác trên bàn." :
                "Đối thủ đang bỏ phiếu bí mật. Phiếu sẽ được công khai khi tổng kết.");
        }
        else if (turns.currentTurn == TurnState.CursePhase)
        {
            SetText(phaseLabel, "NGHI THỨC NGUYỀN RỦA");
            SetText(instruction, "Kéo từ Cursermancer trên bàn tới mục tiêu cùng hàng hoặc cột. Ghế trống chặn lây nhiễm.");
        }
        else
        {
            bool p1 = turns.currentTurn == TurnState.Player1Turn;
            SetText(phaseLabel, p1 ? "LƯỢT PHE CURSERMANCER" : "LƯỢT PHE DÂN LÀNG");
            SetText(instruction, "Đặt bài mình: 2 tim · Bài đối phương / trung lập: 5 tim · Kỹ năng có chi phí riêng.");
        }
        SetText(hint, bot && !draft ? "Bot đang hành động… Mở Luật chơi để tạm dừng." :
            "Chuột phải: xem bài\nESC: mở / đóng luật chơi");
        if (!string.IsNullOrEmpty(turns.GetOnlineFeedback(OnlineSession.LocalRole)))
        {
            SetText(instruction, turns.GetOnlineFeedback(OnlineSession.LocalRole));
            if (instruction != null) instruction.color = actionFeedbackColor;
            SetText(hint, "Thực hiện theo hướng dẫn phía trên.");
        }
        SetText(endLabel, turns.isTallyingVotes ? "Đang tổng kết…" :
            turns.currentTurn == TurnState.SacrificePhase ? "Chốt phiếu" :
            turns.currentTurn == TurnState.CursePhase ? "Sang hiến tế" : "Kết thúc lượt");
        if (turns.btnEndTurn != null && turns.btnEndTurn.TryGetComponent<Button>(out var end)) end.interactable = !turns.isTallyingVotes && (!OnlineSession.Active || OnlineSession.LocalTurn);
        int checks = turns.currentTurn == TurnState.Player1Turn ? turns.p1Checks : turns.currentTurn == TurnState.Player2Turn ? turns.p2Checks : 0;
        SetText(checkLabel, $"Thanh trừng ({checks}/2)");
        if (onlineBlocked)
        {
            SetText(hint, OnlineSession.Instance.PeerLost ? OnlineSession.Instance.Status : "Đang chờ đối phương…");
            if (draft) SetText(instruction, deck.DraftProgress + " · Đang chờ đối phương chọn.");
            else if (setup && !OnlineSession.LocalCur) SetText(instruction, "Người giữ phe Cursermancer đang chọn vai trò bí mật.");
        }
        if (OnlineSession.Guest && OnlineGameBridge.Instance != null && !string.IsNullOrEmpty(OnlineGameBridge.Instance.LocalFeedback))
        {
            SetText(instruction, OnlineGameBridge.Instance.LocalFeedback);
            if (instruction != null) instruction.color = actionFeedbackColor;
        }
        if (!string.IsNullOrEmpty(turns.LocalActionFeedback))
        {
            SetText(instruction, turns.LocalActionFeedback);
            if (instruction != null) instruction.color = actionFeedbackColor;
        }
    }

    private void FitSafeFrame()
    {
        if (canvasRoot == null || safeFrame == null) return;
        if (screenWidth == Screen.width && screenHeight == Screen.height && lastSafeArea == Screen.safeArea) return;
        screenWidth = Screen.width;
        screenHeight = Screen.height;
        lastSafeArea = Screen.safeArea;
        Canvas.ForceUpdateCanvases();
        Vector2 available = canvasRoot.rect.size;
        float width = Mathf.Max(1, safeFrame.rect.width);
        float height = Mathf.Max(1, safeFrame.rect.height);
        float safeWidth = available.x * lastSafeArea.width / Mathf.Max(1, Screen.width);
        float safeHeight = available.y * lastSafeArea.height / Mathf.Max(1, Screen.height);
        safeFrame.localScale = prefabSafeFrameScale * Mathf.Min(1, safeWidth / width, safeHeight / height);
        safeFrame.anchoredPosition = prefabSafeFramePosition + new Vector2(
            (lastSafeArea.center.x / Mathf.Max(1, Screen.width) - .5f) * available.x,
            (lastSafeArea.center.y / Mathf.Max(1, Screen.height) - .5f) * available.y);
    }

    private string SetupChecklist()
    {
        string result = "";
        foreach (DefaultOwner owner in System.Enum.GetValues(typeof(DefaultOwner)))
        {
            int need = GameRules.RequiredCursForOwner(GameSettings.currentMode, GameSettings.currentLevel, owner);
            int have = turns.allCurs.FindAll(c => c != null && c.cardData != null && c.cardData.owner == owner).Count;
            string name = owner == DefaultOwner.Player1 ? "Phe Cursermancer" : owner == DefaultOwner.Player2 ? "Dân làng" : "Trung lập";
            result += $"{name}:  {have}/{need}\n";
        }
        return result;
    }

    public void SetLevel(int level) { GameSettings.currentLevel = GameRules.ClampLevel(level); UpdateLevel(); }
    private void UpdateLevel()
    {
        int level = GameRules.ClampLevel(GameSettings.currentLevel);
        if (levelButtons != null) for (int i = 0; i < levelButtons.Length; i++)
            if (levelButtons[i] != null && levelButtons[i].targetGraphic != null)
                levelButtons[i].targetGraphic.color = i + 1 == level ? new Color(1, .78f, .44f) : Color.white;
        SetText(levelLabel, $"Đấu máy · Cấp {level} · {level} Cursermancer\nOnline luôn có 2 Cursermancer; cấp độ chỉ áp dụng khi đấu máy.");
    }

    public void ShowRules()
    {
        if (rulesPanel == null || rulesWasOpen) return;
        if (CardInteraction.currentlyInspectedCard != null) CardInteraction.currentlyInspectedCard.FastUnzoom();
        previousTimeScale = Time.timeScale;
        rulesWasOpen = true;
        if (!OnlineSession.Active) Time.timeScale = 0;
        SetText(rulesHint, OnlineSession.Active ? "Cuộn để xem đầy đủ · Trận online vẫn tiếp tục" : "Cuộn để xem đầy đủ · Trận đấu đang tạm dừng");
        menuWasVisibleBeforeRules = menuButton != null && menuButton.activeSelf;
        SetVisible(menuButton, false);
        rulesPanel.SetActive(true);
        RaiseOverlay(rulesPanel);
    }
    public void CloseRules()
    {
        if (!rulesWasOpen) return;
        rulesWasOpen = false;
        SetVisible(rulesPanel, false);
        if (menuButton != null && (menuWasVisibleBeforeRules ||
            OnlineSession.Active && OnlineSession.Instance != null && OnlineSession.Instance.PeerLost))
            SetVisible(menuButton, true);
        Time.timeScale = previousTimeScale;
    }
    public void ReturnToMenu() { CloseRules(); if (OnlineSession.Instance != null) OnlineSession.Instance.Leave(); Time.timeScale = 1; SceneManager.LoadScene("MenuScenes"); }
    // Network callbacks can run before LateUpdate. Exit must be raycastable in that frame too.
    public void EnsureMenuAccessible()
    {
        // Preserve the rulebook as the topmost modal; closing it restores Menu.
        if (menuButton == null || rulesWasOpen) return;
        menuButton.SetActive(true);
        if (menuButton.TryGetComponent<Canvas>(out var navigationCanvas)) navigationCanvas.overrideSorting = true;
        menuButton.transform.SetAsLastSibling();
        Canvas.ForceUpdateCanvases();
    }
    public void NextMatch()
    {
        CloseRules();
        if (OnlineSession.Active)
        {
            if (OnlineSession.Instance.IsHost) OnlineSession.Instance.StartMatch();
            else if (turns != null) turns.ShowFeedback("Chờ chủ phòng bắt đầu ván tiếp theo.");
            return;
        }
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }
    public void OpenOnline() { if (onlineLobby != null) onlineLobby.Open(); }
    public void SwapOnlineRoles() { if (OnlineSession.Instance != null) OnlineSession.Instance.SwapRoles(); }
    public bool IsOverHand(Vector2 screenPoint) => handDock != null && RectTransformUtility.RectangleContainsScreenPoint(handDock, screenPoint);
    public void ShowCurHand() { if (deck != null) deck.SwitchToHand(DefaultOwner.Player1); }
    public void ShowVillageHand() { if (TutorialController.Allow(TutorialAction.VillageHand) && deck != null) deck.SwitchToHand(DefaultOwner.Player2); }
    public void ShowNeutralHand() { if (deck != null) deck.SwitchToHand(DefaultOwner.Neutral); }
    public void ToggleHand() { if (deck != null) deck.ToggleCurrentHand(); }
    public void PickDraftLeft() { PickDraft(true); }
    public void PickDraftRight() { PickDraft(false); }
    private void PickDraft(bool left)
    {
        if (deck == null) return;
        var slot = left ? deck.draftSlotLeft : deck.draftSlotRight;
        var card = slot != null ? slot.GetComponentInChildren<CardInstance>() : null;
        if (card != null) deck.PickNeutralCard(card.cardData, left);
    }

    [ContextMenu("Apply Theme To Saved Images")]
    public void ApplyTheme()
    {
        if (theme == null) return;
        if (background != null)
        {
            background.sprite = turns != null ? theme.boardBackground : theme.menuBackground;
            background.color = theme.backgroundTint;
            if (background.sprite != null && background.TryGetComponent<AspectRatioFitter>(out var fit))
                fit.aspectRatio = background.sprite.rect.width / background.sprite.rect.height;
        }
        if (panelImages != null) foreach (var image in panelImages) GameUIAssets.Panel(image, theme);
        if (buttonImages != null) foreach (var image in buttonImages)
        {
            if (image == null) continue;
            image.sprite = theme.buttonArtwork;
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = theme.buttonPixelsPerUnit;
        }
        var portraits = new[] { theme.villagerModeIcon, theme.curModeIcon, theme.onlineModeIcon };
        if (portraitImages != null) for (int i = 0; i < portraitImages.Length && i < portraits.Length; i++)
            if (portraitImages[i] != null) portraitImages[i].sprite = portraits[i];
        foreach (var text in GetComponentsInChildren<Text>(true))
        {
            var font = text.gameObject.name == "Game Title" ? theme.titleFont : theme.bodyFont;
            if (font != null) text.font = font;
        }
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        { if(TutorialAcademyUI.Current!=null&&TutorialAcademyUI.Current.HandleEscape())return;
          if (rulesWasOpen) CloseRules(); else if (onlineLobby != null && onlineLobby.IsOpen) onlineLobby.Close(); else ShowRules(); }
    }
    void OnDisable() { CloseRules(); if (Current == this) Current = null; }
    private static void SetText(Text text, string value) { value = Localizer.T(value); if (text != null && text.text != value) text.text = value; }
    private static void SetVisible(GameObject target, bool visible) { if (target != null && target.activeSelf != visible) target.SetActive(visible); }
    private static void RaiseOverlay(GameObject target) { if (target != null && target.activeSelf) target.transform.SetAsLastSibling(); }
}

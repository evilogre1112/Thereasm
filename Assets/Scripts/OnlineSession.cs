using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public partial class OnlineSession : MonoBehaviour
{
    [SerializeField] private PhotonOnlineConfig settings;
    public static OnlineSession Instance { get; private set; }
    public static bool Active => Instance != null && Instance.Connected && Instance.MatchStarted;
    public static bool Guest => Active && !Instance.IsHost;
    public static DefaultOwner LocalRole => Instance != null ? Instance.localRole : DefaultOwner.Player1;
    public static bool LocalCur => Active && LocalRole == DefaultOwner.Player1;
    public static bool LocalTurn
    {
        get
        {
            if (!Active) return true;
            var tm = TurnManager.instance;
            if (tm == null || Instance.PeerLost || tm.isGameOver || tm.isTallyingVotes) return false;
            if (DeckManager.instance != null && !DeckManager.instance.IsReady)
                return DeckManager.instance.DraftChooser == LocalRole;
            if (Guest && (OnlineGameBridge.Instance == null || !OnlineGameBridge.Instance.HasSnapshot)) return false;
            if (tm.currentTurn == TurnState.SetupPhase || tm.currentTurn == TurnState.CursePhase) return LocalCur;
            if (tm.currentTurn == TurnState.SacrificePhase) return tm.currentVotingPlayer == LocalRole;
            return tm.currentTurn == TurnState.Player1Turn ? LocalCur : !LocalCur;
        }
    }
    public bool Connected { get; private set; }
    public bool IsHost { get; private set; }
    public bool Busy { get; private set; }
    public bool MatchStarted { get; private set; }
    public bool PeerLost { get; private set; }
    public int PlayerCount { get; private set; }
    public string RoomCode { get; private set; }
    public string MatchId { get; private set; }
    public string Status { get; private set; } = "Chọn phe, tạo phòng hoặc nhập mã phòng của bạn.";
    private DefaultOwner localRole = DefaultOwner.Player1;
    private bool explicitRoleChoice;
    private DefaultOwner preferredRole = DefaultOwner.Player1;
    private int roleRevision, acknowledgedRoleRevision;
    private float nextLobbySend;
    public bool RolesSynchronized => Connected && PlayerCount == 2 && roleRevision > 0 &&
        (!IsHost || acknowledgedRoleRevision >= roleRevision);
    [Serializable] private class LobbyState
    { public int revision, hostRole, players; public bool started; public string match; }
    // The host issues one match id; Fusion synchronizes the scene independently.
    // A client scene may be instantiated before its scene callback finishes.
    private bool clientScenePrepared;
    private string lastBoardMatch;
    private readonly HashSet<string> retiredMatches = new HashSet<string>();
    public void ConfirmBoardMatch(string match)
    {
        if (!IsHost && Connected && match == MatchId && !retiredMatches.Contains(match)) lastBoardMatch = match;
    }
    public DefaultOwner RemoteRole => OnlineRules.OtherRole(localRole);
    public PhotonOnlineConfig Settings => settings;
    private void ReportPeerLoss(string message, bool force = false)
    {
        Busy = false;
        PeerLost = MatchStarted || force;
        Status = message;
        if (PeerLost) GameStartUI.Current?.EnsureMenuAccessible();
    }
    public event Action<string, string> HostMessage;
    public event Action<string, string> GuestMessage;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        SceneManager.sceneLoaded += OnSceneLoaded;
    }
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!Connected || settings == null || scene.name != settings.boardScene) return;
        EnsureBoardSessionReady(scene.name);
        Debug.Log($"[Thereasm Online] {(IsHost ? "Host" : "Client")} Unity sceneLoaded: {scene.name}, match={MatchId}");

        // Gameplay snapshots are produced by OnlineGameBridge, NOT by Fusion's scene loading.
        // Repair old MainBoard scenes that do not have the bridge component attached.
        bool bridgePresent = false;
        foreach (var bridge in FindObjectsByType<OnlineGameBridge>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (bridge.gameObject.scene == scene)
            {
                bridge.EnsureInitialized();
                bridgePresent = true;
                break;
            }
        if (bridgePresent) return;

        TurnManager boardTurns = null;
        foreach (var candidate in FindObjectsByType<TurnManager>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (candidate.gameObject.scene == scene) { boardTurns = candidate; break; }
        if (boardTurns != null)
        {
            Debug.LogWarning("[Thereasm Online] MainBoard thiếu OnlineGameBridge. Tự thêm component để đồng bộ bài.");
            boardTurns.gameObject.AddComponent<OnlineGameBridge>();
        }
        else Debug.LogError("[Thereasm Online] MainBoard thiếu TurnManager; không thể khởi tạo OnlineGameBridge.");
    }

    // Can be called from a board component's Awake, before OnSceneLoadStart / sceneLoaded.
    // Never treat a regular offline scene as an online match.
    public void EnsureBoardSessionReady(string sceneName)
    {
        if (!Connected || PeerLost || settings == null || sceneName != settings.boardScene ||
            MatchStarted && !string.IsNullOrEmpty(MatchId)) return;
        if (!IsHost) ClientPrepareForSceneTransition();
    }
    public void CreateRoom(string code) { Connect(true, code); }
    public void JoinRoom(string code) { Connect(false, code); }
    private void Connect(bool host, string code)
    {
        if (Busy || Connected) return;
        if (settings == null || !Guid.TryParse(settings.appIdFusion, out _)) { Status = "Thiếu hoặc sai Photon Fusion App ID trong config."; return; }
        if (!OnlineRules.TryRoomCode(code, out var normalized)) { Status = "Mã phòng gồm 4–20 ký tự A–Z, 0–9 hoặc dấu -."; return; }
        RoomCode = normalized;
        roleRevision = host ? 1 : 0;
        acknowledgedRoleRevision = 0;
        if (host) localRole = explicitRoleChoice ? preferredRole : DefaultOwner.Player1;
        clientScenePrepared = false;
        lastBoardMatch = null; retiredMatches.Clear();
        MatchId = null;
        MatchStarted = false;
        IsHost = host;
        Busy = true;
        PeerLost = false;
        Status = "Đang kết nối Photon Fusion…";
        StartTransport(host, normalized);
    }
    public void ChooseCur() { ChooseRole(DefaultOwner.Player1); }
    public void ChooseVillage() { ChooseRole(DefaultOwner.Player2); }
    public void SwapRoles() { ChooseRole(OnlineRules.OtherRole(localRole)); }
    private bool MayChooseRole => !MatchStarted || TurnManager.instance != null && TurnManager.instance.isGameOver;
    private void ChooseRole(DefaultOwner role)
    {
        if (!MayChooseRole || !OnlineRules.ValidRole(role)) return;
        preferredRole = role; explicitRoleChoice = true;
        if (!Connected) { localRole = role; return; }
        if (IsHost) { localRole = role; roleRevision++; PublishLobby(); }
        else SendTransport("role", ((int)role).ToString());
    }
    public void StartMatch()
    {
        if (!Connected || !IsHost || !RolesSynchronized || Busy || PeerLost) return;
        if (MatchStarted && (TurnManager.instance == null || !TurnManager.instance.isGameOver)) return;

        // Fusion replicates the scene transition to *both* peers. No begin_ack wait.
        MatchId = Guid.NewGuid().ToString("N");
        MatchStarted = true;
        Busy = true;
        roleRevision++;
        PublishLobby();
        PeerLost = false;
        GameSettings.currentMode = GameMode.PvP;
        Time.timeScale = 1;
        Status = "Đang chuyển cả hai máy vào bàn chơi (Fusion)...";
        Debug.Log($"[Thereasm Online] Host bắt đầu ván {MatchId} bằng Fusion LoadScene");
        BeginSynchronizedBoard();
    }

    // The Fusion client receives OnSceneLoadStart before MainBoard Awake() runs.
    // OnlineGameBridge therefore sees Active == true in both peers.
    private void ClientPrepareForSceneTransition()
    {
        if (IsHost || !Connected || PeerLost || clientScenePrepared) return;
        clientScenePrepared = true;
        // Retire the match actually rendered by the previous board, not a newly
        // announced match whose lobby packet may arrive before Fusion loads it.
        if (!string.IsNullOrEmpty(lastBoardMatch)) retiredMatches.Add(lastBoardMatch);
        lastBoardMatch = null;
        // Scene replication can arrive before the lobby announcement for a rematch.
        // Never let the new bridge bind itself to the previous match's epoch.
        MatchId = null;
        MatchStarted = true;
        GameSettings.currentMode = GameMode.PvP;
        Time.timeScale = 1;
        Status = "Host đã bắt đầu. Đang tải bàn chơi (Fusion)...";
        Debug.Log($"[Thereasm Online] Client nhận sự kiện Fusion scene load; match={MatchId}");
    }

    // Only the host can request Fusion scene transitions.
    partial void BeginSynchronizedBoard();
    private void ReportSceneTransitionFailure(string error)
    {
        clientScenePrepared = false;
        MatchId = null;
        MatchStarted = false;
        Busy = false;
        Status = error;
        Debug.LogError("[Thereasm Online] " + error);
    }
    public void Leave()
    {
        StopTransport();
        Connected = false; MatchStarted = false; Busy = false; PlayerCount = 0; PeerLost = false;
        clientScenePrepared = false;
        MatchId = null;
        Status = "Đã rời phòng.";
        lastBoardMatch = null; retiredMatches.Clear();
    }
    public void SendCommand(OnlineCommand command)
    {
        if (!Active || IsHost || PeerLost || string.IsNullOrEmpty(MatchId)) return;
        command.match = MatchId;
        SendTransport("command", JsonUtility.ToJson(command));
    }
    public void SendSnapshot(string json)
    {
        if (Active && IsHost && !PeerLost)
            SendTransport("snapshot", OnlineSnapshotCodec.Encode(json));
    }
    // This is a presentation acknowledgement, not a replacement for state validation.
    public void SendActionResult(string json)
    {
        if (Active && IsHost && !PeerLost) SendTransport("action_result", json);
    }
    public void RequestSnapshot() { if (Active && !IsHost) SendTransport("ready", MatchId); }
    private void Received(string kind, string payload, bool fromHost)
    {
        if (fromHost)
        {
            if (kind == "lobby")
            {
                var state = JsonUtility.FromJson<LobbyState>(payload);
                if (state == null || state.revision < roleRevision || !OnlineRules.ValidRole((DefaultOwner)state.hostRole)) return;
                if (!string.IsNullOrEmpty(state.match) && retiredMatches.Contains(state.match)) return;
                if (!state.started && (clientScenePrepared || settings != null && SceneManager.GetActiveScene().name == settings.boardScene)) return;
                roleRevision = state.revision;
                localRole = OnlineRules.OtherRole((DefaultOwner)state.hostRole);
                PlayerCount = state.players;
                MatchId = state.match;
                MatchStarted = state.started;
                if (state.started) { GameSettings.currentMode = GameMode.PvP; Time.timeScale = 1; }
                if (Connected) SendTransport("role_ack", roleRevision.ToString());
                if (state.started && OnlineGameBridge.Instance != null && !OnlineGameBridge.Instance.HasSnapshot) RequestSnapshot();
            }
            else if (kind == "probe")
            {
                Debug.Log("[Thereasm Online] Client nhận PROBE từ host; phản hồi PROBE_ACK");
                SendTransport("probe_ack", payload);
            }
            else HostMessage?.Invoke(kind, payload);
        }
        else if (kind == "role_ack" && IsHost && int.TryParse(payload, out int ack))
        { if (ack == roleRevision) acknowledgedRoleRevision = ack; }
        else if (kind == "lobby_ready" && IsHost) PublishLobby();
        else if (kind == "probe_ack" && IsHost)
        {
            Debug.Log("[Thereasm Online] Host nhận PROBE_ACK: " + payload);
        }
        else if (kind == "role" && MayChooseRole && int.TryParse(payload, out int role) && OnlineRules.ValidRole((DefaultOwner)role))
        {
            localRole = OnlineRules.OtherRole((DefaultOwner)role);
            roleRevision++;
            PublishLobby();
        }
        else GuestMessage?.Invoke(kind, payload);
    }
    partial void StartTransport(bool host, string code);
    partial void StopTransport();
    partial void SendTransport(string kind, string payload);
    private void PublishLobby()
    {
        if (!IsHost || !Connected) return;
        SendTransport("lobby", JsonUtility.ToJson(new LobbyState { revision = roleRevision, hostRole = (int)localRole,
            players = PlayerCount, match = MatchId, started = MatchStarted }));
    }
    void Update()
    {
        if (!Connected || Time.unscaledTime < nextLobbySend) return;
        nextLobbySend = Time.unscaledTime + 1;
        if (IsHost) PublishLobby();
        else if (roleRevision == 0) SendTransport("lobby_ready", "");
    }

#if !THEREASM_FUSION
    partial void StartTransport(bool host, string code)
    { Busy = false; Status = "Chưa có Photon Fusion 2 SDK. Import SDK rồi chờ Unity biên dịch; App ID đã được lưu sẵn."; }
    partial void BeginSynchronizedBoard()
    { ReportSceneTransitionFailure("Không thể đồng bộ scene: Photon Fusion 2 chưa được biên dịch."); }
    partial void StopTransport() { }
    partial void SendTransport(string kind, string payload) { }
#endif
    void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        if (Instance == this) Instance = null;
    }
}

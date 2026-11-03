#if THEREASM_FUSION
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Fusion;
using Fusion.Sockets;
using Fusion.Photon.Realtime;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.IO;

public partial class OnlineSession : INetworkRunnerCallbacks
{
    private NetworkRunner runner;
    private int messageKey;
    private int sentSnapshots;
    private int receivedSnapshots;
    private bool leaving;
    [Serializable] private class Envelope { public string kind; public string payload; public int protocol = 1; }

    partial void StartTransport(bool host, string code) { StartFusion(host, code); }
    private async void StartFusion(bool host, string code)
    {
        leaving = false;
        var runnerObject = new GameObject("Fusion Runner");
        // Preserve the transport while MenuScenes is replaced by MainBoard.
        DontDestroyOnLoad(runnerObject);
        runner = runnerObject.AddComponent<NetworkRunner>();
        var sceneManager = runnerObject.AddComponent<NetworkSceneManagerDefault>();
        var startedRunner = runner;
        runner.ProvideInput = false;
        runner.AddCallbacks(this);
        try
        {
            var app = PhotonAppSettings.Global.AppSettings.GetCopy();
            app.AppIdFusion = settings.appIdFusion;
            app.AppVersion = settings.appVersion;
            app.FixedRegion = settings.region.Trim().ToLowerInvariant();
            var result = await runner.StartGame(new StartGameArgs
            {
                GameMode = host ? Fusion.GameMode.Host : Fusion.GameMode.Client,
                SessionName = code,
                PlayerCount = 2,
                IsVisible = false,
                EnableClientSessionCreation = false,
                Scene = new NetworkSceneInfo(),
                SceneManager = sceneManager,
                CustomPhotonAppSettings = app
            });
            if (leaving || runner != startedRunner) return;
            Busy = false;
            if (!result.Ok || !startedRunner.IsRunning)
            {
                Status = "Không vào được phòng: " + result.ShutdownReason;
                Connected = false;
                runner = null;
                await startedRunner.Shutdown();
                if (startedRunner != null) Destroy(startedRunner.gameObject);
                return;
            }
            Connected = true;
            PlayerCount = runner.ActivePlayers.Count();
            Status = host ? "Đã tạo phòng. Gửi mã cho người thứ hai." : "Đã vào phòng. Chờ chủ phòng bắt đầu.";
            if (host) PublishLobby();
            else
            {
                SendTransport("lobby_ready", "");
                if (explicitRoleChoice) SendTransport("role", ((int)preferredRole).ToString());
            }
        }
        catch (Exception exception)
        {
            if (leaving || runner != startedRunner) return;
            Busy = false; Connected = false;
            Status = "Lỗi Fusion: " + exception.Message;
            runner = null;
            if (startedRunner != null)
            {
                await startedRunner.Shutdown();
                if (startedRunner != null) Destroy(startedRunner.gameObject);
            }
        }
    }
    partial void StopTransport() { ShutdownFusion(); }
    private async void ShutdownFusion()
    {
        leaving = true;
        var previousRunner = runner;
        runner = null;
        if (previousRunner != null)
        {
            await previousRunner.Shutdown();
            if (previousRunner != null) Destroy(previousRunner.gameObject);
        }
    }
    partial void SendTransport(string kind, string payload)
    {
        if (runner == null || !runner.IsRunning)
        {
            Debug.LogWarning($"[Thereasm Online] Không thể gửi '{kind}': runner chưa chạy.");
            return;
        }
        var data = Encoding.UTF8.GetBytes(JsonUtility.ToJson(new Envelope { kind = kind, payload = payload }));
        if (data.Length > 131072) { Status = "Trạng thái quá lớn để gửi."; return; }
        var key = ReliableKey.FromInts(0x544852, ++messageKey, 0, 0);
        if (kind == "snapshot")
        {
            sentSnapshots++;
            if (sentSnapshots <= 3 || sentSnapshots % 20 == 0)
                Debug.Log($"[Thereasm Online] HOST gửi snapshot #{sentSnapshots}, bytes={data.Length}, players={runner.ActivePlayers.Count()}");
        }
        else if (kind != "command" && kind != "action_result" && kind != "lobby" && kind != "role_ack")
            Debug.Log($"[Thereasm Online] Send {kind}, host={IsHost}, local={runner.LocalPlayer}, players={runner.ActivePlayers.Count()}, bytes={data.Length}");
        if (IsHost)
        {
            foreach (var player in runner.ActivePlayers)
                if (player != runner.LocalPlayer) runner.SendReliableDataToPlayer(player, key, data);
        }
        else runner.SendReliableDataToServer(key, data);
    }
    partial void BeginSynchronizedBoard()
    {
        if (runner == null || !runner.IsRunning || !runner.IsSceneAuthority)
        {
            ReportSceneTransitionFailure("Runner chưa hoạt động hoặc máy này không có quyền chuyển scene.");
            return;
        }

        int sceneIndex = -1;
        for (int i = 0; i < SceneManager.sceneCountInBuildSettings; i++)
        {
            string path = SceneUtility.GetScenePathByBuildIndex(i);
            if (Path.GetFileNameWithoutExtension(path) == settings.boardScene)
            { sceneIndex = i; break; }
        }
        if (sceneIndex < 0)
        {
            ReportSceneTransitionFailure("Không tìm thấy scene '" + settings.boardScene + "' trong Build Profiles/Scenes In Build.");
            return;
        }

        try
        {
            Debug.Log($"[Thereasm Online] Runner.LoadScene({settings.boardScene}, index={sceneIndex})");
            // This automatically replicates the scene switch to the other player.
            runner.LoadScene(SceneRef.FromIndex(sceneIndex), LoadSceneMode.Single);
            // Separate diagnostic: this checks the custom reliable-message channel.
            // Scene loading does NOT wait for the reply.
            SendTransport("probe", MatchId);
        }
        catch (Exception exception)
        {
            ReportSceneTransitionFailure("Fusion không tải được scene: " + exception.Message);
        }
    }

    private void HandleBytes(PlayerRef player, byte[] data)
    {
        // The reliable-stream callback's player value is not a portable sender
        // identity in Host mode. In particular, client-bound streams may carry
        // the client's own PlayerRef. The callback is already scoped to our runner.
        if (leaving || runner == null || data.Length > 131072) return;
        try
        {
            var envelope = JsonUtility.FromJson<Envelope>(Encoding.UTF8.GetString(data));
            if (envelope == null || envelope.protocol != 1 || envelope.payload == null) return;
            if (envelope.kind == "snapshot")
            {
                receivedSnapshots++;
                if (receivedSnapshots <= 3 || receivedSnapshots % 20 == 0)
                    Debug.Log($"[Thereasm Online] CLIENT nhận snapshot #{receivedSnapshots}, bytes={data.Length}, sender={player}");
            }
            else if (envelope.kind != "command" && envelope.kind != "action_result" && envelope.kind != "lobby" && envelope.kind != "role_ack")
                Debug.Log($"[Thereasm Online] Receive {envelope.kind}, sender={player}, local={runner.LocalPlayer}, host={IsHost}");
            Received(envelope.kind, envelope.payload, !IsHost);
        }
        catch (ArgumentException) { Status = "Đã bỏ qua dữ liệu mạng không hợp lệ."; }
    }
#if THEREASM_FUSION_21
    public void OnReliableDataReceived(NetworkRunner source, PlayerRef player, ReliableKey key, ReadOnlySpan<byte> data)
    { if (source == runner) HandleBytes(player, data.ToArray()); }
#else
    public void OnReliableDataReceived(NetworkRunner source, PlayerRef player, ReliableKey key, ArraySegment<byte> data)
    {
        if (source != runner) return;
        var copy = new byte[data.Count];
        Array.Copy(data.Array, data.Offset, copy, 0, data.Count);
        HandleBytes(player, copy);
    }
#endif
    public void OnPlayerJoined(NetworkRunner source, PlayerRef player)
    {
        if (source != runner) return;
        PlayerCount = source.ActivePlayers.Count(current => current != player) + 1;
        if (source.IsServer && player != source.LocalPlayer)
        { Connected = true; roleRevision++; acknowledgedRoleRevision = 0; PublishLobby(); }
    }
    public void OnPlayerLeft(NetworkRunner source, PlayerRef player)
    {
        if (source != runner) return;
        PlayerCount = source.ActivePlayers.Count(current => current != player);
        if (MatchStarted && !leaving) ReportPeerLoss("Đối phương đã rời phòng. Trận đã dừng; hãy về Menu.");
    }
    public void OnConnectRequest(NetworkRunner source, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token)
    { if (source != runner || MatchStarted || PlayerCount >= 2) request.Refuse(); else request.Accept(); }
    public void OnShutdown(NetworkRunner source, ShutdownReason reason)
    {
        if (source != runner || leaving) return;
        if (!MatchStarted) Connected = false;
        ReportPeerLoss("Kết nối đã đóng: " + reason);
    }
    public void OnDisconnectedFromServer(NetworkRunner source, NetDisconnectReason reason)
    { if (source != runner || leaving) return; if (!MatchStarted) Connected = false; ReportPeerLoss("Mất kết nối: " + reason); }
    public void OnConnectFailed(NetworkRunner source, NetAddress address, NetConnectFailedReason reason)
    { if (source != runner || leaving) return; Busy = false; Connected = false; Status = "Không kết nối được: " + reason; }
    public void OnHostMigration(NetworkRunner source, HostMigrationToken token)
    { if (source != runner || leaving) return; ReportPeerLoss("Chủ phòng đã rời. Phiên này không tự chuyển chủ phòng; hãy tạo phòng mới.", true); }
    public void OnConnectedToServer(NetworkRunner source) { }
    public void OnCustomAuthenticationResponse(NetworkRunner source, Dictionary<string, object> data) { }
    public void OnInput(NetworkRunner source, NetworkInput input) { }
    public void OnInputMissing(NetworkRunner source, PlayerRef player, NetworkInput input) { }
    public void OnObjectEnterAOI(NetworkRunner source, NetworkObject obj, PlayerRef player) { }
    public void OnObjectExitAOI(NetworkRunner source, NetworkObject obj, PlayerRef player) { }
    public void OnReliableDataProgress(NetworkRunner source, PlayerRef player, ReliableKey key, float progress) { }
    public void OnSceneLoadDone(NetworkRunner source)
    {
        if (source != runner) return;
        Busy = false;
        Debug.Log($"[Thereasm Online] Fusion OnSceneLoadDone: host={IsHost}, scene={SceneManager.GetActiveScene().name}, match={MatchId}");
        if (!IsHost) EnsureBoardSessionReady(SceneManager.GetActiveScene().name);
        if (MatchStarted && !PeerLost)
            Status = "Đã chuyển sang bàn chơi (Fusion).";
        if (!IsHost) { clientScenePrepared = false; SendTransport("lobby_ready", ""); }
    }
    public void OnSceneLoadStart(NetworkRunner source)
    {
        if (source != runner || leaving) return;
        Debug.Log($"[Thereasm Online] Fusion OnSceneLoadStart: host={IsHost}");
        if (!IsHost) ClientPrepareForSceneTransition();
    }
    public void OnSessionListUpdated(NetworkRunner source, List<SessionInfo> sessions) { }
    public void OnUserSimulationMessage(NetworkRunner source, SimulationMessagePtr message) { }
}
#endif

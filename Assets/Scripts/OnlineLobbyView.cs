using UnityEngine;
using UnityEngine.UI;

public class OnlineLobbyView : MonoBehaviour
{
    [SerializeField] private GameObject panel;
    [SerializeField] private InputField roomCode;
    [SerializeField] private Text status;
    [SerializeField] private Text role;
    [SerializeField] private Button createButton, joinButton, startButton, leaveButton;
    public bool IsOpen => panel != null && panel.activeSelf;
    public void Open() { if (panel != null) panel.SetActive(true); }
    public void Close() { if (panel != null) panel.SetActive(false); }
    public void CreateRoom()
    {
        if (OnlineSession.Instance == null) return;
        if (string.IsNullOrWhiteSpace(roomCode.text)) roomCode.text = System.Guid.NewGuid().ToString("N").Substring(0,6).ToUpperInvariant();
        OnlineSession.Instance.CreateRoom(roomCode.text);
    }
    public void JoinRoom() { if (OnlineSession.Instance != null) OnlineSession.Instance.JoinRoom(roomCode.text); }
    public void StartMatch() { if (OnlineSession.Instance != null) OnlineSession.Instance.StartMatch(); }
    public void LeaveRoom() { if (OnlineSession.Instance != null) OnlineSession.Instance.Leave(); }
    public void ChooseCur() { if (OnlineSession.Instance != null) OnlineSession.Instance.ChooseCur(); }
    public void ChooseVillage() { if (OnlineSession.Instance != null) OnlineSession.Instance.ChooseVillage(); }
    void LateUpdate()
    {
        var session = OnlineSession.Instance;
        if (session == null) return;
        if (status != null) status.text = Localizer.T(session.Status) + (session.Connected ? "\n"+Localizer.T($"Phòng {session.RoomCode} · {session.PlayerCount}/2 người · {session.Settings.region}") : "");
        if (role != null) role.text = Localizer.T("Phe của bạn: ") + Localizer.T(OnlineSession.LocalRole == DefaultOwner.Player1 ? "CURSERMANCER" : "DÂN LÀNG") +
            "\n"+Localizer.T("Chọn phe đang có người giữ sẽ đổi vai của cả hai.");
        if (createButton != null) createButton.interactable = !session.Busy && !session.Connected;
        if (joinButton != null) joinButton.interactable = !session.Busy && !session.Connected;
        if (startButton != null) startButton.interactable = session.IsHost && session.RolesSynchronized && !session.Busy && !session.MatchStarted;
        if (leaveButton != null) leaveButton.interactable = session.Connected || session.Busy;
    }
}

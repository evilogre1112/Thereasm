using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

public class HoldToReveal : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    [Header("Cấu hình văn bản nút bấm")]
    public TextMeshProUGUI buttonText;
    public string unshowString = "HOLD TO SHOW";
    public string showString = "SHOWING...";

    // TurnManager hoặc CardInteraction sẽ đọc biến này để quyết định lật bài
    public static bool isRevealingAll = false; 

    void Start()
    {
        isRevealingAll = false;
        if (buttonText != null) buttonText.text = unshowString;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (!TutorialController.Allow(TutorialAction.Reveal)) return;
        if (OnlineSession.Active && !OnlineSession.LocalCur) return;
        if (TurnManager.instance == null || TurnManager.instance.isGameOver ||
            TurnManager.instance.isTallyingVotes || TurnManager.instance.IsBotTurn ||
            GameStartUI.IsBlockingInput || GameSettings.currentMode == GameMode.PlayerVsCurBot) return;
        if (eventData.button != PointerEventData.InputButton.Left) return;

        isRevealingAll = true;
        if (buttonText != null) buttonText.text = showString;
        
        Debug.Log("<color=green>Đang nhấn giữ Show: Lật mặt bài!</color>");
        
        if (TurnManager.instance != null) TurnManager.instance.UpdateCurVisuals();
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left) return;

        Release();
    }

    void OnDisable() => Release();
    void OnApplicationFocus(bool focused) { if (!focused) Release(); }

    private void Release()
    {
        bool wasRevealing = isRevealingAll;
        isRevealingAll = false;
        if (buttonText != null) buttonText.text = unshowString;
        if (wasRevealing && TurnManager.instance != null) TurnManager.instance.UpdateCurVisuals();
    }
}

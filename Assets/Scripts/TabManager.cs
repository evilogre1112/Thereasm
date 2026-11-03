using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections.Generic;

public class TabManager : MonoBehaviour
{
    public static TabManager instance;
    
    // Bài Neutral được dùng chung giữa hai phe.
    [Header("Khu vực trung lập")]
    public GameObject neutralPanel;

    void Awake() => instance = this;

    void Start() => CloseAllTabs();


    void Update()
    {
        if (EventSystem.current == null || GameStartUI.IsBlockingInput) return;
        if (Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1))
        {
            // Đóng inspection trước khi xử lý thao tác trên tab khác.
            if (CardInteraction.currentlyInspectedCard != null)
            {
                CardInteraction.ForceUnzoom();
                return; 
            }

            if (!EventSystem.current.IsPointerOverGameObject())
            {
            }
            else
            {
                if (TurnManager.instance != null && TurnManager.instance.cemeteryViewPanel != null &&
                    TurnManager.instance.cemeteryViewPanel.activeSelf)
                {
                    PointerEventData eventData = new PointerEventData(EventSystem.current);
                    eventData.position = Input.mousePosition;
                    List<RaycastResult> results = new List<RaycastResult>();
                    EventSystem.current.RaycastAll(eventData, results);

                    bool hitCard = false;
                    bool hitDimmerOrPanel = false;

                    foreach (var result in results)
                    {
                        // Click vào lá trong nghĩa trang không được coi là click nền để đóng bảng.
                        if (result.gameObject.GetComponentInParent<CardInteraction>() != null)
                        {
                            hitCard = true;
                            break;
                        }

                        if (result.gameObject.name == "Canvas Dimmer" || result.gameObject.name == "CemeteryViewPanel")
                        {
                            hitDimmerOrPanel = true;
                        }
                    }

                    // Chỉ đóng nghĩa trang khi click nền mà không trúng lá bài.
                    if (!hitCard && hitDimmerOrPanel)
                    {
                        Debug.Log("<color=red>TabManager: Click trúng nền trống, đóng Nghĩa trang.</color>");
                        TurnManager.instance.CloseCemeteryView();
                    }
                }
            }
        }
    }
    public void ClickMyHand()
    {
        TurnState current = TurnManager.instance.currentTurn;

        if (current == TurnState.Player1Turn || current == TurnState.SetupPhase)
            OpenPanel(DeckManager.instance.p1Panel);
        else
            OpenPanel(DeckManager.instance.p2Panel);
    }

    public void ClickOpponent()
    {
        TurnState current = TurnManager.instance.currentTurn;

        if (current == TurnState.Player1Turn || current == TurnState.SetupPhase)
            OpenPanel(DeckManager.instance.p2Panel);
        else
            OpenPanel(DeckManager.instance.p1Panel);
    }

    public void ClickNeutral() => OpenPanel(neutralPanel);

    public void OpenPanel(GameObject target)
    {
        // Thu nhỏ lá đang xem trước khi tắt panel chứa nó.
        CardInteraction.ForceUnzoom(); 

        CloseAllTabs();
        if (target != null) target.SetActive(true);
        
        if (TurnManager.instance != null)
        {
            TurnManager.instance.RevealSeatCards();
        }
    }

    public void CloseAllTabs()
    {
        CardInteraction.ForceUnzoom(); 

        if (DeckManager.instance != null)
        {
            if (DeckManager.instance.p1Panel != null) DeckManager.instance.p1Panel.SetActive(false);
            if (DeckManager.instance.p2Panel != null) DeckManager.instance.p2Panel.SetActive(false);
        }
        if (neutralPanel != null) neutralPanel.SetActive(false);
    }
}

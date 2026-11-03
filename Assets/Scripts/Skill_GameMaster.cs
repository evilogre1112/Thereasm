using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.UI;

[CreateAssetMenu(fileName = "Skill_GameMaster", menuName = "Card Skills/Passive/Game Master Expansion")]
public class Skill_GameMaster : CardSkill
{
    [Header("Cấu hình Ghế Phụ")]
    [Tooltip("Kéo ExtraSeatPrefab vào đây")]
    public GameObject extraSeatPrefab; 
    public float yOffset = 280f; 
    public float xOffset = 200f; 

    [Header("Hình ảnh Ghế Phụ")]
    public Sprite topSeatSprite;
    public Sprite bottomSeatSprite;
    public Sprite leftSeatSprite;

    [System.NonSerialized]
    private Dictionary<CardInstance, List<SeatDropZone>> spawnedSeatsMap = new Dictionary<CardInstance, List<SeatDropZone>>();
    
    [System.NonSerialized]
    private Dictionary<CardInstance, Coroutine> monitorMap = new Dictionary<CardInstance, Coroutine>();

    public override void ApplyAura(CardInstance owner, int mySeatIndex)
    {
        if (TurnManager.instance == null || extraSeatPrefab == null || owner.isFaceDown) return;

        if (!spawnedSeatsMap.ContainsKey(owner)) spawnedSeatsMap[owner] = new List<SeatDropZone>();
        List<SeatDropZone> mySeats = spawnedSeatsMap[owner];

        if (owner.isSilenced )
        {
            if (mySeats.Count > 0)
            {
                Debug.Log($"<color=yellow>Quản Trò {owner.name} bị vô hiệu hóa! Thu hồi toàn bộ không gian ảo!</color>");
                CleanupSpawnedSeats(owner, false); 
            }
            return; 
        }

        SeatDropZone targetSeat = TurnManager.instance.allSeats[mySeatIndex];
        Transform boardPanel = targetSeat.transform.parent; 
        RectTransform seatRect = targetSeat.GetComponent<RectTransform>();
        Vector2 centerPos = seatRect.anchoredPosition;

        if (mySeats.Count == 0)
        {
            SkillAudio.Emit(owner);
            Debug.Log($"<color=magenta>Quản Trò {owner.name} giáng lâm! Mở ra không gian ảo!</color>");
            SeatDropZone bottomSeat = CreateSeat(boardPanel, centerPos + new Vector2(0f, -yOffset), "ExtraSeat_Bottom", bottomSeatSprite, seatRect);
            SeatDropZone topSeat = CreateSeat(boardPanel, centerPos + new Vector2(0f, yOffset), "ExtraSeat_Top", topSeatSprite, seatRect);
            
            mySeats.Add(bottomSeat); 
            mySeats.Add(topSeat);
            TurnManager.instance.allSeats.Add(bottomSeat);   
            TurnManager.instance.allSeats.Add(topSeat);

            if (mySeatIndex == 0)
            {
                SeatDropZone leftSeat = CreateSeat(boardPanel, centerPos + new Vector2(-xOffset, 0), "ExtraSeat_Left", leftSeatSprite, seatRect);
                mySeats.Add(leftSeat);
                // Thêm ghế ở cuối danh sách để giữ ổn định chỉ số các ghế hiện có.
                TurnManager.instance.allSeats.Add(leftSeat);
            }

            CardInteraction interaction = owner.GetComponent<CardInteraction>();
            if (interaction != null)
            {
                if (monitorMap.ContainsKey(owner) && monitorMap[owner] != null) 
                    owner.StopCoroutine(monitorMap[owner]);
                monitorMap[owner] = owner.StartCoroutine(MonitorLockRoutine(owner, interaction));
            }
        }
        else
        {
            for (int i = mySeats.Count - 1; i >= 0; i--)
            {
                SeatDropZone seat = mySeats[i];
                if (seat == null) continue;
                RectTransform rt = seat.GetComponent<RectTransform>();

                if (seat.name == "ExtraSeat_Top") rt.anchoredPosition = centerPos + new Vector2(0f, yOffset);
                else if (seat.name == "ExtraSeat_Bottom") rt.anchoredPosition = centerPos + new Vector2(0f, -yOffset);
                else if (seat.name == "ExtraSeat_Left")
                {
                    if (mySeatIndex != 0)
                    {
                        Debug.Log("<color=orange>Quản Trò rời mép trái, ghế Left sụp đổ! Bài trên ghế văng về tay.</color>");
                        CardInteraction cardOnSeat = seat.cardPos.GetComponentInChildren<CardInteraction>();
                        if (cardOnSeat != null) cardOnSeat.ForceReturnToHand(); 
                        
                        TurnManager.instance.RemoveSeat(seat);
                        mySeats.Remove(seat);
                        Destroy(seat.gameObject);
                    }
                    else
                    {
                        rt.anchoredPosition = centerPos + new Vector2(-xOffset, 0);
                    }
                }
            }

            if (mySeatIndex == 0 && !mySeats.Exists(s => s.name == "ExtraSeat_Left"))
            {
                SeatDropZone leftSeat = CreateSeat(boardPanel, centerPos + new Vector2(-xOffset, 0), "ExtraSeat_Left", leftSeatSprite, seatRect);
                mySeats.Add(leftSeat);
                // Thêm ở cuối danh sách để giữ chỉ số ghế hiện có.
                TurnManager.instance.allSeats.Add(leftSeat);
            }
        }
        var anchor = owner.GetComponent<CardInteraction>();
        if (anchor != null) anchor.isLockedBySkill = mySeats.Exists(s => s != null && s.cardPos.GetComponentInChildren<CardInteraction>() != null);
    }

    public void CleanupSpawnedSeats(CardInstance owner, bool isDeath = false)
    {
        if (!spawnedSeatsMap.ContainsKey(owner) || spawnedSeatsMap[owner].Count == 0) return;

        List<SeatDropZone> seatsToClean = new List<SeatDropZone>(spawnedSeatsMap[owner]);
        spawnedSeatsMap[owner].Clear(); 
        var anchor = owner.GetComponent<CardInteraction>();
        if (anchor != null) anchor.isLockedBySkill = false;

        foreach (SeatDropZone seat in seatsToClean)
        {
            if (seat == null) continue;

            CardInteraction cardOnSeat = seat.cardPos.GetComponentInChildren<CardInteraction>();
            if (cardOnSeat != null)
            {
                if (isDeath)
                {
                    // Preacher blocks infection and Hunter shots, not a collapsing seat.
                    TurnManager.instance.BanishCardToCemetery(cardOnSeat);
                }
                else
                {
                    Debug.Log($"<color=orange>Ghế sụp đổ! {cardOnSeat.name} bị văng về tay!</color>");
                    cardOnSeat.ForceReturnToHand();
                }
            }

            if (TurnManager.instance != null && TurnManager.instance.allSeats.Contains(seat))
            {
                TurnManager.instance.RemoveSeat(seat);
            }
            Destroy(seat.gameObject);
        }
    }

    public override void OnRemoveFromBoard(CardInstance owner, int seatIndex)
    {
        Debug.Log($"<color=magenta>Quản Trò {owner.name} rời đi!</color>");
        
        if (monitorMap.ContainsKey(owner) && monitorMap[owner] != null) 
        {
            owner.StopCoroutine(monitorMap[owner]);
            monitorMap[owner] = null;
        }

        CardInteraction interaction = owner.GetComponent<CardInteraction>();
        if (interaction != null) interaction.isLockedBySkill = false; 

        CleanupSpawnedSeats(owner, false);
    }

    public override void OnDeath(CardInstance owner, int seatIndex)
    {
        if (monitorMap.TryGetValue(owner, out var routine) && routine != null) owner.StopCoroutine(routine);
        monitorMap.Remove(owner);
        Debug.Log($"<color=magenta>Quản Trò {owner.name} tử trận! Không gian sụp đổ!</color>");
        CleanupSpawnedSeats(owner, true);
    }

    private SeatDropZone CreateSeat(Transform parent, Vector2 pos, string name, Sprite seatSprite, RectTransform originalRect)
    {
        GameObject newSeatObj = Instantiate(extraSeatPrefab, parent);
        newSeatObj.name = name;
        
        if (seatSprite != null)
        {
            Image img = newSeatObj.GetComponent<Image>();
            if (img != null) img.sprite = seatSprite;
        }

        LayoutElement layout = newSeatObj.GetComponent<LayoutElement>();
        if (layout == null) layout = newSeatObj.AddComponent<LayoutElement>();
        layout.ignoreLayout = true; 

        RectTransform rect = newSeatObj.GetComponent<RectTransform>();
        if (originalRect != null)
        {
            rect.anchorMin = originalRect.anchorMin;
            rect.anchorMax = originalRect.anchorMax;
            rect.pivot = originalRect.pivot;
        }
        rect.anchoredPosition = pos;

        return newSeatObj.GetComponent<SeatDropZone>();
    }

    private IEnumerator MonitorLockRoutine(CardInstance owner, CardInteraction gmInteraction)
    {
        while (true)
        {
            bool hasAnyone = false;
            if (spawnedSeatsMap.ContainsKey(owner))
            {
                foreach (SeatDropZone seat in spawnedSeatsMap[owner])
                {
                    if (seat != null && seat.cardPos.childCount > 0)
                    {
                        hasAnyone = true;
                        break;
                    }
                }
            }

            gmInteraction.isLockedBySkill = hasAnyone; 
            yield return new WaitForSeconds(0.2f);
        }
    }
}

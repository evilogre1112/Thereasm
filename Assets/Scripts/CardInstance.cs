using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections; 
using System.Collections.Generic; 
public class CardInstance : MonoBehaviour
{
    [Header("Dữ liệu & UI")]
    public CardData cardData;
    public Image cardImage; 
    public Image skillIconUI; 
    [Header("Trạng thái thực tế")]
    public DefaultOwner currentOwner = DefaultOwner.Neutral;
    [Header("Trạng thái")]
    public bool isInfected = false; 
    public bool isDead = false;
    public int cemeteryOrder;
    public GameObject infectionEffect; 
    public bool hasUsedSkill = false;
    public CardSkill activeSkill; 
    public bool hasUsedSkillInRitual = false; 
    public bool isFaceDown = false;
    [Header("Ritual Memory")]
    // Lưu trữ quân bài đang bị quân Cur này nguyền rủa
    public CardInstance currentVictim;
    [Header("Material States")]
    public Material normalMaterial;   // Material Shiny mặc định
    public Material infectedMaterial; // Material kim loại khi bị nhiễm
    public TextMeshProUGUI voteText;
    [Header("Thông tin người đặt bài")]
    public DefaultOwner lastPlacedBy;
    [Header("Trạng thái Tình Nghi (Detective)")]
    public GameObject suspectedMark;
    [Header("Hệ thống Mũ (Hat System)")]
    public GameObject hatPrefab;
    public Transform hatContainer;
    private int _bonusSuspicion = 0;
    [Header("Trạng thái Cược (Gambler)")]
    public GameObject gamblerMark;
    [Header("Trạng thái Tò Mò (Snooper)")]
    public GameObject snooperMark;
    [Header("Trạng thái Phá Hoại (Saboteur)")]
    public bool isSilenced = false; // Saboteur khóa kỹ năng gốc; không khóa khả năng lây nhiễm.
    [Header("Trạng thái Quản Trò (Game Master)")]
    public GameObject myExtraSeat; // Ghế do Game Master tạo để dọn khi rời bàn.
    [Header("Trạng thái Kẻ Mù (Blind)")]
    public bool wasInfectedWhenDied = false; // Trạng thái thật tại thời điểm chết.
    public bool isRevealedByBlind = false;   // Kết quả Blind đã công bố cho xác này.
    public bool blindRevealAsInfected = false; // Trạng thái nhiễm trong kết quả công bố.
    public bool blindRevealAsCur = false;      // Thân phận Cursermancer trong kết quả công bố.
    // Lịch sử riêng của từng Blind: lưu lần chết, không phải cardNumber.
    // Không lưu trên Skill_Blind (ScriptableObject vì có thể được dùng chung).
    private readonly HashSet<int> blindInspectedCemeteryOrders = new HashSet<int>();

    public bool HasBlindInspected(int cemeteryOrder) =>
        cemeteryOrder > 0 && blindInspectedCemeteryOrders.Contains(cemeteryOrder);

    public void MarkBlindInspected(int cemeteryOrder)
    {
        if (cemeteryOrder > 0) blindInspectedCemeteryOrders.Add(cemeteryOrder);
    }
    void Start()
    {
        if (cardData != null) SetupCard(cardData);
    }

    public void SetupCard(CardData data)
    {
        cardData = data;
        FaceUp();
    }

    public void UseSkill()
    {
        if (activeSkill != null && !hasUsedSkillInRitual)
        {
            activeSkill.Activate(this);
            // Chỉ tiêu hao kỹ năng sau khi lây thành công, không phải lúc bắt đầu kéo.
        }
    }

    public void MarkSkillAsUsed(bool used)
    {
        hasUsedSkillInRitual = used;
        UpdateSkillUI();
    }

public void FaceUp()
    {
        if (cardImage == null || cardData == null) return;
        isFaceDown = false;
        cardImage.color = Color.white;
        if (TurnManager.instance != null) PlayerInformation.Render(this);
        else { cardImage.sprite = cardData.defaultArtwork; cardImage.material = normalMaterial; UpdateSkillUI(); }
    }
   public void UpdateSkillUI()
    {
        if (skillIconUI != null)
            skillIconUI.gameObject.SetActive(activeSkill != null && !isFaceDown && !hasUsedSkillInRitual &&
                (!(activeSkill is CurseInfectionSkill) || PlayerInformation.LocalTruth));
    }

    public void FaceDown()
    {
        if (cardImage == null || cardData == null) return;
        cardImage.sprite = cardData.backArtwork;
        cardImage.color = Color.white; 
        isFaceDown = true;
        if (skillIconUI != null) skillIconUI.gameObject.SetActive(false);
    }
    public void UpdateCardUI()
    {
        if (cardData == null) return;


        FaceUp();

        if (voteText != null) voteText.text = ""; 

        Debug.Log($"<color=green>Lá bài {gameObject.name} đã cập nhật giao diện theo dữ liệu: {cardData.cardName}</color>");
    }
  
    public int bonusSuspicion
    {
        get { return _bonusSuspicion; }
        set
        {
            _bonusSuspicion = value;
            UpdateHatStack();
        }
    }
   public void UpdateHatStack()
    {
        var presentation = GetComponent<CardVotePresentation>();
        if (presentation != null) { presentation.RefreshSuspicion(); return; }
        if (hatContainer == null || hatPrefab == null) return;

        foreach (Transform child in hatContainer)
        {
            Destroy(child.gameObject);
        }

        // Thu nhỏ chồng dấu khi tăng số lượng để giữ trong khung bài.
        float scaleFactor = bonusSuspicion > 5 ? (5f / bonusSuspicion) : 1f;

        for (int i = 0; i < bonusSuspicion; i++)
        {
            GameObject newHat = Instantiate(hatPrefab, hatContainer);
            newHat.transform.localScale = new Vector3(1f, scaleFactor, 1f);
        }
    }
    public void ResetSuspicion()
    {
        bonusSuspicion = 0; 

        if (suspectedMark != null) 
        {
            suspectedMark.SetActive(false); 
        }
    }
    public void ClearDetectiveMarkOnly()
    {
        if (suspectedMark != null) 
        {
            suspectedMark.SetActive(false); 
        }
        // Chỉ tắt dấu; điểm nghi ngờ vẫn được dùng khi tính phiếu.
    }
}

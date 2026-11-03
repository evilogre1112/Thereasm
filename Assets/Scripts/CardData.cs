using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "New Card", menuName = "Card Game/Card Data")]
public class CardData : ScriptableObject
{
    [Header("Thông tin định danh")]
    public string cardName;            // Tên (vd: Villager, Hunter)
    public int cardNumber;
    
    [Tooltip("Phân loại thẻ bài để code dễ nhận diện")]
    public CardType cardType = CardType.Villager; 
    
    [Tooltip("Người chơi sở hữu lá bài này từ đầu game")]
    public DefaultOwner owner = DefaultOwner.Player1;

    [Header("Hình ảnh & Giao diện")]
    public Sprite defaultArtwork;      // Hình gốc (Khỏe mạnh)

    [Tooltip("Hình ảnh Nguyền Thuật Sư (Chỉ Player 1 thấy khi đã chọn)")]
    public Sprite cursemancerArtwork;  
    
    public Sprite infectedArtwork;     // Hình lúc bị Cur nguyền (tùy chọn)
    public Sprite deadArtwork;         // Hình lúc chết (tùy chọn)
    public Sprite backArtwork;         // Hình mặt sau của lá bài (tùy chọn)

    [Header("Mô tả hiển thị")]
    [TextArea(3, 5)] public string description;
    [TextArea(3, 5)] public string curDescription;       // Khi là Cur (Mặt quỷ)
    [TextArea(3, 5)] public string infectedDescription;  // Khi bị nhiễm bệnh (Mắt đỏ)
    [TextArea(3, 5)] public string curInfectedDescription; // Vừa là Cur vừa bị nhiễm

    [Header("Kỹ Năng Lá Bài")]
    public CardSkill cardSkill;
    [Header("Hệ thống thoại (Dialogue System)")]
    [TextArea(2, 4)]
    public List<string> quotes;
    public float customChance = 0.2f;
    public float customInterval = 10f;
}
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class EnergyBarUI : MonoBehaviour
{
    [Header("Thành phần UI")]
    public Image energyBarImage;
    public TextMeshProUGUI energyText;

    [Header("Kho ảnh trạng thái (Kéo thả vào đây)")]
    // Vị trí [0] = Ảnh cạn kiệt năng lượng
    // Vị trí [5] = Ảnh đầy năng lượng
    public Sprite[] energySprites; 

    [Header("Individual hearts — saved Images, left to right")]
    public Image[] heartIcons;
    public Text countLabel;
    public Color availableColor = Color.white;
    public Color spentColor = new Color(.35f, .27f, .23f, .55f);

    public void UpdateEnergyDisplay(int currentEnergy)
    {
        currentEnergy = Mathf.Clamp(currentEnergy, 0, TurnManager.MAX_HEARTS);
        if (countLabel != null) countLabel.text = currentEnergy + "/" + TurnManager.MAX_HEARTS;
        
        if (energyBarImage != null && energySprites != null && energySprites.Length > 0)
        {
            energyBarImage.sprite = energySprites[Mathf.Min(currentEnergy, energySprites.Length - 1)];
        }
        
        if (energyText != null)
        {
            energyText.text = currentEnergy + "/5";
        }
        
        if (heartIcons != null)
            for (int i = 0; i < heartIcons.Length; i++)
                if (heartIcons[i] != null) heartIcons[i].color = i < currentEnergy ? availableColor : spentColor;
    }
}

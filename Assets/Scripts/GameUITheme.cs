using UnityEngine;
using UnityEngine.Serialization;

[CreateAssetMenu(fileName = "Thereasm Theme", menuName = "Thereasm/UI Theme")]
public class GameUITheme : ScriptableObject
{
    [Header("Artwork — drag Sprite assets here")]
    public Sprite menuBackground;
    public Sprite boardBackground;
    public Sprite panelArtwork;
    public Sprite buttonArtwork;
    [Header("Mode symbols — standalone transparent Sprite assets")]
    [FormerlySerializedAs("villagerPortrait")] public Sprite villagerModeIcon;
    [InspectorName("Cursermancer Mode Icon"), FormerlySerializedAs("curPortrait")] public Sprite curModeIcon;
    [FormerlySerializedAs("localPlayPortrait")] public Sprite onlineModeIcon;

    [Header("Fonts saved in the project")]
    public Font bodyFont;
    public Font titleFont;

    [Header("Panel and button borders")]
    [Min(.1f)] public float panelPixelsPerUnit = 8;
    [Min(.1f)] public float buttonPixelsPerUnit = 10;
    public Color backgroundTint = Color.white;
    public Color textColor = new Color32(248, 236, 213, 255);
    public Color accentColor = new Color32(247, 212, 153, 255);
}

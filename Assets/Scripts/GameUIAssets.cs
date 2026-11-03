using UnityEngine;
using UnityEngine.UI;

// Applies the scene's serialized theme to UI images.
public static class GameUIAssets
{
    public static void Panel(Image image, GameUITheme theme = null)
    {
        if (image == null) return;
        if (theme == null) theme = GameStartUI.CurrentTheme;
        if (theme == null && DeckManager.instance != null) theme = DeckManager.instance.uiTheme;
        if (theme == null || theme.panelArtwork == null) return;
        image.sprite = theme.panelArtwork;
        image.type = Image.Type.Sliced;
        image.pixelsPerUnitMultiplier = theme.panelPixelsPerUnit;
        image.color = Color.white;
    }
}

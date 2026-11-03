using System;
using TMPro;
using UnityEngine;

public enum GameLanguage { English, Vietnamese }

[Serializable]
public class TranslationEntry
{
    public string key;
    [TextArea(1, 8)] public string source;
    [TextArea(1, 8)] public string english;
    [TextArea(1, 8)] public string vietnamese;
}

[CreateAssetMenu(menuName="Thereasm/Localization",fileName="GameLocalization")]
public class GameLocalization : ScriptableObject
{
    public GameLanguage defaultLanguage = GameLanguage.English;
    public Font readableFont;
    public TMP_FontAsset readableTmpFont;
    public TranslationEntry[] entries;
    void OnValidate(){Localizer.Invalidate(this);}
}

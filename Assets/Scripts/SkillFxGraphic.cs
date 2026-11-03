using UnityEngine;
using UnityEngine.UI;

public class SkillFxGraphic : Image
{
    public enum Glyph { Ballot, Spark, Burst, Ring }
    public Glyph glyph;
    public Sprite traderSprite, jesterSprite;
    private Color baseTint=Color.white;
    public void Style(bool jester,Color accent)
    {
        sprite=jester&&jesterSprite!=null?jesterSprite:traderSprite;
        preserveAspect=true;raycastTarget=false;
        baseTint=glyph==Glyph.Spark||glyph==Glyph.Ring?Color.Lerp(accent,Color.white,.25f):Color.white;
        color=baseTint;
    }
    public void SetOpacity(float alpha){color=new Color(baseTint.r,baseTint.g,baseTint.b,Mathf.Clamp01(alpha));}
}

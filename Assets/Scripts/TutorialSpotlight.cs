using System.Collections.Generic;
using UnityEngine;

// Saved, non-interactive masks darken distractions while leaving both real targets untouched.
public class TutorialSpotlight : MonoBehaviour
{
    public RectTransform overlay,area;
    public RectTransform[] masks;
    readonly Vector3[] corners=new Vector3[4];
    readonly List<float> cuts=new List<float>();
    readonly List<Rect> holes=new List<Rect>();
    Rect Bounds(RectTransform target)
    {
        target.GetWorldCorners(corners);Vector2 min=new Vector2(float.MaxValue,float.MaxValue),max=-min;
        foreach(var corner in corners){var p=(Vector2)overlay.InverseTransformPoint(corner);min=Vector2.Min(min,p);max=Vector2.Max(max,p);}
        return Rect.MinMaxRect(min.x,min.y,max.x,max.y);
    }
    public void Present(RectTransform source,RectTransform target,bool visible)
    {
        if(overlay==null||area==null||masks==null)return;
        foreach(var mask in masks)if(mask!=null)mask.gameObject.SetActive(false);
        if(!visible)return;
        Rect bounds=Bounds(area);
        // Opposite sides of the cards keep neighbouring source/target captions apart.
        PlaceCaption(source,bounds,true);
        PlaceCaption(target,bounds,false);
        SeparateCaptions(source,target,bounds);
        holes.Clear();cuts.Clear();cuts.Add(bounds.xMin);cuts.Add(bounds.xMax);
        foreach(var frame in new[]{source,target})
        {
            if(frame==null||!frame.gameObject.activeInHierarchy)continue;
            Rect r=Bounds(frame);
            r=Rect.MinMaxRect(Mathf.Max(bounds.xMin,r.xMin),Mathf.Max(bounds.yMin,r.yMin),Mathf.Min(bounds.xMax,r.xMax),Mathf.Min(bounds.yMax,r.yMax));
            if(r.width<=0||r.height<=0)continue;
            holes.Add(r);cuts.Add(r.xMin);cuts.Add(r.xMax);
        }
        cuts.Sort();int used=0;
        for(int i=1;i<cuts.Count;i++)
        {
            float left=cuts[i-1],right=cuts[i];if(right-left<.01f)continue;
            var spans=new List<Rect>();foreach(var hole in holes)if(hole.xMin<right-.01f&&hole.xMax>left+.01f)spans.Add(hole);
            spans.Sort((a,b)=>a.yMin.CompareTo(b.yMin));float bottom=bounds.yMin;
            foreach(var span in spans){Fill(left,bottom,right,span.yMin,ref used);bottom=Mathf.Max(bottom,span.yMax);}
            Fill(left,bottom,right,bounds.yMax,ref used);
        }
    }
    RectTransform Caption(RectTransform frame)
    {
        return frame != null ? frame.GetComponentInChildren<UnityEngine.UI.Text>()?.rectTransform : null;
    }
    void PlaceCaption(RectTransform frame,Rect bounds,bool above)
    {
        if(frame==null||!frame.gameObject.activeInHierarchy)return;
        foreach(Transform child in frame)
        {
            if(child.GetComponent<UnityEngine.UI.Text>()==null&&!child.name.StartsWith("Tutorial Callout Background"))continue;
            if(!(child is RectTransform caption))continue;
            float half=caption.rect.width*.5f;
            Vector2 desired = new Vector2(frame.anchoredPosition.x,
                frame.anchoredPosition.y + (above ? frame.rect.height*.5f+26 : -frame.rect.height*.5f-26));
            desired.x=Mathf.Clamp(desired.x,bounds.xMin+half+8,bounds.xMax-half-8);
            desired.y=Mathf.Clamp(desired.y,bounds.yMin+26,bounds.yMax-26);
            caption.position=overlay.TransformPoint(desired);
        }
    }
    void SeparateCaptions(RectTransform source,RectTransform target,Rect bounds)
    {
        var a=Caption(source);var b=Caption(target);
        if(a==null||b==null||!a.gameObject.activeInHierarchy||!b.gameObject.activeInHierarchy)return;
        Rect first=Bounds(a),second=Bounds(b);
        if(!first.Overlaps(second))return;
        float shift=first.yMin-second.yMax-10;
        foreach(Transform child in target)
        {
            if(child.GetComponent<UnityEngine.UI.Text>()==null&&!child.name.StartsWith("Tutorial Callout Background"))continue;
            Vector3 p=overlay.InverseTransformPoint(child.position);p.y+=shift;
            child.position=overlay.TransformPoint(p);
        }
    }
    void Fill(float left,float bottom,float right,float top,ref int used)
    {
        if(top-bottom<.01f||used>=masks.Length)return;
        var mask=masks[used++];mask.gameObject.SetActive(true);mask.anchoredPosition=new Vector2((left+right)*.5f,(bottom+top)*.5f);mask.sizeDelta=new Vector2(right-left,top-bottom);
    }
}

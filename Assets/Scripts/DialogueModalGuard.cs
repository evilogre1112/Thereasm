using System.Collections.Generic;
using UnityEngine;

[ExecuteAlways]
public class DialogueModalGuard : MonoBehaviour
{
    static readonly HashSet<DialogueModalGuard> visible=new HashSet<DialogueModalGuard>();
    public static bool IsOpen=>visible.Count>0;
    void OnEnable(){visible.Add(this);CardDialogue.HideAllBubbles();}
    void OnDisable(){visible.Remove(this);}
    void OnDestroy(){visible.Remove(this);}
}

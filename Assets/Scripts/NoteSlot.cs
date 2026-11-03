using UnityEngine;
using UnityEngine.EventSystems;

public class NoteSlot : MonoBehaviour, IDropHandler
{
    public void OnDrop(PointerEventData eventData)
    {
        if (eventData.pointerDrag != null)
        {
            NoteBlock draggedBlock = eventData.pointerDrag.GetComponent<NoteBlock>();
            
            if (draggedBlock != null)
            {
                // Trả thẻ cũ về kho trước khi nhận thẻ mới.
                if (transform.childCount > 0)
                {
                    Transform oldBlock = transform.GetChild(0);
                    NoteBlock oldNoteBlock = oldBlock.GetComponent<NoteBlock>();
                    
                    if (oldNoteBlock != null && oldNoteBlock.homeParent != null) {
                        oldBlock.SetParent(oldNoteBlock.homeParent);
                    } else {
                        oldBlock.SetParent(draggedBlock.originalParent);
                    }
                    
                    if (oldNoteBlock != null) oldNoteBlock.RestorePrefabPose(); 
                }

                draggedBlock.transform.SetParent(this.transform);
                draggedBlock.RestorePrefabPose(); 
            }
        }
    }
}
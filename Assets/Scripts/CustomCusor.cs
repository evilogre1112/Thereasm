using UnityEngine;
using UnityEngine.UI;

public class CustomCusor : MonoBehaviour
{
    private RectTransform rectTransform;

    void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        
        // Tắt Raycast để chuột ảo không cản trở việc click bài
        Image img = GetComponent<Image>();
        if (img != null) img.raycastTarget = false; 
    }

    void Update()
    {
        // Unity có thể khôi phục con trỏ hệ thống; ẩn lại khi cập nhật con trỏ riêng.
        Cursor.visible = false; 

        MoveTo(Input.mousePosition);
    }

    public void MoveTo(Vector2 screenPoint)
    {
        var canvas=GetComponentInParent<Canvas>();
        var root=canvas != null ? canvas.rootCanvas : null;
        var camera=root != null && root.renderMode != RenderMode.ScreenSpaceOverlay ? root.worldCamera : null;
        if(RectTransformUtility.ScreenPointToWorldPointInRectangle(rectTransform.parent as RectTransform,screenPoint,camera,out var world))
            rectTransform.position=world;
    }

    void OnDisable() { Cursor.visible = true; }
    void OnApplicationQuit()
    {
        Cursor.visible = true;
    }
}

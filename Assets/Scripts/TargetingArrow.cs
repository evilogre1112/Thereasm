using UnityEngine;
using UnityEngine.UI;

public class TargetingArrow : MonoBehaviour
{
    public static TargetingArrow instance;

    [Header("Cấu hình Target")]
    public GameObject targetPrefab;
    
    private RectTransform targetRect;
    private Canvas parentCanvas;

    void Awake()
    {
        instance = this;
        parentCanvas = GetComponentInParent<Canvas>();

        if (targetPrefab != null)
        {
            GameObject obj = Instantiate(targetPrefab, transform);
            targetRect = obj.GetComponent<RectTransform>();
            
            targetRect.pivot = new Vector2(0.5f, 0.5f);
            targetRect.anchorMin = new Vector2(0.5f, 0.5f);
            targetRect.anchorMax = new Vector2(0.5f, 0.5f);
        }
        
        gameObject.SetActive(false);
    }

    public void Show(Vector3 startPos)
    {
        gameObject.SetActive(true);
    }

    void OnTransformParentChanged()
    {
        parentCanvas = GetComponentInParent<Canvas>();
    }

    public void Hide() => gameObject.SetActive(false);

    public void UpdateArrow(Vector3 screenMousePos)
    {
        if (parentCanvas == null || targetRect == null) return;

        Vector2 localPoint;
        var root = parentCanvas.rootCanvas;
        var camera = root.renderMode == RenderMode.ScreenSpaceOverlay ? null : root.worldCamera;
        // Tính tọa độ chuột trong cùng parent với tâm ngắm để tránh lệch vị trí.
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
            targetRect.parent as RectTransform, screenMousePos, camera, out localPoint))
            targetRect.localPosition = new Vector3(localPoint.x,localPoint.y,0);
    }
}

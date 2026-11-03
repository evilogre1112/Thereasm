using UnityEngine;
using UnityEngine.EventSystems;

public class NoteBlock : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerDownHandler
{
    [HideInInspector] public Transform originalParent;
    [HideInInspector] public Transform homeParent; // Slot gốc trong kho để trả thẻ về.
    [Header("Âm thanh")]
    public AudioClip clickSound;
    
    private CanvasGroup canvasGroup;
    private RectTransform rectTransform;
    private Vector3 prefabAnchoredPosition;
    private Vector3 prefabScale;

    public void RestorePrefabPose()
    {
        if (rectTransform == null) return;
        rectTransform.anchoredPosition3D = prefabAnchoredPosition;
        rectTransform.localScale = prefabScale;
    } 
    
    // Tọa độ kéo được tính trong canvas ngoài cùng.
    private Transform canvasTransform; 

    void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        if (rectTransform != null)
        {
            prefabAnchoredPosition = rectTransform.anchoredPosition3D;
            prefabScale = rectTransform.localScale;
        } 
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();

        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas != null) canvasTransform = canvas.transform;
    }

    void Start()
    {
        // Lưu slot gốc trước khi thẻ được kéo sang ô ghi chú.
        homeParent = transform.parent;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        var currentCanvas = GetComponentInParent<Canvas>();
        if (currentCanvas != null) canvasTransform = currentCanvas.transform;
        originalParent = transform.parent;
        
        if (canvasTransform != null) transform.SetParent(canvasTransform);
        else transform.SetParent(transform.root);
        
        transform.SetAsLastSibling();
        canvasGroup.blocksRaycasts = false;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (RectTransformUtility.ScreenPointToWorldPointInRectangle(
            rectTransform.parent as RectTransform, 
            eventData.position, 
            eventData.pressEventCamera, 
            out Vector3 globalMousePos))
        {
            transform.position = globalMousePos;
        }
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        canvasGroup.blocksRaycasts = true;

        // Thả ra ngoài slot hợp lệ thì trả về slot gốc.
        if (transform.parent == canvasTransform || transform.parent == transform.root)
        {
            transform.SetParent(originalParent);
            if (rectTransform != null) {
                RestorePrefabPose(); 
            }
        }
    }
    public void OnPointerDown(PointerEventData eventData)
    {
        if (AudioManager.instance != null && clickSound != null)
        {
            AudioManager.instance.PlaySFX(clickSound);
        }
    }
}

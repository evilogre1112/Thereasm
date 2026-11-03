using UnityEngine;
using UnityEngine.UI;
using System.Collections;

[RequireComponent(typeof(CanvasGroup))]
public class CanvasDimmer : MonoBehaviour 
{
    public static CanvasDimmer instance;

    [Header("Cấu hình hiệu ứng")]
    [Range(0f, 1f)]
    public float maxDimAlpha = 0.7f; 
    public float dimSpeed = 5f;      

    private CanvasGroup canvasGroup;
    private Coroutine dimCoroutine;

    void Awake()
    {
        instance = this;
        canvasGroup = GetComponent<CanvasGroup>();
        
        canvasGroup.alpha = 0;
        canvasGroup.blocksRaycasts = false;
        canvasGroup.interactable = false;
    }

    public void Dim()
    {
        if (gameObject.activeSelf == false) gameObject.SetActive(true);
        canvasGroup.blocksRaycasts = true;
        canvasGroup.interactable = true;

        if (dimCoroutine != null) StopCoroutine(dimCoroutine);
        dimCoroutine = StartCoroutine(FadeAlpha(maxDimAlpha));
    }

    public void Undim()
    {
        // Input must be released even if a pause menu stops the fade's clock.
        canvasGroup.blocksRaycasts = false;
        canvasGroup.interactable = false;
        if (dimCoroutine != null) StopCoroutine(dimCoroutine);
        dimCoroutine = StartCoroutine(FadeAlpha(0f));
    }

    private IEnumerator FadeAlpha(float target)
    {
        float start = canvasGroup.alpha;
        float time = 0;
        while (time < 1)
        {
            time += Time.unscaledDeltaTime * dimSpeed;
            canvasGroup.alpha = Mathf.Lerp(start, target, Mathf.SmoothStep(0, 1, time));
            yield return null;
        }
        canvasGroup.alpha = target;
        
        if (target <= 0)
        {
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;
        }
    }
}

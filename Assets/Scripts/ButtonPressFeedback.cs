using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Saved visual-only feedback; never accepts, repeats or changes a gameplay command.
[DisallowMultipleComponent]
public class ButtonPressFeedback : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
{
    public Button button;
    public Image flash;
    public RectTransform content;
    public Color flashColor = new Color(1,.83f,.43f,1);
    [Range(.8f,1)] public float pressedScale = .94f;
    [Min(.1f)] public float confirmationSeconds = .3f;
    public bool pulseWhenAvailable = true;
    Vector3 contentScale;
    float clickedAt = -100;
    bool pressed;

    void Awake(){if(button==null)button=GetComponent<Button>();contentScale=content!=null?content.localScale:Vector3.one;}
    void OnEnable(){if(button==null)button=GetComponent<Button>();if(button!=null)button.onClick.AddListener(Confirm);}
    void OnDisable()
    {
        if(button!=null)button.onClick.RemoveListener(Confirm);
        pressed=false;if(content!=null)content.localScale=contentScale;
        if(flash!=null){var color=flash.color;color.a=0;flash.color=color;}
    }
    public void Confirm(){clickedAt=Time.unscaledTime;pressed=false;Refresh();}
    public void OnPointerDown(PointerEventData data){if(data.button==PointerEventData.InputButton.Left&&button!=null&&button.IsInteractable())pressed=true;}
    public void OnPointerUp(PointerEventData data){pressed=false;}
    public void OnPointerExit(PointerEventData data){pressed=false;}
    void Update(){Refresh();}
    void Refresh()
    {
        bool available=button!=null&&button.IsInteractable()&&!GameStartUI.IsBlockingInput;
        float elapsed=Time.unscaledTime-clickedAt;
        float confirmation=Mathf.Clamp01(1-elapsed/confirmationSeconds);
        bool humanTurn=TurnManager.instance==null||!TurnManager.instance.IsBotTurn;
        float pulse=available&&humanTurn&&pulseWhenAvailable?.045f+.04f*(1+Mathf.Sin(Time.unscaledTime*3)):0;
        if(flash!=null){var color=flashColor;color.a=Mathf.Max(pulse,pressed?.22f:confirmation*.4f);flash.color=color;}
        if(content!=null)content.localScale=contentScale*(pressed?pressedScale:1-.045f*confirmation);
    }
}

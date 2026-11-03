using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections;
using UnityEngine.UI;
using System.Collections.Generic;

public enum CardAffiliation { Mine, Opponent, Neutral }

public class CardInteraction : MonoBehaviour, IPointerClickHandler, IInitializePotentialDragHandler, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerEnterHandler, IPointerExitHandler, IDropHandler
{
    public static CardInteraction currentlyInspectedCard = null;

    [Header("Hierarchy & State")]
    public Transform handParent;
    public Transform originalParent; 
    public Transform visualChild; 
    private CanvasGroup canvasGroup;
    public CardInteraction lastSwappedCard = null; 
    
    public bool isPlaced = false;
    public bool canDrag = false;
    private int paidPlacementCost = 2;
    public int lockState = 0; 
    private int permanentHandIndex;
    [Header("Special States")]
    public bool isLockedByElder = false;

    [Header("Hiệu ứng Hover")]
    public float hoverOffsetY = 40f;  
    public float hoverSpeed = 20f;    
    private Vector2 prefabVisualAnchor;
    private Quaternion prefabVisualRotation;
    private Vector3 prefabVisualScale;
    private bool capturedPrefabVisual;
    public Vector3 PrefabRootScale { get; private set; } = Vector3.one;

    private void CaptureVisualPrefabPose()
    {
        if (capturedPrefabVisual || !(visualChild is RectTransform rect)) return;
        prefabVisualAnchor = rect.anchoredPosition;
        prefabVisualRotation = rect.localRotation;
        prefabVisualScale = rect.localScale;
        capturedPrefabVisual = true;
    }

    public void RestoreVisualPrefabPose()
    {
        CaptureVisualPrefabPose();
        if (!capturedPrefabVisual || !(visualChild is RectTransform rect)) return;
        rect.anchoredPosition = prefabVisualAnchor;
        rect.localRotation = prefabVisualRotation;
        rect.localScale = prefabVisualScale;
    }
    private Vector2 VisualPrefabAnchor
    {
        get { CaptureVisualPrefabPose(); return prefabVisualAnchor; }
    }

    [Header("Hiệu ứng Zoom")]
    public bool isInspecting = false;
    public float inspectScaleMultiplier = 1.8f; 
    public float inspectSpeed = 12f;
    [Header("UI Mô tả Kỹ Năng")]
    public GameObject descriptionPanel;
    public TMPro.TextMeshProUGUI descriptionText;
    private Vector2 preInspectPos;
    private Quaternion preInspectRot;
    private Vector3 preInspectScale;
    private Coroutine inspectCoroutine;

    [Header("Drag Tilt Effect")]
    public float tiltAmount = 80f;
    public float tiltSpeed = 20f;
    private Vector3 lastMousePos;

    [Header("Hiệu ứng Rung")]
    private bool isShaking = false;
    public float shakeDuration = 0.2f;
    public float shakeMagnitude = 5f;
    public static bool isAnyCardDragging = false;
    [Header("Trạng thái Đặc biệt")]
    public bool isLockedBySkill = false; // Game Master có người trên ghế phụ thì bị khóa di chuyển thủ công.
    [Header("Trạng thái Bảo vệ")]
    public bool isProtectedByPreacher = false; // Bảo vệ khỏi Hunter và Cur
    [Header("Âm Thanh")]
    public AudioClip clickSFX;
    public AudioClip hoverSFX;
    private AudioSource audioSource;

    private Vector2 initialMousePos;
    private Vector2 initialAnchoredPos;
    private Coroutine dragCoroutine;

    void Awake()
    {
        PrefabRootScale = transform.localScale;
        CaptureVisualPrefabPose();
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null) 
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
        }
    }

    void Start()
    {
        handParent = transform.parent;
        permanentHandIndex = transform.GetSiblingIndex();
        originalParent = transform.parent;
    }

    public void ResetTurnState()
    {
        lastSwappedCard = null; 
        if (lockState == 1) lockState = 2;      
        else if (lockState == 2) lockState = 3; 
    }
    public void ResetAllAuraStates()
    {
        isLockedByElder = false;
        isProtectedByPreacher = false;
        
        CardInstance ci = GetComponent<CardInstance>();
        if (ci != null)
        {
            ci.isSilenced = false; 
            // Blind lưu lịch sử của từng xác; chỉ đặt lại kết quả cho lá còn sống.
            if (!ci.isDead) ci.isRevealedByBlind = false;
        }
    }

    // Host và guest dùng chung hiệu ứng cục bộ; pointer feedback không gửi RPC.
    public void OnPointerEnter(PointerEventData eventData)
    {
        CardLocalVisualFeedback.For(this).PointerEnter();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        CardLocalVisualFeedback.For(this).PointerExit();
    }

    // The common presenter uses the authored prefab anchor (not a moving drag location).
    public Vector2 HoverBaseAnchor => VisualPrefabAnchor;

    public void OnInitializePotentialDrag(PointerEventData eventData)
    {
        // Cancelling only BeginDrag is too late: the input module can already cancel the click
        // after a few pixels of movement. Keep click-only lessons out of drag processing.
        if (TutorialController.Active && !TutorialController.CanDrag(GetComponent<CardInstance>(),
            eventData.button == PointerEventData.InputButton.Right))
        { eventData.pointerDrag = null; }
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (!TutorialController.CanDrag(GetComponent<CardInstance>(), eventData.button == PointerEventData.InputButton.Right))
        { TutorialController.ExplainBlockedAction(); eventData.pointerDrag = null; return; }
        // Guests have their own responsive pointer presentation in OnlineCardInput.
        if (OnlineSession.Guest) return;
        if (GameStartUI.IsBlockingInput) return;
        string blocked = ActionFeedback.BeginDragBlock(this, eventData.button == PointerEventData.InputButton.Right);
        if (blocked != null)
        {
            ActionFeedback.Reject(blocked);
            if(!TutorialWorkshop.Active)StartCoroutine(ShakeCard());
            eventData.pointerDrag = null;return;
        }
        if (isInspecting || currentlyInspectedCard != null) return;
        if (isAnyCardDragging) return;
        if (isLockedBySkill) 
        {
            Debug.Log("<color=red>Lá bài này đang bị khóa mỏ neo, không thể di chuyển!</color>");
            StartCoroutine("ShakeCard");
            return; 
        }
        if (TurnManager.instance == null || TurnManager.instance.isGameOver || TurnManager.instance.isTallyingVotes ||
            TurnManager.instance.IsBotTurn || GameStartUI.IsBlockingInput) return;
        if (OnlineSession.Active && !OnlineSession.LocalTurn) return;

        if (TurnManager.instance.currentTurn == TurnState.SetupPhase) return;

        CardInstance ci = GetComponent<CardInstance>();
        if (ci == null || ci.isFaceDown) return;

        LayoutElement le = GetComponent<LayoutElement>();
        if (le != null) le.ignoreLayout = true;

        canDrag = false; 

        if (eventData.button == PointerEventData.InputButton.Right)
        {
            if (isPlaced && ci.cardData.cardSkill != null && (ci.cardData.cardSkill.skillType == SkillType.Active || ci.cardData.cardSkill.skillType == SkillType.Hybrid))
            {
                if (isInspecting) FastUnzoom(); 
                
                if (TargetingArrow.instance != null) TargetingArrow.instance.Show(transform.position);
                canDrag = true; 
                isAnyCardDragging = true;
            }
            else if (!isPlaced)
            {
                ActionFeedback.Reject("Play this card onto a seat before using its skill. Right-click without dragging to read it.");
                StartCoroutine("ShakeCard"); 
            }
            return; 
        }

        if (eventData.button == PointerEventData.InputButton.Left)
        {
            if (TurnManager.instance.currentTurn == TurnState.CursePhase || TurnManager.instance.currentTurn == TurnState.SacrificePhase)
            {
                if (TurnManager.instance.currentTurn == TurnState.CursePhase)
                {
                    bool isCur = TurnManager.instance.allCurs.Contains(ci);
                    if (isCur && isPlaced)
                    {
                        if (isInspecting) FastUnzoom();
                        
                        TurnManager.instance.StartCurseSelection(ci);
                        canDrag = true;
                        isAnyCardDragging = true;
                        if (TargetingArrow.instance != null) TargetingArrow.instance.Show(transform.position);
                        canvasGroup.blocksRaycasts = false;
                        return; 
                    }
                }
                else if (TurnManager.instance.currentTurn == TurnState.SacrificePhase)
                {
                    if (isPlaced && TurnManager.instance.IsMyVoteTurn(ci))
                    {
                        if (isInspecting) FastUnzoom();
                        
                        TurnManager.instance.StartVoteSelection(ci);
                        canDrag = true;
                        isAnyCardDragging = true;
                        if (TargetingArrow.instance != null) TargetingArrow.instance.Show(transform.position);
                        canvasGroup.blocksRaycasts = false;
                        return; 
                    }
                }
                eventData.pointerDrag = null;
                return;
            }

            if ((isPlaced && lockState < 3) || isLockedByElder)
            {
                ActionFeedback.Reject(ActionFeedback.MovementBlock(this) ?? ActionFeedback.Cost(ci, GetMoveToEmptyCost(this)));
                StartCoroutine(HandleFailedPlace());
                eventData.pointerDrag = null;
                return;
            }

           canDrag = true;
            isAnyCardDragging = true;

            CardLocalVisualFeedback.For(this).StopHover(true);
            RestoreVisualPrefabPose();

            // Truyền 'true' để FastUnzoom không nhảy về khay bài, gây giật Hierarchy
            if (isInspecting) FastUnzoom(true); 

            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas != null)
            {
                canvas = canvas.rootCanvas; // Dùng canvas gốc để tọa độ kéo không phụ thuộc canvas riêng của lá bài.
                transform.SetParent(canvas.transform, true);
            }

            transform.localRotation = Quaternion.identity;
            if (visualChild != null) visualChild.localRotation = Quaternion.identity;
            canvasGroup.blocksRaycasts = false;

            RectTransform canvasRect = canvas.transform as RectTransform;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, eventData.position, eventData.pressEventCamera, out Vector2 startMousePos))
            {
                initialMousePos = startMousePos;
                initialAnchoredPos = (transform as RectTransform).anchoredPosition;
            }

            if (dragCoroutine != null) StopCoroutine(dragCoroutine);
            dragCoroutine = StartCoroutine(TiltDuringDrag());
        
        }
    }
    public void OnDrag(PointerEventData eventData)
    {
        if (!canDrag) return;

        bool isRightClickSkill = (eventData.button == PointerEventData.InputButton.Right);
        bool isRitualPhase = (TurnManager.instance.currentTurn == TurnState.CursePhase || TurnManager.instance.currentTurn == TurnState.SacrificePhase);

        if (isRightClickSkill || isRitualPhase)
        {
            if (TargetingArrow.instance != null) TargetingArrow.instance.UpdateArrow(eventData.position);
            return;
        }

        if (eventData.button == PointerEventData.InputButton.Left)
        {
            RectTransform rect = transform as RectTransform;
            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas != null) canvas = canvas.rootCanvas;
            RectTransform canvasRect = canvas.transform as RectTransform;

            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, eventData.position, eventData.pressEventCamera, out Vector2 currentMousePos))
            {
                rect.anchoredPosition = initialAnchoredPos + (currentMousePos - initialMousePos);
            }
        }
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        
        if (!canDrag) return;
        isAnyCardDragging = false;
        canvasGroup.blocksRaycasts = true;
        canDrag = false;

        if (visualChild != null) visualChild.localRotation = Quaternion.identity;
        transform.localRotation = Quaternion.identity;

        CardInstance ci = GetComponent<CardInstance>();

        if (eventData.button == PointerEventData.InputButton.Right)
        {
            if (TargetingArrow.instance != null) TargetingArrow.instance.Hide();
            
            List<RaycastResult> results = new List<RaycastResult>();
            EventSystem.current.RaycastAll(eventData, results);
            CardInstance targetCard = null;

            foreach (var result in results)
            {
                CardInteraction interaction = result.gameObject.GetComponentInParent<CardInteraction>();
                if (interaction != null && interaction != this)
                {
                    targetCard = interaction.GetComponent<CardInstance>();
                    break;
                }
            }

            if (ci != null && ci.cardData != null && ci.cardData.cardSkill != null)
            {
                ci.cardData.cardSkill.TryCastActiveSkill(ci, targetCard);
            }
            return; 
        }

        if (eventData.button != PointerEventData.InputButton.Left) return;

        if (TurnManager.instance.currentTurn == TurnState.CursePhase || TurnManager.instance.currentTurn == TurnState.SacrificePhase)
        {
            if (TargetingArrow.instance != null) TargetingArrow.instance.Hide();
            
            List<RaycastResult> results = new List<RaycastResult>();
            EventSystem.current.RaycastAll(eventData, results);

            foreach (var result in results)
            {
                SeatDropZone targetSeat = result.gameObject.GetComponentInParent<SeatDropZone>();
                if (targetSeat != null)
                {
                    int seatIndex = TurnManager.instance.allSeats.IndexOf(targetSeat);
                    if (seatIndex != -1)
                    {
                        if (TurnManager.instance.currentTurn == TurnState.CursePhase)
                            TurnManager.instance.ApplyCurse(seatIndex);
                        else if (TurnManager.instance.currentTurn == TurnState.SacrificePhase)
                            TurnManager.instance.ApplyVote(seatIndex);
                        break;
                    }
                }
            }
            return; 
        }

        
        SeatDropZone seat = transform.parent.GetComponentInParent<SeatDropZone>();

        if (seat != null) 
        {
            StartCoroutine(SmoothResetRotation(transform.localRotation, Quaternion.Euler(0, 0, 0f)));
        }
        else if (transform.parent != originalParent) 
        {
            if (!isPlaced && string.IsNullOrEmpty(TurnManager.instance.LocalActionFeedback))
                ActionFeedback.Reject("Drop the card onto a seat, or into the hand area to retrieve a seated card.");
            transform.SetParent(originalParent);
            transform.SetSiblingIndex((int)lastMousePos.y); 
            ReturnToHand(); 
        }

        if (TurnManager.instance != null)
        {
            TurnManager.instance.CheckGameOver();
        }
    }
    private IEnumerator TiltDuringDrag()
    {
        float currentTilt = 0;
        float entrySpeed = 0; 
        
        while (canDrag)
        {
            float targetTilt = Mathf.Clamp(Input.GetAxis("Mouse X") * -tiltAmount, -tiltAmount, tiltAmount);
            entrySpeed = Mathf.Lerp(entrySpeed, 1, Time.deltaTime * 10f); 
            currentTilt = Mathf.Lerp(currentTilt, targetTilt * entrySpeed, Time.deltaTime * tiltSpeed);
            
            if (visualChild != null)
                visualChild.localRotation = Quaternion.Euler(0, 0, currentTilt);
            else
                transform.localRotation = Quaternion.Euler(0, 0, currentTilt);

            yield return null;
        }
    }


    public void ToggleInspect()
    {
        RectTransform rect = transform as RectTransform;

        if (!isInspecting)
        {
            CardLocalVisualFeedback.For(this).StopHover(true);
            RestoreVisualPrefabPose();

            if (currentlyInspectedCard != null && currentlyInspectedCard != this)
                currentlyInspectedCard.FastUnzoom();

            isInspecting = true;
            currentlyInspectedCard = this;

            if (descriptionPanel != null && descriptionText != null)
            {
                RestoreCardDescription();
                descriptionPanel.SetActive(true);
            }

            if (CanvasDimmer.instance != null) CanvasDimmer.instance.Dim();

            Canvas canvas = GetComponent<Canvas>();
            if (canvas == null) canvas = gameObject.AddComponent<Canvas>();
            if (GetComponent<GraphicRaycaster>() == null) gameObject.AddComponent<GraphicRaycaster>();

            if (canvas != null) 
            {
                canvas.overrideSorting = true;
                canvas.sortingOrder = 1000;
            }

            preInspectPos = rect.anchoredPosition;
            preInspectRot = rect.localRotation;
            preInspectScale = rect.localScale;

            if (inspectCoroutine != null) StopCoroutine(inspectCoroutine);
            inspectCoroutine = StartCoroutine(InspectRoutine(true));
        }
        else
        {
            if (descriptionPanel != null) descriptionPanel.SetActive(false);

            bool isInCemetery = false;
            Transform checkParent = transform.parent;
            while (checkParent != null)
            {
                if (checkParent.name == "CemeteryViewPanel") { isInCemetery = true; break; }
                checkParent = checkParent.parent;
            }

            if (CanvasDimmer.instance != null && !isInCemetery) CanvasDimmer.instance.Undim();

            if (inspectCoroutine != null) StopCoroutine(inspectCoroutine);
            inspectCoroutine = StartCoroutine(InspectRoutine(false));
        }
    }

    public void FastUnzoom(bool isStartingDrag = false)
    {
        bool wasInspecting = isInspecting || currentlyInspectedCard == this;
        if (inspectCoroutine != null) StopCoroutine(inspectCoroutine);
        isInspecting = false;
        if (currentlyInspectedCard == this) currentlyInspectedCard = null;

        if (descriptionPanel != null) descriptionPanel.SetActive(false);
        if (wasInspecting && CanvasDimmer.instance != null &&
            (TurnManager.instance == null || TurnManager.instance.cemeteryViewPanel == null ||
             !TurnManager.instance.cemeteryViewPanel.activeInHierarchy)) CanvasDimmer.instance.Undim();

        RectTransform rect = transform as RectTransform;
        rect.localScale = preInspectScale;
        rect.localRotation = preInspectRot;

        if (!isStartingDrag)
        {
            if (originalParent != null) transform.SetParent(originalParent, false);
            rect.anchoredPosition = preInspectPos;
        }

        Canvas canvas = GetComponent<Canvas>();
        if (canvas != null) canvas.overrideSorting = false;
        
        Canvas.ForceUpdateCanvases();
    }
    private IEnumerator InspectRoutine(bool zoomIn)
    {
        Vector3 startPos = transform.position;
        Quaternion startRot = transform.rotation;
        Vector3 startScale = transform.localScale;

        Vector3 targetPos; 
        Quaternion targetRot; 
        Vector3 targetScale;

        if (zoomIn)
        {
            targetPos = InspectionPosition();
            targetRot = Quaternion.identity;
            targetScale = InspectionLocalScale();
        }
        else
        {
            RectTransform rect = transform as RectTransform;
            Vector2 oldAnchored = rect.anchoredPosition;
            rect.anchoredPosition = preInspectPos;
            targetPos = transform.position;
            rect.anchoredPosition = oldAnchored;

            targetRot = (preInspectRot.w == 0 && preInspectRot.x == 0 && preInspectRot.y == 0 && preInspectRot.z == 0) 
                        ? Quaternion.identity : preInspectRot;
            targetScale = preInspectScale;
        }

        float t = 0;
        while (t < 1)
        {
            t += Time.unscaledDeltaTime * inspectSpeed;
            float smoothT = Mathf.SmoothStep(0, 1, t);
            transform.position = Vector3.Lerp(startPos, targetPos, smoothT);
            transform.localScale = Vector3.Lerp(startScale, targetScale, smoothT);
            transform.rotation = Quaternion.Slerp(startRot, targetRot, smoothT);
            yield return null;
        }

        if (!zoomIn)
        {
            isInspecting = false;
            if (currentlyInspectedCard == this) currentlyInspectedCard = null;
            RectTransform rect = transform as RectTransform;
            rect.anchoredPosition = preInspectPos;
            rect.localRotation = targetRot;
            rect.localScale = preInspectScale;
            Canvas canvas = GetComponent<Canvas>();
            if (canvas != null) canvas.overrideSorting = false;
        }
    }

    public void PlaceOnSeat(Transform seatPos)
    {
        if (OnlineSession.Guest) return;
        if (TutorialController.Active && !TutorialController.Allow(TutorialAction.Place, GetComponent<CardInstance>(),
            seat: TurnManager.instance.allSeats.FindIndex(s => s.cardPos == seatPos))) return;
        if (TurnManager.instance == null || TurnManager.instance.isGameOver || TurnManager.instance.isTallyingVotes) return;
        if (isPlaced && (isLockedByElder || isLockedBySkill))
        { ActionFeedback.Reject(ActionFeedback.MovementBlock(this)); return; }
        CardInstance ci = GetComponent<CardInstance>();
        if (ci != null && ci.cardData != null && ci.cardData.cardSkill is Skill_GameMaster)
        {
            SeatDropZone targetSeat = seatPos.GetComponentInParent<SeatDropZone>();
            if (targetSeat != null && targetSeat.gameObject.name.Contains("ExtraSeat"))
            {
                ActionFeedback.Reject(ActionFeedback.ExtraSeatBlock(this, seatPos));
                
                StartCoroutine(HandleFailedPlace()); 
                return;
            }
        }

        if (TurnManager.instance == null) return;
        if (seatPos == originalParent) { ExecutePlace(seatPos); return; }

        int cost = GetMoveToEmptyCost(this);
        if (TurnManager.instance.HasEnoughHearts(cost))
        {
            TurnManager.instance.ConsumeHearts(cost);
            lastSwappedCard = null;
            ExecutePlace(seatPos); 
        }
        else { ActionFeedback.Reject(ActionFeedback.Cost(ci, cost)); StartCoroutine(HandleFailedPlace()); }
    }

   public void ReturnToHand()
    {
        if (OnlineSession.Guest) return;
        if (isPlaced && !TutorialController.Allow(TutorialAction.Return, GetComponent<CardInstance>())) return;
        if (TurnManager.instance == null || TurnManager.instance.isGameOver || TurnManager.instance.isTallyingVotes) return;
        if (isLockedByElder || isLockedBySkill)
        {
            ActionFeedback.Reject(ActionFeedback.MovementBlock(this));
            StartCoroutine(HandleFailedPlace());
            return;
        }

        if (!isPlaced && originalParent == handParent) { ExecuteReturnToHand(); return; }

        int cost = GetReturnToHandCost(this);

        CardAffiliation aff = GetAffiliation(this);
        bool isOwnerRetrieving = (aff == CardAffiliation.Mine || aff == CardAffiliation.Neutral);

        if (isOwnerRetrieving)
        {
            int refund = GameRules.RetrievalRefund(paidPlacementCost);
            TurnManager.instance.ConsumeHearts(-refund);
            Debug.Log($"<color=green>{name} được rút về, hoàn lại {refund} Tim.</color>");
            
            lastSwappedCard = null;
            ExecuteReturnToHand();
        }
        else
        {
            // Ép bài đối phương về tay phải trả phí và không hoàn tim cho chủ lá.
            if (TurnManager.instance.HasEnoughHearts(cost))
            {
                TurnManager.instance.ConsumeHearts(cost);
                lastSwappedCard = null;
                ExecuteReturnToHand();
            }
            else 
            {
                ActionFeedback.Reject(ActionFeedback.Cost(GetComponent<CardInstance>(), cost));
                StartCoroutine(HandleFailedPlace());
            }
        }
    }

    public void SwapWith(CardInteraction otherCard, Transform targetSeat)
    {
        if (OnlineSession.Guest) return;
        if (!TutorialController.Allow(TutorialAction.Swap, GetComponent<CardInstance>(), otherCard != null ? otherCard.GetComponent<CardInstance>() : null)) return;
        if (otherCard == null || TurnManager.instance == null || TurnManager.instance.isGameOver ||
            TurnManager.instance.isTallyingVotes) return;
        if (isLockedByElder || isLockedBySkill || otherCard.isLockedByElder || otherCard.isLockedBySkill ||
            isPlaced && lockState < 3 || otherCard.isPlaced && otherCard.lockState != 1 && otherCard.lockState != 3)
        { ActionFeedback.Reject(ActionFeedback.SwapBlock(this, otherCard, targetSeat)); return; }
        CardInstance myCi = GetComponent<CardInstance>();
        if (myCi != null && myCi.cardData != null && myCi.cardData.cardSkill is Skill_GameMaster)
        {
            SeatDropZone targetDropZone = targetSeat.GetComponentInParent<SeatDropZone>();
            if (targetDropZone != null && targetDropZone.gameObject.name.Contains("ExtraSeat"))
            {
                ActionFeedback.Reject(ActionFeedback.ExtraSeatBlock(this, targetSeat));
                StartCoroutine(HandleFailedPlace());
                return; 
            }
        }
        var otherCi = otherCard.GetComponent<CardInstance>();
        var oldDropZone = originalParent != null ? originalParent.GetComponentInParent<SeatDropZone>() : null;
        if (otherCi != null && otherCi.cardData != null && otherCi.cardData.cardSkill is Skill_GameMaster &&
            isPlaced && oldDropZone != null && oldDropZone.name.Contains("ExtraSeat"))
        { ActionFeedback.Reject(ActionFeedback.ExtraSeatBlock(otherCard, originalParent)); StartCoroutine(HandleFailedPlace()); return; }
        if (this.lastSwappedCard == otherCard || otherCard.lastSwappedCard == this)
        {
            ActionFeedback.Reject(ActionFeedback.SwapBlock(this, otherCard, targetSeat));
            StartCoroutine(HandleFailedPlace()); 
            return;
        }

        int cost = GetSwapCost(this, otherCard);
        if (TurnManager.instance.HasEnoughHearts(cost))
        {
            // Lưu ghế gốc để đổi vị trí hai lá trên bàn.
            Transform myOldSeat = this.originalParent; 

            if (this.isPlaced && otherCard.isPlaced)
            {
                TurnManager.instance.ConsumeHearts(cost);
                this.lastSwappedCard = otherCard;
                otherCard.lastSwappedCard = this;

                Debug.Log($"<color=cyan>Swap Board-to-Board: {this.name} <-> {otherCard.name}</color>");
                
                TurnManager.instance.BeginBoardMutation();
                try
                {
                    this.transform.SetParent(null);
                    otherCard.transform.SetParent(null);
                    otherCard.ExecutePlace(myOldSeat);
                    this.ExecutePlace(targetSeat);
                }
                finally { TurnManager.instance.EndBoardMutation(); }
            }
            else if (!this.isPlaced && otherCard.isPlaced)
            {
                TurnManager.instance.ConsumeHearts(cost);
                Debug.Log($"<color=yellow>Swap Hand-to-Board: {otherCard.name} bị đá về tay.</color>");
                
                TurnManager.instance.BeginBoardMutation();
                try { otherCard.ExecuteReturnToHand(); this.ExecutePlace(targetSeat); }
                finally { TurnManager.instance.EndBoardMutation(); }
            }
        }
        else 
        {
            ActionFeedback.Reject(ActionFeedback.Cost(myCi, cost));
            StartCoroutine(HandleFailedPlace());
        }
    }

    public void ExecutePlace(Transform seat)
    {
        if (OnlineSession.Guest) return;
        if (!isPlaced) paidPlacementCost = GetMoveToEmptyCost(this);
        isPlaced = true;
        if (lockState == 0) lockState = 1; 
        
        transform.SetParent(seat, false);
        originalParent = seat;

        if (canvasGroup != null)
        {
            canvasGroup.blocksRaycasts = true;
            canvasGroup.interactable = true;
        }

        // Kỹ năng HotPoint dùng người đặt bài gần nhất.
        CardInstance ci = GetComponent<CardInstance>();
        if (ci != null)
        {
            // Lật ngửa để lá vừa đặt có thể nhận input.
            ci.FaceUp(); 

            if (TurnManager.instance.currentTurn == TurnState.Player1Turn || TurnManager.instance.currentTurn == TurnState.SetupPhase)
                ci.lastPlacedBy = DefaultOwner.Player1; 
            else if (TurnManager.instance.currentTurn == TurnState.Player2Turn)
                ci.lastPlacedBy = DefaultOwner.Player2;
        } 

        RectTransform rect = transform as RectTransform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero; // Seat center is a gameplay position.
        rect.localPosition = Vector3.zero;
        transform.localScale = PrefabRootScale;
        transform.localRotation = Quaternion.identity; 

        // Dừng hover trước khi đặt lại pose để coroutine không ghi đè ở frame kế tiếp.
        CardLocalVisualFeedback.For(this).StopHover(true);
        RestoreVisualPrefabPose();

        LayoutElement layoutElement = GetComponent<LayoutElement>();
        if (layoutElement != null) layoutElement.ignoreLayout = true; 
        
        CardLocalVisualFeedback.For(this).StopHover(true);
        
        if (handParent != null) DeckManager.instance.ArrangeFanLayout(handParent);
        TurnManager.instance.RefreshAllAuras();
        if (ci != null && ci.cardData != null && ci.cardData.cardSkill != null)
        {
            int seatIdx = TurnManager.instance.GetSeatIndex(this);
            ci.cardData.cardSkill.TryOnPlaceOnBoard(ci, seatIdx);
        }
        // Bot, đổi chỗ và lệnh online cũng đi qua ExecutePlace.
        if (isPlaced && TryGetComponent<CardDialogue>(out var dialogue)) dialogue.StartChatting();
    }
    public void ExecuteReturnToHand()
    {
        if (OnlineSession.Guest) return;
        CardInstance ci = GetComponent<CardInstance>();
        if (isPlaced && ci != null && ci.cardData != null && ci.cardData.cardSkill != null)
        {
            int seatIdx = TurnManager.instance.GetSeatIndex(this);
            isPlaced = false; // Leaving takes effect before cleanup can refresh other auras.
            ci.cardData.cardSkill.OnRemoveFromBoard(ci, seatIdx);
        }

        CardDialogue dialogue = GetComponent<CardDialogue>();
        if (dialogue != null) dialogue.StopChatting();
        
        isPlaced = false;
        lockState = 0; 
        isLockedByElder = false;
        isProtectedByPreacher = false;

        LayoutElement le = GetComponent<LayoutElement>();
        if (le != null) le.ignoreLayout = false;

        if (ci != null)
        {
            if (ci.cardData.cardSkill is Skill_Prankster || ci.cardData.cardSkill is Skill_Detective)
            {
                ci.hasUsedSkill = false; 
            }
            ci.ClearDetectiveMarkOnly();
        }

        Transform targetHand = handParent; 
        if (ci != null && ci.cardData != null)
        {
            if (ci.cardData.owner == DefaultOwner.Player1) targetHand = DeckManager.instance.player1Hand; 
            else if (ci.cardData.owner == DefaultOwner.Player2) targetHand = DeckManager.instance.player2Hand;
            else if (ci.cardData.owner == DefaultOwner.Neutral) targetHand = DeckManager.instance.neutralHand;
        }

        // Đặt lại scale và góc trước khi xếp vào tay để tránh giữ pose đang kéo.
        transform.SetParent(targetHand, false);
        transform.localScale = PrefabRootScale;
        transform.localRotation = Quaternion.identity;
        
        originalParent = targetHand; 
        handParent = targetHand; 

        if (DeckManager.instance != null)
        {
            DeckManager.instance.SortHand(targetHand);
            DeckManager.instance.ArrangeFanLayout(targetHand);
        }
        
       if (TurnManager.instance != null)
        {
            // Hoãn cập nhật aura trong lúc dọn bàn ở pha hiến tế.
            if (TurnManager.instance.currentTurn != TurnState.SacrificePhase)
            {
                TurnManager.instance.RefreshAllAuras();
            }
        }
    }
    public void OnDrop(PointerEventData eventData)
    {
        if (OnlineSession.Guest) return;
        if (TurnManager.instance == null) return;
        if (eventData.button != PointerEventData.InputButton.Left) return;
        
        TurnState state = TurnManager.instance.currentTurn;
        if (state == TurnState.SetupPhase || state == TurnState.CursePhase || state == TurnState.SacrificePhase) 
            return;

        GameObject droppedObj = eventData.pointerDrag;
        if (droppedObj != null)
        {
            CardInteraction draggedCard = droppedObj.GetComponent<CardInteraction>();
            
            if (draggedCard != null && draggedCard != this && this.isPlaced && draggedCard.canDrag)
            {
                if (this.lockState == 1 || this.lockState == 3)
                {
                    draggedCard.SwapWith(this, this.originalParent);
                }
                else
                {
                    ActionFeedback.Reject(ActionFeedback.SwapBlock(draggedCard, this, originalParent));
                    
                    if (draggedCard.isPlaced)
                    {
                        draggedCard.ExecutePlace(draggedCard.originalParent);
                    }
                    else
                    {
                        draggedCard.ExecuteReturnToHand();
                    }

                    draggedCard.StartCoroutine("ShakeCard");
                }
            }
        }
    }

    public int PlacementCost => GetMoveToEmptyCost(this);
    public int ReturnCost => GetReturnToHandCost(this);
    // Retrieving your own or a Neutral card refunds hearts; it does not charge this cost.
    public int ReturnPaymentCost => GetAffiliation(this) == CardAffiliation.Opponent ? ReturnCost : 0;
    public int SwapCost(CardInteraction target) => GetSwapCost(this, target);
    private int GetMoveToEmptyCost(CardInteraction card)
    {
        CardInstance ci = card.GetComponent<CardInstance>();
        if (ci != null && !ci.isSilenced && ci.cardData != null && ci.cardData.cardSkill is Skill_Totem)
            return 1; // Đặt Totem luôn chỉ tốn 1 Tim
        if (ci != null && ci.cardData != null && ci.cardData.cardSkill is Skill_Prankster)
        {
            if (!card.isPlaced) return 3; // Từ tay đặt xuống tốn đúng 3 Tim
        }
        CardAffiliation aff = GetAffiliation(card);
        if (!card.isPlaced) 
        {
            if (aff == CardAffiliation.Mine) return 2;
            if (aff == CardAffiliation.Opponent) return 5;
            if (aff == CardAffiliation.Neutral) return 5;
        }
        else 
        {
            if (aff == CardAffiliation.Mine || aff == CardAffiliation.Neutral) return 1;
            if (aff == CardAffiliation.Opponent) return 3;
        }
        return 0;
    }

    private int GetReturnToHandCost(CardInteraction card)
    {
        CardInstance ci = card.GetComponent<CardInstance>();
        if (ci != null && !ci.isSilenced && ci.cardData != null && ci.cardData.cardSkill is Skill_Totem)
            return 1; // Thu hồi Totem luôn chỉ tốn 1 Tim
        CardAffiliation aff = GetAffiliation(card);
        if (aff == CardAffiliation.Mine) return 2;
        if (aff == CardAffiliation.Opponent) return 5;
        if (aff == CardAffiliation.Neutral) return 2;
        return 0;
    }

    private int GetSwapCost(CardInteraction card1, CardInteraction card2)
    {
        CardAffiliation aff1 = GetAffiliation(card1);
        CardAffiliation aff2 = GetAffiliation(card2);
        bool isHandSwap = (!card1.isPlaced || !card2.isPlaced);

        if (isHandSwap) 
        {
            if ((aff1 == CardAffiliation.Mine || aff1 == CardAffiliation.Neutral) && 
                (aff2 == CardAffiliation.Mine || aff2 == CardAffiliation.Neutral)) return 3;
            return 5;
        }
        else 
        {
            int oppCount = (aff1 == CardAffiliation.Opponent ? 1 : 0) + (aff2 == CardAffiliation.Opponent ? 1 : 0);
            if (oppCount == 0) return 2; 
            if (oppCount == 1) return 3; 
            return 5; 
        }
    }

    private CardAffiliation GetAffiliation(CardInteraction card)
    {
        CardInstance ci = card.GetComponent<CardInstance>();
        if (ci.cardData.owner == DefaultOwner.Neutral) return CardAffiliation.Neutral;
        bool isP1Turn = TurnManager.instance.currentTurn == TurnState.Player1Turn;
        if (isP1Turn && ci.cardData.owner == DefaultOwner.Player1) return CardAffiliation.Mine;
        if (!isP1Turn && ci.cardData.owner == DefaultOwner.Player2) return CardAffiliation.Mine;
        return CardAffiliation.Opponent;
    }

    private IEnumerator HandleFailedPlace()
    {
        isAnyCardDragging = false; 
        canDrag = false;
        canvasGroup.blocksRaycasts = true;

        // Đưa về đúng panel trước khi xếp lại góc quạt của tay bài.
        if (isPlaced && originalParent != null && originalParent.name != "Hand") 
        {
            ExecutePlace(originalParent); 
        }
        else 
        {
            ExecuteReturnToHand(); 
        }

        Image cardImage = null;
        if (visualChild != null) cardImage = visualChild.GetComponent<Image>();
        Color originalColor = (cardImage != null) ? cardImage.color : Color.white;

        if (cardImage != null) cardImage.color = new Color(1f, 0.5f, 0.5f);
        yield return StartCoroutine(ShakeCard()); 
        if (cardImage != null) cardImage.color = originalColor;

        yield return new WaitForEndOfFrame();
    }

    private IEnumerator ShakeCard()
    {
        if (isShaking || visualChild == null) yield break;
        isShaking = true;

        RectTransform visRect = visualChild as RectTransform;
        Vector2 originalPos = visRect.anchoredPosition;
        float elapsed = 0f;

        while (elapsed < shakeDuration)
        {
            // Refusal motion is presentation only; it must not consume the gameplay RNG.
            float t=elapsed/Mathf.Max(.01f,shakeDuration);
            visRect.anchoredPosition = originalPos + Vector2.right*Mathf.Sin(t*Mathf.PI*8)*shakeMagnitude*(1-t);
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        visRect.anchoredPosition = originalPos;
        isShaking = false;
    }
    private IEnumerator SmoothResetRotation(Quaternion startRot, Quaternion targetRot)
    {
        float t = 0;
        while (t < 1)
        {
            t += Time.deltaTime * 8f;
            transform.localRotation = Quaternion.Lerp(startRot, targetRot, t);
            yield return null;
        }
        transform.localRotation = targetRot;
    }

    public void ForceReturnToHand()
    {
        CardInstance ci = GetComponent<CardInstance>();
        if (ci != null) 
        {
            // Bệnh vẫn được giữ khi lá bài trở về tay.
            if (ci.infectionEffect != null) ci.infectionEffect.SetActive(false);

            ci.FaceUp(); 
        }

        ExecuteReturnToHand();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        CardActionRouter.PointerClick(this, GetComponent<OnlineCardInput>(), eventData);
    }

    // Router dùng chung cho host và guest; phần đổi trạng thái chỉ chạy trên host.
    public void PlayLocalClickAudio()
    {
        if (audioSource != null && clickSFX != null) audioSource.PlayOneShot(clickSFX);
    }

    public void PerformAuthoritativePointerClick(PointerEventData eventData)
    {
        if (TurnManager.instance == null || GameStartUI.IsBlockingInput) return;
        if (isInspecting)
        {
            if (eventData.button == PointerEventData.InputButton.Right)
            {
                ToggleInspect();
            }
            return;
        }

        if (currentlyInspectedCard != null) return;
        CardInstance ci = GetComponent<CardInstance>();
        if (ci == null) return;

        if (ci.isFaceDown) 
        {
            ActionFeedback.Reject("This card is face down. Its information and actions are unavailable.");
            return;
        }

        if (eventData.button == PointerEventData.InputButton.Right)
        {
            if (!TutorialController.Allow(TutorialAction.Inspect, ci)) return;
            ToggleInspect();
            return;
        }

        if (OnlineSession.Guest) return;
        string blocked = ActionFeedback.InputBlock();
        if (blocked != null) { ActionFeedback.Reject(blocked); return; }
        if (TurnManager.instance.isGameOver || TurnManager.instance.isTallyingVotes ||
            TurnManager.instance.IsBotTurn || ci.isDead) return;
        if (OnlineSession.Active && !OnlineSession.LocalTurn) return;

        if (eventData.button == PointerEventData.InputButton.Left && !eventData.dragging)
        {
            Debug.Log($"<color=white>Chuột Trái vào {ci.name} | Giai đoạn: {TurnManager.instance.currentTurn}</color>");
            if (transform.parent != null && transform.parent.name == "CemeteryViewPanel") 
            {
                return;
            }

            if (isPlaced && ci.cardData.cardSkill != null && (ci.cardData.cardSkill.skillType == SkillType.Active || ci.cardData.cardSkill.skillType == SkillType.Hybrid))
            {
                // Target null biểu thị thao tác click, khác với kéo kỹ năng vào một mục tiêu.
                bool isActivated = ci.cardData.cardSkill.TryCastActiveSkill(ci, null);
                
                // Kỹ năng thành công kết thúc xử lý click để không chọn thêm mục tiêu bên dưới.
                if (isActivated) return; 
            }

            if (TurnManager.instance.currentTurn == TurnState.SetupPhase) 
            {
                TurnManager.instance.SelectCurCard(ci);
            }
            else if (TurnManager.instance.currentTurn == TurnState.CursePhase)
            {
                bool isCur = TurnManager.instance.allCurs.Contains(ci);
                if (isCur && isPlaced) 
                {
                    ci.UseSkill();
                }
                else 
                {
                    TurnManager.instance.ApplyCurse(TurnManager.instance.GetSeatIndex(this));
                }
            }
        }
    }
    public static void ForceUnzoom()
    {
        if (currentlyInspectedCard != null && currentlyInspectedCard.isInspecting)
            currentlyInspectedCard.ToggleInspect();
    }
    // Inspection uses root-Canvas scale; closing restores the saved board or hand pose.
    private Vector3 InspectionLocalScale()
    {
        Vector3 reference = transform.root.lossyScale;
        Vector3 parent = transform.parent != null ? transform.parent.lossyScale : Vector3.one;
        Vector3 desired = PrefabRootScale * inspectScaleMultiplier;
        return new Vector3(desired.x * reference.x / Mathf.Max(.0001f, Mathf.Abs(parent.x)),
            desired.y * reference.y / Mathf.Max(.0001f, Mathf.Abs(parent.y)),
            desired.z * reference.z / Mathf.Max(.0001f, Mathf.Abs(parent.z)));
    }

    // Center the card and description together.
    public Vector3 InspectionPosition()
    {
        Vector3 center=transform.root.position;
        if(descriptionPanel==null||!descriptionPanel.activeSelf)return center;
        var cardRect=(RectTransform)transform;var panel=(RectTransform)descriptionPanel.transform;
        float left=Mathf.Min(cardRect.rect.xMin,panel.anchoredPosition.x-panel.rect.width*panel.pivot.x);
        float right=Mathf.Max(cardRect.rect.xMax,panel.anchoredPosition.x+panel.rect.width*(1-panel.pivot.x));
        float parentScale=transform.parent!=null?Mathf.Abs(transform.parent.lossyScale.x):1;
        return center-transform.root.right*((left+right)*.5f*InspectionLocalScale().x*parentScale);
    }

    public void SetDescription(string source)
    {
        if(descriptionText==null)return;
        descriptionText.text=Localizer.T(source);
        var localized=descriptionText.GetComponent<LocalizedLabel>();
        if(localized!=null)localized.Refresh();
    }

    public void ShowHatDescription()
    {
        if (descriptionText != null)
        {
            SetDescription("<color=yellow><b>SUSPICION MARK:</b></color>\nThis card is under suspicion by the Detective. Each stacked hat increases the target's suspicion level.");
        }
    }

    public void RestoreCardDescription()
    {
        if (descriptionText != null)
        {
            CardInstance ci = GetComponent<CardInstance>();
            if (ci != null && ci.cardData != null)
            {
                if(ci.cardImage==null)return;
                bool isShowingCurArt = ci.cardData.cursemancerArtwork!=null&&ci.cardImage.sprite==ci.cardData.cursemancerArtwork;
                bool isShowingInfection = ci.infectedMaterial!=null&&ci.cardImage.material==ci.infectedMaterial||
                    ci.infectionEffect!=null&&ci.infectionEffect.activeSelf;
                string source=isShowingCurArt&&isShowingInfection?ci.cardData.curInfectedDescription:
                    isShowingCurArt?ci.cardData.curDescription:isShowingInfection?ci.cardData.infectedDescription:ci.cardData.description;
                SetDescription(string.IsNullOrWhiteSpace(source)?ci.cardData.description:source);
            }
        }
    }
}

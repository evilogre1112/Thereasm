using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Camera-like viewport for the meeting table only. Never move/scale the fixed HUD,
// controls or hand dock. Seats and cards remain children of the same authored board.
// Scroll = zoom at cursor; left-drag EMPTY board or middle-drag = pan.
[DisallowMultipleComponent]
public class GameBoardLayout : MonoBehaviour
{
    [SerializeField] private TurnManager turns;
    [SerializeField] private RectTransform board;
    [SerializeField] private RectTransform viewport;
    [SerializeField] private bool fitOnStart = true;
    [SerializeField] private bool fitWhenSeatCountChanges = true;
    [SerializeField, Min(0)] private float padding = 48f;

    [Header("Reserved region under faction / turn bars")]
    [SerializeField, Min(0)] private float lowerBoardBy = 16f;
    [SerializeField] private bool clipToBoardArea = true;

    [Header("Board navigation (not card inspection)")]
    [SerializeField] private bool enableWheelZoom = true;
    [SerializeField, Range(1.01f, 2f)] private float zoomPerWheelStep = 1.15f;
    [SerializeField, Min(1f)] private float maximumZoom = 5f;
    [SerializeField] private bool middleMousePan = true;
    [SerializeField] private bool leftMousePanFromEmptySpace = true;
    [SerializeField] private bool resetZoomWhenSeatsChange = true;
    [SerializeField, Min(0)] private float visibleEdgePadding = 8f;

    public static GameBoardLayout Active { get; private set; }
    public RectTransform Viewport => viewport;

    private int lastSeatCount = -1;
    private bool refitAfterSeatChange;
    private int panMouseButton = -1;
    private Vector2 lastPointer;
    private Vector2 boundsMin, boundsMax;
    private float fitScale = 1f;
    private float zoom = 1f;
    private Vector2 pan;
    private Vector2 lastViewportSize;
    private bool hasBounds;
    private readonly List<RaycastResult> raycastResults = new List<RaycastResult>(16);
    private PointerEventData pointerEvent;
    private readonly Vector3[] corners = new Vector3[4];

    void Awake()
    {
        Active = this;
        // Add the viewport mask for scenes that do not already have one.
        EnsureClip();
    }

    void OnDestroy() { if (Active == this) Active = null; }

    public void Initialize(TurnManager manager, RectTransform area)
    {
        turns = manager;
        if (area != null) viewport = area;
        if (board == null && turns != null && turns.allSeats != null && turns.allSeats.Count > 0)
            board = turns.allSeats[0].transform.parent as RectTransform;
        EnsureClip();
        if (fitOnStart) FitBoard();
    }

    private void EnsureClip()
    {
        RectTransform target = viewport != null ? viewport : transform as RectTransform;
        if (!clipToBoardArea || target == null) return;
        // Standard UI text/labels remain clipped by RectMask2D. Custom art
        // shaders lack _ClipRect/stencil, so BoardArtworkClip trims those meshes.
        if (target.GetComponent<RectMask2D>() == null)
            target.gameObject.AddComponent<RectMask2D>();
    }

    // Relative to the fixed viewport: 1 means normal authored-card size.
    public float BoardVisualScale => board != null ? Mathf.Abs(board.localScale.x) : 1f;

    private void AttachArtworkClips()
    {
        if (!clipToBoardArea || viewport == null || turns == null || turns.allSeats == null) return;
        foreach (SeatDropZone seat in turns.allSeats)
        {
            if (seat == null || seat.cardPos == null || !seat.gameObject.activeInHierarchy) continue;
            CardInstance card = seat.cardPos.GetComponentInChildren<CardInstance>();
            if (card == null || card.cardImage == null || !card.transform.IsChildOf(viewport)) continue;
            Image art = card.cardImage;
            BoardArtworkClip effect = art.GetComponent<BoardArtworkClip>();
            if (effect == null) effect = art.gameObject.AddComponent<BoardArtworkClip>();
            effect.SetViewport(viewport);
        }
    }

    void LateUpdate()
    {
        if (turns != null && turns.allSeats != null && board != null && viewport != null && fitWhenSeatCountChanges)
        {
            bool countChanged = turns.allSeats.Count != lastSeatCount;
            if (countChanged)
            {
                if (resetZoomWhenSeatsChange) zoom = 1f;
                pan = Vector2.zero;
                panMouseButton = -1;
                FitBoard();
                // Destroy() of an old extra seat is deferred until frame end.
                refitAfterSeatChange = true;
            }
            else if (refitAfterSeatChange)
            {
                refitAfterSeatChange = false;
                FitBoard();
            }
            else if ((lastViewportSize - viewport.rect.size).sqrMagnitude > .01f)
            {
                FitBoard();
            }
            else if (ReadSeatBounds(out Vector2 min, out Vector2 max) &&
                ((min - boundsMin).sqrMagnitude > .25f || (max - boundsMax).sqrMagnitude > .25f))
            {
                // A GameMaster can move, repositioning extra seats without
                // changing their number. Do not leave new seats outside camera bounds.
                FitBoard();
            }
        }
        HandleBoardInput();
        AttachArtworkClips();
    }

    private bool CanNavigateBoard()
    {
        return hasBounds && board != null && viewport != null &&
            !CardInteraction.isAnyCardDragging && CardInteraction.currentlyInspectedCard == null &&
            !GameStartUI.IsBlockingInput && !DialogueModalGuard.IsOpen &&
            (turns == null || (!turns.isGameOver && !turns.isTallyingVotes)) &&
            (EventSystem.current == null || EventSystem.current.currentSelectedGameObject == null ||
             !EventSystem.current.currentSelectedGameObject.TryGetComponent<InputField>(out _));
    }

    private Camera ViewportCamera()
    {
        Canvas canvas = viewport != null ? viewport.GetComponentInParent<Canvas>() : null;
        return canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
    }

    private bool PointerWithinBoard(out Vector2 localPoint)
    {
        localPoint = Vector2.zero;
        if (viewport == null) return false;
        Camera camera = ViewportCamera();
        if (!RectTransformUtility.RectangleContainsScreenPoint(viewport, Input.mousePosition, camera)) return false;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(viewport, Input.mousePosition, camera, out localPoint)) return false;
        // Board has center anchors; viewport itself has top-left pivot.
        localPoint -= viewport.rect.center;
        return true;
    }

    private bool IsEmptyBoardUnderMouse()
    {
        if (EventSystem.current == null) return true;
        if (pointerEvent == null)
            pointerEvent = new PointerEventData(EventSystem.current);
        pointerEvent.position = Input.mousePosition;
        raycastResults.Clear();
        EventSystem.current.RaycastAll(pointerEvent, raycastResults);
        foreach (RaycastResult hit in raycastResults)
        {
            if (hit.gameObject == null) continue;
            Transform t = hit.gameObject.transform;
            // Never steal a card/seat's drag, a button click, a tooltip's
            // interaction, or a control that happens to overlap the board.
            if (t.GetComponentInParent<CardInteraction>() != null ||
                t.GetComponentInParent<SeatDropZone>() != null ||
                t.GetComponentInParent<Selectable>() != null)
                return false;
        }
        return true;
    }

    private void HandleBoardInput()
    {
        if (!CanNavigateBoard()) { panMouseButton = -1; return; }
        bool inside = PointerWithinBoard(out Vector2 point);
        // Convenient way to recover the overview after navigating far away.
        if (inside && Input.GetKeyDown(KeyCode.Home))
        {
            ResetView();
            return;
        }
        if (enableWheelZoom && inside)
        {
            float wheel = Input.mouseScrollDelta.y;
            if (Mathf.Abs(wheel) > .001f)
            {
                float previousScale = fitScale * zoom;
                float newZoom = Mathf.Clamp(zoom * Mathf.Pow(zoomPerWheelStep, wheel), 1f, maximumZoom);
                if (!Mathf.Approximately(newZoom, zoom) && previousScale > .00001f)
                {
                    Vector2 previousBoardPosition = board.anchoredPosition;
                    zoom = newZoom;
                    float nextScale = fitScale * zoom;
                    // Keep world point below cursor in place while zooming.
                    Vector2 desiredPosition = point - (point - previousBoardPosition) * (nextScale / previousScale);
                    pan = desiredPosition - FittedCenter(nextScale);
                    ApplyView();
                }
            }
        }

        if (panMouseButton < 0 && inside)
        {
            if (middleMousePan && Input.GetMouseButtonDown(2))
                panMouseButton = 2;
            else if (leftMousePanFromEmptySpace && Input.GetMouseButtonDown(0) && IsEmptyBoardUnderMouse())
                panMouseButton = 0;
            if (panMouseButton >= 0) lastPointer = point;
        }
        if (panMouseButton < 0) return;
        if (Input.GetMouseButtonUp(panMouseButton) || !Input.GetMouseButton(panMouseButton))
        {
            panMouseButton = -1;
            return;
        }
        // Continue dragging even if the cursor briefly leaves the viewport,
        // but clamp the board so it cannot get lost offscreen.
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
            viewport, Input.mousePosition, ViewportCamera(), out Vector2 newPoint))
        {
            newPoint -= viewport.rect.center;
            pan += newPoint - lastPointer;
            lastPointer = newPoint;
            ApplyView();
        }
    }

    private bool ReadSeatBounds(out Vector2 min, out Vector2 max)
    {
        min = new Vector2(float.MaxValue, float.MaxValue);
        max = new Vector2(float.MinValue, float.MinValue);
        if (turns == null || turns.allSeats == null || board == null) return false;
        bool found = false;
        foreach (SeatDropZone seat in turns.allSeats)
        {
            if (seat == null || !seat.gameObject.activeInHierarchy) continue;
            if (!(seat.transform is RectTransform rect) || !rect.IsChildOf(board)) continue;
            rect.GetWorldCorners(corners);
            foreach (Vector3 corner in corners)
            {
                Vector3 p = board.InverseTransformPoint(corner);
                min = Vector2.Min(min, new Vector2(p.x, p.y));
                max = Vector2.Max(max, new Vector2(p.x, p.y));
            }
            found = true;
        }
        // The Cemetery is a separate board child, not a SeatDropZone and not
        // present in turns.allSeats. Include its *outer* rect in the fitting
        // bounds, otherwise ResetView/Home can clip it at the right edge.
        // cemeteryCardPos is a child of the visible Cemetery button/slot.
        if (turns.cemeteryCardPos != null)
        {
            RectTransform cemetery = turns.cemeteryCardPos.parent as RectTransform;
            if (cemetery != null && cemetery.gameObject.activeInHierarchy && cemetery.IsChildOf(board))
            {
                cemetery.GetWorldCorners(corners);
                foreach (Vector3 corner in corners)
                {
                    Vector3 p = board.InverseTransformPoint(corner);
                    min = Vector2.Min(min, new Vector2(p.x, p.y));
                    max = Vector2.Max(max, new Vector2(p.x, p.y));
                }
                found = true;
            }
        }
        return found && max.x > min.x && max.y > min.y;
    }

    [ContextMenu("Fit All Seats (Reset Pan and Zoom)")]
    public void ResetView()
    {
        zoom = 1f;
        pan = Vector2.zero;
        FitBoard();
    }

    [ContextMenu("Fit Board In Reserved Area")]
    public void FitBoard()
    {
        if (board == null || viewport == null || turns == null || turns.allSeats == null) return;
        lastSeatCount = turns.allSeats.Count;
        if (!ReadSeatBounds(out boundsMin, out boundsMax)) { hasBounds = false; return; }
        hasBounds = true;
        lastViewportSize = viewport.rect.size;
        Vector2 size = boundsMax - boundsMin;
        float availableWidth = Mathf.Max(1f, viewport.rect.width - padding);
        float availableHeight = Mathf.Max(1f, viewport.rect.height - padding);
        fitScale = Mathf.Max(.001f, Mathf.Min(1f, availableWidth / size.x, availableHeight / size.y));
        ApplyView();
    }

    private Vector2 FittedCenter(float scale) =>
        -(boundsMin + boundsMax) * (.5f * scale) + Vector2.down * lowerBoardBy;

    private void ApplyView()
    {
        if (!hasBounds || board == null || viewport == null) return;
        float scale = fitScale * zoom;
        board.localScale = Vector3.one * scale;
        Vector2 position = FittedCenter(scale) + pan;
        float hw = Mathf.Max(8f, viewport.rect.width * .5f - visibleEdgePadding);
        float hh = Mathf.Max(8f, viewport.rect.height * .5f - visibleEdgePadding);
        position.x = ClampAxis(position.x, boundsMin.x * scale, boundsMax.x * scale, hw);
        position.y = ClampAxis(position.y, boundsMin.y * scale, boundsMax.y * scale, hh);
        board.anchoredPosition = position;
        pan = position - FittedCenter(scale);
    }

    private static float ClampAxis(float position, float contentMin, float contentMax, float halfArea)
    {
        float lower = halfArea - contentMax;
        float upper = -halfArea - contentMin;
        return lower <= upper ? Mathf.Clamp(position, lower, upper) : Mathf.Clamp(position, upper, lower);
    }

    public static void MoveHand(Transform hand, RectTransform area)
    {
        if (!(hand is RectTransform rect)) return;
        rect.SetParent(area, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
        rect.anchoredPosition = new Vector2(0, -8);
        rect.sizeDelta = Vector2.zero;
        rect.localScale = Vector3.one * .72f;
    }
}

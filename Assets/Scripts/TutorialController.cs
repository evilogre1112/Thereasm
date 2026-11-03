using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Exists only in TutorialBoard. All UI, artwork and lesson data are authored scene/asset references.
[DefaultExecutionOrder(500)]
public class TutorialController : MonoBehaviour
{
    public TutorialWorkshop workshop;
    [SerializeField] private TutorialLesson lesson;
    [SerializeField] private TurnManager turns;
    [SerializeField] private DeckManager deck;
    [SerializeField] private RectTransform overlay, sourceFrame, targetFrame, directionArrow;
    [SerializeField] private Image sourceOutline, targetOutline, progressFill;
    [SerializeField] private TutorialSpotlight spotlight;
    [SerializeField] private Text sourceActionLabel,targetActionLabel;
    [SerializeField] private Text progress, title, instruction, purpose, feedback, continueLabel, header, headerHint;
    [SerializeField] private Button continueButton, practiceButton, villageTab;
    [SerializeField] private GameObject startControl, endControl, checkControl, revealControl, cemeteryControl;
    [SerializeField] private GameObject setupPanel, resultPanel;
    [SerializeField] private GameObject heartGuidePanel;
    [SerializeField] private Text heartGuideText;
    [SerializeField] private Button[] controlledButtons;
    public static TutorialController Instance { get; private set; }
    public static bool Active => Instance != null;
    public TutorialStage Stage { get; private set; }
    private readonly Dictionary<int, CardInstance> cards = new Dictionary<int, CardInstance>();
    private readonly Vector3[] corners = new Vector3[4];
    private bool ready, scripted, leaving, restoreSettings = true;
    private float passedAt = -1;
    private GameMode previousMode;
    private int previousLevel;
    private string lastSuccess = "Đừng lo, tôi sẽ đi cùng bạn từng bước.";

    void Awake()
    {
        Instance = this;
        previousMode = GameSettings.currentMode; previousLevel = GameSettings.currentLevel;
        if (OnlineSession.Instance != null) OnlineSession.Instance.Leave();
        GameSettings.currentMode = GameMode.PvP;
        GameSettings.currentLevel = 1;
        // Awake runs before any Start. Build the exercise ourselves after the managers initialize.
        deck.enabled = false;
        if (deck.draftPanel != null) deck.draftPanel.SetActive(false);
    }

    IEnumerator Start()
    {
        yield return null;
        if(workshop!=null&&(TutorialJourney.Selected!=null||workshop.previewExercise!=null))
        {yield return workshop.Begin(TutorialJourney.Selected!=null?TutorialJourney.Selected:workshop.previewExercise);yield break;}
        var sequence = lesson != null && lesson.steps != null ? Array.ConvertAll(lesson.steps, s => s != null ? (int)s.stage : -1) : null;
        if (!TutorialRules.ValidSequence(sequence) || !ValidCards())
        {
            instruction.text = "Bài hướng dẫn thiếu dữ liệu. Chọn Về menu, hoặc sửa FirstMatchLesson trong Inspector.";
            Debug.LogError("Thereasm tutorial: invalid lesson sequence or fixed card set.");
            yield break;
        }
        deck.BuildTutorialDeck(lesson.cards);
        yield return null; // Let CardInteraction record each card's authored hand parent.
        foreach (var card in GetComponentsInScene<CardInstance>())
            if (card.cardData != null && !card.transform.IsChildOf(deck.draftPanel.transform))
            {
                cards[card.cardData.cardNumber] = card;
                // A guided target stays under the pointer instead of moving on hover.
                card.GetComponent<CardInteraction>().hoverOffsetY = 0;
            }
        if (cards.Count != 7) { Debug.LogError("Tutorial expected exactly seven exercise cards."); yield break; }
        turns.firstPlayerThisRound = TurnState.Player2Turn;
        turns.allCurs.Clear(); turns.allyCur = turns.enemyCur = null;
        turns.currentTurn = TurnState.SetupPhase;
        turns.RefreshTutorialView();
        deck.SwitchToHand(DefaultOwner.Player1);
        ready = true;
        Enter(TutorialStage.Welcome);
    }

    private bool ValidCards()
    {
        if (lesson.cards == null || lesson.cards.Length != 7) return false;
        var numbers = new HashSet<int>();
        foreach (var card in lesson.cards)
        {
            if (card == null || !numbers.Add(card.cardNumber)) return false;
            if (card.owner != (card.cardNumber <= 3 ? DefaultOwner.Player1 : DefaultOwner.Player2)) return false;
            if (card.cardNumber == 9 && !(card.cardSkill is Skill_Hunter)) return false;
            if (card.cardNumber != 9 && card.cardSkill != null) return false;
        }
        return numbers.SetEquals(new[] { 1, 2, 3, 9, 10, 11, 12 });
    }
    private T[] GetComponentsInScene<T>() where T : Component
    {
        var found = new List<T>();
        foreach (var component in FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (component.gameObject.scene == gameObject.scene) found.Add(component);
        return found.ToArray();
    }
    private CardInstance Card(int number) => cards.TryGetValue(number, out var card) ? card : null;
    private static int Number(CardInstance card) => card != null && card.cardData != null ? card.cardData.cardNumber : 0;
    public static bool Allow(TutorialAction action, CardInstance source = null, CardInstance target = null, int seat = -1)
    {
        var guide = Instance;
        if(guide!=null&&guide.workshop!=null&&guide.workshop.Running)return guide.workshop.Allows(action,source,target,seat);
        if (guide == null || guide.scripted) return true;
        return guide.ready && !guide.leaving && guide.passedAt < 0 &&
            TutorialRules.Allows(guide.Stage, action, Number(source), Number(target), seat);
    }
    public static void ExplainBlockedAction()
    {
        if (Active && !IsScripted) ActionFeedback.Reject("Try the highlighted action first. The current instruction tells you which card and mouse button to use.");
    }
    public static bool CanDrag(CardInstance source, bool rightButton)
    {
        if (!Active) return true;
        if(Instance.workshop!=null&&Instance.workshop.Running)return Instance.workshop.CanDrag(source,rightButton);
        if (rightButton) return Allow(TutorialAction.Skill, source, Instance.Card(1));
        for (int seat = 0; seat < Instance.turns.allSeats.Count; seat++)
            if (Allow(TutorialAction.Place, source, seat: seat) || Allow(TutorialAction.Curse, source, seat: seat) ||
                Allow(TutorialAction.Vote, source, seat: seat)) return true;
        if (Allow(TutorialAction.Return, source) || Allow(TutorialAction.Swap, source, Instance.Card(3))) return true;
        return false;
    }
    public static bool CanChooseSource(TutorialAction action, CardInstance source)
    {
        if (!Active || Instance.scripted) return true;
        for (int seat = 0; seat < Instance.turns.allSeats.Count; seat++)
            if (Allow(action, source, seat: seat)) return true;
        return false;
    }
    public static bool IsScripted => Active && (Instance.scripted||Instance.workshop!=null&&Instance.workshop.Scripted);
    public static bool OverlapsCallout(RectTransform bubble)
    {
        if(!Active || bubble == null) return false;
        var guide=Instance;
        var bounds=RectTransformUtility.CalculateRelativeRectTransformBounds(guide.overlay,bubble);
        foreach(var label in new[]{guide.sourceActionLabel,guide.targetActionLabel})
        {
            if(label==null || !label.gameObject.activeInHierarchy) continue;
            var callout=RectTransformUtility.CalculateRelativeRectTransformBounds(guide.overlay,label.rectTransform);
            if(bounds.min.x<callout.max.x && bounds.max.x>callout.min.x && bounds.min.y<callout.max.y && bounds.max.y>callout.min.y) return true;
        }
        return false;
    }
    private void Script(Action action)
    { scripted = true; try { action(); } finally { scripted = false; } }

    void Update()
    {
        if (!ready || leaving || GameStartUI.IsBlockingInput || Stage == TutorialStage.Complete) return;
        if (!Satisfied()) { passedAt = -1; return; }
        if (passedAt < 0) passedAt = Time.unscaledTime;
        float delay = Stage == TutorialStage.Shoot || Stage == TutorialStage.Purge ? 1.5f : .25f;
        feedback.text = Localizer.T("Đúng rồi! "+lesson.steps[(int)Stage].success);
        if (Time.unscaledTime - passedAt >= delay)
        {
            lastSuccess = lesson.steps[(int)Stage].success;
            Enter(Stage + 1);
        }
    }
    private bool Satisfied()
    {
        switch (Stage)
        {
            case TutorialStage.ChooseCur: return turns.allCurs.Contains(Card(1));
            case TutorialStage.OpenVillageHand: return deck.p2Panel.activeSelf;
            case TutorialStage.ChooseHiddenCur: return turns.allCurs.Contains(Card(11));
            case TutorialStage.StartMatch: return turns.currentTurn == TurnState.Player2Turn;
            case TutorialStage.Inspect: return Card(10).GetComponent<CardInteraction>().isInspecting;
            case TutorialStage.CloseInspect: return !Card(10).GetComponent<CardInteraction>().isInspecting;
            case TutorialStage.PlaceVillager: return At(10, 0);
            case TutorialStage.PlaceHunter: return At(9, 1);
            case TutorialStage.EndVillageTurn: return turns.currentTurn == TurnState.Player1Turn;
            case TutorialStage.PlaceCur: return At(1, 2);
            case TutorialStage.EndCurTurn: return turns.currentTurn == TurnState.Player2Turn;
            case TutorialStage.Shoot: return Card(1).isDead && Card(9).hasUsedSkill;
            case TutorialStage.OpenCemetery: return turns.cemeteryViewPanel.activeSelf;
            case TutorialStage.CloseCemetery: return !turns.cemeteryViewPanel.activeSelf;
            case TutorialStage.Curse: return Card(11).currentVictim == Card(9) && Card(9).isInfected;
            case TutorialStage.EndCurse: return turns.currentTurn == TurnState.SacrificePhase;
            case TutorialStage.VoteVillager: return turns.GetVoteTarget(Card(10)) == 3;
            case TutorialStage.VoteHunter: return turns.GetVoteTarget(Card(9)) == 3;
            case TutorialStage.VoteHiddenCur: return turns.GetVoteTarget(Card(11)) == 3;
            case TutorialStage.ConfirmVotes: return turns.currentVotingPlayer == DefaultOwner.Player1;
            case TutorialStage.Tally: return !turns.isTallyingVotes && Card(2).isDead && !At(10, 0);
            case TutorialStage.Purge: return Card(9).isDead && turns.p2Checks == 1;
            case TutorialStage.BudgetPlace: return At(12,0) && turns.p2Hearts == 3;
            case TutorialStage.BudgetMove: return At(12,1) && turns.p2Hearts == 2;
            case TutorialStage.BudgetReturn: return !Card(12).GetComponent<CardInteraction>().isPlaced && turns.p2Hearts == 4;
            case TutorialStage.BudgetOpponentPlace: return At(3,0) && turns.p2Hearts == 0;
            case TutorialStage.BudgetOpponentMove: return At(3,1) && turns.p2Hearts == 2;
            case TutorialStage.BudgetSwap: return At(12,1) && !Card(3).GetComponent<CardInteraction>().isPlaced && turns.p2Hearts == 0;
            default: return false;
        }
    }
    private bool At(int number, int seat) => turns.allSeats[seat].cardPos.GetComponentInChildren<CardInstance>() == Card(number);

    private void Enter(TutorialStage stage)
    {
        turns.ClearLocalActionFeedback();
        Stage = stage; passedAt = -1;
        var step = lesson.steps[(int)stage];
        progress.text = $"BƯỚC {(int)stage + 1:00} / {TutorialRules.StageCount}";
        progressFill.rectTransform.anchorMin = Vector2.zero;
        progressFill.rectTransform.anchorMax = new Vector2(((int)stage + 1f) / TutorialRules.StageCount, 1);
        progressFill.rectTransform.sizeDelta = Vector2.zero;
        title.text = step.title; instruction.text = step.instruction; purpose.text = step.purpose;
        feedback.text = lastSuccess;
        if (stage == TutorialStage.Purge)
        {
            // Explicitly described as a new exercise in the lesson text.
            turns.currentTurn = TurnState.Player2Turn;
            turns.p2Hearts = 5; turns.RefreshTutorialView();
            deck.SwitchToHand(DefaultOwner.Player2);
        }
        if(stage == TutorialStage.HeartIntro)
        {
            // This is an explicitly separate practice round, not a free refill in the match.
            turns.currentTurn = TurnState.Player2Turn;
            turns.p2Hearts = 5; turns.RefreshTutorialView();
            deck.SwitchToHand(DefaultOwner.Player2);
        }
        if(stage == TutorialStage.BudgetMove) Card(12).GetComponent<CardInteraction>().lockState = 3;
        // Continue() advances two real turns, whose normal UI refresh restores the
        // Villager hand. Re-open the required source hand when the exercise starts.
        if(stage == TutorialStage.BudgetOpponentIntro || stage == TutorialStage.BudgetOpponentPlace ||
            stage == TutorialStage.BudgetOpponentMove) deck.SwitchToHand(DefaultOwner.Player1);
        if(stage == TutorialStage.BudgetOpponentMove) Card(3).GetComponent<CardInteraction>().lockState = 3;
        if(stage == TutorialStage.BudgetSwap) deck.SwitchToHand(DefaultOwner.Player2);
    }
    public void Continue()
    {
        if(workshop!=null&&workshop.Running){workshop.Continue();return;}
        if (!Allow(TutorialAction.Continue)) return;
        if (Stage == TutorialStage.Complete) { Exit(); return; }
        if (Stage == TutorialStage.RitualIntro)
        {
            Script(() =>
            {
                Card(11).GetComponent<CardInteraction>().ExecutePlace(turns.allSeats[2].cardPos);
                Card(2).GetComponent<CardInteraction>().ExecutePlace(turns.allSeats[3].cardPos);
                turns.ForceCursePhase();
            });
        }
        if (Stage == TutorialStage.OpponentExample)
        {
            Script(() => { turns.StartVoteSelection(Card(2)); turns.ApplyVote(0); turns.EndTurn(); });
        }
        if(Stage == TutorialStage.BudgetOpponentIntro || Stage == TutorialStage.BudgetRefill || Stage == TutorialStage.BudgetSwapIntro)
        {
            // Advance real turns so both sides' normal turn-start refill runs.
            Script(() => { turns.EndTurn(); turns.EndTurn(); });
        }
        lastSuccess = lesson.steps[(int)Stage].success;
        Enter(Stage + 1);
    }
    public void Restart()
    {
        if(workshop!=null&&workshop.Running){workshop.Restart();return;}
        if (leaving) return;
        leaving = true; Time.timeScale = 1;
        SceneManager.LoadScene(gameObject.scene.name);
    }
    public void Exit()
    {
        if(workshop!=null&&workshop.Running){workshop.Exit();return;}
        if (leaving) return;
        leaving = true; Time.timeScale = 1;
        SceneManager.LoadScene(lesson != null ? lesson.menuScene : "MenuScenes");
    }
    public void Practice()
    {
        if(workshop!=null&&workshop.Running){workshop.Answer(1);return;}
        if (Stage != TutorialStage.Complete || leaving) return;
        leaving = true; restoreSettings = false;
        GameSettings.currentMode = GameMode.PlayerVsCurBot; GameSettings.currentLevel = 1;
        Time.timeScale = 1; SceneManager.LoadScene(lesson.practiceScene);
    }

    void LateUpdate()
    {
        if (!ready) return;
        bool reference = Stage == TutorialStage.HeartIntro || Stage == TutorialStage.BudgetReference || Stage == TutorialStage.BudgetSwapReference || Stage == TutorialStage.BudgetSkills;
        if(heartGuidePanel != null) heartGuidePanel.SetActive(reference && !GameStartUI.IsBlockingInput);
        if(reference && heartGuideText != null) heartGuideText.text = Localizer.T(Stage == TutorialStage.HeartIntro ? lesson.heartOverview :
            Stage == TutorialStage.BudgetReference ? lesson.movementCosts : Stage == TutorialStage.BudgetSwapReference ? lesson.swapCosts : lesson.skillCosts);
        // The normal view still supplies the live board/header. Guide controls own the left column.
        setupPanel.SetActive(false); resultPanel.SetActive(false);
        foreach (var button in controlledButtons) if (button != null) button.interactable = false;
        SetControl(startControl, TutorialAction.StartMatch);
        SetControl(endControl, TutorialAction.EndTurn);
        SetControl(checkControl, TutorialAction.Purge);
        if (revealControl != null) revealControl.SetActive(false);
        SetControl(cemeteryControl, TutorialAction.Cemetery);
        if (villageTab != null) villageTab.interactable = Allow(TutorialAction.VillageHand);
        continueButton.gameObject.SetActive(Stage != TutorialStage.Complete && TutorialRules.Allows(Stage, TutorialAction.Continue));
        continueButton.interactable = Allow(TutorialAction.Continue);
        continueLabel.text = Localizer.T(Stage == TutorialStage.Welcome ? "Bắt đầu học" : Stage == TutorialStage.RitualIntro ? "Thử nghi thức" :
            Stage == TutorialStage.OpponentExample ? "Xem lượt đối phương" : Stage == TutorialStage.Complete ? "Về menu" : "Đã hiểu · Tiếp tục");
        practiceButton.gameObject.SetActive(Stage == TutorialStage.Complete);
        var inspected = Card(10).GetComponent<CardInteraction>();
        if (inspected.isInspecting && inspected.descriptionText != null) inspected.descriptionText.text = Localizer.T(lesson.inspectDescription);
        bool curTurn = turns.currentTurn == TurnState.SetupPhase || turns.currentTurn == TurnState.Player1Turn || turns.currentTurn == TurnState.CursePhase ||
            turns.currentTurn == TurnState.SacrificePhase && turns.currentVotingPlayer == DefaultOwner.Player1;
        header.text = Localizer.T($"HƯỚNG DẪN · {(int)Stage + 1}/{TutorialRules.StageCount} · " +
            (curTurn ? "PHE CURSERMANCER" : "PHE DÂN LÀNG"));
        if (headerHint != null) headerHint.text = Localizer.T(lesson.steps[(int)Stage].instruction);
        if (headerHint != null && !string.IsNullOrEmpty(turns.LocalActionFeedback)) headerHint.text = Localizer.T(turns.LocalActionFeedback);
        FocusTargets(out var source, out var target);
        bool visible = !GameStartUI.IsBlockingInput && passedAt < 0;
        FitFrame(sourceFrame, visible ? source : null);
        FitFrame(targetFrame, visible ? target : null);
        // A reading panel needs a clear spotlight hole, not a coloured action wash.
        targetOutline.enabled = !reference;
        foreach(Transform child in targetFrame)
            if(child.name.StartsWith("Focus Edge")) child.gameObject.SetActive(!reference);
        bool dragStep = Stage == TutorialStage.Shoot || Stage == TutorialStage.Curse ||
            Stage == TutorialStage.PlaceVillager || Stage == TutorialStage.PlaceHunter || Stage == TutorialStage.PlaceCur ||
            Stage == TutorialStage.VoteVillager || Stage == TutorialStage.VoteHunter || Stage == TutorialStage.VoteHiddenCur ||
            Stage == TutorialStage.BudgetPlace || Stage == TutorialStage.BudgetMove || Stage == TutorialStage.BudgetReturn ||
            Stage == TutorialStage.BudgetOpponentPlace || Stage == TutorialStage.BudgetOpponentMove || Stage == TutorialStage.BudgetSwap;
        directionArrow.gameObject.SetActive(visible && dragStep && sourceFrame.gameObject.activeInHierarchy && targetFrame.gameObject.activeInHierarchy);
        if (directionArrow.gameObject.activeSelf)
        {
            var from = (Vector2)overlay.InverseTransformPoint(sourceFrame.position);
            var to = (Vector2)overlay.InverseTransformPoint(targetFrame.position);
            directionArrow.position = overlay.TransformPoint(Vector2.Lerp(from,to,.5f+.08f*Mathf.Sin(Time.unscaledTime*3)));
            directionArrow.localRotation = Quaternion.Euler(0,0,Mathf.Atan2(to.y-from.y,to.x-from.x)*Mathf.Rad2Deg);
        }
        float pulse = .15f + .035f * Mathf.Sin(Time.unscaledTime * 5);
        sourceOutline.color = new Color(1,.85f,.35f,pulse);
        targetOutline.color = new Color(.48f,1,.7f,pulse);
        if(sourceActionLabel!=null)sourceActionLabel.text=Localizer.T(SourceAction());
        if(targetActionLabel!=null)targetActionLabel.text=Localizer.T(Stage==TutorialStage.Reveal?"VAI TRÒ BÍ MẬT":Stage==TutorialStage.Purge?"BÀI NHIỄM SẼ BỊ LOẠI":"2 · THẢ CHUỘT Ở ĐÂY");
        if(spotlight!=null)spotlight.Present(sourceFrame,targetFrame,visible&&(sourceFrame.gameObject.activeSelf||targetFrame.gameObject.activeSelf));
    }
    private string SourceAction()
    {
        if(Stage==TutorialStage.Inspect||Stage==TutorialStage.CloseInspect)return "1 · BẤM CHUỘT PHẢI";
        if(Stage==TutorialStage.Shoot)return "1 · KÉO CHUỘT PHẢI";
        if(Stage >= TutorialStage.BudgetPlace && Stage <= TutorialStage.BudgetSwap && !TutorialRules.Allows(Stage,TutorialAction.Continue)) return "1 · KÉO CHUỘT TRÁI";
        if(Stage==TutorialStage.PlaceVillager||Stage==TutorialStage.PlaceHunter||Stage==TutorialStage.PlaceCur||Stage==TutorialStage.Curse||
            Stage==TutorialStage.VoteVillager||Stage==TutorialStage.VoteHunter||Stage==TutorialStage.VoteHiddenCur)return "1 · KÉO CHUỘT TRÁI";
        return "1 · BẤM CHUỘT TRÁI";
    }
    private void SetControl(GameObject control, TutorialAction action)
    {
        bool show = TutorialRules.Allows(Stage, action);
        control.SetActive(show);
        if (control.TryGetComponent<Button>(out var button)) button.interactable = Allow(action);
    }
    private RectTransform CardRect(int number) => Card(number) != null ? Card(number).GetComponent<RectTransform>() : null;
    private RectTransform Rect(GameObject go) => go != null ? go.GetComponent<RectTransform>() : null;
    private void FocusTargets(out RectTransform source, out RectTransform target)
    {
        source = target = null;
        switch (Stage)
        {
            case TutorialStage.Welcome: case TutorialStage.RitualIntro: case TutorialStage.OpponentExample:
                source = Rect(continueButton.gameObject); break;
            case TutorialStage.HeartIntro: case TutorialStage.BudgetReference: case TutorialStage.BudgetSwapReference: case TutorialStage.BudgetSkills:
                source = Rect(continueButton.gameObject); target = Rect(heartGuidePanel); break;
            case TutorialStage.BudgetOpponentIntro: case TutorialStage.BudgetRefill: case TutorialStage.BudgetSwapIntro:
                source = Rect(continueButton.gameObject); target = Rect(turns.p1EnergyBar.gameObject); break;
            case TutorialStage.BudgetPlace: case TutorialStage.BudgetMove: source = CardRect(12); break;
            case TutorialStage.BudgetReturn: source = CardRect(12); target = deck.player2Hand.parent as RectTransform; break;
            case TutorialStage.BudgetOpponentPlace: case TutorialStage.BudgetOpponentMove: source = CardRect(3); break;
            case TutorialStage.BudgetSwap: source = CardRect(12); target = CardRect(3); break;
            case TutorialStage.ChooseCur: case TutorialStage.PlaceCur: source = CardRect(1); break;
            case TutorialStage.ChooseHiddenCur: source = CardRect(11); break;
            case TutorialStage.OpenVillageHand: source = Rect(villageTab.gameObject); break;
            case TutorialStage.StartMatch: source = Rect(startControl); break;
            case TutorialStage.Inspect: case TutorialStage.CloseInspect: case TutorialStage.PlaceVillager: source = CardRect(10); break;
            case TutorialStage.PlaceHunter: source = CardRect(9); break;
            case TutorialStage.EndVillageTurn: case TutorialStage.EndCurTurn: case TutorialStage.EndCurse: case TutorialStage.ConfirmVotes:
                source = Rect(endControl); break;
            case TutorialStage.Reveal: source = Rect(continueButton.gameObject); target = CardRect(1); break;
            case TutorialStage.Shoot: source = CardRect(9); target = CardRect(1); break;
            case TutorialStage.OpenCemetery: case TutorialStage.CloseCemetery: source = Rect(cemeteryControl); break;
            case TutorialStage.Curse: source = CardRect(11); target = CardRect(9); break;
            case TutorialStage.VoteVillager: source = CardRect(10); target = CardRect(2); break;
            case TutorialStage.VoteHunter: source = CardRect(9); target = CardRect(2); break;
            case TutorialStage.VoteHiddenCur: source = CardRect(11); target = CardRect(2); break;
            case TutorialStage.Purge: source = Rect(checkControl); target = CardRect(9); break;
            case TutorialStage.Complete: source = Rect(practiceButton.gameObject); break;
        }
        if (Stage == TutorialStage.PlaceVillager) target = turns.allSeats[0].GetComponent<RectTransform>();
        if (Stage == TutorialStage.PlaceHunter) target = turns.allSeats[1].GetComponent<RectTransform>();
        if (Stage == TutorialStage.PlaceCur) target = turns.allSeats[2].GetComponent<RectTransform>();
        if(Stage == TutorialStage.BudgetPlace || Stage == TutorialStage.BudgetOpponentPlace) target = turns.allSeats[0].GetComponent<RectTransform>();
        if(Stage == TutorialStage.BudgetMove || Stage == TutorialStage.BudgetOpponentMove) target = turns.allSeats[1].GetComponent<RectTransform>();
    }
    private void FitFrame(RectTransform frame, RectTransform target)
    {
        frame.gameObject.SetActive(target != null && target.gameObject.activeInHierarchy);
        if (!frame.gameObject.activeSelf) return;
        var card=target.GetComponent<CardInstance>();
        bool caption=card!=null||target.GetComponent<SeatDropZone>()!=null;
        foreach(Transform child in frame)
            if(child.GetComponent<Text>()!=null||child.name.StartsWith("Tutorial Callout Background"))child.gameObject.SetActive(caption);
        if(card!=null&&card.cardImage!=null)target=card.cardImage.rectTransform;
        target.GetWorldCorners(corners);
        var min = new Vector2(float.MaxValue, float.MaxValue); var max = new Vector2(float.MinValue, float.MinValue);
        foreach (var corner in corners)
        {
            Vector2 point = overlay.InverseTransformPoint(corner);
            min = Vector2.Min(min, point); max = Vector2.Max(max, point);
        }
        frame.anchoredPosition = (min+max)*.5f;
        frame.sizeDelta = max-min + (card!=null?Vector2.zero:new Vector2(8,8));
    }
    void OnDestroy()
    {
        if (Instance != this) return;
        Instance = null; HoldToReveal.isRevealingAll = false; CardInteraction.isAnyCardDragging = false;
        if (restoreSettings) { GameSettings.currentMode = previousMode; GameSettings.currentLevel = previousLevel; }
    }
}

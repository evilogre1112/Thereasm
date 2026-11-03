using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Short, independent exercises use real board actions and the current card skills.
[DefaultExecutionOrder(600)]
public class TutorialWorkshop : MonoBehaviour
{
    public static bool Active => TutorialController.Instance!=null&&TutorialController.Instance.workshop!=null&&TutorialController.Instance.workshop.Running;
    public TurnManager turns;
    public DeckManager deck;
    public TutorialController guide;
    [Tooltip("Lesson to preview when playing this scene directly. A menu selection takes priority.")]
    public TutorialExercise previewExercise;
    public RectTransform overlay,sourceFrame,targetFrame,directionArrow;
    public TutorialSpotlight spotlight;
    public Text progress,title,instruction,purpose,feedback,continueLabel,header,headerHint,choiceLabel,detailsText;
    public Button continueButton,choiceButton,notesButton;
    public Button quizFirstButton;
    public Text quizFirstLabel,quizQuestion;
    public GameObject quizPanel,legacyPractice;
    public GameObject[] sectionHeadings;
    public Image progressFill;
    public GameObject startControl,endControl,checkControl,cemeteryControl,setupPanel,resultPanel,detailsPanel;
    public Button[] controlledButtons;
    public NoteSlot[] noteSlots;
    public Button passWagerButton;
    public Text passWagerLabel;
    public bool LastWagerUsed { get; private set; }
    public DefaultOwner Viewer => Actor;
    public DefaultOwner Actor => turns.currentTurn==TurnState.Player1Turn||turns.currentTurn==TurnState.CursePhase ? DefaultOwner.Player1 :
        turns.currentTurn==TurnState.SacrificePhase ? turns.currentVotingPlayer : DefaultOwner.Player2;
    public bool AwaitingRefusal => passedAt>=0&&Blocked;
    public bool RefusalAnimating { get; private set; }
    bool Blocked => Beat!=null&&(Beat.action==LessonAction.BlockedPlace||Beat.action==LessonAction.BlockedReturn||Beat.action==LessonAction.BlockedSkill);
    int viewRevision,viewAt;
    bool refused;
    bool declinedWager;
    public bool Running { get; private set; }
    public bool Scripted { get; private set; }
    public TutorialExercise Exercise { get; private set; }
    public int Index { get; private set; }
    public LessonBeat Beat => Exercise!=null&&Index<Exercise.beats.Length?Exercise.beats[Index]:null;
    public bool Complete => Running&&Beat==null;
    readonly Dictionary<int,CardInstance> cards=new Dictionary<int,CardInstance>();
    readonly List<UnityEngine.Object> ownedData=new List<UnityEngine.Object>();
    float passedAt=-1,enteredAt;
    int checksAt,phaseAt,voterAt;
    bool leaving;
    TutorialCopy response;
    bool wrongAnswer;
    readonly Vector3[] corners=new Vector3[4];
    public CardInstance Card(int number)
    {
        if(number<=0)return null;
        cards.TryGetValue(number,out var card);
        if(card!=null&&!card.isDead)return card;
        // Invoker creates a new live instance and destroys the corpse.
        var revived=FindObjectsByType<CardInstance>(FindObjectsInactive.Include,FindObjectsSortMode.None)
            .FirstOrDefault(c=>c.gameObject.scene==gameObject.scene&&c.cardData!=null&&c.cardData.cardNumber==number&&!c.isDead&&
                (turns.discoverPanel==null||!c.transform.IsChildOf(turns.discoverPanel.transform)));
        if(revived!=null){cards[number]=revived;return revived;}
        return card;
    }
    public IEnumerator Begin(TutorialExercise exercise)
    {
        Exercise=exercise;Running=true;
        var copies=exercise.cards.Select(data=>
        {
            var copy=Instantiate(data);ownedData.Add(copy);
            if(copy.cardSkill!=null){copy.cardSkill=Instantiate(copy.cardSkill);ownedData.Add(copy.cardSkill);}
            return copy;
        }).ToArray();
        deck.BuildTutorialDeck(copies);yield return null;
        if(noteSlots==null||noteSlots.Length==0)noteSlots=turns.notePanel.GetComponentsInChildren<NoteSlot>(true)
            .Where(slot=>slot.name.StartsWith("Note Seat ",StringComparison.Ordinal)).OrderBy(slot=>slot.transform.GetSiblingIndex()).ToArray();
        foreach(var card in FindObjectsByType<CardInstance>(FindObjectsInactive.Include,FindObjectsSortMode.None))
            if(card.gameObject.scene==gameObject.scene&&card.cardData!=null&&copies.Contains(card.cardData))
            {cards[card.cardData.cardNumber]=card;card.GetComponent<CardInteraction>().hoverOffsetY=0;}
        turns.currentTurn=TurnState.Player2Turn;turns.firstPlayerThisRound=TurnState.Player1Turn;
        turns.currentVotingPlayer=DefaultOwner.Player2;turns.p1Hearts=turns.p2Hearts=5;
        turns.allCurs.Clear();turns.allyCur=turns.enemyCur=null;
        Apply(exercise.setup);SetViewer(Actor);Enter(0);
    }
    public bool Allows(TutorialAction action,CardInstance source,CardInstance target,int seat)
    {
        if(Scripted)return true;
        // Inspection is optional reference material, not a repeated mandatory lesson.
        if(Running&&!leaving&&action==TutorialAction.Inspect&&source!=null)return true;
        if(!Running||leaving||passedAt>=0||Beat==null)return false;
        int number=source!=null&&source.cardData!=null?source.cardData.cardNumber:0;
        bool match=number==Beat.source;
        switch(Beat.action)
        {
            case LessonAction.Read:case LessonAction.Quiz:return action==TutorialAction.Continue;
            case LessonAction.Inspect:case LessonAction.CloseInspect:return action==TutorialAction.Inspect&&match;
            case LessonAction.Place:case LessonAction.BlockedPlace:return action==TutorialAction.Place&&match&&seat==Beat.seat;
            case LessonAction.Return:case LessonAction.BlockedReturn:return action==TutorialAction.Return&&match;
            case LessonAction.Skill:case LessonAction.BlockedSkill:case LessonAction.OpenInvoker:
                return action==TutorialAction.Skill&&match&&(Beat.target==0?target==null:target!=null&&target.cardData.cardNumber==Beat.target);
            case LessonAction.Wager:return action==TutorialAction.Skill&&match&&turns.IsLivingBoardCard(target);
            case LessonAction.Swap:return action==TutorialAction.Swap&&match&&target!=null&&target.cardData.cardNumber==Beat.target;
            case LessonAction.Vote:return action==TutorialAction.Vote&&match&&seat==Beat.seat;
            case LessonAction.EndTurn:case LessonAction.Tally:return action==TutorialAction.EndTurn;
            case LessonAction.Purge:return action==TutorialAction.Purge;
            case LessonAction.OpenCemetery:case LessonAction.CloseCemetery:return action==TutorialAction.Cemetery;
            case LessonAction.Curse:return action==TutorialAction.Curse&&match&&seat==Beat.seat;
            default:return false;
        }
    }
    public bool CanDrag(CardInstance card,bool right)
    {
        if(Beat==null||card==null||card.cardData.cardNumber!=Beat.source)return false;
        return right?(Beat.action==LessonAction.Skill||Beat.action==LessonAction.BlockedSkill||Beat.action==LessonAction.Wager)&&Beat.target!=0:
            Beat.action==LessonAction.Place||Beat.action==LessonAction.BlockedPlace||Beat.action==LessonAction.Return||
            Beat.action==LessonAction.BlockedReturn||Beat.action==LessonAction.Vote||Beat.action==LessonAction.Curse||Beat.action==LessonAction.Swap;
    }
    void Apply(LessonSetup[] commands)
    {
        Scripted=true;
        try
        {
            foreach(var command in commands??Array.Empty<LessonSetup>())
            {
                var card=Card(command.number);var input=card!=null?card.GetComponent<CardInteraction>():null;
                switch(command.kind)
                {
                    case LessonSetupKind.ResetTable:ResetTable();break;
                    case LessonSetupKind.Place:
                        RestorePracticeInput(card);
                        card.transform.SetParent(turns.allSeats[command.value].cardPos,false);
                        input.isPlaced=true;input.originalParent=card.transform.parent;input.lockState=3;card.lastPlacedBy=DefaultOwner.Player2;
                        ((RectTransform)card.transform).anchoredPosition=Vector2.zero;card.transform.localRotation=Quaternion.identity;card.transform.localScale=input.PrefabRootScale;break;
                    case LessonSetupKind.Return:input.ForceReturnToHand();break;
                    case LessonSetupKind.Hearts:if(command.number==0)turns.p1Hearts=command.value;else turns.p2Hearts=command.value;break;
                    case LessonSetupKind.Phase:
                        turns.currentTurn=(TurnState)command.value;turns.currentVotingPlayer=(DefaultOwner)command.target;break;
                    case LessonSetupKind.Lock:input.lockState=command.value;break;
                    case LessonSetupKind.Infect:card.isInfected=command.value!=0;break;
                    case LessonSetupKind.Cur:if(!turns.allCurs.Contains(card))turns.allCurs.Add(card);break;
                    case LessonSetupKind.Kill:turns.BanishCardToCemetery(input);break;
                    case LessonSetupKind.ResetSkill:card.hasUsedSkill=false;card.hasUsedSkillInRitual=false;break;
                    case LessonSetupKind.Vote:
                        var voter=turns.currentVotingPlayer;turns.currentVotingPlayer=card.cardData.owner;
                        turns.StartVoteSelection(card);turns.ApplyVote(command.value);turns.currentVotingPlayer=voter;break;
                    case LessonSetupKind.Curse:
                        turns.currentTurn=TurnState.CursePhase;turns.StartCurseSelection(card);turns.ApplyCurse(command.value);break;
                    case LessonSetupKind.ClosePanels:
                        turns.notePanel.SetActive(false);turns.cemeteryViewPanel.SetActive(false);turns.CloseInvokerDiscoverUI();break;
                    case LessonSetupKind.Cast:card.cardData.cardSkill.TryCastActiveSkill(card,Card(command.target));break;
                }
            }
            turns.RefreshAllAuras();
            // UpdateTurnUI selects the active faction hand. Doing that while reading a
            // Neutral hand card disables the enlarged card before it can be closed.
            if(CardInteraction.currentlyInspectedCard==null)turns.RefreshTutorialView();
            turns.UpdateCurVisuals();turns.UpdateHeartsUI();
        }
        finally{Scripted=false;}
    }
    void Enter(int index)
    {
        Index=index;passedAt=-1;enteredAt=Time.unscaledTime;wrongAnswer=false;response=null;refused=false;declinedWager=false;viewAt=viewRevision;
        turns.ClearLocalActionFeedback();
        if(Beat==null)
        {
            PlayerPrefs.SetInt(TutorialJourney.CompletionKey(Exercise.id),1);PlayerPrefs.Save();
            turns.notePanel.SetActive(false);turns.cemeteryViewPanel.SetActive(false);return;
        }
        Apply(Beat.prepare);phaseAt=(int)turns.currentTurn;voterAt=(int)turns.currentVotingPlayer;checksAt=turns.p1Checks+turns.p2Checks;
        if(Beat.action!=LessonAction.View&&(Beat.action!=LessonAction.Read||Array.Exists(Beat.prepare,c=>c.kind==LessonSetupKind.Phase)))SetViewer(Actor);
        var source=Card(Beat.source);
        if(CardInteraction.currentlyInspectedCard==null)
        {
            if(source!=null&&!source.GetComponent<CardInteraction>().isPlaced&&!source.isDead)deck.SwitchToHand(source.cardData.owner);
            else deck.SwitchToHand(turns.currentTurn==TurnState.Player1Turn||turns.currentTurn==TurnState.CursePhase?DefaultOwner.Player1:DefaultOwner.Player2);
        }
    }
    void Update()
    {
        if(!Running||leaving||Beat==null||GameStartUI.IsBlockingInput)return;
        if(CardInteraction.currentlyInspectedCard!=null&&Beat.action!=LessonAction.Inspect&&Beat.action!=LessonAction.CloseInspect)return;
        if(!Satisfied())return;
        if(passedAt<0)
        {
            passedAt=Time.unscaledTime;response=Beat.success;
            if(Beat.action==LessonAction.Wager)LastWagerUsed=!declinedWager;
        }
        if(Blocked)return; // A refusal is a discovery. Leave its explanation until the player continues.
        float delay=Beat.action==LessonAction.Purge||Beat.action==LessonAction.Skill?1.1f:.45f;
        if(Time.unscaledTime-passedAt>delay)Enter(Index+1);
    }
    bool Satisfied()
    {
        var source=Card(Beat.source);var input=source!=null?source.GetComponent<CardInteraction>():null;
        switch(Beat.action)
        {
            case LessonAction.Inspect:return input.isInspecting;
            case LessonAction.CloseInspect:return !input.isInspecting;
            case LessonAction.Place:return turns.GetBoardCard(Beat.seat)==source;
            case LessonAction.Return:return !input.isPlaced;
            case LessonAction.BlockedPlace:case LessonAction.BlockedReturn:case LessonAction.BlockedSkill:
                return refused;
            case LessonAction.View:return viewRevision>viewAt&&Viewer==(DefaultOwner)Beat.seat;
            case LessonAction.Wager:return declinedWager||Skill_Gambler.HasUsedBy(source,Actor);
            case LessonAction.Skill:return source.hasUsedSkill||Beat.target>0&&Card(Beat.target).isDead;
            case LessonAction.Vote:return turns.GetVoteTarget(source)==Beat.seat;
            case LessonAction.Swap:return turns.GetBoardCard(Beat.seat)==source&&!Card(Beat.target).GetComponent<CardInteraction>().isPlaced;
            case LessonAction.EndTurn:return (int)turns.currentTurn!=phaseAt||(int)turns.currentVotingPlayer!=voterAt;
            case LessonAction.Tally:return Time.unscaledTime-enteredAt>1&&!turns.isTallyingVotes&&turns.currentTurn!=TurnState.SacrificePhase;
            case LessonAction.Purge:return turns.p1Checks+turns.p2Checks<checksAt;
            case LessonAction.OpenNotes:return turns.notePanel.activeSelf;
            case LessonAction.PlaceNote:return noteSlots.Length>Beat.seat&&noteSlots[Beat.seat].GetComponentInChildren<NoteBlock>()!=null;
            case LessonAction.CloseNotes:return !turns.notePanel.activeSelf;
            case LessonAction.OpenCemetery:return turns.cemeteryViewPanel.activeSelf;
            case LessonAction.CloseCemetery:return !turns.cemeteryViewPanel.activeSelf;
            case LessonAction.Curse:return source.currentVictim==Card(Beat.target);
            case LessonAction.OpenInvoker:return turns.discoverPanel.activeSelf;
            case LessonAction.Resurrect:return !Card(Beat.target).isDead;
            default:return false;
        }
    }
    public void Continue()
    {
        if(!Running||leaving)return;
        if(Complete){Exit();return;}
        if(AwaitingRefusal){Enter(Index+1);return;}
        if(Beat.action==LessonAction.Quiz){Answer(0);return;}
        if(Beat.action!=LessonAction.Read)return;
        response=Beat.success;Enter(Index+1);
    }
    public void Answer(int choice)
    {
        if(Beat==null||Beat.action!=LessonAction.Quiz)return;
        if(choice!=Beat.answer){wrongAnswer=true;return;}
        response=Beat.success;Enter(Index+1);
    }
    public void AnswerFirst()=>Answer(0);
    public void AnswerSecond()=>Answer(1);
    public void PassWager()
    {
        if(Beat!=null&&Beat.action==LessonAction.Wager&&passedAt<0&&!GameStartUI.IsBlockingInput)declinedWager=true;
    }
    void SetViewer(DefaultOwner viewer){turns.RefreshWorkshopViewer();}
    public void Refused(string message)
    {
        if(!Running||Beat==null||!Blocked||refused||Scripted||message.StartsWith("Try the highlighted action",StringComparison.Ordinal))return;
        refused=true;StartCoroutine(RefusalPulse(Card(Beat.source)));
    }
    IEnumerator RefusalPulse(CardInstance card)
    {
        if(card==null)yield break;
        RefusalAnimating=true;
        var image=card.cardImage;var rect=image.rectTransform;var origin=rect.anchoredPosition;var color=image.color;
        float start=Time.unscaledTime;
        while(Time.unscaledTime-start<.5f)
        {
            float t=(Time.unscaledTime-start)/.5f;
            rect.anchoredPosition=origin+Vector2.right*(Mathf.Sin(t*Mathf.PI*8)*12*(1-t));
            image.color=Color.Lerp(new Color(1,.32f,.22f,1),color,t);
            yield return null;
        }
        rect.anchoredPosition=origin;image.color=color;RefusalAnimating=false;
    }
    void ResetTable()
    {
        // Only the authored exercise calls this, between two explicitly introduced examples.
        turns.ResetWorkshopVotes();turns.RefreshCemeteryPresentation(true);
        turns.CloseCemeteryView();turns.notePanel.SetActive(false);turns.CloseInvokerDiscoverUI();
        turns.allCurs.Clear();turns.deadCardsData.Clear();Skill_Gambler.ResetComboChain();
        turns.firstPlayerThisRound=TurnState.Player1Turn;
        foreach(var item in cards.Values)
        {
            if(item==null)continue;
            item.isDead=false;item.isInfected=false;item.wasInfectedWhenDied=false;item.isRevealedByBlind=false;
            item.hasUsedSkill=false;item.hasUsedSkillInRitual=false;item.currentVictim=null;
            if(item.cardData.cardSkill is Skill_Snooper snooper)snooper.ResetRound();
            item.GetComponent<CardInteraction>().ForceReturnToHand();
            RestorePracticeInput(item);
        }
        turns.p1Hearts=turns.p2Hearts=5;turns.p1Checks=turns.p2Checks=2;
    }
    void RestorePracticeInput(CardInstance card)
    {
        var input=card.GetComponent<CardInteraction>();input.enabled=true;input.canDrag=false;
        // A closed cemetery deliberately disables raycasts. Reusing that instance in
        // a new exercise must restore input, as well as its visible pose and life state.
        if(card.TryGetComponent<CanvasGroup>(out var group))
        {group.interactable=true;group.blocksRaycasts=true;group.alpha=1;}
    }
    public void ToggleNotes()
    {
        if(Beat==null)return;
        if(Beat.action==LessonAction.OpenNotes||Beat.action==LessonAction.PlaceNote)turns.notePanel.SetActive(true);
        else if(Beat.action==LessonAction.CloseNotes)turns.notePanel.SetActive(false);
    }
    public void Exit()
    {
        if(leaving)return;leaving=true;TutorialJourney.OpenOnReturn=true;Time.timeScale=1;SceneManager.LoadScene("MenuScenes");
    }
    public void Restart(){if(!leaving){leaving=true;SceneManager.LoadScene(gameObject.scene.name);}}
    void SetControl(GameObject go,bool active){if(go==null)return;go.SetActive(active);if(go.TryGetComponent<Button>(out var button))button.interactable=active;}
    void LateUpdate()
    {
        if(!Running)return;
        foreach(var heading in sectionHeadings??Array.Empty<GameObject>())if(heading!=null)heading.SetActive(false);
        bool bodyOnly=Beat!=null&&(Beat.action==LessonAction.Read||AwaitingRefusal);
        purpose.rectTransform.anchoredPosition=new Vector2(20,bodyOnly?-182:-330);purpose.rectTransform.sizeDelta=new Vector2(244,bodyOnly?330:210);purpose.fontSize=18;
        feedback.rectTransform.anchoredPosition=new Vector2(20,-542);feedback.rectTransform.sizeDelta=new Vector2(244,40);feedback.fontSize=16;
        legacyPractice.SetActive(false);
        setupPanel.SetActive(false);resultPanel.SetActive(false);detailsPanel.SetActive(false);
        foreach(var button in controlledButtons)if(button!=null)button.interactable=false;
        SetControl(startControl,false);SetControl(endControl,Beat!=null&&(Beat.action==LessonAction.EndTurn||Beat.action==LessonAction.Tally));
        SetControl(checkControl,Beat!=null&&Beat.action==LessonAction.Purge);
        SetControl(cemeteryControl,Beat!=null&&(Beat.action==LessonAction.OpenCemetery||Beat.action==LessonAction.CloseCemetery));
        bool noteStep=Beat!=null&&(Beat.action==LessonAction.OpenNotes||Beat.action==LessonAction.PlaceNote||Beat.action==LessonAction.CloseNotes);
        notesButton.gameObject.SetActive(noteStep);notesButton.interactable=noteStep;
        if(noteStep)notesButton.GetComponentInChildren<Text>().text=TutorialAcademyUI.L("Notes","Sổ tay");
        bool quiz=false;
        continueButton.gameObject.SetActive(Complete||Beat.action==LessonAction.Read||AwaitingRefusal);
        continueButton.interactable=true;choiceButton.gameObject.SetActive(false);
        quizPanel.SetActive(false);
        passWagerButton.gameObject.SetActive(Beat!=null&&Beat.action==LessonAction.Wager);
        passWagerButton.interactable=passedAt<0;
        passWagerLabel.text=TutorialAcademyUI.L("Pass · do not bet","Bỏ qua · không cược");
        if(quiz){quizFirstLabel.text=Beat.choiceA.Text;quizQuestion.text=Beat.instruction.Text;}
        continueLabel.text=Complete?TutorialAcademyUI.L("Choose another lesson","Chọn bài học khác"):quiz?Beat.choiceA.Text:TutorialAcademyUI.L("Let's continue","Mình thử tiếp nhé");
        if(quiz)choiceLabel.text=Beat.choiceB.Text;
        title.text=Complete?TutorialAcademyUI.L("One more thing you can do now","Bạn đã có thêm một nước đi"):Beat.title.Text;
        instruction.text=Complete?TutorialAcademyUI.L("Head back to the subjects. Choose the next thing you're curious about.","Trở về danh sách bài học và chọn điều tiếp theo bạn muốn thử nhé."):Beat.instruction.Text;
        if(Beat!=null&&instruction.text.Contains("{turnButton}"))
            instruction.text=instruction.text.Replace("{turnButton}",endControl.GetComponentInChildren<Text>(true).text);
        instruction.gameObject.SetActive(!string.IsNullOrWhiteSpace(instruction.text)&&!AwaitingRefusal);
        purpose.text=Complete?Exercise.summary.Text:AwaitingRefusal?Beat.success.Text:
            Beat.betResult!=null&&!string.IsNullOrEmpty(Beat.betResult.english)?(LastWagerUsed?Beat.betResult.Text:Beat.passResult.Text):Beat.purpose.Text;
        purpose.gameObject.SetActive(!string.IsNullOrWhiteSpace(purpose.text));
        feedback.text=wrongAnswer?TutorialAcademyUI.L("Read the clue above, then try again.","Đọc gợi ý bên trên rồi thử lại nhé."):
            !AwaitingRefusal&&response!=null?response.Text:"";
        progress.text=TutorialAcademyUI.L($"LESSON · {Mathf.Min(Index+1,Exercise.beats.Length)}/{Exercise.beats.Length}",$"BÀI HỌC · {Mathf.Min(Index+1,Exercise.beats.Length)}/{Exercise.beats.Length}");
        progressFill.rectTransform.anchorMax=new Vector2(Mathf.Min(Index+1f,Exercise.beats.Length)/Exercise.beats.Length,1);
        header.text=Exercise.title.Text;headerHint.text=AwaitingRefusal?TutorialAcademyUI.L("The card stayed in place. Read the reason on the left.","Lá bài vẫn ở nguyên chỗ. Xem lý do ở bên trái nhé."):instruction.text;
        Focus(out var source,out var target);Fit(sourceFrame,source);Fit(targetFrame,target);
        bool drag=Beat!=null&&(Beat.action==LessonAction.Place||Beat.action==LessonAction.BlockedPlace||Beat.action==LessonAction.Return||Beat.action==LessonAction.BlockedReturn||Beat.action==LessonAction.Vote||Beat.action==LessonAction.Curse||Beat.action==LessonAction.Swap||Beat.action==LessonAction.PlaceNote||(Beat.action==LessonAction.Skill||Beat.action==LessonAction.BlockedSkill||Beat.action==LessonAction.Wager)&&Beat.target>0);
        directionArrow.gameObject.SetActive(drag&&sourceFrame.gameObject.activeSelf&&targetFrame.gameObject.activeSelf&&!GameStartUI.IsBlockingInput&&passedAt<0);
        if(directionArrow.gameObject.activeSelf)
        {
            directionArrow.position=Vector3.Lerp(sourceFrame.position,targetFrame.position,.5f+.08f*Mathf.Sin(Time.unscaledTime*3));
            var delta=overlay.InverseTransformVector(targetFrame.position-sourceFrame.position);
            directionArrow.localRotation=Quaternion.Euler(0,0,Mathf.Atan2(delta.y,delta.x)*Mathf.Rad2Deg);
        }
        bool hasModal=turns.notePanel.activeSelf||turns.cemeteryViewPanel.activeSelf||turns.discoverPanel.activeSelf||CardInteraction.currentlyInspectedCard!=null;
        bool observe=Beat==null||Beat.action==LessonAction.Read||Beat.action==LessonAction.View;
        spotlight.Present(sourceFrame,targetFrame,!GameStartUI.IsBlockingInput&&passedAt<0&&!hasModal&&!quiz&&!observe);
        // Inspection remains above the lesson mask; keep its highlighted source visible.
        if(hasModal){sourceFrame.gameObject.SetActive(false);targetFrame.gameObject.SetActive(false);directionArrow.gameObject.SetActive(false);}
    }
    void Focus(out RectTransform source,out RectTransform target)
    {
        source=target=null;if(Beat==null)return;
        if(Beat.source>0)source=Card(Beat.source)?.GetComponent<RectTransform>();
        if(Beat.target>0)target=Card(Beat.target)?.GetComponent<RectTransform>();
        if(Beat.action==LessonAction.Place||Beat.action==LessonAction.BlockedPlace||Beat.action==LessonAction.Vote)target=turns.allSeats[Beat.seat].GetComponent<RectTransform>();
        if(Beat.action==LessonAction.Return||Beat.action==LessonAction.BlockedReturn)target=deck.player2Hand.parent as RectTransform;
        if(Beat.action==LessonAction.Read)source=(RectTransform)continueButton.transform;
        if(Beat.action==LessonAction.Quiz)source=null;
        if(Beat.action==LessonAction.EndTurn||Beat.action==LessonAction.Tally)source=(RectTransform)endControl.transform;
        if(Beat.action==LessonAction.Purge)source=(RectTransform)checkControl.transform;
        if(Beat.action==LessonAction.OpenNotes||Beat.action==LessonAction.CloseNotes)source=(RectTransform)notesButton.transform;
        if(Beat.action==LessonAction.PlaceNote&&Beat.seat<noteSlots.Length)target=noteSlots[Beat.seat].GetComponent<RectTransform>();
        if(Beat.action==LessonAction.OpenCemetery||Beat.action==LessonAction.CloseCemetery)source=(RectTransform)cemeteryControl.transform;
    }
    void Fit(RectTransform frame,RectTransform target)
    {
        frame.gameObject.SetActive(target!=null&&target.gameObject.activeInHierarchy&&!GameStartUI.IsBlockingInput&&passedAt<0);
        foreach(Transform child in frame)if(child.GetComponent<Text>()!=null||child.name.StartsWith("Tutorial Callout Background"))child.gameObject.SetActive(false);
        if(!frame.gameObject.activeSelf)return;
        var card=target.GetComponent<CardInstance>();if(card!=null)target=card.cardImage.rectTransform;
        target.GetWorldCorners(corners);Vector2 min=new Vector2(float.MaxValue,float.MaxValue),max=-min;
        foreach(var corner in corners){Vector2 point=overlay.InverseTransformPoint(corner);min=Vector2.Min(min,point);max=Vector2.Max(max,point);}
        frame.anchoredPosition=(min+max)*.5f;frame.sizeDelta=max-min;
    }
    void OnDestroy(){foreach(var item in ownedData)if(item!=null)Destroy(item);}
}

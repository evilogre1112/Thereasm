using System;
using System.Collections.Generic;
using UnityEngine;

// Only the host simulates gameplay. The other peer receives a perspective-filtered view.
[DefaultExecutionOrder(-500)]
public class OnlineGameBridge : MonoBehaviour
{
    [SerializeField] private TurnManager turns;
    [SerializeField] private DeckManager deck;
    public static OnlineGameBridge Instance { get; private set; }
    public static bool ApplyingCommand { get; private set; }
    private readonly Dictionary<CardInstance, int> ids = new Dictionary<CardInstance, int>();
    private readonly Dictionary<int, CardInstance> views = new Dictionary<int, CardInstance>();
    private readonly Dictionary<string, CardData> catalog = new Dictionary<string, CardData>();
    private readonly SortedDictionary<int, OnlineCommand> commands = new SortedDictionary<int, OnlineCommand>();
    private readonly Dictionary<SeatDropZone, int> seatIds = new Dictionary<SeatDropZone, int>();
    private readonly List<int> remoteSeatIds = new List<int>();
    private int nextSeatId;
    private readonly List<OnlineCommand> unacknowledged = new List<OnlineCommand>();
    private readonly List<CommandReceipt> receipts = new List<CommandReceipt>();
    private readonly Dictionary<int, int> ownVoteTargets = new Dictionary<int, int>();
    private readonly Dictionary<int, int> resourceReservations = new Dictionary<int, int>();
    private string localFeedback;
    private float feedbackUntil;
    private float resendAt;
    private int lastQuickResult, latestSnapshotAck;
    private int nextId, sequence, receivedSequence, revision, appliedRevision;
    private float nextSend, lastSent, nextGuestRequest;
    private int guestRequestCount, guestSnapshotCount, hostReadyCount;
    private bool guestReady;
    private bool initialized;
    private string lastState;
    private string boundMatch;
    private string commandError;
    private OnlineSnapshot pending;
    public int Voted { get; private set; }
    public int RequiredVotes { get; private set; }
    public bool HasSnapshot => appliedRevision > 0 && OnlineSession.Instance != null && boundMatch == OnlineSession.Instance.MatchId;
    public bool CommandPending => unacknowledged.Count > 0;
    public int PendingSequence => sequence;
    public int PendingCount => unacknowledged.Count;
    public bool HasPhaseBarrier => unacknowledged.Exists(c => c.action == OnlineAction.EndTurn || c.action == OnlineAction.StartGame || c.action == OnlineAction.Draft);
    public string LocalFeedback => Time.unscaledTime < feedbackUntil ? localFeedback : null;
    public void Explain(string message) { localFeedback = message; feedbackUntil = Time.unscaledTime + 7; }
    public void ReportActionRefusal(string message)
    { if (ApplyingCommand) commandError = message; }
    public int DisplayHearts(int confirmed)
    {
        if (!OnlineSession.Guest || !OnlineSession.LocalTurn) return confirmed;
        foreach (int cost in resourceReservations.Values) confirmed -= cost;
        return Mathf.Clamp(confirmed, 0, TurnManager.MAX_HEARTS);
    }
    public bool CanStartGame { get; private set; }

    [Serializable] private class SeatView { public int id; public float x, y, width, height; public bool cemetery; }
    [Serializable] private class CardView
    {
        public int id, seat = -1, owner, placer, lockState, suspicion, deadOrder, voteTarget = -1;
        public bool mustVote;
        public string data;
        public bool dead, infected, deadInfected, cur, used, silenced, protectedCard, elder, skillLocked;
        public bool blind, blindCur, blindInfected, ritualUsed, gambler, snooper, suspected, changedVotes;
        public int votes;
    }
    [Serializable] private class CommandReceipt { public int sequence; public string error; }
    // Small, perspective-filtered feedback. State still comes from authoritative snapshots.
    [Serializable] private class QuickActionResult
    {
        public string match;
        public int sequence;
        public string feedback;
        public SkillSoundEvent[] skillSounds;
    }
    [Serializable] private class OnlineSnapshot
    {
        public string match, feedback, winner;
        public string commandError;
        public int revision, phase, voting, firstPlayer, p1Hearts, p2Hearts, p1Checks, p2Checks, voted, required, acknowledged;
        public int draftStep, draftTotal;
        public bool picking, canStart;
        public string[] draftOptions;
        public string[] deckCards;
        public bool ready, tally, over;
        public SeatView[] seats;
        public CardView[] cards;
        public string[] options;
        public int[] optionIndices;
        public CommandReceipt[] receipts;
        public VoteEffectRecord[] voteEffects;
        public SkillSoundEvent[] skillSounds;
    }

    void Awake() { EnsureInitialized(); }

    // Can be retried from OnlineSession.sceneLoaded if bridge Awake ran before session readiness.
    public void EnsureInitialized()
    {
        if (initialized) return;
        var session = OnlineSession.Instance;
        if (session != null) session.EnsureBoardSessionReady(gameObject.scene.name);
        if (!OnlineSession.Active && (session == null || !session.Connected)) { enabled = false; return; }
        // Scene references are optional: OnSceneLoaded may have auto-created this bridge.
        if (turns == null)
            foreach (var item in FindObjectsByType<TurnManager>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (item.gameObject.scene == gameObject.scene) { turns = item; break; }
        if (deck == null)
            foreach (var item in FindObjectsByType<DeckManager>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (item.gameObject.scene == gameObject.scene) { deck = item; break; }
        if (turns == null || deck == null)
        {
            Debug.LogError("[Thereasm Online] OnlineGameBridge thiếu TurnManager hoặc DeckManager trên MainBoard.");
            enabled = false;
            return;
        }
        Instance = this;
        initialized = true;
        // A new guest scene waits for its first accepted snapshot. A delayed
        // lobby packet must never bind a fresh board to the previous match.
        boundMatch = OnlineSession.Guest ? null : session.MatchId;
        enabled = true;
        if (deck.allCards != null) foreach (var data in deck.allCards) AddData(data);
        if (deck.neutralCardPool != null) foreach (var data in deck.neutralCardPool) AddData(data);
        Debug.Log($"[Thereasm Online] BRIDGE Awake host={session.IsHost} guest={OnlineSession.Guest}, match={session.MatchId}, catalog={catalog.Count}");
        if (OnlineSession.Guest)
        {
            // Client is view-only: never generate its own deck or simulate turns.
            turns.enabled = false;
            deck.enabled = false;
            if (deck.draftPanel != null) deck.draftPanel.SetActive(false);
            // TurnManager.Start() will not execute on a client: initialize its authored UI here.
            GameStartUI.CreateBoard(turns);
            if (turns.villagerBot != null) turns.villagerBot.gameObject.SetActive(false);
            if (turns.curBot != null) turns.curBot.gameObject.SetActive(false);
            turns.currentTurn = TurnState.SetupPhase;
        }
        OnlineSession.Instance.HostMessage += OnHostMessage;
        OnlineSession.Instance.GuestMessage += OnGuestMessage;
        if (OnlineSession.Guest)
        {
            HoldToReveal.isRevealingAll = false;
            CardInteraction.isAnyCardDragging = false;
            if (turns.winPanel != null) turns.winPanel.SetActive(false);
            OnlineSession.Instance.RequestSnapshot();
            nextGuestRequest = Time.unscaledTime + .25f;
        }
    }
    void OnDestroy()
    {
        if (OnlineSession.Instance != null)
        {
            OnlineSession.Instance.HostMessage -= OnHostMessage;
            OnlineSession.Instance.GuestMessage -= OnGuestMessage;
        }
        if (Instance == this) Instance = null;
    }
    private static string Key(CardData data) => ((int)data.owner) + ":" + data.cardNumber + ":" + data.name;
    private void AddData(CardData data) { if (data != null) catalog[Key(data)] = data; }
    private int Id(CardInstance card)
    {
        if (!ids.TryGetValue(card, out int id)) ids[card] = id = ++nextId;
        return id;
    }
    private CardInstance Find(int id)
    {
        foreach (var pair in ids) if (pair.Value == id && pair.Key != null) return pair.Key;
        return null;
    }
    public static bool Forward(OnlineAction action, CardInstance source = null, CardInstance target = null, int seat = -1, int option = -1)
    {
        if (!OnlineSession.Guest || ApplyingCommand) return false;
        if (Instance != null)
        {
            var input = source != null ? source.GetComponent<OnlineCardInput>() : null;
            var targetInput = target != null ? target.GetComponent<OnlineCardInput>() : null;
            Instance.Send(action, input != null ? input.CardId : 0, targetInput != null ? targetInput.CardId : 0, seat, option);
        }
        return true;
    }
    public bool Send(OnlineAction action, int source = 0, int target = 0, int seat = -1, int option = -1)
    {
        if (GameStartUI.IsBlockingInput) return false;
        if (!HasSnapshot || boundMatch != OnlineSession.Instance.MatchId)
        { Explain("The board is still synchronizing. Wait for the cards to finish loading."); return false; }
        if (!OnlineSession.LocalTurn) { Explain(ActionFeedback.InputBlock()); return false; }
        if (HasPhaseBarrier) { Explain("Your turn change is being confirmed. Wait a moment before the next action."); return false; }
        if (unacknowledged.Count >= 16) { Explain("Kết nối đang chậm. Các thao tác đã được giữ lại, vui lòng đợi một chút."); return false; }
        if (action == OnlineAction.EndTurn && turns.currentTurn == TurnState.SacrificePhase)
        {
            var missing = MissingVoterNames();
            if (missing.Count > 0) { Explain("Chưa bỏ phiếu: " + string.Join(", ", missing) + "."); return false; }
        }
        var command = new OnlineCommand { sequence = ++sequence, action = action,
            phase = (int)turns.currentTurn, voting = (int)turns.currentVotingPlayer,
            source = source, target = target, seat = seat, option = option, draftStep = deck.DraftStep,
            seatId = seat >= 0 && seat < remoteSeatIds.Count ? remoteSeatIds[seat] : 0 };
        unacknowledged.Add(command);
        if (views.TryGetValue(source, out var sourceCard))
        {
            var input = sourceCard.GetComponent<CardInteraction>();
            if (action == OnlineAction.Return) resourceReservations[command.sequence] = input.ReturnPaymentCost;
            if (action == OnlineAction.Place) resourceReservations[command.sequence] = views.TryGetValue(target, out var targetCard) ? input.SwapCost(targetCard.GetComponent<CardInteraction>()) : input.PlacementCost;
        }
        OnlineSession.Instance.SendCommand(command);
        resendAt = Time.unscaledTime + 1;
        localFeedback = null;
        turns.ClearLocalActionFeedback();
        RenderPredictedVotes();
        // Selecting a Cur is a private, reversible local visual prediction.
        // The host still validates the command; its next snapshot overwrites it.
        if (action == OnlineAction.SelectCur) { RenderPredictedCur(); turns.UpdateCurVisuals(); }
        turns.UpdateHeartsUI();
        return true;
    }
    public List<string> MissingVoterNames()
    {
        var targets = ProjectedVotes();
        var names = new List<string>();
        foreach (var pair in targets)
            if (pair.Value < 0 && views.TryGetValue(pair.Key, out var card)) names.Add(TurnManager.CardLabel(card));
        return names;
    }
    private Dictionary<int,int> ProjectedVotes()
    {
        var targets = new Dictionary<int,int>(ownVoteTargets);
        foreach (var command in unacknowledged)
            if (command.action == OnlineAction.Vote && targets.ContainsKey(command.source) && command.source != command.target &&
                views.TryGetValue(command.target, out var target) && !target.isDead) targets[command.source] = command.seat;
        return targets;
    }
    private void RenderPredictedCur()
    {
        // Preview only our own secret selection, never the other player's card.
        // No card skill, turn, hearts or win condition runs on the guest.
        if (!OnlineSession.Guest || turns.currentTurn != TurnState.SetupPhase || !deck.IsReady ||
            !OnlineSession.LocalCur) return;
        for (int i = unacknowledged.Count - 1; i >= 0; i--)
        {
            var command = unacknowledged[i];
            if (command.action != OnlineAction.SelectCur) continue;
            if (!views.TryGetValue(command.source, out var selected) || selected == null ||
                selected.isDead || selected.cardData == null ||
                selected.cardData.owner != OnlineSession.LocalRole ||
                selected.cardData.cardNumber == 9 || selected.cardData.cardType == CardType.Hunter)
                continue;
            turns.allCurs.RemoveAll(card => card != null && card.cardData != null &&
                card.cardData.owner == OnlineSession.LocalRole);
            turns.allCurs.Add(selected);
            break;
        }
    }
    private void RenderPredictedVotes()
    {
        if (!OnlineSession.Guest || turns.currentTurn != TurnState.SacrificePhase || turns.isTallyingVotes || turns.currentVotingPlayer != OnlineSession.LocalRole) return;
        var targets = ProjectedVotes();
        RequiredVotes = targets.Count; Voted = 0;
        var counts = new int[turns.allSeats.Count];
        foreach (int target in targets.Values) if (target >= 0 && target < counts.Length) { counts[target]++; Voted++; }
        foreach (var card in views.Values)
        {
            if (card == null || card.voteText == null) continue;
            int seat = turns.GetSeatIndex(card.GetComponent<CardInteraction>());
            int count = seat >= 0 && seat < counts.Length ? counts[seat] : 0;
            card.voteText.text = count.ToString(); card.voteText.gameObject.SetActive(count > 0);
        }
    }
    private void OnGuestMessage(string kind, string payload)
    {
        if (boundMatch != OnlineSession.Instance.MatchId) return;
        if (kind == "ready" && payload == OnlineSession.Instance.MatchId)
        {
            lastState = null; nextSend = 0;
            if (++hostReadyCount <= 5) Debug.Log($"[Thereasm Online] HOST nhận ready từ client (lần {hostReadyCount}), match={payload}");
            return;
        }
        if (kind != "command" || payload.Length > 4096 || commands.Count >= 32) return;
        try
        {
            var command = JsonUtility.FromJson<OnlineCommand>(payload);
            if (command == null || command.match != OnlineSession.Instance.MatchId) return;
            if (command.sequence <= receivedSequence) { nextSend = 0; lastState = null; return; }
            commands[command.sequence] = command;
        }
        catch (ArgumentException) { }
    }
    private void OnHostMessage(string kind, string payload)
    {
        if (kind == "action_result")
        {
            try
            {
                var result = JsonUtility.FromJson<QuickActionResult>(payload);
                if (result == null || OnlineSession.Instance == null ||
                    result.match != OnlineSession.Instance.MatchId ||
                    result.sequence <= lastQuickResult || result.sequence <= latestSnapshotAck ||
                    result.sequence > sequence) return;
                lastQuickResult = result.sequence;
                if (!string.IsNullOrEmpty(result.feedback)) Explain(result.feedback);
                SkillAudio.Current?.ApplyRemote(result.skillSounds);
            }
            catch (ArgumentException) { }
            return;
        }
        if (kind != "snapshot") return;
        try
        {
            // Decode transport compression before reading the snapshot JSON.
            var snapshot = JsonUtility.FromJson<OnlineSnapshot>(OnlineSnapshotCodec.Decode(payload));
            if (snapshot == null) return;
            if (snapshot.match != OnlineSession.Instance.MatchId)
            {
                if (guestSnapshotCount++ < 5)
                    Debug.LogWarning($"[Thereasm Online] CLIENT sai MatchId: host={snapshot.match}, local={OnlineSession.Instance.MatchId}");
                return;
            }
            if (!string.IsNullOrEmpty(boundMatch) && boundMatch != snapshot.match) return;
            boundMatch = snapshot.match;
            if (guestSnapshotCount++ < 5)
                Debug.Log($"[Thereasm Online] CLIENT snapshot rev={snapshot.revision}, ready={snapshot.ready}, cards={snapshot.cards?.Length ?? 0}, seats={snapshot.seats?.Length ?? 0}");
            if (snapshot.revision > appliedRevision && (pending == null || snapshot.revision > pending.revision))
                pending = snapshot;
        }
        catch (Exception exception)
        {
            Debug.LogWarning("[Thereasm Online] Ignoring malformed snapshot: " + exception.Message);
        }
    }
    void Update()
    {
        if (!OnlineSession.Active || OnlineSession.Instance.PeerLost ||
            !string.IsNullOrEmpty(boundMatch) && boundMatch != OnlineSession.Instance.MatchId) return;
        if (OnlineSession.Guest)
        {
            if (pending != null)
            {
                var snapshot = pending;
                pending = null; // Clear first, so one bad snapshot cannot throw every frame.
                try { Apply(snapshot); }
                catch (Exception exception)
                {
                    Debug.LogError($"[Thereasm Online] CLIENT không áp dụng được snapshot rev={snapshot.revision}: {exception}");
                }
            }
            if (CommandPending && Time.unscaledTime >= resendAt)
            { foreach (var command in unacknowledged) OnlineSession.Instance.SendCommand(command); resendAt = Time.unscaledTime + 1; }
            // A reliable packet can be missed while changing scenes. Keep requesting a full state
            // until one has been successfully applied, even if the original ready was lost.
            if (appliedRevision == 0 && Time.unscaledTime >= nextGuestRequest)
            {
                nextGuestRequest = Time.unscaledTime + (guestRequestCount < 3 ? .35f : .85f);
                if (++guestRequestCount <= 5)
                    Debug.LogWarning($"[Thereasm Online] CLIENT chưa áp dụng snapshot, gửi lại ready lần {guestRequestCount}");
                OnlineSession.Instance.RequestSnapshot();
            }
            return;
        }
        for (int processed = 0; processed < 16 && commands.TryGetValue(receivedSequence + 1, out var next); processed++)
        { commands.Remove(next.sequence); ApplyCommand(next); nextSend = 0; }
        if (Time.unscaledTime < nextSend) return;
        // Avoid flooding reliable streams with complete board views every 50 ms.
        // Commands bypass this throttle (nextSend=0); idle state is resent periodically.
        nextSend = Time.unscaledTime + Mathf.Max(.08f, OnlineSession.Instance.Settings.snapshotInterval);
        var state = Capture();
        string comparison = JsonUtility.ToJson(state);
        // A slow scene load can miss the first message; periodically send a complete view again.
        if (comparison == lastState && Time.unscaledTime - lastSent < 3) return;
        lastState = comparison;
        lastSent = Time.unscaledTime;
        state.revision = ++revision;
        OnlineSession.Instance.SendSnapshot(JsonUtility.ToJson(state));
    }
    private void ApplyCommand(OnlineCommand command)
    {
        if (command.match != OnlineSession.Instance.MatchId || command.sequence <= receivedSequence) return;
        int feedbackBefore = turns.FeedbackRevision;
        try { ExecuteCommand(command); }
        catch (Exception exception) { RejectCommand(command, "Không thực hiện được thao tác. Hãy chọn lại mục tiêu."); Debug.LogException(exception); }
        receipts.Add(new CommandReceipt { sequence = command.sequence, error = commandError });
        if (receipts.Count > 32) receipts.RemoveAt(0);

        // Fast feedback for the submitting player. Do not send hidden game state.
        // In particular, sounds and feedback are filtered for the remote player's role.
        string message = commandError;
        if (string.IsNullOrEmpty(message) && turns.FeedbackRevision != feedbackBefore)
            message = turns.GetOnlineFeedback(OnlineSession.Instance.RemoteRole);
        var result = new QuickActionResult
        {
            match = command.match, sequence = command.sequence, feedback = message,
            skillSounds = SkillAudio.Current != null
                ? SkillAudio.Current.For(OnlineSession.Instance.RemoteRole) : Array.Empty<SkillSoundEvent>()
        };
        OnlineSession.Instance.SendActionResult(JsonUtility.ToJson(result));
    }
    private void ExecuteCommand(OnlineCommand command)
    {
        if (command.match != OnlineSession.Instance.MatchId || command.sequence <= receivedSequence) return;
        receivedSequence = command.sequence;
        commandError = null;
        if (command.action == OnlineAction.Draft)
        {
            if (turns.currentTurn != TurnState.SetupPhase || deck.IsReady || deck.IsPicking ||
                command.draftStep != deck.DraftStep || !OnlineRules.CanDraft(OnlineSession.Instance.RemoteRole, deck.DraftStep, deck.DraftTotal, deck.IsPicking)) return;
            var offers = deck.DraftOptions();
            if (command.option < 0 || command.option >= offers.Length) return;
            ApplyingCommand = true;
            try { deck.PickNeutralCard(offers[command.option], command.option == 0); }
            finally { ApplyingCommand = false; }
            return;
        }
        if (!deck.IsReady) { RejectCommand(command, "Hãy chọn xong bài trung lập trước."); return; }
        if (command.phase != (int)turns.currentTurn || turns.currentTurn == TurnState.SacrificePhase &&
            command.voting != (int)turns.currentVotingPlayer) { RejectCommand(command, "Lượt đã thay đổi. Hãy thao tác lại."); return; }
        var role = OnlineSession.Instance.RemoteRole;
        if (!OnlineRules.Allows(command.action, role, (int)turns.currentTurn, turns.currentVotingPlayer, turns.isTallyingVotes, turns.isGameOver))
        { RejectCommand(command, "Thao tác này không hợp lệ trong lượt hiện tại."); return; }
        var card = Find(command.source);
        var target = Find(command.target);
        var interaction = card != null ? card.GetComponent<CardInteraction>() : null;
        if (command.seatId != 0)
        {
            command.seat = turns.allSeats.FindIndex(s => seatIds.TryGetValue(s, out int id) && id == command.seatId);
            if (command.seat < 0) { RejectCommand(command, "Ghế này không còn tồn tại."); return; }
            var occupant = turns.GetBoardCard(command.seat);
            if (command.target != 0 && occupant != target || command.target == 0 && occupant != null)
            { RejectCommand(command, "Mục tiêu đã thay đổi. Hãy chọn lại lá bài."); return; }
        }
        int feedbackBefore = turns.FeedbackRevision;
        ApplyingCommand = true;
        try
        {
            switch (command.action)
            {
                case OnlineAction.EndTurn:
                    if (turns.currentTurn == TurnState.SacrificePhase && turns.GetMissingVoterNames().Count > 0)
                        RejectCommand(command, "Chưa bỏ phiếu: " + string.Join(", ", turns.GetMissingVoterNames()) + ".");
                    else turns.EndTurn();
                    break;
                case OnlineAction.Check: turns.ExecuteCheck(); break;
                case OnlineAction.StartGame: turns.StartRealGame(); break;
                case OnlineAction.SelectCur:
                    if (card != null && !card.isDead && interaction != null && !interaction.isPlaced) turns.SelectCurCard(card);
                    else RejectCommand(command, "Chỉ chọn vai trò cho lá còn sống trong khay bài.");
                    break;
                case OnlineAction.Vote:
                    if (card != null && card.cardData.owner == role)
                    {
                        if (target == null || target.isDead || target == card) RejectCommand(command, "Phiếu phải nhắm vào một lá còn sống khác trên bàn.");
                        else { turns.StartVoteSelection(card); turns.ApplyVote(command.seat); }
                    }
                    else RejectCommand(command, "Hãy bỏ phiếu từ một lá thuộc phe của bạn đang ở trên bàn.");
                    break;
                case OnlineAction.Curse:
                    if (card != null && turns.IsCur(card) && interaction != null && interaction.isPlaced)
                    { turns.StartCurseSelection(card); turns.ApplyCurse(command.seat); }
                    break;
                case OnlineAction.Discover:
                    if (turns.CanChooseOnlineDiscover(command.option, role)) turns.ResolveInvokerSelection(command.option);
                    else RejectCommand(command, "Lựa chọn hồi sinh đã thay đổi. Hãy mở lại danh sách.");
                    break;
                case OnlineAction.Skill:
                    if (card != null && !card.isDead && interaction != null && interaction.isPlaced && card.cardData.cardSkill != null)
                    {
                        if (!card.cardData.cardSkill.TryCastActiveSkill(card, target)) RejectCommand(command, ActionFeedback.SkillFailure(card.cardData.cardSkill, card, target));
                    }
                    else RejectCommand(command, "Lá bài chưa ở trên bàn hoặc không có kỹ năng chủ động phù hợp.");
                    break;
                case OnlineAction.Return:
                    if (Movable(interaction) && interaction.isPlaced)
                    {
                        if (!turns.HasEnoughHearts(interaction.ReturnPaymentCost)) RejectCommand(command, ActionFeedback.Cost(card, interaction.ReturnPaymentCost));
                        else interaction.ReturnToHand();
                    }
                    else RejectCommand(command, interaction != null ? ActionFeedback.MovementBlock(interaction) ?? "Choose a seated card to retrieve." : "This card is no longer available. Choose another card.");
                    break;
                case OnlineAction.Place:
                    Debug.Log($"[Thereasm Online] Place cmd source={card?.name}, target={target?.name}, seat={command.seat}, state={interaction?.lockState}, movable={Movable(interaction)}");
                    if (!Movable(interaction) || command.seat < 0 || command.seat >= turns.allSeats.Count)
                    { RejectCommand(command, interaction != null ? ActionFeedback.MovementBlock(interaction) ?? "Choose a valid seat on the board." : "This card is no longer available. Choose another card."); break; }
                    var position = turns.allSeats[command.seat].cardPos;
                    var occupant = position.GetComponentInChildren<CardInteraction>();
                    int cost = occupant == null ? interaction.PlacementCost : interaction.SwapCost(occupant);
                    if (!turns.HasEnoughHearts(cost)) { RejectCommand(command, ActionFeedback.Cost(card, cost)); break; }
                    if (occupant == null) interaction.PlaceOnSeat(position);
                    else if (occupant != interaction && !occupant.isLockedByElder && !occupant.isLockedBySkill &&
                        (occupant.lockState == 1 || occupant.lockState == 3)) interaction.SwapWith(occupant, position);
                    else RejectCommand(command, occupant != null ? ActionFeedback.SwapBlock(interaction, occupant, position) : "Choose a valid seat on the board.");
                    break;
            }
        }
        finally
        {
            if (commandError == null && turns.FeedbackRevision != feedbackBefore) RejectCommand(command, turns.feedbackMessage);
            ApplyingCommand = false;
        }
    }
    private void RejectCommand(OnlineCommand command, string message)
    {
        commandError = message;
        Debug.LogWarning("[Thereasm Online] " + message + " (lệnh " + command.sequence + ")");
    }
    private static bool Movable(CardInteraction card)
    {
        if (card == null || card.isLockedByElder || card.isLockedBySkill) return false;
        var state = card.GetComponent<CardInstance>();
        return state != null && !state.isDead && (!card.isPlaced || card.lockState >= 3);
    }
    private OnlineSnapshot Capture()
    {
        var cards = new List<CardView>();
        var votes = turns.GetOnlineVoteCounts(OnlineSession.Instance.RemoteRole);
        foreach (var card in FindObjectsByType<CardInstance>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (card.cardData == null || !catalog.ContainsKey(Key(card.cardData))) continue;
            if (turns.discoverPanel != null && card.transform.IsChildOf(turns.discoverPanel.transform) ||
                deck.draftPanel != null && card.transform.IsChildOf(deck.draftPanel.transform)) continue;
            var interaction = card.GetComponent<CardInteraction>();
            int seat = interaction != null ? turns.GetSeatIndex(interaction) : -1;
            if (seat < 0 && interaction != null && interaction.isPlaced)
                seat = turns.allSeats.FindIndex(s => s.cardPos == interaction.originalParent);
            bool privateAccess = PlayerInformation.Secrets(OnlineSession.Instance.RemoteRole, turns.isGameOver);
            cards.Add(new CardView { id = Id(card), data = Key(card.cardData), seat = seat,
                dead = card.isDead, owner = (int)card.currentOwner, placer = (int)card.lastPlacedBy,
                deadOrder = card.cemeteryOrder,
                mustVote = turns.currentTurn == TurnState.SacrificePhase && turns.currentVotingPlayer == OnlineSession.Instance.RemoteRole && turns.IsMyVoteTurn(card) && seat >= 0 && turns.FindLegalVoteTarget(card, -1) >= 0,
                voteTarget = turns.currentTurn == TurnState.SacrificePhase && turns.currentVotingPlayer == OnlineSession.Instance.RemoteRole && card.cardData.owner == OnlineSession.Instance.RemoteRole ? turns.GetVoteTarget(card) : -1,
                infected = privateAccess && card.isInfected, cur = privateAccess && turns.IsCur(card),
                deadInfected = privateAccess && card.wasInfectedWhenDied,
                used = PlayerInformation.UsedSkill(card, OnlineSession.Instance.RemoteRole, turns.isGameOver), silenced = card.isSilenced, suspicion = card.bonusSuspicion,
                protectedCard = interaction != null && interaction.isProtectedByPreacher,
                elder = interaction != null && interaction.isLockedByElder, skillLocked = interaction != null && interaction.isLockedBySkill,
                lockState = interaction != null ? interaction.lockState : 0,
                blind = card.isRevealedByBlind, blindCur = card.isRevealedByBlind && card.blindRevealAsCur,
                blindInfected = card.isRevealedByBlind && card.blindRevealAsInfected,
                ritualUsed = privateAccess && card.hasUsedSkillInRitual,
                gambler = PlayerInformation.GamblerMark(card, OnlineSession.Instance.RemoteRole),
                snooper = PlayerInformation.SnooperMark(card, OnlineSession.Instance.RemoteRole),
                suspected = card.suspectedMark != null && card.suspectedMark.activeSelf,
                votes = turns.isTallyingVotes ? VisibleVoteCount(card) : seat >= 0 && seat < votes.Length ? votes[seat] : 0,
                changedVotes = turns.isTallyingVotes && card.voteText != null && card.voteText.color == Color.yellow });
        }
        cards.Sort((a,b) => a.id.CompareTo(b.id));
        var seats = new List<SeatView>();
        foreach (var seat in turns.allSeats)
        {
            var rect = seat.GetComponent<RectTransform>();
            if (!seatIds.TryGetValue(seat, out int seatId)) seatIds[seat] = seatId = ++nextSeatId;
            seats.Add(new SeatView { id = seatId, x = rect.anchoredPosition.x, y = rect.anchoredPosition.y,
                width = rect.sizeDelta.x, height = rect.sizeDelta.y, cemetery = seat.isNextToCemetery });
        }
        // Progress is private too: never broadcast the host's voter count to
        // the guest while the host votes (and vice versa).
        int voted = 0, required = 0;
        if (turns.currentTurn == TurnState.SacrificePhase && !turns.isTallyingVotes &&
            turns.currentVotingPlayer == OnlineSession.Instance.RemoteRole)
            turns.GetVotingProgress(out voted, out required);
        var options = new List<string>(); var indices = new List<int>();
        var draftOptions = new List<string>();
        var deckCards = new List<string>();
        foreach (var data in deck.allCards) if (data != null) deckCards.Add(Key(data));
        if (!deck.IsReady) foreach (var data in deck.DraftOptions()) draftOptions.Add(Key(data));
        if (turns.InvokerDiscoverOpenFor(OnlineSession.Instance.RemoteRole))
            foreach (var option in turns.GetOnlineDiscoverOptions()) { options.Add(Key(option)); indices.Add(turns.deadCardsData.IndexOf(option)); }
        return new OnlineSnapshot { match = OnlineSession.Instance.MatchId, phase = (int)turns.currentTurn,
            acknowledged = receivedSequence, commandError = commandError, draftStep = deck.DraftStep, draftTotal = deck.DraftTotal,
            picking = deck.IsPicking, draftOptions = draftOptions.ToArray(), canStart = turns.CanStartGame, deckCards = deckCards.ToArray(),
            voting = (int)turns.currentVotingPlayer, firstPlayer = (int)turns.firstPlayerThisRound,
            p1Hearts = turns.p1Hearts, p2Hearts = turns.p2Hearts, p1Checks = turns.p1Checks, p2Checks = turns.p2Checks,
            ready = deck.IsReady, tally = turns.isTallyingVotes, over = turns.isGameOver,
            feedback = turns.GetOnlineFeedback(OnlineSession.Instance.RemoteRole), winner = turns.isGameOver && turns.winText != null ? turns.winText.text : "",
            voted = voted, required = required, cards = cards.ToArray(), seats = seats.ToArray(),
            receipts = receipts.ToArray(),
            voteEffects = turns.voteEffects != null ? turns.voteEffects.For(OnlineSession.Instance.RemoteRole) : new VoteEffectRecord[0],
            skillSounds = SkillAudio.Current != null ? SkillAudio.Current.For(OnlineSession.Instance.RemoteRole) : Array.Empty<SkillSoundEvent>(),
            options = options.ToArray(), optionIndices = indices.ToArray() };
    }
    private static int VisibleVoteCount(CardInstance card) => card.voteText != null && card.voteText.gameObject.activeSelf &&
        int.TryParse(card.voteText.text, out int count) ? count : 0;
    private void Apply(OnlineSnapshot state)
    {
        if (state.cards == null || state.seats == null || state.cards.Length > 80 || state.seats.Length < 1 || state.seats.Length > 32) return;
        turns.currentTurn = (TurnState)state.phase;
        turns.voteEffects?.ApplyRemote(state.voteEffects);
        turns.currentVotingPlayer = (DefaultOwner)state.voting;
        turns.firstPlayerThisRound = (TurnState)state.firstPlayer;
        turns.p1Hearts = state.p1Hearts; turns.p2Hearts = state.p2Hearts;
        turns.p1Checks = state.p1Checks; turns.p2Checks = state.p2Checks;
        turns.isTallyingVotes = state.tally; turns.isGameOver = state.over;
        Voted = state.voted; RequiredVotes = state.required;
        if (state.deckCards != null)
        {
            // Most snapshots repeat the same deck. Avoid allocating and replacing
            // its list on every network update (causes extra UI/layout work).
            bool deckChanged = deck.allCards == null || deck.allCards.Count != state.deckCards.Length;
            if (!deckChanged)
                for (int i = 0; i < state.deckCards.Length; i++)
                    if (!catalog.TryGetValue(state.deckCards[i], out var expected) ||
                        deck.allCards[i] != expected) { deckChanged = true; break; }
            if (deckChanged)
            {
                deck.allCards = new List<CardData>(state.deckCards.Length);
                foreach (var key in state.deckCards)
                    if (catalog.TryGetValue(key, out var data)) deck.allCards.Add(data);
            }
        }
        CanStartGame = state.canStart;
        int previousAcknowledged = unacknowledged.Count > 0 ? unacknowledged[0].sequence - 1 : sequence;
        latestSnapshotAck = Mathf.Max(latestSnapshotAck, state.acknowledged);
        unacknowledged.RemoveAll(command => command.sequence <= state.acknowledged);
        foreach (int sent in new List<int>(resourceReservations.Keys)) if (sent <= state.acknowledged) resourceReservations.Remove(sent);
        if (state.receipts != null) foreach (var receipt in state.receipts)
            if (receipt.sequence > previousAcknowledged && !string.IsNullOrEmpty(receipt.error)) Explain(receipt.error);
        ownVoteTargets.Clear();
        if (turns.winText != null) turns.winText.text = state.winner;
        turns.SetOnlineFeedback(string.IsNullOrEmpty(state.commandError) ? state.feedback : state.commandError);
        // Move views out before deleting/rebuilding topology; a shrinking extra-seat
        // list must not destroy a living card which has moved to another seat.
        bool topologyChanged = remoteSeatIds.Count != state.seats.Length;
        if (!topologyChanged) for (int i = 0; i < state.seats.Length; i++)
            if (remoteSeatIds[i] != state.seats[i].id) { topologyChanged = true; break; }
        // Hand sorting and cemetery re-layout are expensive on the guest.
        // Only repeat them if actual membership/topology changed.
        bool handLayoutDirty = topologyChanged || !guestReady && state.ready;
        bool cemeteryDirty = topologyChanged || appliedRevision == 0;
        if (topologyChanged) foreach (var card in views.Values)
            if (card != null && !card.GetComponent<CardInteraction>().isInspecting &&
                (card.GetComponent<OnlineCardInput>() == null || !card.GetComponent<OnlineCardInput>().IsPresenting))
                card.transform.SetParent(turns.cemeteryCardPos.parent, true);
        while (turns.allSeats.Count < state.seats.Length)
        {
            GameObject template = null;
            foreach (var data in catalog.Values)
                if (data.cardSkill is Skill_GameMaster skill && skill.extraSeatPrefab != null) { template = skill.extraSeatPrefab; break; }
            if (template == null) throw new InvalidOperationException("Missing editable Game Master extra-seat prefab.");
            var clone = Instantiate(template, turns.allSeats[0].transform.parent);
            var seat = clone.GetComponent<SeatDropZone>();
            foreach (var child in seat.cardPos.GetComponentsInChildren<CardInstance>(true)) Destroy(child.gameObject);
            clone.name = "Remote extra seat " + turns.allSeats.Count;
            turns.allSeats.Add(seat);
        }
        while (turns.allSeats.Count > state.seats.Length)
        { int last = turns.allSeats.Count - 1; var seat = turns.allSeats[last]; turns.allSeats.RemoveAt(last); Destroy(seat.gameObject); }
        remoteSeatIds.Clear();
        for (int i = 0; i < state.seats.Length; i++)
        {
            remoteSeatIds.Add(state.seats[i].id);
            var rect = turns.allSeats[i].GetComponent<RectTransform>(); var data = state.seats[i];
            rect.anchoredPosition = new Vector2(data.x, data.y); rect.sizeDelta = new Vector2(data.width, data.height);
            turns.allSeats[i].isNextToCemetery = data.cemetery;
        }
        turns.allCurs.Clear();
        var present = new HashSet<int>();
        foreach (var data in state.cards)
        {
            if (!catalog.TryGetValue(data.data, out var definition)) continue;
            present.Add(data.id);
            Transform parent = data.dead ? (turns.cemeteryViewPanel.activeSelf ? turns.CemeteryContent : turns.cemeteryCardPos) :
                data.seat >= 0 && data.seat < turns.allSeats.Count ? turns.allSeats[data.seat].cardPos :
                definition.owner == DefaultOwner.Player1 ? deck.player1Hand : definition.owner == DefaultOwner.Player2 ? deck.player2Hand : deck.neutralHand;
            bool viewCreated = !views.TryGetValue(data.id, out var card) || card == null;
            if (viewCreated)
            {
                handLayoutDirty = true;
                cemeteryDirty = true;
                card = Instantiate(turns.cardPrefab, parent).GetComponent<CardInstance>();
                card.SetupCard(definition); // Explicitly render even when its hand panel started inactive.
                card.GetComponent<CardInteraction>().enabled = false;
                var input = card.gameObject.AddComponent<OnlineCardInput>(); input.CardId = data.id;
                views[data.id] = card;
            }
            var existingInteraction = card.GetComponent<CardInteraction>();
            var onlineInput = card.GetComponent<OnlineCardInput>();
            bool presenting = onlineInput != null && onlineInput.PreservePresentation(state.acknowledged, state.phase, data.dead, state.over);
            bool parentChanged = !presenting && !existingInteraction.isInspecting && card.transform.parent != parent;
            if (parentChanged)
            {
                handLayoutDirty = true;
                cemeteryDirty = true;
                card.transform.SetParent(parent, false);
            }
            if (card.isDead != data.dead || card.cemeteryOrder != data.deadOrder)
                cemeteryDirty = true;
            card.currentOwner = (DefaultOwner)data.owner; card.lastPlacedBy = (DefaultOwner)data.placer;
            card.isDead = data.dead; card.isInfected = data.infected; card.hasUsedSkill = data.used; card.isSilenced = data.silenced;
            card.cemeteryOrder = data.deadOrder;
            if (data.mustVote) ownVoteTargets[data.id] = data.voteTarget;
            card.wasInfectedWhenDied = data.deadInfected;
            if (card.bonusSuspicion != data.suspicion) card.bonusSuspicion = data.suspicion;
            card.isRevealedByBlind = data.blind; card.blindRevealAsCur = data.blindCur; card.blindRevealAsInfected = data.blindInfected;
            if (data.cur) turns.allCurs.Add(card);
            var interaction = card.GetComponent<CardInteraction>();
            interaction.originalParent = parent;
            interaction.handParent = definition.owner == DefaultOwner.Player1 ? deck.player1Hand :
                definition.owner == DefaultOwner.Player2 ? deck.player2Hand : deck.neutralHand;
            card.hasUsedSkillInRitual = data.ritualUsed;
            if (card.gamblerMark != null) card.gamblerMark.SetActive(data.gambler);
            if (card.snooperMark != null) card.snooperMark.SetActive(data.snooper);
            if (card.suspectedMark != null) card.suspectedMark.SetActive(data.suspected);
            interaction.isPlaced = !data.dead && data.seat >= 0; interaction.lockState = data.lockState;
            if (card.TryGetComponent<CardDialogue>(out var dialogue))
            {
                if (interaction.isPlaced && !state.over) dialogue.StartChatting();
                else dialogue.StopChatting();
            }
            interaction.isProtectedByPreacher = data.protectedCard; interaction.isLockedByElder = data.elder; interaction.isLockedBySkill = data.skillLocked;
            var rect = card.GetComponent<RectTransform>();
            if (!presenting && !interaction.isInspecting && !data.dead)
            {
                // Do not overwrite a hand card's local hover/fanning animation on
                // every network snapshot (the client receives many per second).
                if (viewCreated || parentChanged || data.seat >= 0)
                    rect.localScale = interaction.PrefabRootScale;
                if (data.seat >= 0) { rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f,.5f); rect.anchoredPosition = Vector2.zero; rect.localRotation = Quaternion.identity; }
            }
            var group = card.GetComponent<CanvasGroup>();
            if (group != null) { group.interactable = true; group.blocksRaycasts = !presenting &&
                (onlineInput == null || !onlineInput.IsDragging) && (!data.dead || turns.cemeteryViewPanel.activeSelf); }
            if (card.TryGetComponent<UnityEngine.UI.LayoutElement>(out var layout))
                layout.ignoreLayout = !data.dead || !turns.cemeteryViewPanel.activeSelf;
            if (card.voteText != null)
            {
                card.voteText.text = data.votes.ToString(); card.voteText.gameObject.SetActive(data.votes > 0);
                card.voteText.color = data.changedVotes ? Color.yellow : Color.white;
            }
        }
        foreach (var pair in new List<KeyValuePair<int,CardInstance>>(views))
            if (!present.Contains(pair.Key))
            {
                handLayoutDirty = true;
                cemeteryDirty = true;
                if (pair.Value != null) Destroy(pair.Value.gameObject);
                views.Remove(pair.Key);
            }
        turns.deadCardsData.Clear();
        var corpses = new List<CardInstance>();
        foreach (var card in views.Values) if (card != null && card.isDead) corpses.Add(card);
        corpses.Sort((a,b) => a.cemeteryOrder.CompareTo(b.cemeteryOrder));
        foreach (var card in corpses) turns.deadCardsData.Add(card.cardData);
        if (cemeteryDirty) turns.RefreshCemeteryPresentation();
        if (state.ready)
        {
            // Cards are already instantiated at this point. Suppress the normal
            // staged entrance fan until the first complete client hand has been laid out.
            if (!guestReady) deck.BeginGuestHandSync();
            deck.InitializeOnlineView(state.draftStep, state.draftTotal);
            if (!guestReady)
            {
                deck.SwitchToHand(OnlineSession.LocalRole);
                deck.FinishGuestHandSync();
                guestReady = true;
            }
        }
        if (!state.ready)
        {
            var offers = new List<CardData>();
            if (state.draftOptions != null) foreach (var key in state.draftOptions)
                if (catalog.TryGetValue(key, out var data)) offers.Add(data);
            deck.ApplyOnlineDraft(state.draftStep, state.draftTotal, state.picking, offers.ToArray());
        }
        if (handLayoutDirty && CardInteraction.currentlyInspectedCard == null)
        {
            foreach (var hand in new[] { deck.player1Hand, deck.player2Hand, deck.neutralHand })
            { deck.SortHand(hand); deck.ArrangeFanLayout(hand); }
        }
        RenderPredictedCur();
        turns.UpdateCurVisuals();
        turns.UpdateHeartsUI();
        RenderPredictedVotes();
        if (turns.btnStartGame != null) turns.btnStartGame.SetActive(state.ready && state.phase == 0);
        if (turns.btnEndTurn != null) turns.btnEndTurn.SetActive(state.ready && state.phase != 0 && !state.over);
        if (turns.btnCheck != null && turns.btnCheck.TryGetComponent<UnityEngine.UI.Button>(out var check))
            check.interactable = OnlineSession.LocalTurn && (state.phase == 1 && state.p1Checks > 0 || state.phase == 2 && state.p2Checks > 0);
        RenderDiscover(state);
        SkillAudio.Current?.ApplyRemote(state.skillSounds);
        appliedRevision = state.revision; // Only confirm success after the complete view was applied.
        boundMatch = state.match;
        OnlineSession.Instance.ConfirmBoardMatch(state.match);
        if (appliedRevision == 1 || appliedRevision % 20 == 0)
            Debug.Log($"[Thereasm Online] CLIENT áp dụng snapshot rev={appliedRevision}, views={views.Count}, deckReady={deck.IsReady}, localTurn={OnlineSession.LocalTurn}");
    }
    private void RenderDiscover(OnlineSnapshot state)
    {
        bool visible = state.options != null && state.optionIndices != null && state.options.Length > 0 &&
            state.options.Length == state.optionIndices.Length;
        if (turns.discoverPanel == null) return;
        turns.discoverPanel.SetActive(visible);
        string signature = visible ? string.Join("|", state.options) + ":" + string.Join(",", state.optionIndices) : "";
        if (signature == discoverSignature) return;
        discoverSignature = signature;
        foreach (var slot in turns.discoverSlots) foreach (Transform child in slot) Destroy(child.gameObject);
        if (!visible) return;
        for (int i = 0; i < state.options.Length && i < turns.discoverSlots.Length; i++)
        {
            if (!catalog.TryGetValue(state.options[i], out var definition)) continue;
            var card = Instantiate(turns.cardPrefab, turns.discoverSlots[i]).GetComponent<CardInstance>();
            // Discover uses the same prefab-authored offset and relative
            // enlargement as the offline discover view.
            card.transform.localScale = Vector3.Scale(card.transform.localScale, new Vector3(1.5f, 1.5f, 1f));
            card.cardData = definition; card.GetComponent<CardInteraction>().enabled = false; card.FaceUp();
            var input = card.gameObject.AddComponent<OnlineCardInput>(); input.DiscoverIndex = state.optionIndices[i];
        }
    }
    private string discoverSignature;
}

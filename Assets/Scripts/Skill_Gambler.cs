using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "Skill_Gambler", menuName = "Card Skills/Active/Gambler")]
public class Skill_Gambler : CardSkill
{
    public int skillCost = 0;

    // Danh sách bí mật lưu những người chơi (P1/P2) đã tham gia cược
    public static List<DefaultOwner> playersWhoGambled = new List<DefaultOwner>();
    
    // Lưu mục tiêu cược (+1 phiếu) của từng người chơi
    public static Dictionary<DefaultOwner, int> playerTargetMap = new Dictionary<DefaultOwner, int>();
    private static Dictionary<DefaultOwner, CardInstance> playerTargetCards = new Dictionary<DefaultOwner, CardInstance>();
    private static Dictionary<DefaultOwner, CardInstance> playerCasters = new Dictionary<DefaultOwner, CardInstance>();
    public static bool HasUsedBy(CardInstance card, DefaultOwner viewer) => playerCasters.TryGetValue(viewer, out var caster) && caster == card;
    public static bool IsMarkedFor(CardInstance card, DefaultOwner viewer) =>
        playerTargetCards.TryGetValue(viewer, out var target) && target == card && card != null && !card.isDead;

    // Cờ hiệu để đảm bảo logic tính toán phiếu chỉ chạy 1 lần duy nhất cho cả vòng
    private static bool isResolvedThisRound = false;

    public override bool CastActiveSkill(CardInstance caster, CardInstance target)
    {
        if (target == null || TurnManager.instance.currentTurn != TurnState.SacrificePhase) return false;

        // Cược theo người đang vote, không theo người đặt Gambler.
        DefaultOwner currentPlayer = TurnManager.instance.currentVotingPlayer;
        int targetIdx = TurnManager.instance.GetSeatIndex(target.GetComponent<CardInteraction>());
        if (target.isDead || targetIdx < 0) return false;

        // Cho phép đổi mục tiêu; chỉ ghi nhận một lần tham gia cược mỗi người.
        if (!playersWhoGambled.Contains(currentPlayer))
        {
            if (!TurnManager.instance.HasEnoughHearts(skillCost)) return false;
            TurnManager.instance.ConsumeHearts(skillCost);
            playersWhoGambled.Add(currentPlayer);
        }

        if (playerTargetCards.TryGetValue(currentPlayer, out var previous) && previous != null)
        {
            if (previous.gamblerMark != null) previous.gamblerMark.SetActive(false);
        }

        if (target.gamblerMark != null)
        {
            target.gamblerMark.SetActive(!OnlineSession.Active || OnlineSession.LocalRole == currentPlayer);
        }

        playerTargetMap[currentPlayer] = targetIdx; 
        playerTargetCards[currentPlayer] = target;
        playerCasters[currentPlayer] = caster;
        caster.hasUsedSkill = true;
        PlayerInformation.RefreshHostMarks();
        return true;
    }

    public override void OnBeforeVoteCalculate(CardInstance owner, int[] voteCounts, int mySeatIndex)
    {
        if (isResolvedThisRound) return;
        isResolvedThisRound = true;

        // First là người mở đầu pha hiến tế; Second là người còn lại.
        DefaultOwner voter1 = (TurnManager.instance.firstPlayerThisRound == TurnState.Player1Turn) ? DefaultOwner.Player1 : DefaultOwner.Player2;
        DefaultOwner voter2 = (voter1 == DefaultOwner.Player1) ? DefaultOwner.Player2 : DefaultOwner.Player1;

        bool v1Used = playersWhoGambled.Contains(voter1);
        bool v2Used = playersWhoGambled.Contains(voter2);

        
        if (v1Used)
        {
            // First được cộng phiếu nếu Second không cược.
            bool getE1 = !v2Used; 
            ExecuteEffect(voter1, getE1, voteCounts);
        }

        if (v2Used)
        {
            // Second được cộng phiếu nếu First cũng cược.
            bool getE1 = v1Used; 
            ExecuteEffect(voter2, getE1, voteCounts);
        }
    }

    private void ExecuteEffect(DefaultOwner player, bool isE1, int[] voteCounts)
    {
        int[] before = (int[])voteCounts.Clone();
        if (isE1)
        {
            // Cộng một phiếu cho mục tiêu đã chọn.
            if (playerTargetCards.TryGetValue(player, out var target) && target != null && !target.isDead)
            {
                int idx = TurnManager.instance.GetSeatIndex(target.GetComponent<CardInteraction>());
                if (idx >= 0 && idx < voteCounts.Length)
                {
                    voteCounts[idx]++;
                    Debug.Log($"<color=orange>Gambler {player}: Thắng cược! Nhận E1 (+1 phiếu ghế {idx})</color>");
                }
            }
        }
        else
        {
            // Trừ một phiếu do phe của người cược đã bỏ.
            int myVoteSeat = TurnManager.instance.GetVoteTargetOfPlayer(player);
            if (myVoteSeat != -1 && voteCounts[myVoteSeat] > 0)
            {
                voteCounts[myVoteSeat]--;
                Debug.Log($"<color=red>Gambler {player}: Thua cược! Nhận E2 (-1 phiếu bầu của bạn)</color>");
            }
        }
        bool first = player == (TurnManager.instance.firstPlayerThisRound == TurnState.Player1Turn ? DefaultOwner.Player1 : DefaultOwner.Player2);
        string reason = isE1 ? (first ? "The first bettor wins: no counter-bet was placed. Add 1 vote to the chosen target." :
            "The second bettor wins because both players bet. Add 1 vote to the chosen target.") :
            (first ? "The first bettor loses because both players bet. Remove 1 vote from that player's recorded ballot target, if possible." :
            "The second bettor loses: only the second player bet. Remove 1 vote from that player's recorded ballot target, if possible.");
        if (playerCasters.TryGetValue(player,out var caster)) VoteEffectFeed.Current?.RecordChange(caster,"Gambler",reason,before,voteCounts);
    }

    public static void ResetComboChain()
    {
        playersWhoGambled.Clear();
        playerTargetMap.Clear();
        playerTargetCards.Clear();
        playerCasters.Clear();
        isResolvedThisRound = false;
    }
}

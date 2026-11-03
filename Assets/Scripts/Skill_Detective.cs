using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "Skill_Detective", menuName = "Card Skills/Passive/Detective Inspect")]
public class Skill_Detective : CardSkill
{
    public override void Activate(CardInstance owner)
    {
        if (TurnManager.instance == null) return;

        CardInteraction interaction = owner.GetComponent<CardInteraction>();
        if (interaction == null || !interaction.isPlaced || owner.isDead || owner.isSilenced || owner.isFaceDown || owner.hasUsedSkill) return;

        int mySeatIndex = TurnManager.instance.GetSeatIndex(interaction);
        if (mySeatIndex == -1) return;

        RectTransform mySeatRect = TurnManager.instance.allSeats[mySeatIndex].GetComponent<RectTransform>();
        Vector2 myPos = mySeatRect.anchoredPosition;

        SeatDropZone nearestLeftSeat = null;
        SeatDropZone nearestRightSeat = null;

        float minLeftDist = float.MaxValue;
        float minRightDist = float.MaxValue;

        // Tìm ghế gần nhất ở hai bên, kể cả ghế trống.
        foreach (var seat in TurnManager.instance.allSeats)
        {
            if (seat == TurnManager.instance.allSeats[mySeatIndex]) continue;

            RectTransform otherSeatRect = seat.GetComponent<RectTransform>();
            Vector2 otherPos = otherSeatRect.anchoredPosition;

            // Chỉ quét ghế cùng hàng ngang (Sai số 5 pixel)
            if (Mathf.Abs(otherPos.y - myPos.y) < 5f)
            {
                float xDist = otherPos.x - myPos.x;

                if (xDist < -5f) 
                {
                    float absDist = Mathf.Abs(xDist);
                    if (absDist < minLeftDist)
                    {
                        minLeftDist = absDist;
                        nearestLeftSeat = seat;
                    }
                }
                else if (xDist > 5f) 
                {
                    if (xDist < minRightDist)
                    {
                        minRightDist = xDist;
                        nearestRightSeat = seat;
                    }
                }
            }
        }

        // Detective chỉ hoạt động khi cả hai ghế liền kề đều có người.
        if (nearestLeftSeat == null || nearestRightSeat == null || 
            nearestLeftSeat.cardPos.childCount == 0 || nearestRightSeat.cardPos.childCount == 0)
        {
            Debug.Log($"<color=yellow>[Detective] {owner.name} thất bại: Thiếu hàng xóm hoặc ghế kề bên bị trống.</color>");
            return;
        }

        CardInstance leftCard = nearestLeftSeat.cardPos.GetComponentInChildren<CardInstance>();
        CardInstance rightCard = nearestRightSeat.cardPos.GetComponentInChildren<CardInstance>();
        
        if (leftCard == null || rightCard == null) return;

        int leftScore = CalculateSuspicion(leftCard);
        int rightScore = CalculateSuspicion(rightCard);


        CardInstance targetToSuspect = null;
        if (leftScore > rightScore) targetToSuspect = leftCard;
        else if (rightScore > leftScore) targetToSuspect = rightCard;
        else 
        {
            // Hòa điểm: Ưu tiên Cur
            if (TurnManager.instance.IsCur(leftCard) && !TurnManager.instance.IsCur(rightCard)) targetToSuspect = leftCard;
            else if (TurnManager.instance.IsCur(rightCard) && !TurnManager.instance.IsCur(leftCard)) targetToSuspect = rightCard;
            else targetToSuspect = (Random.value > 0.5f) ? leftCard : rightCard;
        }

        if (targetToSuspect != null)
        {
            targetToSuspect.bonusSuspicion += 1;
            if (targetToSuspect.suspectedMark != null) targetToSuspect.suspectedMark.SetActive(true);
            
            owner.hasUsedSkill = true; 
            VoteEffectFeed.Current?.Detective(owner,targetToSuspect);
            Debug.Log($"<color=red>-> [Detective] Đã găm bi vào: {targetToSuspect.name}!</color>");
        }
    }

    private int CalculateSuspicion(CardInstance card)
    {
        int baseScore = TurnManager.instance.IsCur(card) ? 2 : 1;
        return baseScore + card.bonusSuspicion;
    }
}

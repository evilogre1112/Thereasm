public enum TutorialStage
{
    Welcome, ChooseCur, OpenVillageHand, ChooseHiddenCur, StartMatch, Inspect, CloseInspect,
    PlaceVillager, PlaceHunter, EndVillageTurn, Reveal, PlaceCur, EndCurTurn, Shoot,
    OpenCemetery, CloseCemetery, RitualIntro, Curse, EndCurse, VoteVillager, VoteHunter,
    VoteHiddenCur, ConfirmVotes, OpponentExample, Tally, Purge,
    HeartIntro, BudgetPlace, BudgetMove, BudgetReturn, BudgetOpponentIntro, BudgetOpponentPlace,
    BudgetRefill, BudgetOpponentMove, BudgetSwapIntro, BudgetSwap, BudgetReference, BudgetSwapReference, BudgetSkills, Complete
}

public enum TutorialAction { Continue, SelectCur, VillageHand, StartMatch, Inspect, Place, EndTurn, Reveal, Skill, Cemetery, Curse, Vote, Purge, Return, Swap }

// Input constraints are shared by the guide and the real gameplay entry points.
public static class TutorialRules
{
    public const int StageCount = (int)TutorialStage.Complete + 1;
    public static bool Allows(TutorialStage stage, TutorialAction action, int source = 0, int target = 0, int seat = -1)
    {
        switch (stage)
        {
            case TutorialStage.Welcome:
            case TutorialStage.RitualIntro:
            case TutorialStage.Reveal:
            case TutorialStage.OpponentExample:
            case TutorialStage.HeartIntro:
            case TutorialStage.BudgetOpponentIntro:
            case TutorialStage.BudgetRefill:
            case TutorialStage.BudgetSwapIntro:
            case TutorialStage.BudgetReference:
            case TutorialStage.BudgetSwapReference:
            case TutorialStage.BudgetSkills:
            case TutorialStage.Complete: return action == TutorialAction.Continue;
            case TutorialStage.BudgetPlace: return action == TutorialAction.Place && source == 12 && seat == 0;
            case TutorialStage.BudgetMove: return action == TutorialAction.Place && source == 12 && seat == 1;
            case TutorialStage.BudgetReturn: return action == TutorialAction.Return && source == 12;
            case TutorialStage.BudgetOpponentPlace: return action == TutorialAction.Place && source == 3 && seat == 0;
            case TutorialStage.BudgetOpponentMove: return action == TutorialAction.Place && source == 3 && seat == 1;
            case TutorialStage.BudgetSwap: return action == TutorialAction.Swap && source == 12 && target == 3;
            case TutorialStage.ChooseCur: return action == TutorialAction.SelectCur && source == 1;
            case TutorialStage.OpenVillageHand: return action == TutorialAction.VillageHand;
            case TutorialStage.ChooseHiddenCur: return action == TutorialAction.SelectCur && source == 11;
            case TutorialStage.StartMatch: return action == TutorialAction.StartMatch;
            case TutorialStage.Inspect:
            case TutorialStage.CloseInspect: return action == TutorialAction.Inspect && source == 10;
            case TutorialStage.PlaceVillager: return action == TutorialAction.Place && source == 10 && seat == 0;
            case TutorialStage.PlaceHunter: return action == TutorialAction.Place && source == 9 && seat == 1;
            case TutorialStage.EndVillageTurn:
            case TutorialStage.EndCurTurn:
            case TutorialStage.EndCurse:
            case TutorialStage.ConfirmVotes: return action == TutorialAction.EndTurn;
            case TutorialStage.PlaceCur: return action == TutorialAction.Place && source == 1 && seat == 2;
            case TutorialStage.Shoot: return action == TutorialAction.Skill && source == 9 && target == 1;
            case TutorialStage.OpenCemetery:
            case TutorialStage.CloseCemetery: return action == TutorialAction.Cemetery;
            case TutorialStage.Curse: return action == TutorialAction.Curse && source == 11 && seat == 1;
            case TutorialStage.VoteVillager: return action == TutorialAction.Vote && source == 10 && seat == 3;
            case TutorialStage.VoteHunter: return action == TutorialAction.Vote && source == 9 && seat == 3;
            case TutorialStage.VoteHiddenCur: return action == TutorialAction.Vote && source == 11 && seat == 3;
            case TutorialStage.Purge: return action == TutorialAction.Purge;
            default: return false;
        }
    }
    public static bool ValidSequence(int[] stages)
    {
        if (stages == null || stages.Length != StageCount) return false;
        for (int i = 0; i < stages.Length; i++) if (stages[i] != i) return false;
        return true;
    }
}

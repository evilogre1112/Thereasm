using System;

public static class TutorialRulesTests
{
    private static int assertions;
    private static void Expect(bool condition, string message)
    { assertions++; if (!condition) throw new Exception(message); }

    public static string Run()
    {
        assertions = 0;
        int[] sequence = new int[TutorialRules.StageCount];
        for (int i = 0; i < sequence.Length; i++) sequence[i] = i;
        Expect(TutorialRules.ValidSequence(sequence), "Authored tutorial order rejected.");
        Expect(!TutorialRules.ValidSequence(null), "Missing lesson must not run.");
        Expect(!TutorialRules.ValidSequence(new[] { 0, 1 }), "Incomplete lesson must not run.");
        sequence[3] = 2;
        Expect(!TutorialRules.ValidSequence(sequence), "A duplicated/misordered step could deadlock the lesson.");
        foreach (TutorialStage stage in Enum.GetValues(typeof(TutorialStage)))
        {
            bool manual = stage == TutorialStage.Welcome || stage == TutorialStage.RitualIntro || stage == TutorialStage.Reveal ||
                stage == TutorialStage.OpponentExample || stage == TutorialStage.Complete ||
                stage == TutorialStage.HeartIntro || stage == TutorialStage.BudgetOpponentIntro ||
                stage == TutorialStage.BudgetRefill || stage == TutorialStage.BudgetSwapIntro ||
                stage == TutorialStage.BudgetReference || stage == TutorialStage.BudgetSwapReference || stage == TutorialStage.BudgetSkills;
            Expect(TutorialRules.Allows(stage, TutorialAction.Continue) == manual, "A real exercise can be skipped by Continue.");
        }
        foreach (var sample in new[] {
            new[] { (int)TutorialStage.PlaceVillager, 10, 0 },
            new[] { (int)TutorialStage.PlaceHunter, 9, 1 },
            new[] { (int)TutorialStage.PlaceCur, 1, 2 },
            new[] { (int)TutorialStage.BudgetPlace, 12, 0 }, new[] { (int)TutorialStage.BudgetMove, 12, 1 },
            new[] { (int)TutorialStage.BudgetOpponentPlace, 3, 0 }, new[] { (int)TutorialStage.BudgetOpponentMove, 3, 1 } })
        {
            for (int card = 1; card <= 16; card++)
            for (int seat = -1; seat < 7; seat++)
                Expect(TutorialRules.Allows((TutorialStage)sample[0], TutorialAction.Place, card, seat: seat) ==
                    (card == sample[1] && seat == sample[2]), "Wrong card/seat was authorized during a placement exercise.");
        }
        foreach (var sample in new[] {
            new[] { (int)TutorialStage.VoteVillager, 10 }, new[] { (int)TutorialStage.VoteHunter, 9 },
            new[] { (int)TutorialStage.VoteHiddenCur, 11 } })
        {
            for (int card = 1; card <= 16; card++)
            for (int seat = -1; seat < 7; seat++)
                Expect(TutorialRules.Allows((TutorialStage)sample[0], TutorialAction.Vote, card, seat: seat) ==
                    (card == sample[1] && seat == 3), "A wrong vote can advance the tutorial.");
        }
        foreach (TutorialStage stage in Enum.GetValues(typeof(TutorialStage)))
        for (int source = 1; source <= 16; source++)
        for (int target = 0; target <= 16; target++)
            Expect(TutorialRules.Allows(stage, TutorialAction.Skill, source, target) ==
                (stage == TutorialStage.Shoot && source == 9 && target == 1), "Hunter lesson allowed a missing/wrong target or wrong stage.");

        Expect(TutorialRules.Allows(TutorialStage.Curse, TutorialAction.Curse, 11, seat: 1), "Expected curse rejected.");
        Expect(!TutorialRules.Allows(TutorialStage.Curse, TutorialAction.Curse, 1, seat: 1), "Dead Cur may not be used for the exercise.");
        Expect(!TutorialRules.Allows(TutorialStage.Curse, TutorialAction.Curse, 11, seat: 0), "Curse on the wrong target accepted.");
        foreach (TutorialAction action in Enum.GetValues(typeof(TutorialAction)))
            Expect(!TutorialRules.Allows(TutorialStage.Tally, action, 9, 1, 0), "Input interrupted automatic tallying.");
        Expect(!TutorialRules.Allows((TutorialStage)999, TutorialAction.Continue), "Unknown lesson stage accepted.");
        Expect(!TutorialRules.Allows(TutorialStage.Welcome, TutorialAction.Cemetery), "Cemetery overlay must remain closed at tutorial startup.");
        Expect(TutorialRules.Allows(TutorialStage.OpenCemetery, TutorialAction.Cemetery), "Cemetery lesson must still be able to open the overlay.");
        Expect(TutorialRules.Allows(TutorialStage.CloseCemetery, TutorialAction.Cemetery), "Cemetery lesson must be able to close the overlay again.");

        // The real winner rule must not finish this fixed exercise before its final checkpoint.
        Expect(GameRules.EvaluateWinner(2, 3) == GameOutcome.InProgress, "Seven-card setup ends before the first action.");
        Expect(GameRules.EvaluateWinner(1, 3) == GameOutcome.InProgress, "Hunter exercise ends the lesson early.");
        Expect(GameRules.EvaluateWinner(1, 2) == GameOutcome.InProgress, "Purge exercise must leave the final summary reachable.");
        return $"PASS: {assertions} tutorial sequencing, wrong-input and exercise-safety assertions.";
    }
}

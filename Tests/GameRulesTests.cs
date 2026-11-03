using System;

public static class GameRulesTests
{
    private static int assertions;

    private static void Expect(bool condition, string message)
    {
        assertions++;
        if (!condition) throw new Exception(message);
    }

    public static string Run()
    {
        assertions = 0;
        foreach (GameMode mode in Enum.GetValues(typeof(GameMode)))
        {
            foreach (int level in new[] { int.MinValue, -1, 0, 1, 2, 3, 4, 5, 6, int.MaxValue })
            {
                int total = 0;
                foreach (DefaultOwner owner in Enum.GetValues(typeof(DefaultOwner)))
                {
                    int quota = GameRules.RequiredCursForOwner(mode, level, owner);
                    Expect(quota >= 0 && quota <= 2, "A faction quota exceeded its allowed range.");
                    total += quota;
                }
                Expect(total == GameRules.RequiredCurCount(mode, level), "Bot/setup quotas disagree with the total.");
                Expect(mode != GameMode.PvP || total == 2, "PvP difficulty must not change its two hidden roles.");
            }
        }
        // The first match is learnable, and every higher level adds exactly one Cur.
        Expect(GameRules.RequiredCurCount(GameMode.PlayerVsCurBot, 1) == 1, "Beginner level must contain one Cur.");
        for (int level = 2; level <= 5; level++)
            Expect(GameRules.RequiredCurCount(GameMode.PlayerVsCurBot, level) -
                GameRules.RequiredCurCount(GameMode.PlayerVsCurBot, level - 1) == 1, "Difficulty must grow by one Cur.");

        // A cheap-card placement/retrieval loop must never create energy.
        for (int price = 0; price <= 5; price++)
        {
            int energy = 5;
            for (int cycle = 0; cycle < 100; cycle++)
            {
                int before = energy;
                if (energy >= price) energy = Math.Min(5, energy - price + GameRules.RetrievalRefund(price));
                Expect(energy <= before, "Repeated retrieval manufactured energy.");
            }
        }
        Expect(GameRules.RetrievalRefund(1) == 1, "Totem must not refund more than it costs.");
        Expect(GameRules.RetrievalRefund(5) == 2, "Expensive cards still refund at most two hearts.");
        Expect(GameRules.RetrievalRefund(-1) == 0, "Negative cost produced a negative refund.");

        // Outcome boundaries, including simultaneous elimination and neutral Curs.
        Expect(GameRules.EvaluateWinner(0, 0) == GameOutcome.VillagersWin, "Eliminating every Cur takes priority.");
        Expect(GameRules.EvaluateWinner(0, 8) == GameOutcome.VillagersWin, "Last Cur elimination did not end the match.");
        Expect(GameRules.EvaluateWinner(1, 2) == GameOutcome.InProgress, "A lone Cur won too early.");
        Expect(GameRules.EvaluateWinner(2, 2) == GameOutcome.CursWin, "Cur parity must win.");
        Expect(GameRules.EvaluateWinner(1, 0) == GameOutcome.CursWin, "No villagers remain.");
        Expect(GameRules.EvaluateWinner(3, 2) == GameOutcome.CursWin, "Neutral Curs must count toward Cur strength.");
        return $"PASS: {assertions} gameplay rule assertions.";
    }
}

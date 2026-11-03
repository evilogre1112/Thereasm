using System;

public enum GameOutcome { InProgress, VillagersWin, CursWin }

// Shared by setup, the bots and the rulebook. No scene or UI dependencies.
public static class GameRules
{
    public static int ClampLevel(int level) => Math.Max(1, Math.Min(5, level));

    public static int RequiredCurCount(GameMode mode, int level) =>
        mode == GameMode.PvP ? 2 : ClampLevel(level);

    public static int RequiredCursForOwner(GameMode mode, int level, DefaultOwner owner)
    {
        if (mode == GameMode.PvP) return owner == DefaultOwner.Neutral ? 0 : 1;
        level = ClampLevel(level);
        if (owner == DefaultOwner.Player1) return level >= 4 ? 2 : 1;
        if (owner == DefaultOwner.Player2) return level >= 5 ? 2 : level >= 2 ? 1 : 0;
        return level >= 3 ? 1 : 0;
    }

    public static int RetrievalRefund(int placementCost) => Math.Max(0, Math.Min(2, placementCost));

    public static GameOutcome EvaluateWinner(int livingCurs, int livingVillagers)
    {
        if (livingCurs <= 0) return GameOutcome.VillagersWin;
        return livingCurs >= livingVillagers ? GameOutcome.CursWin : GameOutcome.InProgress;
    }
}

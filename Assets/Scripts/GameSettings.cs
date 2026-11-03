public enum GameMode
{
    PvP,             // Two-human rules used by online matches and the guided tutorial.
    PlayerVsVillBot, // Người chơi Cursermancer, bot Village.
    PlayerVsCurBot   // Người chơi Village, bot Cursermancer.
}

public static class GameSettings
{
    public static GameMode currentMode = GameMode.PlayerVsCurBot;
    public static int currentLevel = 1;
}

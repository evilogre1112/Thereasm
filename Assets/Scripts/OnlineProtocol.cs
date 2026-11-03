using System;

public enum OnlineAction { Place, Return, Skill, Vote, EndTurn, Check, SelectCur, StartGame, Discover, Curse, Draft }

[Serializable]
public class OnlineCommand
{
    public string match;
    public int sequence;
    public OnlineAction action;
    public int phase;
    public int voting;
    public int source;
    public int target;
    public int seat = -1;
    public int option = -1;
    public int draftStep = -1;
    public int seatId;
}

public static class OnlineRules
{
    public static bool TryRoomCode(string input, out string code)
    {
        code = (input ?? "").Trim().ToUpperInvariant();
        if (code.Length < 4 || code.Length > 20) return false;
        foreach (char c in code) if (!(c >= 'A' && c <= 'Z') && !(c >= '0' && c <= '9') && c != '-') return false;
        return true;
    }
    public static DefaultOwner OtherRole(DefaultOwner role) => role == DefaultOwner.Player1 ? DefaultOwner.Player2 : DefaultOwner.Player1;
    public static bool ValidRole(DefaultOwner role) => role == DefaultOwner.Player1 || role == DefaultOwner.Player2;
    public static DefaultOwner DraftRole(int step, int total) => step < 0 || step >= total ? DefaultOwner.Neutral :
        step < 4 ? DefaultOwner.Player1 : DefaultOwner.Player2;
    public static bool CanDraft(DefaultOwner role, int step, int total, bool picking) =>
        ValidRole(role) && !picking && DraftRole(step, total) == role;

    public static bool Allows(OnlineAction action, DefaultOwner role, int phase, DefaultOwner voting,
        bool tallying, bool over)
    {
        if (!ValidRole(role) || tallying || over || action < OnlineAction.Place || action > OnlineAction.Curse) return false;
        if (phase == 0) return role == DefaultOwner.Player1 && (action == OnlineAction.SelectCur || action == OnlineAction.StartGame);
        if (phase == 4)
            return role == voting && (action == OnlineAction.Vote || action == OnlineAction.EndTurn ||
                action == OnlineAction.Skill || action == OnlineAction.Discover);
        if (phase == 3) return role == DefaultOwner.Player1 && (action == OnlineAction.Curse || action == OnlineAction.EndTurn);
        return (phase == 1 && role == DefaultOwner.Player1 || phase == 2 && role == DefaultOwner.Player2) &&
            action != OnlineAction.Vote && action != OnlineAction.SelectCur && action != OnlineAction.StartGame &&
            action != OnlineAction.Discover && action != OnlineAction.Curse;
    }
}

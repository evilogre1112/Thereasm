using System;

public static class OnlineRulesTests
{
    private static int assertions;
    private static void Expect(bool value, string message)
    { assertions++; if (!value) throw new Exception(message); }

    public static string Run()
    {
        assertions = 0;
        for (int step = 0; step < 8; step++)
        {
            var chooser = step < 4 ? DefaultOwner.Player1 : DefaultOwner.Player2;
            Expect(OnlineRules.CanDraft(chooser, step, 8, false), "Correct draft owner was blocked.");
            Expect(!OnlineRules.CanDraft(OnlineRules.OtherRole(chooser), step, 8, false), "Host/other role stole a draft pick.");
            Expect(!OnlineRules.CanDraft(chooser, step, 8, true), "Duplicate pick accepted during animation.");
        }
        foreach (int step in new[] { -1, 8, 9 })
            Expect(!OnlineRules.CanDraft(DefaultOwner.Player1, step, 8, false) && !OnlineRules.CanDraft(DefaultOwner.Player2, step, 8, false), "Out-of-range draft accepted.");
        foreach (string invalid in new[] { null, "", "   ", "abc", "ROOM CODE", "LÀNG", "ROOM_01", "A/B12", new string('A', 21) })
            Expect(!OnlineRules.TryRoomCode(invalid, out _), "Invalid room code accepted: " + invalid);
        Expect(OnlineRules.TryRoomCode("  lang-01  ", out var code) && code == "LANG-01", "Both peers must normalize the same room code.");
        Expect(OnlineRules.TryRoomCode(new string('A', 20), out _), "Maximum valid room code rejected.");
        foreach (var role in new[] { DefaultOwner.Player1, DefaultOwner.Player2 })
        {
            var other = OnlineRules.OtherRole(role);
            Expect(other != role && OnlineRules.ValidRole(other), "Choosing a side must give the other player the opposite side.");
            Expect(OnlineRules.OtherRole(other) == role, "Swapping twice must restore both roles.");
        }
        foreach (int phase in new[] { -1, 0, 1, 2, 3, 4, 5 })
        foreach (DefaultOwner role in Enum.GetValues(typeof(DefaultOwner)))
        foreach (var voting in new[] { DefaultOwner.Player1, DefaultOwner.Player2 })
        foreach (OnlineAction action in Enum.GetValues(typeof(OnlineAction)))
        {
            bool expected = false;
            if (OnlineRules.ValidRole(role))
            {
                if (phase == 0) expected = role == DefaultOwner.Player1 && (action == OnlineAction.SelectCur || action == OnlineAction.StartGame);
                if (phase == 1 || phase == 2)
                {
                    bool ownTurn = phase == 1 ? role == DefaultOwner.Player1 : role == DefaultOwner.Player2;
                    expected = ownTurn && (action == OnlineAction.Place || action == OnlineAction.Return ||
                        action == OnlineAction.Skill || action == OnlineAction.Check || action == OnlineAction.EndTurn);
                }
                if (phase == 3) expected = role == DefaultOwner.Player1 && (action == OnlineAction.Curse || action == OnlineAction.EndTurn);
                if (phase == 4) expected = role == voting && (action == OnlineAction.Vote || action == OnlineAction.Skill ||
                    action == OnlineAction.Discover || action == OnlineAction.EndTurn);
            }
            Expect(OnlineRules.Allows(action, role, phase, voting, false, false) == expected,
                $"Wrong command authorization: {action}, {role}, phase {phase}, voter {voting}.");
            Expect(!OnlineRules.Allows(action, role, phase, voting, true, false), "A command interrupted vote resolution.");
            Expect(!OnlineRules.Allows(action, role, phase, voting, false, true), "A command changed a completed match.");
        }
        Expect(!OnlineRules.Allows((OnlineAction)999, DefaultOwner.Player1, 1, DefaultOwner.Player1, false, false), "Unknown network action accepted.");
        Expect(!OnlineRules.Allows((OnlineAction)(-1), DefaultOwner.Player2, 2, DefaultOwner.Player2, false, false), "Negative network action accepted.");
        return $"PASS: {assertions} room-code, role-swap and online turn-authorization assertions.";
    }
}

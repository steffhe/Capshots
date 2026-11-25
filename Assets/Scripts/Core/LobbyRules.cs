// A created room's lobby: the match waits in warmup until every person in it is ready, or the host
// starts it anyway. Bots never hold it up. The match (Manager.Lobby.cs) counts the players; the
// rules are here so the EditMode tests can check them.
public static class LobbyRules
{
    // Whether the countdown to the match may run: the host started it, or there's someone in the
    // room and all of them are ready
    public static bool CanStart(int people, int ready, bool forced)
    {
        if (forced) return true;
        return people > 0 && ready >= people;
    }

    // The line under the lobby's player list. counting: the countdown to the match is running,
    // which with someone not ready means the host started it.
    public static string Status(int people, int ready, bool counting)
    {
        int waiting = people - ready;
        if (counting) return waiting > 0 ? "THE HOST STARTED THE MATCH" : "EVERYONE'S READY";
        if (people <= 0) return "WAITING FOR PLAYERS";
        if (waiting <= 0) return "EVERYONE'S READY";
        return waiting == 1 ? "WAITING FOR 1 PLAYER TO READY UP" : $"WAITING FOR {waiting} PLAYERS TO READY UP";
    }
}

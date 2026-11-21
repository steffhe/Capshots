// Types shared by the match code.

using Fusion;

// One player's entry on the scoreboard: a local copy of the host's networked PlayerStats,
// rebuilt whenever the host changes them.
public class PlayerInfo
{
    public ProfileData profile;
    // PlayerRef.PlayerId of the player this entry belongs to
    public int actor;
    public short kills;
    public short deaths;
    public byte team;
    // Round trip time in milliseconds, measured by the host. -1 if not known yet.
    public int ping = -1;

    public PlayerInfo (ProfileData p, int a, short k, short d)
    {
        this.profile = p;
        this.actor = a;
        this.kills = k;
        this.deaths = d;
    }
}

// What the host keeps for each player in the match. Fusion syncs it to everyone (Manager.Stats).
public struct PlayerStats : INetworkStruct
{
    public NetworkString<_32> Username;
    public int Level;
    public short Kills;
    public short Deaths;
    public byte Team;
    public short Ping;
}

// The match's game mode. (Not "GameMode", which is Fusion's host/client mode.)
public enum MatchMode
{
    FreeForAll = 0,
    TeamDeathmatch = 1
}

public enum GameState
{
    Waiting = 0,
    Starting = 1,
    Playing = 2,
    Ending = 3
}

// Physics layers, matching Project Settings > Tags and Layers
public static class Layers
{
    public const int LocalPlayer = 6;
    // Other players' guns (and, before LocalGun existed, every gun). Bullets don't hit it.
    public const int Gun = 7;
    // Other players' bodies
    public const int RemotePlayer = 8;
    // The local player's own gun, drawn by the viewmodel camera on top of everything so it
    // never clips into walls. Bullets don't hit it either.
    public const int LocalGun = 9;
    // Fusion hitboxes (body and head) that shots are checked against with lag compensation.
    // Nothing else lives on this layer, so a shot's raycast can include it without hitting colliders.
    public const int Hitbox = 10;
}

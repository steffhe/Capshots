// Types shared by the match code.

public class PlayerInfo
{
    public ProfileData profile;
    public int actor;
    public short kills;
    public short deaths;
    public byte team;

    public PlayerInfo (ProfileData p, int a, short k, short d)
    {
        this.profile = p;
        this.actor = a;
        this.kills = k;
        this.deaths = d;
    }
}


public enum GameMode
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

// Stats changed with Manager.ChangeStat_S. Sent over the network as a byte.
public enum PlayerStat : byte
{
    Kills = 0,
    Deaths = 1,
    Level = 2
}

// Physics layers, matching Project Settings > Tags and Layers
public static class Layers
{
    public const int LocalPlayer = 6;
    // Other players' guns (and, before LocalGun existed, every gun). Bullets don't hit it.
    public const int Gun = 7;
    // Other players' colliders, which bullets can hit
    public const int RemotePlayer = 8;
    // The local player's own gun, drawn by the viewmodel camera on top of everything so it
    // never clips into walls. Bullets don't hit it either.
    public const int LocalGun = 9;
}

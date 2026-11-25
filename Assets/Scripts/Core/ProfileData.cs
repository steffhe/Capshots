// A player's saved profile: name, level, XP and mouse sensitivity. Saved by Data.SaveProfile.
[System.Serializable]
public class ProfileData
{
    public string username;
    public int level;
    public int xp;
    public float xsens;
    public float ysens;
    // The hat picked to wear (see Hats). Worn only while unlocked at this level, otherwise the cap.
    public int hat;
    // The face picked for the drone's screen (see Faces). Shown only while unlocked, otherwise the classic.
    public int face;

    public ProfileData()
    {
        this.username = "DEFAULT USERNAME";
        this.level = 0;
        this.xp = 0;
        this.xsens = 150f;
        this.ysens = 150f;
    }

    // XP needed to go from this level to the next one
    public static int XpToNextLevel(int level)
    {
        return 500 + 250 * level;
    }

    // Adds XP and levels up as many times as the XP allows. Returns the number of levels gained.
    public int AddXp(int amount)
    {
        int levelsGained = 0;
        xp += amount;
        while (xp >= XpToNextLevel(level))
        {
            xp -= XpToNextLevel(level);
            level++;
            levelsGained++;
        }
        return levelsGained;
    }

    public ProfileData (string u, int l, int x, float xs, float ys)
    {
        this.username = u;
        this.level = l;
        this.xp = x;
        this.xsens = xs;
        this.ysens = ys;
    }

}

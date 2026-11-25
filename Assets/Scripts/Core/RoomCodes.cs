// Room codes: every match someone hosts gets a short code like KXQT, which is also its Photon session
// name, so friends can join it by typing the code (Launcher, Join with code). Kept here so the
// EditMode tests can check the rules.
public static class RoomCodes
{
    public const int Length = 4;
    // No I, O or similar-looking pairs, so a code read out loud or off a phone is easy to type
    public const string Letters = "ABCDEFGHJKLMNPQRSTUVWXYZ";

    // A new random code. next: a random number from 0 (inclusive) to the given maximum (exclusive).
    public static string New(System.Func<int, int> next)
    {
        char[] code = new char[Length];
        for (int i = 0; i < Length; i++) code[i] = Letters[next(Letters.Length)];
        return new string(code);
    }

    // What a player typed, as a code: upper case, without spaces or dashes
    public static string Normalize(string typed)
    {
        if (string.IsNullOrEmpty(typed)) return "";
        System.Text.StringBuilder s = new System.Text.StringBuilder();
        foreach (char c in typed.ToUpperInvariant())
        {
            if (c == ' ' || c == '-' || c == '_') continue;
            s.Append(c);
        }
        return s.ToString();
    }

    public static bool IsValid(string code)
    {
        if (code == null || code.Length != Length) return false;
        foreach (char c in code) if (Letters.IndexOf(c) < 0) return false;
        return true;
    }
}

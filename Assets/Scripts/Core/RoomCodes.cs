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

    // The room code in an invite link's query (?code=KXQT, any case), or null if it has none or an
    // invalid one
    public static string FromUrl(string url)
    {
        if (string.IsNullOrEmpty(url)) return null;
        int query = url.IndexOf('?');
        if (query < 0) return null;
        int end = url.IndexOf('#', query);
        string q = end < 0 ? url.Substring(query + 1) : url.Substring(query + 1, end - query - 1);
        foreach (string pair in q.Split('&'))
        {
            int eq = pair.IndexOf('=');
            if (eq <= 0) continue;
            if (pair.Substring(0, eq).ToLowerInvariant() != "code") continue;
            string code = Normalize(System.Uri.UnescapeDataString(pair.Substring(eq + 1)));
            return IsValid(code) ? code : null;
        }
        return null;
    }

    // A link that joins this code's match: the page's address without its query or fragment, then ?code=
    public static string InviteLink(string pageUrl, string code)
    {
        string page = pageUrl ?? "";
        int cut = page.IndexOfAny(new[] { '?', '#' });
        if (cut >= 0) page = page.Substring(0, cut);
        return page + "?code=" + code;
    }
}

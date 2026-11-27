using System.Text;

// Player-typed text shown to other players: names and match titles. Rich text tags in them would
// let one player restyle everyone's kill feed, scoreboard and room list (huge text, colours), so the
// host cleans every name it's sent (Manager.RPC_Register), the menu cleans what's typed (Launcher), and
// anything shown from another machine can be escaped too. Kept here so the EditMode tests can check it.
public static class TextRules
{
    public const int MaxNameLength = 20;
    public const int MaxTitleLength = 32;

    // A name as everyone should see it: no tag brackets or control characters, runs of spaces made
    // one, trimmed, and cut to maxLength. Empty if nothing is left (the caller picks a default).
    public static string CleanName(string text, int maxLength = MaxNameLength)
    {
        if (string.IsNullOrEmpty(text)) return "";
        StringBuilder s = new StringBuilder(text.Length);
        bool space = false;
        foreach (char c in text)
        {
            if (c == '<' || c == '>') continue;
            // Whitespace (tabs and line breaks included) becomes a single space between words
            if (char.IsWhiteSpace(c))
            {
                if (s.Length > 0) space = true;
                continue;
            }
            if (char.IsControl(c)) continue;
            if (space) s.Append(' ');
            space = false;
            s.Append(c);
            if (s.Length >= maxLength) break;
        }
        return s.ToString();
    }

    // Whether a character may be typed into a name or title field
    public static bool AllowedInName(char c) => c != '<' && c != '>' && !char.IsControl(c);

    // Shows text exactly as written in a rich text label: a zero-width space after every '<' stops
    // it being read as a tag
    public static string Escape(string text)
    {
        if (string.IsNullOrEmpty(text)) return "";
        return text.Replace("<", "<​");
    }
}

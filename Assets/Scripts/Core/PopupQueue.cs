using System.Collections.Generic;

// The order of the messages in the one popup under the callouts (Manager.ShowMessage), so one never
// silently wipes out another that matters more:
//  - Urgent (CONNECTION LOST, YOU'RE THE HOST NOW) shows at once and clears everything waiting.
//  - Info (YOU ARE ON BLUE TEAM, YOU'RE STUCK!, FIGHT!) waits its turn, but cuts an XP message short.
//  - Xp (+100 XP KILL) replaces an XP message showing or waiting (the newest is the one that counts),
//    and waits behind info.
// The same text as the one showing or one waiting is dropped. Pure logic, so the EditMode tests can
// check it; the Manager does the showing and the timing.
public sealed class PopupQueue
{
    public enum Kind { Xp, Info, Urgent }

    public struct Popup
    {
        public string text;
        public bool icon;
        public Kind kind;
    }

    public const int MaxWaiting = 3;

    private readonly List<Popup> waiting = new List<Popup>();
    private bool showing;
    private Popup current;

    public bool Showing => showing;
    public Popup Current => current;
    public int Waiting => waiting.Count;

    // A new message. True if it should show now (it's the new Current); false if it waits or was dropped.
    public bool Push(Popup popup)
    {
        if (showing && current.text == popup.text) return false;
        foreach (Popup w in waiting) if (w.text == popup.text) return false;

        if (popup.kind == Kind.Urgent)
        {
            waiting.Clear();
            return Show(popup);
        }
        if (!showing) return Show(popup);

        if (popup.kind == Kind.Xp)
        {
            if (current.kind == Kind.Xp) return Show(popup);
            // Behind the info showing: only the newest XP message waits
            waiting.RemoveAll(w => w.kind == Kind.Xp);
            Add(popup);
            return false;
        }

        // Info: cuts an XP message short (it's shown again after, if nothing newer came)
        if (current.kind == Kind.Xp)
        {
            waiting.RemoveAll(w => w.kind == Kind.Xp);
            waiting.Insert(0, current);
            return Show(popup);
        }
        // Behind other info, ahead of any XP waiting
        int at = waiting.FindIndex(w => w.kind == Kind.Xp);
        if (at < 0) Add(popup);
        else waiting.Insert(at, popup);
        Trim();
        return false;
    }

    // The message showing has finished. True with the next one to show, if any.
    public bool Next(out Popup next)
    {
        showing = false;
        if (waiting.Count == 0)
        {
            next = default;
            return false;
        }
        next = waiting[0];
        waiting.RemoveAt(0);
        Show(next);
        return true;
    }

    public void Clear()
    {
        waiting.Clear();
        showing = false;
    }

    private bool Show(Popup popup)
    {
        current = popup;
        showing = true;
        return true;
    }

    private void Add(Popup popup)
    {
        waiting.Add(popup);
        Trim();
    }

    // Too many waiting: the oldest goes
    private void Trim()
    {
        while (waiting.Count > MaxWaiting) waiting.RemoveAt(0);
    }
}

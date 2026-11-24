using Fusion;
using UnityEngine;

// Breakable glass (the Office map, see OfficeGlass): the host counts the hits on each pane and
// every player shows the panes as the host has them. Two bits per pane, 16 panes to a word:
// 0 intact, 1 cracked, 2 broken.
public partial class Manager
{
    private const int PanesPerWord = 16;
    private const int GlassWords = OfficeMapData.MaxPanes / PanesPerWord;

    [Networked, Capacity(GlassWords)] private NetworkArray<int> GlassPanes => default;

    // The words as this player last showed them
    private readonly int[] glassShown = new int[GlassWords];

    // Host only: a shot went through this pane. It cracks, or shatters if it was cracked.
    public void HitGlass(int pane)
    {
        if (!HasStateAuthority || pane < 0 || pane >= OfficeMapData.MaxPanes) return;
        int word = pane / PanesPerWord, shift = pane % PanesPerWord * 2;
        int value = GlassPanes[word];
        int state = (value >> shift) & 3;
        if (state >= OfficeGlass.Broken) return;
        value = (value & ~(3 << shift)) | ((state + 1) << shift);
        GlassPanes.Set(word, value);
        // Shot out: bots can walk (and path) through the gap now, like people
        if (state + 1 == OfficeGlass.Broken && OfficeGlass.Instance != null)
        {
            BotNavigation.RemoveObstacle(OfficeGlass.Instance.ColliderOf(pane));
        }
    }

    // Shows any panes that changed. Without effects (when joining), they just appear as they are.
    private void SyncGlass(bool effects)
    {
        OfficeGlass glass = OfficeGlass.Instance;
        if (glass == null) return;
        for (int word = 0; word < GlassWords; word++)
        {
            int value = GlassPanes[word];
            int shown = glassShown[word];
            if (value == shown) continue;
            for (int i = 0; i < PanesPerWord; i++)
            {
                int shift = i * 2;
                int state = (value >> shift) & 3;
                if (state != ((shown >> shift) & 3)) glass.SetState(word * PanesPerWord + i, state, effects);
            }
            glassShown[word] = value;
        }
    }
}

using System.Collections.Generic;
using Fusion;
using UnityEngine;
using UnityEngine.UI;

// End-of-match awards (MatchAwards): CAPSHOT KING, ON A ROLL, STICKY FINGERS, UNTOUCHABLE and
// PUNCHING BAG. The host counts cap shots, grenade kills and best streaks while the match is played
// (kills and deaths are on the scoreboard already), picks the awards when it ends and sends them to
// everyone. They show as a row of cards along the top of the results screen.
public partial class Manager
{
    // Host only: what each player did this match, beyond kills and deaths
    private struct AwardCount
    {
        public int capshots, grenadeKills, bestStreak;
    }
    private readonly Dictionary<PlayerRef, AwardCount> awardCounts = new Dictionary<PlayerRef, AwardCount>();

    // Everyone: the awards the host sent, and whether the results screen is up to show them
    private List<MatchAwards.Award> awards;
    private bool resultsShown;
    private RectTransform awardsRow;

    // Host only: a kill while the match is being played
    private void CountForAwards(PlayerRef killer, PlayerRef victim, string weaponName, bool headshot, int streak)
    {
        if (!killer.IsRealPlayer || killer == victim) return;
        awardCounts.TryGetValue(killer, out AwardCount count);
        if (headshot) count.capshots++;
        if (weaponName == PlasmaWeaponName) count.grenadeKills++;
        count.bestStreak = Mathf.Max(count.bestStreak, streak);
        awardCounts[killer] = count;
    }

    // Host only: the match ended. Picks the awards and sends them to everyone.
    private void SendAwards()
    {
        List<MatchAwards.Tally> tallies = new List<MatchAwards.Tally>();
        foreach (KeyValuePair<PlayerRef, PlayerStats> entry in Stats)
        {
            awardCounts.TryGetValue(entry.Key, out AwardCount count);
            tallies.Add(new MatchAwards.Tally
            {
                actor = entry.Key.PlayerId,
                name = entry.Value.Username.ToString(),
                kills = entry.Value.Kills,
                deaths = entry.Value.Deaths,
                capshots = count.capshots,
                grenadeKills = count.grenadeKills,
                bestStreak = count.bestStreak,
            });
        }
        // The same order on every machine, so ties go the same way
        tallies.Sort((a, b) => a.actor.CompareTo(b.actor));
        List<MatchAwards.Award> chosen = MatchAwards.Choose(tallies);
        if (chosen.Count > 0) RPC_Awards(MatchAwards.Pack(chosen));
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_Awards(string packed)
    {
        awards = MatchAwards.Unpack(packed);
        if (resultsShown) BuildAwards();
    }

    // A row of cards along the top of the results screen: the award, who won it and why
    private void BuildAwards()
    {
        if (awards == null || awards.Count == 0 || endgame == null || awardsRow != null) return;

        const float cardWidth = 250f, cardHeight = 74f, gap = 14f;
        Transform design = endgame.Find("Design");
        awardsRow = new GameObject("Awards", typeof(RectTransform)).GetComponent<RectTransform>();
        awardsRow.SetParent(design != null ? design : endgame, false);
        float width = awards.Count * cardWidth + (awards.Count - 1) * gap;
        UITheme.Place(awardsRow, new Vector2(0.5f, 0.5f), new Vector2(0f, 322f), new Vector2(width, cardHeight));

        int me = LocalActor;
        for (int i = 0; i < awards.Count; i++)
        {
            MatchAwards.Award award = awards[i];
            bool mine = award.actor == me;

            Image card = UITheme.NewImage("Award", awardsRow, mine ? new Color(0.45f, 0.36f, 0.08f, 0.85f) : new Color(0f, 0f, 0f, 0.55f), 10f);
            RectTransform rect = card.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 0.5f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.sizeDelta = new Vector2(cardWidth, cardHeight);
            rect.anchoredPosition = new Vector2(i * (cardWidth + gap), 0f);

            Text title = UITheme.NewText("Title", rect, 15, new Color(1f, 0.8f, 0.3f), TextAnchor.MiddleCenter);
            UITheme.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -16f), new Vector2(cardWidth - 12f, 20f));
            title.text = award.title;

            Text name = UITheme.NewText("Name", rect, 22, Color.white, TextAnchor.MiddleCenter);
            UITheme.Place(name.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 0f), new Vector2(cardWidth - 12f, 26f));
            name.text = mine ? "YOU" : award.name.ToUpperInvariant();

            Text detail = UITheme.NewText("Detail", rect, 13, UITheme.MutedText, TextAnchor.MiddleCenter);
            UITheme.Place(detail.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 14f), new Vector2(cardWidth - 12f, 18f));
            detail.text = award.detail;

            // Pop in one after another, after the scoreboard rows
            CanvasGroup group = card.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            StartCoroutine(RevealAward(group, 0.8f + i * 0.15f));
        }
    }

    private System.Collections.IEnumerator RevealAward(CanvasGroup group, float delay)
    {
        yield return new WaitForSecondsRealtime(delay);
        if (group != null) yield return FadeGroup(group, 0.3f);
    }
}

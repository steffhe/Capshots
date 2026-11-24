using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

// How a match opens and closes for the local player:
// - Intro: the loading screen fades away and the map's name and rules slide in.
// - Ending: everyone freezes where they stand, the world drains of colour and a big VICTORY /
//   DEFEAT banner shows. Then the screen fades to the results (title, scoreboard, the XP earned
//   filling the level bar, a countdown) and finally back out to the menu.
public partial class Manager
{
    // How long everyone stays frozen on the result banner before the cut to the results
    private const float BannerTime = 3.2f;
    private const float CutFadeTime = 0.35f;
    // The host removes the bodies just after everyone has cut away from the frozen match
    private const float DespawnDelay = BannerTime + CutFadeTime + 0.3f;
    private const float ResultsTime = 10f;

    // XP earned this match and the reason for the last award (the result), for the results screen
    private int xpGained;
    private string xpReason;
    private int xpStartLevel;
    private int xpStartXp;

    private Volume endVolume;

    // Where the level bar starts on the results screen: the profile as the match began
    private void RememberStartingXp()
    {
        xpStartLevel = Launcher.myProfile.level;
        xpStartXp = Launcher.myProfile.xp;
        xpGained = 0;
    }

    #region Intro

    private void PlayIntro()
    {
        LoadingScreen.Hide(0.3f);
        if (state == GameState.Ending) return;
        StartCoroutine(IntroBanner());
    }

    private IEnumerator IntroBanner()
    {
        yield return new WaitForSecondsRealtime(0.8f);

        RectTransform root = NewLayer("Map Intro", new Vector2(0.5f, 0.24f), new Vector2(1200f, 140f));
        CanvasGroup group = root.gameObject.AddComponent<CanvasGroup>();

        Text kicker = UITheme.NewText("Kicker", root, 18, UITheme.MutedText, TextAnchor.MiddleCenter);
        UITheme.Row(kicker.rectTransform, 0f, 24f);
        kicker.text = "NOW PLAYING";

        Image line = UITheme.NewImage("Line", root, UITheme.AccentText, 1f);
        UITheme.Place(line.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -30f), new Vector2(48f, 2f));

        Text map = UITheme.NewText("Map", root, 56, UITheme.TextColor, TextAnchor.MiddleCenter);
        UITheme.Row(map.rectTransform, 36f, 66f);
        map.text = CurrentMapName().ToUpperInvariant();
        UITheme.AddShadow(map);

        Text rules = UITheme.NewText("Rules", root, 20, UITheme.AccentText, TextAnchor.MiddleCenter);
        UITheme.Row(rules.rectTransform, 104f, 28f);
        string goal;
        if (HasKillLimit) goal = IsTeamMode ? $"FIRST TEAM TO {killcount} KILLS" : $"FIRST TO {killcount} KILLS";
        else goal = $"MOST KILLS IN {Mathf.Max(1, matchlength / 60)} MIN";
        if (HasKillLimit && HasTimeLimit) goal += $"   ·   {Mathf.Max(1, matchlength / 60)} MIN";
        rules.text = $"{LoadingScreen.ModeName((int)mode)}   ·   {goal}";
        UITheme.AddShadow(rules);

        // Rise and fade in, hold, fade out
        Vector2 home = root.anchoredPosition;
        for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / 0.5f)
        {
            float e = Mathf.SmoothStep(0f, 1f, t);
            group.alpha = e;
            root.anchoredPosition = home + new Vector2(0f, -24f * (1f - e));
            line.rectTransform.sizeDelta = new Vector2(Mathf.Lerp(0f, 48f, e), 2f);
            yield return null;
        }
        group.alpha = 1f;
        root.anchoredPosition = home;

        yield return new WaitForSecondsRealtime(3.5f);
        for (float t = 0f; t < 1f && state != GameState.Ending; t += Time.unscaledDeltaTime / 0.6f)
        {
            group.alpha = 1f - t;
            yield return null;
        }
        Destroy(root.gameObject);
    }

    #endregion

    #region Ending

    // Whether the match is over, from the networked state: players and guns stop taking input
    public bool Ending => Object != null && Object.IsValid && State == GameState.Ending;

    // Host: removes everyone's body once they've all cut away to the results
    private void DespawnPlayers()
    {
        foreach (Player p in FindObjectsByType<Player>())
        {
            if (p.Object != null && p.Object.IsValid) Runner.Despawn(p.Object);
        }
    }

    private IEnumerator EndSequence(bool timeUp)
    {
        if (xpPopup != null) xpPopup.gameObject.SetActive(false);
        leaderboard.gameObject.SetActive(false);
        GameObject intro = GameObject.Find("Canvas/Map Intro");
        if (intro != null) Destroy(intro);

        string kicker, title, subtitle;
        Color titleColor;
        DescribeResult(timeUp, out kicker, out title, out titleColor, out subtitle);

        // 1. Frozen in place: drain the colour and show the result over the match
        endVolume = CreateEndVolume();
        RectTransform banner = BuildBanner(kicker, title, titleColor, subtitle, out CanvasGroup bannerGroup, out RectTransform band, out Text titleText);
        for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / 0.45f)
        {
            float e = 1f - Mathf.Pow(1f - t, 3f);
            bannerGroup.alpha = Mathf.Clamp01(t * 2f);
            band.localScale = new Vector3(1f, e, 1f);
            titleText.rectTransform.localScale = Vector3.one * Mathf.Lerp(1.35f, 1f, e);
            endVolume.weight = e;
            yield return null;
        }
        bannerGroup.alpha = 1f;
        band.localScale = titleText.rectTransform.localScale = Vector3.one;
        endVolume.weight = 1f;

        yield return new WaitForSecondsRealtime(BannerTime - 0.45f);

        // 2. Fade to black and cut to the map camera
        Image black = UITheme.NewImage("End Fade", GameObject.Find("Canvas").transform, Color.clear, 0f);
        UITheme.Stretch(black.rectTransform);
        black.raycastTarget = true;
        yield return FadeImage(black, 0f, 1f, CutFadeTime);

        Destroy(banner.gameObject);
        endVolume.weight = 0f;
        if (Player.Local != null && Player.Local.cameraParent != null) Player.Local.cameraParent.SetActive(false);
        mapcam.SetActive(true);

        // 3. Results (and the vote for the next map, when there is one)
        bool voting = VotingAvailable;
        float resultsTime = voting ? VoteTime : ResultsTime + (HasStateAuthority ? 1f : 0f);
        Text countdown = ShowResults(title, titleColor, kicker, subtitle);
        black.transform.SetAsLastSibling();
        yield return FadeImage(black, 1f, 0f, 0.5f);
        black.raycastTarget = false;

        float end = Time.unscaledTime + resultsTime;
        while (Time.unscaledTime < end)
        {
            int seconds = Mathf.CeilToInt(end - Time.unscaledTime);
            if (countdown != null) countdown.text = voting ? $"NEXT MAP IN {seconds}" : $"BACK TO THE MENU IN {seconds}";
            yield return null;
        }

        // 4. The next map, loaded by the host for everyone (this coroutine ends with this scene)...
        if (voting)
        {
            if (HasStateAuthority)
            {
                StartNextMap();
                yield break;
            }
            if (countdown != null) countdown.text = "LOADING NEXT MAP";
            // ...unless the host never does
            yield return new WaitForSecondsRealtime(12f);
        }

        // ...or out to the menu (the loading screen fades in over the results)
        NetworkSession.Leave();
    }

    // The banner's words for the local player
    private void DescribeResult(bool timeUp, out string kicker, out string title, out Color color, out string subtitle)
    {
        kicker = timeUp ? "TIME'S UP" : "KILL LIMIT REACHED";
        Color win = UITheme.AccentText;
        Color lose = new Color(1f, 0.42f, 0.38f);

        if (IsTeamMode)
        {
            int myTeam = GetTeam(LocalActor);
            int winner = WinningTeam();
            string score = $"<color={TeamHex(0)}>{TeamNames[0]} {TeamKills(0)}</color>   -   <color={TeamHex(1)}>{TeamKills(1)} {TeamNames[1]}</color>";
            if (winner < 0) { title = "DRAW"; color = UITheme.TextColor; }
            else if (winner == myTeam) { title = "VICTORY"; color = win; }
            else { title = "DEFEAT"; color = lose; }
            subtitle = score;
            return;
        }

        List<PlayerInfo> sorted = SortPlayers(playerInfo);
        PlayerInfo top = sorted.Count > 0 ? sorted[0] : null;
        int myKills = (myind >= 0 && playerInfo.Count > myind) ? playerInfo[myind].kills : 0;
        int place = 1;
        for (int i = 0; i < sorted.Count; i++)
        {
            if (sorted[i].kills > myKills) place++;
        }

        if (top == null || top.kills <= 0)
        {
            title = "MATCH OVER"; color = UITheme.TextColor;
            subtitle = "NOBODY SCORED";
        }
        else if (myKills == top.kills)
        {
            title = "VICTORY"; color = win;
            subtitle = $"TOP OF THE BOARD WITH {myKills} KILLS";
        }
        else
        {
            title = top.profile.username.ToUpperInvariant() + " WINS"; color = UITheme.TextColor;
            subtitle = $"YOU PLACED {Ordinal(place)} WITH {myKills} KILLS";
        }
    }

    private static string Ordinal(int n)
    {
        int mod100 = n % 100;
        string suffix = mod100 >= 11 && mod100 <= 13 ? "TH" : (n % 10) switch { 1 => "ST", 2 => "ND", 3 => "RD", _ => "TH" };
        return n + suffix;
    }

    // A dark band across the middle of the screen with the result on it
    private RectTransform BuildBanner(string kicker, string title, Color color, string subtitle, out CanvasGroup group, out RectTransform band, out Text titleText)
    {
        RectTransform root = NewLayer("Match Result", new Vector2(0.5f, 0.55f), new Vector2(0f, 240f));
        root.anchorMin = new Vector2(0f, 0.55f);
        root.anchorMax = new Vector2(1f, 0.55f);
        root.sizeDelta = new Vector2(0f, 240f);
        group = root.gameObject.AddComponent<CanvasGroup>();
        group.alpha = 0f;

        Image backdrop = UITheme.NewImage("Band", root, new Color(0.01f, 0.015f, 0.03f, 0.72f), 0f);
        band = backdrop.rectTransform;
        UITheme.Stretch(band);
        Image accent = UITheme.NewImage("Accent", band, new Color(color.r, color.g, color.b, 0.8f), 0f);
        UITheme.Row(accent.rectTransform, 0f, 2f);
        Image accentBottom = UITheme.NewImage("Accent", band, new Color(color.r, color.g, color.b, 0.8f), 0f);
        UITheme.Row(accentBottom.rectTransform, 238f, 2f);

        Text kick = UITheme.NewText("Kicker", root, 22, UITheme.MutedText, TextAnchor.MiddleCenter);
        UITheme.Row(kick.rectTransform, 28f, 28f);
        kick.text = kicker;

        titleText = UITheme.NewText("Title", root, 104, color, TextAnchor.MiddleCenter);
        UITheme.Row(titleText.rectTransform, 58f, 120f);
        titleText.text = title;
        UITheme.AddShadow(titleText, 0.8f);

        Text sub = UITheme.NewText("Subtitle", root, 24, UITheme.TextColor, TextAnchor.MiddleCenter);
        UITheme.Row(sub.rectTransform, 178f, 32f);
        sub.text = subtitle;
        return root;
    }

    // Fills in the end screen: title, scoreboard (rows fading in one by one), XP and a countdown.
    // Returns the countdown text.
    private Text ShowResults(string title, Color color, string kicker, string subtitle)
    {
        endgame.gameObject.SetActive(true);
        endgame.SetAsLastSibling();

        Text message = endgame.Find("Design/Message")?.GetComponent<Text>();
        if (message != null)
        {
            message.text = title;
            message.color = color;
            message.fontSize = 72;
            UITheme.Place(message.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 180f), new Vector2(1200f, 90f));
        }

        Transform design = endgame.Find("Design");
        Transform parent = design != null ? design : endgame;
        Text kick = UITheme.NewText("Kicker", parent, 20, UITheme.MutedText, TextAnchor.MiddleCenter);
        UITheme.Place(kick.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 240f), new Vector2(1200f, 26f));
        kick.text = kicker;
        Text sub = UITheme.NewText("Subtitle", parent, 22, UITheme.TextColor, TextAnchor.MiddleCenter);
        UITheme.Place(sub.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 122f), new Vector2(1200f, 30f));
        sub.text = subtitle;

        Transform board = endgame.Find("Leaderboard");
        Leaderboard(board);
        StartCoroutine(RevealRows(board));

        BuildXpPanel(endgame);
        FitResultsBoard(board as RectTransform, endgame.Find("XP") as RectTransform);
        if (VotingAvailable) BuildVotePanel(endgame);

        Text countdown = UITheme.NewText("Countdown", endgame, 18, UITheme.MutedText, TextAnchor.MiddleCenter);
        UITheme.Place(countdown.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 22f), new Vector2(800f, 24f));
        return countdown;
    }

    // The end screen's scoreboard hangs just below the result's subtitle and stops a gap above the XP
    // panel, shrinking to fit when there are many players. (Centred, as in the prefab, it grew both
    // ways and ran into the result title from about four players.)
    private void FitResultsBoard(RectTransform board, RectTransform xpPanel)
    {
        if (board == null || board.childCount < 2) return;
        const float gap = 18f;
        // The subtitle's bottom edge (ShowResults: centred 122 above the middle, 30 tall)
        const float top = 122f - 15f - gap;
        float half = ((RectTransform)endgame).rect.height / 2f;
        float xpTop = xpPanel != null ? xpPanel.anchoredPosition.y + xpPanel.sizeDelta.y : 180f;
        float bottom = -half + xpTop + gap;

        float header = ((RectTransform)board.GetChild(0)).sizeDelta.y;
        float row = ((RectTransform)board.GetChild(1)).sizeDelta.y;
        float needed = header + row * Mathf.Max(1, playerInfo.Count);
        float scale = Mathf.Clamp(Mathf.Min(1f, (top - bottom) / needed), 0.5f, 1f);

        // The layout centres the rows in the board, so a board exactly their height holds them exactly
        board.sizeDelta = new Vector2(board.sizeDelta.x, needed);
        board.localScale = Vector3.one * scale;
        board.anchoredPosition = new Vector2(board.anchoredPosition.x, top - needed * scale / 2f);
    }

    // Scoreboard rows (after the header and the hidden template) slide in one after another
    private IEnumerator RevealRows(Transform board)
    {
        List<CanvasGroup> rows = new List<CanvasGroup>();
        for (int i = 2; i < board.childCount; i++)
        {
            CanvasGroup row = board.GetChild(i).gameObject.AddComponent<CanvasGroup>();
            row.alpha = 0f;
            rows.Add(row);
        }
        yield return new WaitForSecondsRealtime(0.35f);
        foreach (CanvasGroup row in rows)
        {
            StartCoroutine(FadeGroup(row, 0.25f));
            yield return new WaitForSecondsRealtime(0.07f);
        }
    }

    // The player's own match: accuracy, cap shots, best streak and favourite gun form, in one line
    private static string SummaryText()
    {
        MatchSummary s = Summary;
        string muted = UITheme.Hex(UITheme.MutedText);
        string line = $"<color={muted}>ACCURACY</color> {s.AccuracyPercent}%" +
                      $"    <color={muted}>CAPSHOTS</color> {s.capshots}" + (s.kills > 0 ? $" ({s.CapshotPercent}%)" : "") +
                      $"    <color={muted}>BEST STREAK</color> {s.bestStreak}";
        string gun = s.FavouriteGun;
        if (gun != null) line += $"    <color={muted}>FAVOURITE</color> {gun.ToUpperInvariant()}";
        return line;
    }

    // Your match in numbers (SummaryText), then the XP earned this match, with the level bar filling
    // up (and rolling over on a level up), and any hats the new level unlocked
    private void BuildXpPanel(Transform parent)
    {
        string newHats = Manager.NewHatsText(xpStartLevel, Launcher.myProfile.level);
        const float summaryRow = 30f;
        RectTransform panel = NewLayer("XP", new Vector2(0.5f, 0f), new Vector2(620f, (newHats != null ? 124f : 92f) + summaryRow), parent);
        // A row taller for the new hats, grown downwards (towards the countdown) to keep clear of the leaderboard
        panel.anchoredPosition = new Vector2(0f, newHats != null ? 40f : 56f);
        Image bg = UITheme.NewImage("Background", panel, UITheme.Panel, 10f);
        UITheme.Stretch(bg.rectTransform);

        Text summary = UITheme.NewText("Summary", panel, 16, UITheme.TextColor, TextAnchor.MiddleCenter);
        UITheme.Row(summary.rectTransform, 12f, 24f);
        summary.horizontalOverflow = HorizontalWrapMode.Overflow;
        summary.supportRichText = true;
        summary.text = SummaryText();

        Text gained = UITheme.NewText("Gained", panel, 22, UITheme.AccentText, TextAnchor.MiddleLeft);
        UITheme.Row(gained.rectTransform, 14f + summaryRow, 30f, 0f, 0.7f);
        gained.rectTransform.offsetMin = new Vector2(22f, gained.rectTransform.offsetMin.y);
        gained.text = xpGained > 0 ? $"+{xpGained} XP   <color={UITheme.Hex(UITheme.MutedText)}>{xpReason}</color>" : "";

        Text level = UITheme.NewText("Level", panel, 20, UITheme.TextColor, TextAnchor.MiddleRight);
        UITheme.Row(level.rectTransform, 14f + summaryRow, 30f, 0.6f, 1f);
        level.rectTransform.offsetMax = new Vector2(-22f, level.rectTransform.offsetMax.y);

        Image track = UITheme.NewImage("Track", panel, UITheme.Track, 4f);
        UITheme.Row(track.rectTransform, 56f + summaryRow, 8f);
        track.rectTransform.offsetMin = new Vector2(22f, track.rectTransform.offsetMin.y);
        track.rectTransform.offsetMax = new Vector2(-22f, track.rectTransform.offsetMax.y);
        Image fill = UITheme.NewImage("Fill", track.transform, UITheme.AccentText, 4f);
        UITheme.Stretch(fill.rectTransform);

        Text progress = UITheme.NewText("Progress", panel, 14, UITheme.MutedText, TextAnchor.MiddleRight);
        UITheme.Row(progress.rectTransform, 66f + summaryRow, 22f);
        progress.rectTransform.offsetMax = new Vector2(-22f, progress.rectTransform.offsetMax.y);

        Text hats = null;
        if (newHats != null)
        {
            hats = UITheme.NewText("New Hats", panel, 18, UITheme.AccentText, TextAnchor.MiddleLeft);
            UITheme.Row(hats.rectTransform, 92f + summaryRow, 24f);
            hats.rectTransform.offsetMin = new Vector2(22f, hats.rectTransform.offsetMin.y);
            hats.horizontalOverflow = HorizontalWrapMode.Overflow;
            hats.text = newHats;
            // Shown once the bar has filled up to the level that unlocks them
            hats.enabled = false;
        }

        StartCoroutine(FillXp(level, fill.rectTransform, progress, hats));
    }

    private IEnumerator FillXp(Text levelText, RectTransform fill, Text progress, Text newHats)
    {
        int level = xpStartLevel;
        float xp = xpStartXp;
        ProfileData final = Launcher.myProfile;

        void Draw()
        {
            int needed = ProfileData.XpToNextLevel(level);
            bool levelledUp = level > xpStartLevel;
            levelText.text = levelledUp ? $"LEVEL UP!   LEVEL {level}" : "LEVEL " + level;
            levelText.color = levelledUp ? UITheme.AccentText : UITheme.TextColor;
            fill.anchorMax = new Vector2(Mathf.Clamp01(xp / needed), 1f);
            progress.text = $"{Mathf.FloorToInt(xp)} / {needed} XP";
        }

        Draw();
        yield return new WaitForSecondsRealtime(0.9f);

        // About 1.5 seconds for the whole gain, however big, rolling over into new levels
        float rate = Mathf.Max(60f, xpGained / 1.5f);
        float given = 0f;
        while (given < xpGained)
        {
            float step = Mathf.Min(rate * Time.unscaledDeltaTime, xpGained - given);
            given += step;
            xp += step;
            while (level < final.level && xp >= ProfileData.XpToNextLevel(level))
            {
                xp -= ProfileData.XpToNextLevel(level);
                level++;
            }
            Draw();
            yield return null;
        }

        // Settle on the saved profile, whatever rounding did on the way
        level = final.level;
        xp = final.xp;
        Draw();
        if (newHats != null) newHats.enabled = true;
    }

    // Grey and a little darker, for the frozen moment at the end
    private Volume CreateEndVolume()
    {
        GameObject go = new GameObject("End Of Match Look");
        Volume volume = go.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.priority = 100f;
        volume.weight = 0f;
        VolumeProfile profile = ScriptableObject.CreateInstance<VolumeProfile>();
        ColorAdjustments color = profile.Add<ColorAdjustments>(true);
        color.saturation.Override(-75f);
        color.postExposure.Override(-0.35f);
        Vignette vignette = profile.Add<Vignette>(true);
        vignette.intensity.Override(0.45f);
        vignette.smoothness.Override(0.5f);
        volume.sharedProfile = profile;
        return volume;
    }

    private void DestroyEndVolume()
    {
        if (endVolume == null) return;
        if (endVolume.sharedProfile != null) Destroy(endVolume.sharedProfile);
        Destroy(endVolume.gameObject);
    }

    #endregion

    #region Helpers

    // An empty, centred UI rect on the match canvas (or 'parent')
    private static RectTransform NewLayer(string name, Vector2 anchor, Vector2 size, Transform parent = null)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        RectTransform rect = (RectTransform)go.transform;
        rect.SetParent(parent != null ? parent : GameObject.Find("Canvas").transform, false);
        UITheme.Place(rect, anchor, Vector2.zero, size);
        rect.SetAsLastSibling();
        return rect;
    }

    private static IEnumerator FadeImage(Image image, float from, float to, float duration)
    {
        Color c = Color.black;
        for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / duration)
        {
            c.a = Mathf.Lerp(from, to, Mathf.SmoothStep(0f, 1f, t));
            image.color = c;
            yield return null;
        }
        c.a = to;
        image.color = c;
    }

    private static IEnumerator FadeGroup(CanvasGroup group, float duration)
    {
        for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / duration)
        {
            if (group == null) yield break;
            group.alpha = Mathf.SmoothStep(0f, 1f, t);
            yield return null;
        }
        if (group != null) group.alpha = 1f;
    }

    #endregion
}

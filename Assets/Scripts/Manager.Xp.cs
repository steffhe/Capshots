using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Fusion;

// XP and the message popup used for XP, level ups and match messages.
public partial class Manager
{
    [Header("XP")]
    public int xpPerKill = 100;
    public int xpMatchComplete = 150;
    public int xpWin = 300;

    private Text xpPopup;
    private Coroutine xpPopupRoutine;
    private CanvasGroup xpPopupGroup;

    public static string UsernameWithLevel(ProfileData profile)
    {
        return $"{profile.username}  LVL {profile.level}";
    }

    private void AwardMatchXp()
    {
        if (IsTeamMode)
        {
            int myTeam = GetTeam(LocalActor);
            int winner = WinningTeam();
            if (myTeam >= 0 && myTeam == winner) AwardXp(xpMatchComplete + xpWin, "VICTORY");
            else AwardXp(xpMatchComplete, winner < 0 ? "DRAW" : "MATCH COMPLETE");
            return;
        }

        short myKills = (myind >= 0 && playerInfo.Count > myind) ? playerInfo[myind].kills : (short)0;
        short topKills = 0;
        foreach (PlayerInfo a in playerInfo)
        {
            if (a.kills > topKills) topKills = a.kills;
        }

        bool won = topKills > 0 && myKills == topKills;
        if (won) AwardXp(xpMatchComplete + xpWin, "VICTORY");
        else AwardXp(xpMatchComplete, "MATCH COMPLETE");
    }

    private void AwardXp(int amount, string reason)
    {
        ProfileData profile = Launcher.myProfile;
        // Added up for the results screen, which fills the level bar with the whole match's XP
        xpGained += amount;
        xpReason = reason;
        int oldLevel = profile.level;
        int levelsGained = profile.AddXp(amount);
        Data.SaveProfile(profile);

        string message = $"+{amount} XP  {reason}";
        if (levelsGained > 0)
        {
            message += $"\nLEVEL UP!  LVL {profile.level}";
            string hats = NewHatsText(oldLevel, profile.level);
            if (hats != null) message += "\n" + hats;
            // Let everyone update this player's level on the scoreboard
            RPC_LevelUp(levelsGained);
        }

        if (uiUsername != null) uiUsername.text = UsernameWithLevel(profile);
        // At the end of the match the results screen shows the XP instead
        if (!matchEnded) ShowMessage(message);
    }

    // "NEW CAP UNLOCKED: BEANIE" for the hats unlocked between these levels, or null if none were
    public static string NewHatsText(int oldLevel, int newLevel)
    {
        List<int> hats = Hats.UnlockedBetween(oldLevel, newLevel);
        if (hats.Count == 0) return null;
        string[] names = new string[hats.Count];
        for (int i = 0; i < names.Length; i++) names[i] = Hats.Name(hats[i]);
        return (hats.Count == 1 ? "NEW CAP UNLOCKED: " : "NEW CAPS UNLOCKED: ") + string.Join(", ", names);
    }

    // Lets the host update this player's level on everyone's scoreboard
    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_LevelUp(int levels, RpcInfo info = default)
    {
        PlayerRef player = info.Source.IsRealPlayer ? info.Source : Runner.LocalPlayer;
        PlayerStats stats;
        if (levels <= 0 || !Stats.TryGet(player, out stats)) return;
        stats.Level += levels;
        Stats.Set(player, stats);
    }

    private void CreateXpPopup()
    {
        // Built at runtime from the existing Kills text so it matches the HUD font and style
        GameObject popupObject = Instantiate(myKills.gameObject, GameObject.Find("Canvas").transform);
        popupObject.name = "XP Popup";

        xpPopup = popupObject.GetComponent<Text>();
        // Faded with a CanvasGroup so coloured words (like a team name) fade too
        xpPopupGroup = popupObject.AddComponent<CanvasGroup>();
        xpPopupGroup.interactable = false;
        xpPopupGroup.blocksRaycasts = false;
        xpPopup.supportRichText = true;
        xpPopup.fontSize = 34;
        xpPopup.alignment = TextAnchor.MiddleCenter;
        xpPopup.horizontalOverflow = HorizontalWrapMode.Overflow;
        xpPopup.verticalOverflow = VerticalWrapMode.Overflow;

        RectTransform rect = popupObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.7f);
        rect.anchorMax = new Vector2(0.5f, 0.7f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(600f, 100f);

        popupObject.SetActive(false);
    }

    private void ShowMessage(string message)
    {
        if (xpPopup == null) return;
        if (xpPopupRoutine != null) StopCoroutine(xpPopupRoutine);
        xpPopupRoutine = StartCoroutine(XpPopup(message));
    }

    private IEnumerator XpPopup(string message)
    {
        const float holdTime = 1.5f;
        const float fadeTime = 0.5f;

        xpPopup.text = message;
        xpPopup.transform.SetAsLastSibling();
        xpPopup.gameObject.SetActive(true);

        xpPopupGroup.alpha = 1f;

        yield return new WaitForSeconds(holdTime);

        for (float t = 0f; t < fadeTime; t += Time.deltaTime)
        {
            xpPopupGroup.alpha = 1f - t / fadeTime;
            yield return null;
        }

        xpPopup.gameObject.SetActive(false);
        xpPopupRoutine = null;
    }
}

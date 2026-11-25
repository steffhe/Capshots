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
    // On top of xpPerKill for a cap shot (headshot) kill
    public int xpCapshotBonus = 50;
    public int xpMatchComplete = 150;
    public int xpWin = 300;

    private Text xpPopup;
    private Image xpPopupIcon;
    private const float XpPopupIconSize = 56f;
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

    // capshot: the reason is shown in the logo's gold, with the cap logo over the popup
    private void AwardXp(int amount, string reason, bool capshot = false)
    {
        ProfileData profile = Launcher.myProfile;
        // Added up for the results screen, which fills the level bar with the whole match's XP
        xpGained += amount;
        xpReason = reason;
        int oldLevel = profile.level;
        int levelsGained = profile.AddXp(amount);
        Data.SaveProfile(profile);

        string message = capshot ? $"+{amount} XP  <color={FeedbackUI.CapshotColor}>{reason}</color>" : $"+{amount} XP  {reason}";
        if (levelsGained > 0)
        {
            message += $"\nLEVEL UP!  LVL {profile.level}";
            string hats = NewHatsText(oldLevel, profile.level);
            if (hats != null) message += "\n" + hats;
            // Let everyone update this player's level on the scoreboard
            RPC_LevelUp(levelsGained);
            // At the end of the match the fanfare plays as the results screen's level bar rolls over
            if (!matchEnded) FeedbackUI.LevelUp();
        }

        if (uiUsername != null) uiUsername.text = UsernameWithLevel(profile);
        // At the end of the match the results screen shows the XP instead
        if (!matchEnded) ShowMessage(message, capshot);
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

        // Under the kill streak callouts (FeedbackUI), hung from the top of the screen like them: the
        // cap logo shown above a cap shot's message clears the callout by a small gap
        RectTransform rect = popupObject.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(0f, -(FeedbackUI.CalloutBottom + 14f + XpPopupIconSize + 4f + 20f));
        rect.sizeDelta = new Vector2(600f, 100f);

        // The cap logo, over the message on a cap shot. A child, so it fades with the message.
        GameObject icon = new GameObject("Capshot Icon", typeof(RectTransform), typeof(Image));
        RectTransform iconRect = (RectTransform)icon.transform;
        iconRect.SetParent(rect, false);
        iconRect.anchorMin = iconRect.anchorMax = iconRect.pivot = new Vector2(0.5f, 0.5f);
        iconRect.sizeDelta = new Vector2(XpPopupIconSize, XpPopupIconSize);
        xpPopupIcon = icon.GetComponent<Image>();
        xpPopupIcon.preserveAspect = true;
        xpPopupIcon.raycastTarget = false;
        icon.SetActive(false);

        popupObject.SetActive(false);
    }

    private void ShowMessage(string message, bool capshotIcon = false)
    {
        if (xpPopup == null) return;
        if (xpPopupRoutine != null) StopCoroutine(xpPopupRoutine);
        xpPopupRoutine = StartCoroutine(XpPopup(message, capshotIcon));
    }

    private IEnumerator XpPopup(string message, bool capshotIcon)
    {
        const float holdTime = 1.5f;
        const float fadeTime = 0.5f;

        xpPopup.text = message;
        Sprite icon = capshotIcon ? FeedbackUI.CapshotIcon : null;
        if (xpPopupIcon != null)
        {
            xpPopupIcon.gameObject.SetActive(icon != null);
            if (icon != null)
            {
                xpPopupIcon.sprite = icon;
                // Just above the message's first line, however many lines it has
                xpPopupIcon.rectTransform.anchoredPosition = new Vector2(0f, xpPopup.preferredHeight / 2f + XpPopupIconSize / 2f + 4f);
            }
        }
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

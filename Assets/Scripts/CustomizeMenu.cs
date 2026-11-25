using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// The Customize panel: the hat you wear and the gun form you spawn holding. Opened by the Customize
// button under Settings, in the main menu and in the pause menu. Hats unlock as you level up (see
// Hats): every hat is listed, the locked ones greyed out with the level that unlocks them.
// In the main menu the panel sits on the left, where the menu column was, so the player model
// stays in view wearing the hat picked. In a match, a new hat shows on your drone at once.
public class CustomizeMenu : MenuPanel
{
    private Transform anchor;
    private GameObject mainButtons;
    private GameObject panel;
    private bool inMainMenu;

    private Text startingGunText;
    private Text levelText;
    private Text nextUnlockText;
    // The player body's gun, for the forms the starting gun can be
    private Weapon playerWeapon;

    private class HatRow
    {
        public int hat;
        public Button button;
        public Text name;
        public Text status;
    }
    private readonly List<HatRow> hatRows = new List<HatRow>();

    // Panel layout, in canvas units from the panel's centre
    private const float PanelWidth = 640f;
    private const float PanelHeight = 820f;
    private const float ColumnWidth = 560f;
    private const float RowStep = 32f;
    // The hats are listed in two columns, left to right then down, with this gap between them
    private const float HatColumnGap = 8f;
    // Gap between the screen's left edge and the panel, in the main menu
    private const float MainMenuMargin = 80f;

    // For the main menu: the panel goes on canvas, hideWhileOpen (the menu column) is hidden while
    // it's open, and its buttons and labels are copies of the templates given. Open it with Open().
    public static CustomizeMenu CreateForMainMenu(Transform canvas, GameObject hideWhileOpen, GameObject buttonTemplate, Text labelTemplate)
    {
        GameObject holder = new GameObject("Customize Menu", typeof(RectTransform));
        RectTransform rect = (RectTransform)holder.transform;
        rect.SetParent(canvas, false);
        rect.SetAsLastSibling();
        UITheme.Stretch(rect);

        CustomizeMenu menu = holder.AddComponent<CustomizeMenu>();
        menu.inMainMenu = true;
        menu.Setup(rect, hideWhileOpen, buttonTemplate, labelTemplate);
        return menu;
    }

    // For the pause menu (see SettingsMenu): the panel goes on the pause menu's anchor, and its
    // main buttons are hidden while it's open
    public static CustomizeMenu CreateForPause(GameObject host, Transform anchor, GameObject mainButtons, GameObject buttonTemplate, Text labelTemplate)
    {
        CustomizeMenu menu = host.AddComponent<CustomizeMenu>();
        menu.Setup(anchor, mainButtons, buttonTemplate, labelTemplate);
        return menu;
    }

    private void Setup(Transform anchor, GameObject mainButtons, GameObject buttonTemplate, Text labelTemplate)
    {
        this.anchor = anchor;
        this.mainButtons = mainButtons;
        this.buttonTemplate = buttonTemplate;
        this.labelTemplate = labelTemplate;
    }

    public void Open()
    {
        if (panel == null)
        {
            if (buttonTemplate == null || labelTemplate == null || anchor == null) return;
            BuildPanel();
        }
        if (mainButtons != null) mainButtons.SetActive(false);
        panel.SetActive(true);
        Refresh();
    }

    private void Close()
    {
        if (panel != null) panel.SetActive(false);
        if (mainButtons != null) mainButtons.SetActive(true);
    }

    private void Update()
    {
        if (panel == null || !panel.activeSelf) return;
        // Unpausing hides the whole pause menu: go back to the main pause buttons for next time
        if (!anchor.gameObject.activeInHierarchy) { Close(); return; }
        // In the main menu, Esc closes the panel (in a match it unpauses, closing it as above)
        if (inMainMenu && !OptionPicker.Showing)
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame) Close();
        }
    }

    private void BuildPanel()
    {
        panel = new GameObject("Customize", typeof(RectTransform), typeof(Image));
        RectTransform rect = (RectTransform)panel.transform;
        rect.SetParent(anchor, false);
        if (inMainMenu)
        {
            // On the left, so the player model stays in view
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 0.5f);
            rect.anchoredPosition = new Vector2(MainMenuMargin, 0f);
        }
        else
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
        }
        rect.sizeDelta = new Vector2(PanelWidth, PanelHeight);
        Image background = panel.GetComponent<Image>();
        UITheme.Round(background, 14f);
        background.color = UITheme.Panel;

        // Children are placed from the panel's centre, whichever way the panel itself is anchored
        RectTransform content = new GameObject("Content", typeof(RectTransform)).GetComponent<RectTransform>();
        content.SetParent(rect, false);
        UITheme.Stretch(content);

        float top = PanelHeight / 2f;
        NewLabel(content, "CUSTOMIZE", new Vector2(0f, top - 48f), ColumnWidth, 34, TextAnchor.MiddleCenter);

        // Starting gun
        NewLabel(content, "STARTING GUN", new Vector2(0f, top - 106f), ColumnWidth, 16, TextAnchor.MiddleLeft).color = UITheme.AccentText;
        Button startingGunButton = null;
        GameObject startingGun = NewButton(content, "", () => PickStartingGun(startingGunButton));
        startingGunButton = startingGun.GetComponent<Button>();
        UITheme.StyleSelector(startingGunButton);
        Place(startingGun, new Vector2(0f, top - 146f), new Vector2(ColumnWidth, 48f));
        startingGunText = startingGun.GetComponentInChildren<Text>();

        // Hats: every one, in unlock order, in two columns
        float y = top - 204f;
        NewLabel(content, "CAP", new Vector2(0f, y), ColumnWidth, 16, TextAnchor.MiddleLeft).color = UITheme.AccentText;
        levelText = NewLabel(content, "", new Vector2(0f, y), ColumnWidth, 14, TextAnchor.MiddleRight);
        levelText.color = UITheme.MutedText;

        y -= 36f;
        for (int i = 0; i < Hats.Count; i++)
        {
            AddHatRow(content, i, y, i % 2);
            if (i % 2 == 1 || i == Hats.Count - 1) y -= RowStep;
        }

        nextUnlockText = NewLabel(content, "", new Vector2(0f, y - 4f), ColumnWidth, 14, TextAnchor.MiddleCenter);
        nextUnlockText.color = UITheme.MutedText;

        GameObject done = NewButton(content, "DONE", Close);
        UITheme.StyleButton(done.GetComponent<Button>(), UITheme.ButtonStyle.Primary);
        Place(done, new Vector2(0f, -top + 50f), new Vector2(ColumnWidth, 52f));
    }

    private void AddHatRow(RectTransform parent, int hat, float y, int column)
    {
        HatRow row = new HatRow { hat = hat };
        GameObject button = NewButton(parent, "", () => GameSettings.Hat = row.hat);
        float width = (ColumnWidth - HatColumnGap) / 2f;
        float x = (column == 0 ? -1f : 1f) * (width + HatColumnGap) / 2f;
        Place(button, new Vector2(x, y), new Vector2(width, RowStep - 4f));
        row.button = button.GetComponent<Button>();

        row.name = button.GetComponentInChildren<Text>();
        UITheme.StyleText(row.name, 16, UITheme.TextColor, TextAnchor.MiddleLeft);
        UITheme.Inset(row.name.rectTransform, 14f, 14f);
        row.name.text = Hats.Name(hat);

        row.status = UITheme.NewText("Status", button.transform, 14, UITheme.MutedText, TextAnchor.MiddleRight);
        UITheme.Inset(row.status.rectTransform, 14f, 14f);
        hatRows.Add(row);
    }

    private void OnEnable()
    {
        GameSettings.HatChanged += Refresh;
        GameSettings.StartingGunChanged += Refresh;
    }

    private void OnDisable()
    {
        GameSettings.HatChanged -= Refresh;
        GameSettings.StartingGunChanged -= Refresh;
    }

    private void Refresh()
    {
        if (panel == null) return;

        Weapon weapon = PlayerWeapon();
        string gun = weapon != null ? weapon.loadout[weapon.PreferredStartingSlot].gunName : "-";
        startingGunText.text = UITheme.Option("STARTING GUN", gun.ToUpperInvariant());

        int level = Launcher.myProfile.level;
        int worn = GameSettings.Hat;
        levelText.text = "LEVEL " + level;

        foreach (HatRow row in hatRows)
        {
            bool unlocked = Hats.IsUnlocked(row.hat, level);
            bool wearing = row.hat == worn;
            row.button.interactable = unlocked;
            UITheme.StyleButtonBackground(row.button, wearing ? UITheme.ButtonStyle.Primary : UITheme.ButtonStyle.Secondary, 6f);
            row.name.color = unlocked ? UITheme.TextColor : UITheme.MutedText;
            row.status.text = wearing ? "WEARING" : unlocked ? "" : "LEVEL " + Hats.UnlockLevel(row.hat);
            row.status.color = wearing ? UITheme.TextColor : UITheme.MutedText;
        }

        int next = Hats.NextUnlock(level);
        nextUnlockText.text = next >= 0
            ? $"NEXT CAP AT LEVEL {Hats.UnlockLevel(next)}: {Hats.Name(next)}"
            : "EVERY CAP UNLOCKED";
    }

    // A list of the gun's forms, in loadout order
    private void PickStartingGun(Button button)
    {
        Weapon weapon = PlayerWeapon();
        if (weapon == null) return;
        string[] options = new string[weapon.loadout.Length];
        for (int i = 0; i < options.Length; i++) options[i] = weapon.loadout[i].gunName.ToUpperInvariant();
        OptionPicker.Open(button, options, weapon.PreferredStartingSlot, i =>
        {
            GameSettings.StartingGun = weapon.loadout[i].gunName;
        });
    }

    private Weapon PlayerWeapon()
    {
        if (playerWeapon == null)
        {
            GameObject prefab = Resources.Load<GameObject>("Player");
            playerWeapon = prefab != null ? prefab.GetComponent<Weapon>() : null;
        }
        return playerWeapon != null && playerWeapon.loadout != null && playerWeapon.loadout.Length > 0 ? playerWeapon : null;
    }
}

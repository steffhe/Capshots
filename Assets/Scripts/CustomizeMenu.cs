using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// The Customize panel: the hat you wear and the gun form you spawn holding. Opened by the Customize
// button under Settings, in the main menu and in the pause menu. Hats unlock as you level up (see
// Hats): every hat is listed, the locked ones greyed out with the level that unlocks them. In the
// main menu, pointing at a locked hat (or tapping it) shows it on the player model as a see-through
// grey preview; it can't be worn until it's unlocked.
// In the main menu the panel sits on the left, where the menu column was, so the player model
// stays in view wearing the hat picked, and can be turned round by dragging it (or with a
// controller's right stick; see MenuScenery.Turnable). In a match, a new hat shows on your drone at once.
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
    // The locked hat shown on the main menu's player model (-1: the hat worn), and the one last
    // clicked, which stays shown when the pointer moves off it
    private int previewing = -1;
    private int pinned = -1;
    // The level hats were last looked at, before this opening: hats unlocked since are marked NEW
    private int seenLevel;
    // What was selected when the panel opened (its button), to go back to with a controller
    private Selectable returnTo;
    // In the main menu: a hint under the model saying it can be turned (see MenuScenery.Turnable)
    private Text turnHint;

    // Whether the panel is showing (the pause menu's B button leaves it to close itself)
    public override bool IsOpen => panel != null && panel.activeSelf;

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
        returnTo = MenuNavigation.Current;
        if (mainButtons != null) mainButtons.SetActive(false);
        panel.SetActive(true);
        if (inMainMenu) MenuScenery.Turnable = true;
        if (turnHint != null) turnHint.gameObject.SetActive(true);
        // Looking at the hats: the ones unlocked since last time are marked NEW this time, and the
        // Customize button's NEW badge goes
        seenLevel = GameSettings.HatsSeenLevel;
        GameSettings.HatsSeenLevel = Mathf.Max(seenLevel, Launcher.myProfile.level);
        Refresh();
        // With a controller: start on the hat worn
        foreach (HatRow row in hatRows)
        {
            if (row.hat == GameSettings.Hat) MenuNavigation.Focus(row.button);
        }
    }

    private void Close()
    {
        pinned = -1;
        Preview(-1);
        if (panel != null) panel.SetActive(false);
        if (inMainMenu) MenuScenery.Turnable = false;
        if (turnHint != null) turnHint.gameObject.SetActive(false);
        if (mainButtons != null) mainButtons.SetActive(true);
        MenuNavigation.Focus(returnTo);
    }

    private void Update()
    {
        if (panel == null || !panel.activeSelf) return;
        // Unpausing hides the whole pause menu: go back to the main pause buttons for next time
        if (!anchor.gameObject.activeInHierarchy) { Close(); return; }
        // The controller's B closes the panel, in the main menu and the pause menu
        if (!OptionPicker.Showing && MenuNavigation.TakeBack()) { Close(); return; }
        // With a controller, a locked hat is previewed while it's selected (as pointing at it does)
        if (inMainMenu && MenuNavigation.UsingGamepad) PreviewSelected();
        if (turnHint != null) turnHint.text = MenuNavigation.UsingGamepad ? "RIGHT STICK  ·  TURN YOUR DRONE" : "DRAG  ·  TURN YOUR DRONE";
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

        if (inMainMenu)
        {
            // Under the model, right of the panel
            turnHint = UITheme.NewText("Turn Hint", anchor, 16, UITheme.MutedText, TextAnchor.MiddleCenter);
            turnHint.raycastTarget = false;
            UITheme.Place(turnHint.rectTransform, new Vector2(0.62f, 0f), new Vector2(0f, 56f), new Vector2(420f, 24f));
            turnHint.gameObject.SetActive(false);
        }
    }

    private void AddHatRow(RectTransform parent, int hat, float y, int column)
    {
        HatRow row = new HatRow { hat = hat };
        GameObject button = NewButton(parent, "", () => ClickHat(row.hat));
        if (inMainMenu) PointerHover.Add(button, () => { if (!IsUnlocked(row.hat)) Preview(row.hat); }, () => Preview(pinned));
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

    private void PreviewSelected()
    {
        GameObject selected = UnityEngine.EventSystems.EventSystem.current != null ? UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject : null;
        foreach (HatRow row in hatRows)
        {
            if (row.button.gameObject != selected) continue;
            Preview(IsUnlocked(row.hat) ? pinned : row.hat);
            return;
        }
    }

    private static bool IsUnlocked(int hat) => Hats.IsUnlocked(hat, Launcher.myProfile.level);

    // An unlocked hat is put on; a locked one is previewed (in the main menu), and stays previewed
    private void ClickHat(int hat)
    {
        if (IsUnlocked(hat))
        {
            pinned = -1;
            Preview(-1);
            GameSettings.Hat = hat;
        }
        else if (inMainMenu)
        {
            pinned = hat;
            Preview(hat);
        }
    }

    // Shows a locked hat on the main menu's player model as a grey preview, or (-1) the hat worn again
    private void Preview(int hat)
    {
        if (!inMainMenu || hat == previewing) return;
        previewing = hat;
        PlayerCap.AttachToSceneHeads(PlayerCap.LogoColor, hat >= 0 ? hat : GameSettings.Hat, hat >= 0);
        Refresh();
    }

    // A new hat put on dresses the player model in it (Launcher), replacing any preview
    private void OnHatChanged()
    {
        previewing = pinned = -1;
        Refresh();
    }

    private void OnEnable()
    {
        GameSettings.HatChanged += OnHatChanged;
        GameSettings.StartingGunChanged += Refresh;
    }

    private void OnDisable()
    {
        GameSettings.HatChanged -= OnHatChanged;
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
            // In the main menu a locked hat can still be clicked, to preview it
            row.button.interactable = unlocked || inMainMenu;
            UITheme.StyleButtonBackground(row.button, wearing ? UITheme.ButtonStyle.Primary : UITheme.ButtonStyle.Secondary, 6f);
            row.name.color = unlocked ? UITheme.TextColor : UITheme.MutedText;
            bool isNew = unlocked && Hats.UnlockLevel(row.hat) > seenLevel;
            row.status.text = wearing ? "WEARING" : isNew ? "NEW!" : unlocked ? "" : row.hat == previewing ? "PREVIEW" : "LEVEL " + Hats.UnlockLevel(row.hat);
            row.status.color = wearing ? UITheme.TextColor : isNew ? UITheme.AccentText : UITheme.MutedText;
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

using UnityEngine;

// Player settings. Mouse sensitivity is saved in the profile (profile.json), where it always was;
// everything else is stored in PlayerPrefs.
public static class GameSettings
{
    public const float MinSensitivity = 10f;
    public const float MaxSensitivity = 500f;

    public static float XSensitivity
    {
        get => Launcher.myProfile.xsens;
        set { Launcher.myProfile.xsens = Mathf.Clamp(value, MinSensitivity, MaxSensitivity); Data.SaveProfile(Launcher.myProfile); }
    }

    public static float YSensitivity
    {
        get => Launcher.myProfile.ysens;
        set { Launcher.myProfile.ysens = Mathf.Clamp(value, MinSensitivity, MaxSensitivity); Data.SaveProfile(Launcher.myProfile); }
    }

    private const string FovKey = "settings.fov";
    private const string VolumeKey = "settings.volume";
    private const string QualityKey = "settings.quality";
    private const string AdsSensitivityKey = "settings.adsSensitivity";
    private const string StartingGunKey = "settings.startingGun";

    public const float DefaultFov = 70f;
    public const float MinFov = 60f;
    public const float MaxFov = 110f;

    public static float Fov
    {
        get => PlayerPrefs.GetFloat(FovKey, DefaultFov);
        set { PlayerPrefs.SetFloat(FovKey, Mathf.Clamp(value, MinFov, MaxFov)); PlayerPrefs.Save(); }
    }

    // Look sensitivity is multiplied by this while aiming down sights
    public static float AdsSensitivity
    {
        get => PlayerPrefs.GetFloat(AdsSensitivityKey, 1f);
        set { PlayerPrefs.SetFloat(AdsSensitivityKey, Mathf.Clamp(value, 0.1f, 2f)); PlayerPrefs.Save(); }
    }

    // The gun form to spawn holding, by its gunName; empty for the game's default (Weapon.defaultStartingGun)
    public static string StartingGun
    {
        get => PlayerPrefs.GetString(StartingGunKey, "");
        set
        {
            PlayerPrefs.SetString(StartingGunKey, value ?? "");
            PlayerPrefs.Save();
            StartingGunChanged?.Invoke();
        }
    }

    // The main menu's player model switches to the new starting gun at once
    public static event System.Action StartingGunChanged;

    // The hat to wear (see Hats), saved in the profile. Reads as the cap while the hat picked is
    // still locked at the player's level; setting a locked hat is ignored.
    public static int Hat
    {
        get => Hats.Wearable(Launcher.myProfile.hat, Launcher.myProfile.level);
        set
        {
            if (!Hats.IsUnlocked(value, Launcher.myProfile.level) || value == Launcher.myProfile.hat) return;
            Launcher.myProfile.hat = value;
            Data.SaveProfile(Launcher.myProfile);
            HatChanged?.Invoke();
        }
    }

    // The main menu's player model, or your drone in a match, puts the new hat on at once
    public static event System.Action HatChanged;

    // The level the player was at when they last looked at their hats in Customize: hats unlocked
    // since then are new (a NEW badge on the Customize button, and on their rows). 0 at first, so
    // every unlocked hat starts out new.
    private const string HatsSeenLevelKey = "hats.seenLevel";
    public static int HatsSeenLevel
    {
        get => PlayerPrefs.GetInt(HatsSeenLevelKey, 0);
        set { PlayerPrefs.SetInt(HatsSeenLevelKey, value); PlayerPrefs.Save(); }
    }

    public static bool HasNewHats => Hats.AnyUnlockedBetween(HatsSeenLevel, Launcher.myProfile.level);

    public static float Volume
    {
        get => PlayerPrefs.GetFloat(VolumeKey, 1f);
        set
        {
            PlayerPrefs.SetFloat(VolumeKey, Mathf.Clamp01(value));
            PlayerPrefs.Save();
            AudioListener.volume = Mathf.Clamp01(value);
        }
    }

    public static int Quality
    {
        get => Mathf.Clamp(PlayerPrefs.GetInt(QualityKey, QualitySettings.GetQualityLevel()), 0, QualitySettings.names.Length - 1);
        set
        {
            int level = Mathf.Clamp(value, 0, QualitySettings.names.Length - 1);
            PlayerPrefs.SetInt(QualityKey, level);
            PlayerPrefs.Save();
            QualitySettings.SetQualityLevel(level, true);
        }
    }

    // Colours that people with red-green colour blindness can tell apart: blue against orange teams,
    // and player colours from the Okabe-Ito palette (Manager.TeamColors, PlayerCap.ColorFor)
    private const string ColourblindKey = "settings.colourblind";
    public static bool Colourblind
    {
        get => PlayerPrefs.GetInt(ColourblindKey, 0) == 1;
        set
        {
            if (value == Colourblind) return;
            PlayerPrefs.SetInt(ColourblindKey, value ? 1 : 0);
            PlayerPrefs.Save();
            ColoursChanged?.Invoke();
        }
    }

    // A match recolours everyone at once (Manager)
    public static event System.Action ColoursChanged;

    // A frame rate readout in the top right corner (FpsCounter)
    private const string ShowFpsKey = "settings.showFps";
    public static bool ShowFps
    {
        get => PlayerPrefs.GetInt(ShowFpsKey, 0) == 1;
        set
        {
            if (value == ShowFps) return;
            PlayerPrefs.SetInt(ShowFpsKey, value ? 1 : 0);
            PlayerPrefs.Save();
            ShowFpsChanged?.Invoke();
        }
    }

    public static event System.Action ShowFpsChanged;

    public static string QualityName => QualitySettings.names.Length > 0 ? QualitySettings.names[Quality].ToUpper() : "-";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void ApplySaved()
    {
        AudioListener.volume = Volume;
        if (PlayerPrefs.HasKey(QualityKey)) QualitySettings.SetQualityLevel(Quality, true);
    }
}

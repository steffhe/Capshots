using UnityEngine;

// Player settings. Mouse sensitivity is saved in the profile (profile.json), where it always was;
// everything else is stored in PlayerPrefs (look, audio, graphics, colours, the FPS counter, the
// crosshair, sprint mode and camera effects).
public static class GameSettings
{
    #region Cached saved values

    // Settings are read every frame (field of view, invert Y, sensitivities, camera effects, colour
    // blind colours for every player), so each saved value is read from PlayerPrefs once and kept here;
    // setting one writes both. Every settings key is only ever written through these. (Graphics
    // quality, whose default is the live quality level, still reads PlayerPrefs directly.)
    private static readonly System.Collections.Generic.Dictionary<string, float> savedFloats = new System.Collections.Generic.Dictionary<string, float>();
    private static readonly System.Collections.Generic.Dictionary<string, int> savedInts = new System.Collections.Generic.Dictionary<string, int>();

    private static float GetFloat(string key, float fallback)
    {
        if (!savedFloats.TryGetValue(key, out float value))
        {
            value = PlayerPrefs.GetFloat(key, fallback);
            savedFloats[key] = value;
        }
        return value;
    }

    private static void SetFloat(string key, float value)
    {
        PlayerPrefs.SetFloat(key, value);
        savedFloats[key] = value;
    }

    private static int GetInt(string key, int fallback)
    {
        if (!savedInts.TryGetValue(key, out int value))
        {
            value = PlayerPrefs.GetInt(key, fallback);
            savedInts[key] = value;
        }
        return value;
    }

    private static void SetInt(string key, int value)
    {
        PlayerPrefs.SetInt(key, value);
        savedInts[key] = value;
    }

    #endregion

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
    private const string MusicVolumeKey = "settings.musicVolume";
    private const string EffectsVolumeKey = "settings.effectsVolume";
    private const string InvertYKey = "settings.invertY";
    private const string ToggleSprintKey = "settings.toggleSprint";
    private const string StickSensitivityKey = "settings.stickSensitivity";
    public const float MinStickSensitivity = 0.3f;
    public const float MaxStickSensitivity = 2.5f;
    private const string ScreenShakeKey = "settings.screenShake";
    private const string QualityKey = "settings.quality";
    private const string AdsSensitivityKey = "settings.adsSensitivity";
    private const string StartingGunKey = "settings.startingGun";

    public const float DefaultFov = 70f;
    public const float MinFov = 60f;
    public const float MaxFov = 110f;

    public static float Fov
    {
        get => GetFloat(FovKey, DefaultFov);
        set { SetFloat(FovKey, Mathf.Clamp(value, MinFov, MaxFov)); PlayerPrefs.Save(); }
    }

    // Look sensitivity is multiplied by this while aiming down sights
    public static float AdsSensitivity
    {
        get => GetFloat(AdsSensitivityKey, 1f);
        set { SetFloat(AdsSensitivityKey, Mathf.Clamp(value, 0.1f, 2f)); PlayerPrefs.Save(); }
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

    // The face shown on the drone's screen (see Faces), saved in the profile. Reads as the classic
    // face while the one picked is still locked; setting a locked face is ignored.
    public static int Face
    {
        get => Faces.Wearable(Launcher.myProfile.face, Launcher.myProfile.level);
        set
        {
            if (!Faces.IsUnlocked(value, Launcher.myProfile.level) || value == Launcher.myProfile.face) return;
            Launcher.myProfile.face = value;
            Data.SaveProfile(Launcher.myProfile);
            FaceChanged?.Invoke();
        }
    }

    // The main menu's player model, or your drone in a match, changes face at once
    public static event System.Action FaceChanged;

    // The level the player was at when they last looked at their hats in Customize: hats unlocked
    // since then are new (a NEW badge on the Customize button, and on their rows). 0 at first, so
    // every unlocked hat starts out new.
    private const string HatsSeenLevelKey = "hats.seenLevel";
    public static int HatsSeenLevel
    {
        get => GetInt(HatsSeenLevelKey, 0);
        set { SetInt(HatsSeenLevelKey, value); PlayerPrefs.Save(); }
    }

    // (Faces unlock with levels too, so a new face counts as well)
    public static bool HasNewHats => Hats.AnyUnlockedBetween(HatsSeenLevel, Launcher.myProfile.level) || Faces.AnyUnlockedBetween(HatsSeenLevel, Launcher.myProfile.level);

    // The master volume, over both the sound effects and the music
    public static float Volume
    {
        get => GetFloat(VolumeKey, 1f);
        set
        {
            SetFloat(VolumeKey, Mathf.Clamp01(value));
            PlayerPrefs.Save();
            ApplyVolume();
        }
    }

    // Every sound but the music, under the master volume. There's no audio mixer: the listener's
    // volume is master x effects, and the music's sources ignore it and use MusicOutput instead.
    public static float EffectsVolume
    {
        get => GetFloat(EffectsVolumeKey, 1f);
        set
        {
            SetFloat(EffectsVolumeKey, Mathf.Clamp01(value));
            PlayerPrefs.Save();
            ApplyVolume();
        }
    }

    private static void ApplyVolume() => AudioListener.volume = Mathf.Clamp01(Volume * EffectsVolume);

    // How loud the music plays, 0 to 1: the music volume under the master volume. For music sources
    // set to ignoreListenerVolume (MenuAudio, MatchMusic).
    public static float MusicOutput => MusicVolume * Volume;

    // The menu's music and the end of a match's (MatchMusic), under the master volume
    public static float MusicVolume
    {
        get => GetFloat(MusicVolumeKey, 0.7f);
        set
        {
            SetFloat(MusicVolumeKey, Mathf.Clamp01(value));
            PlayerPrefs.Save();
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
        get => GetInt(ColourblindKey, 0) == 1;
        set
        {
            if (value == Colourblind) return;
            SetInt(ColourblindKey, value ? 1 : 0);
            PlayerPrefs.Save();
            ColoursChanged?.Invoke();
        }
    }

    // A match recolours everyone at once (Manager)
    public static event System.Action ColoursChanged;

    // A frame rate readout at the bottom of the screen (FpsCounter)
    private const string ShowFpsKey = "settings.showFps";
    public static bool ShowFps
    {
        get => GetInt(ShowFpsKey, 0) == 1;
        set
        {
            if (value == ShowFps) return;
            SetInt(ShowFpsKey, value ? 1 : 0);
            PlayerPrefs.Save();
            ShowFpsChanged?.Invoke();
        }
    }

    public static event System.Action ShowFpsChanged;

    // Pushing the stick or mouse up looks down, and the other way round
    // Gamepad look speed, as a multiple of Look.StickYawSpeed/StickPitchSpeed at a full push. Separate
    // from the mouse sensitivity, which means mouse distance per turn.
    public static float StickSensitivity
    {
        get => Mathf.Clamp(GetFloat(StickSensitivityKey, 1f), MinStickSensitivity, MaxStickSensitivity);
        set { SetFloat(StickSensitivityKey, Mathf.Clamp(value, MinStickSensitivity, MaxStickSensitivity)); PlayerPrefs.Save(); }
    }

    // Sprint by pressing once instead of holding (GameInput.SprintWanted); off by default
    public static bool ToggleSprint
    {
        get => GetInt(ToggleSprintKey, 0) == 1;
        set { SetInt(ToggleSprintKey, value ? 1 : 0); PlayerPrefs.Save(); }
    }

    public static bool InvertY
    {
        get => GetInt(InvertYKey, 0) == 1;
        set { SetInt(InvertYKey, value ? 1 : 0); PlayerPrefs.Save(); }
    }

    // Camera effects, shown as CAMERA EFFECTS in the settings: the view's shake when firing, on a kill
    // and from a nearby grenade (Look), and the field of view widening with speed (Player.UpdateCamera).
    // Off for players who get motion sick or want a still view; recoil still moves the aim, since
    // that's part of how the guns handle. (Saved under its old name, screen shake.)
    public static bool ScreenShake
    {
        get => GetInt(ScreenShakeKey, 1) == 1;
        set { SetInt(ScreenShakeKey, value ? 1 : 0); PlayerPrefs.Save(); }
    }

    #region Crosshair

    // The crosshair's colour (an index into CrosshairColours), whether it has the centre dot and the
    // spread ticks, and its size. Read by HudFeedback, which redraws it when CrosshairChanged fires.
    private const string CrosshairColourKey = "settings.crosshairColour";
    private const string CrosshairDotKey = "settings.crosshairDot";
    private const string CrosshairTicksKey = "settings.crosshairTicks";
    private const string CrosshairSizeKey = "settings.crosshairSize";
    public const float MinCrosshairSize = 0.75f, MaxCrosshairSize = 1.5f;

    // Picked to stand out against the maps: white first (the default), then the usual alternatives
    public static readonly string[] CrosshairColourNames = { "WHITE", "GREEN", "YELLOW", "CYAN", "MAGENTA" };
    public static readonly Color[] CrosshairColours =
    {
        new Color(1f, 1f, 1f), new Color(0.35f, 1f, 0.35f), new Color(1f, 0.95f, 0.3f), new Color(0.3f, 0.95f, 1f), new Color(1f, 0.4f, 1f),
    };

    public static int CrosshairColourIndex
    {
        get => Mathf.Clamp(GetInt(CrosshairColourKey, 0), 0, CrosshairColours.Length - 1);
        set { SetInt(CrosshairColourKey, Mathf.Clamp(value, 0, CrosshairColours.Length - 1)); PlayerPrefs.Save(); CrosshairChanged?.Invoke(); }
    }

    public static Color CrosshairColour => CrosshairColours[CrosshairColourIndex];

    public static bool CrosshairDot
    {
        get => GetInt(CrosshairDotKey, 1) == 1;
        set { SetInt(CrosshairDotKey, value ? 1 : 0); PlayerPrefs.Save(); CrosshairChanged?.Invoke(); }
    }

    public static bool CrosshairTicks
    {
        get => GetInt(CrosshairTicksKey, 1) == 1;
        set { SetInt(CrosshairTicksKey, value ? 1 : 0); PlayerPrefs.Save(); CrosshairChanged?.Invoke(); }
    }

    public static float CrosshairSize
    {
        get => Mathf.Clamp(GetFloat(CrosshairSizeKey, 1f), MinCrosshairSize, MaxCrosshairSize);
        set { SetFloat(CrosshairSizeKey, Mathf.Clamp(value, MinCrosshairSize, MaxCrosshairSize)); PlayerPrefs.Save(); CrosshairChanged?.Invoke(); }
    }

    public static event System.Action CrosshairChanged;

    #endregion

    public static string QualityName => QualitySettings.names.Length > 0 ? QualitySettings.names[Quality].ToUpper() : "-";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void ApplySaved()
    {
        ApplyVolume();
        if (PlayerPrefs.HasKey(QualityKey)) QualitySettings.SetQualityLevel(Quality, true);
    }
}

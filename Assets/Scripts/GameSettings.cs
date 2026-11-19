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

    public static string QualityName => QualitySettings.names.Length > 0 ? QualitySettings.names[Quality].ToUpper() : "-";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void ApplySaved()
    {
        AudioListener.volume = Volume;
        if (PlayerPrefs.HasKey(QualityKey)) QualitySettings.SetQualityLevel(Quality, true);
    }
}

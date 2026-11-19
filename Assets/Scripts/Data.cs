using System.IO;
using UnityEngine;

// Saves and loads the player's profile (name, level, XP, sensitivity).
// Desktop builds use a JSON file. Browser (WebGL) builds keep files only in memory until the
// page syncs them, so there the same JSON goes into PlayerPrefs, which the browser stores reliably.
public class Data : MonoBehaviour
{
    private static string ProfilePath => Path.Combine(Application.persistentDataPath, "profile.json");
    private const string ProfilePrefsKey = "profile.json";

    public static void SaveProfile(ProfileData profile)
    {
        try
        {
            string json = JsonUtility.ToJson(profile);
#if UNITY_WEBGL && !UNITY_EDITOR
            PlayerPrefs.SetString(ProfilePrefsKey, json);
            PlayerPrefs.Save();
#else
            File.WriteAllText(ProfilePath, json);
#endif
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("Could not save profile: " + e.Message);
        }
    }

    public static ProfileData LoadProfile()
    {
        try
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            string json = PlayerPrefs.GetString(ProfilePrefsKey, "");
#else
            string json = File.Exists(ProfilePath) ? File.ReadAllText(ProfilePath) : "";
#endif
            if (!string.IsNullOrEmpty(json))
            {
                ProfileData loaded = JsonUtility.FromJson<ProfileData>(json);
                if (loaded != null) return loaded;
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("Could not load profile: " + e.Message);
        }

        return new ProfileData();
    }
}

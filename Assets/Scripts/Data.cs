using System.IO;
using UnityEngine;

public class Data : MonoBehaviour
{
    private static string ProfilePath => Path.Combine(Application.persistentDataPath, "profile.json");

    public static void SaveProfile(ProfileData profile)
    {
        try
        {
            File.WriteAllText(ProfilePath, JsonUtility.ToJson(profile));
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("Could not save profile to file: " + e.Message);
        }
    }

    public static ProfileData LoadProfile()
    {
        try
        {
            if (File.Exists(ProfilePath))
            {
                ProfileData loaded = JsonUtility.FromJson<ProfileData>(File.ReadAllText(ProfilePath));
                if (loaded != null) return loaded;
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("Could not load profile from file: " + e.Message);
        }

        return new ProfileData();
    }
}

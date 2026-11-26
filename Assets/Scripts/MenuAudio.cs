using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// The main menu's sound: a mellow synthwave loop (MenuMusic) and short blips for the buttons: a
// tick when one is pointed at or picked with a controller, a rising chirp when it's pressed, and a
// falling one for BACK, DONE and the like. Everything is made in code, like the game's other sounds.
// The Launcher creates it with the menu; it goes with the menu scene, fading the music out when a
// match starts loading. Music volume is its own setting (GameSettings.MusicVolume), under the
// master volume.
public class MenuAudio : MonoBehaviour
{
    private const float MusicLevel = 0.42f, UiLevel = 0.5f;
    private const float FadeIn = 2.5f, FadeOut = 0.8f;
    // New buttons (panels built later, option lists) are looked for this often
    private const float ScanInterval = 0.3f;

    private static MenuAudio instance;
    private static AudioClip music, hover, click, back;

    private AudioSource musicSource, uiSource;
    private float fade;
    private float nextScan;
    private float lastClickAt = -1f;

    public static void Create()
    {
        if (instance != null) return;
        instance = new GameObject("Menu Audio").AddComponent<MenuAudio>();
    }

    private void Awake()
    {
        musicSource = gameObject.AddComponent<AudioSource>();
        musicSource.loop = true;
        musicSource.playOnAwake = false;
        musicSource.spatialBlend = 0f;
        musicSource.volume = 0f;
        uiSource = gameObject.AddComponent<AudioSource>();
        uiSource.playOnAwake = false;
        uiSource.spatialBlend = 0f;

        if (hover == null) BuildUiClips();
        StartCoroutine(StartMusic());
    }

    private IEnumerator StartMusic()
    {
        // Made once a session, a slice each frame so the menu doesn't stall while it's made
        if (music == null)
        {
            MenuMusic maker = new MenuMusic();
            IEnumerator steps = maker.Build();
            while (steps.MoveNext()) yield return null;
            music = maker.Clip;
        }
        musicSource.clip = music;
        musicSource.Play();
    }

    private void Update()
    {
        // Fades in with the menu, and out under the loading screen when a match starts
        bool leaving = LoadingScreen.Visible;
        fade = Mathf.MoveTowards(fade, leaving ? 0f : 1f, Time.unscaledDeltaTime / (leaving ? FadeOut : FadeIn));
        musicSource.volume = MusicLevel * GameSettings.MusicVolume * fade;

        if (Time.unscaledTime >= nextScan)
        {
            nextScan = Time.unscaledTime + ScanInterval;
            HookButtons();
        }
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    // Every button, slider and the like in the menu gets a MenuSoundHook (once)
    private static void HookButtons()
    {
        foreach (Selectable selectable in FindObjectsByType<Selectable>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (selectable.GetComponent<MenuSoundHook>() == null) selectable.gameObject.AddComponent<MenuSoundHook>();
        }
    }

    internal static void Hovered()
    {
        // Not right after a press: the press often moves the selection too
        if (instance == null || Time.unscaledTime - instance.lastClickAt < 0.12f) return;
        instance.uiSource.PlayOneShot(hover, UiLevel * 0.6f);
    }

    internal static void Pressed(bool goingBack)
    {
        if (instance == null) return;
        instance.lastClickAt = Time.unscaledTime;
        instance.uiSource.PlayOneShot(goingBack ? back : click, UiLevel);
    }

    #region Clips

    private static void BuildUiClips()
    {
        const int rate = 44100;
        // A soft, short tick, slightly rising
        hover = Make("Menu Hover", rate, 0.035f, t =>
        {
            float f = Mathf.Lerp(1700f, 2100f, t / 0.035f);
            return Mathf.Sin(2f * Mathf.PI * f * t) * Mathf.Exp(-t * 110f) * Mathf.Clamp01(t * 2000f);
        });
        // Two quick notes going up (a fifth), with a bright edge
        click = Make("Menu Click", rate, 0.13f, t => TwoNotes(t, 880f, 1318.5f));
        // The same, going down
        back = Make("Menu Back", rate, 0.13f, t => TwoNotes(t, 1046.5f, 698.5f));
    }

    private static float TwoNotes(float t, float first, float second)
    {
        const float split = 0.045f;
        float f = t < split ? first : second;
        float local = t < split ? t : t - split;
        float wave = Triangle(f * t) * 0.8f + Mathf.Sin(2f * Mathf.PI * f * 2f * t) * 0.2f;
        return wave * Mathf.Exp(-local * 38f) * Mathf.Clamp01(local * 1500f) * 0.75f;
    }

    private static float Triangle(float phase)
    {
        float p = phase - Mathf.Floor(phase);
        return 4f * Mathf.Abs(p - 0.5f) - 1f;
    }

    private static AudioClip Make(string name, int rate, float length, System.Func<float, float> wave)
    {
        int samples = Mathf.CeilToInt(rate * length);
        float[] data = new float[samples];
        for (int i = 0; i < samples; i++)
        {
            float t = (float)i / rate;
            // A short fade at the very end so it never clicks
            data[i] = wave(t) * Mathf.Clamp01((samples - i) / (rate * 0.004f));
        }
        AudioClip clip = AudioClip.Create(name, samples, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    #endregion
}

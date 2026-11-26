using System.Collections;
using UnityEngine;

// Music for the end of a match (Manager.Presentation): the triumphant VictoryMusic over the awards,
// crossfading into the menu's loop (MenuMusic, via MenuAudio) for the results and the map vote, and
// fading out under the loading screen when the next map or the menu loads. Purely local, and it goes
// with the match scene. Both clips are made in code over a few frames, starting when the match ends
// (Prepare), while everyone is frozen on the result banner, so they're ready by the awards. Music
// volume is its own setting, under the master volume (GameSettings.MusicOutput).
public class MatchMusic : MonoBehaviour
{
    private enum Track { None, Victory, Menu }

    private const float FadeIn = 1f, Crossfade = 1.5f, FadeOut = 0.8f;

    private static MatchMusic instance;
    private static AudioClip victory;

    // Two sources, so one track can fade out while the next fades in
    private AudioSource current, previous;
    private Track wanted = Track.None, playing = Track.None;
    private float currentFade, previousFade, leaveFade = 1f;

    // Starts making the clips, so they're ready when wanted
    public static void Prepare()
    {
        Instance();
    }

    public static void PlayVictory() => Instance().wanted = Track.Victory;
    public static void PlayMenu() => Instance().wanted = Track.Menu;

    private static MatchMusic Instance()
    {
        if (instance == null) instance = new GameObject("Match Music").AddComponent<MatchMusic>();
        return instance;
    }

    private void Awake()
    {
        current = NewSource();
        previous = NewSource();
        StartCoroutine(BuildClips());
    }

    private AudioSource NewSource()
    {
        AudioSource source = gameObject.AddComponent<AudioSource>();
        source.loop = true;
        source.playOnAwake = false;
        source.spatialBlend = 0f;
        source.volume = 0f;
        // Under the music volume rather than the sound effects' (GameSettings.MusicOutput)
        source.ignoreListenerVolume = true;
        // Ahead of the game's sounds, so it isn't the first thing dropped when many play at once
        source.priority = 0;
        return source;
    }

    private IEnumerator BuildClips()
    {
        // The victory loop first: the awards come before the results. Made once a session.
        if (victory == null)
        {
            VictoryMusic maker = new VictoryMusic();
            IEnumerator steps = maker.Build();
            while (steps.MoveNext()) yield return null;
            victory = maker.Clip;
        }
        yield return MenuAudio.BuildMusic();
    }

    private void Update()
    {
        // Switch track as soon as the wanted one is ready (a track that isn't ready yet keeps the
        // current one going rather than cutting to silence)
        AudioClip clip = wanted == Track.Victory ? victory : wanted == Track.Menu ? MenuAudio.MusicClip : null;
        if (wanted != playing && clip != null)
        {
            // The player's camera, and maybe its listener, is switched off by now (the map camera
            // films the end of the match)
            MenuAudio.EnsureListener(gameObject);
            AudioSource old = current;
            current = previous;
            previous = old;
            previousFade = currentFade;
            currentFade = 0f;
            current.clip = clip;
            current.Play();
            playing = wanted;
        }

        float dt = Time.unscaledDeltaTime;
        currentFade = Mathf.MoveTowards(currentFade, 1f, dt / (previous.isPlaying ? Crossfade : FadeIn));
        previousFade = Mathf.MoveTowards(previousFade, 0f, dt / Crossfade);
        if (previousFade <= 0f && previous.isPlaying) previous.Stop();

        // Out under the loading screen, for the next map or the menu
        leaveFade = Mathf.MoveTowards(leaveFade, LoadingScreen.Visible ? 0f : 1f, dt / FadeOut);

        float level = MenuAudio.MusicLevel * GameSettings.MusicOutput * leaveFade;
        current.volume = level * currentFade;
        previous.volume = level * previousFade;
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }
}

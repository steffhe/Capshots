using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

// Keeps the browser version's frame rate up. Browsers draw through WebGL, which costs more than the
// Windows build's graphics, and when frames are slow the browser queues them up, which delays
// the mouse. Only the browser build uses this; the Windows build looks as before.
//
// Lighter settings, whichever graphics quality is picked: hard shadows instead of soft ones, one
// shadow cascade reaching 30 m, a 1024 shadow map at most, and FXAA instead of SMAA anti-aliasing.
// The changes are made to the loaded copies of the settings, never saved to the project.
//
// Adaptive resolution: when frames take longer than at 60 FPS, the world is drawn at a lower
// resolution (down to 60%) and scaled up to the screen; when there's room again, it goes back up.
// Menus and the HUD are drawn at full resolution either way. If a lower resolution doesn't help
// (the processor is the limit, not the graphics), it goes back up and stops trying for a while.
public class BrowserGraphics : MonoBehaviour
{
    public const float MinScale = 0.6f;
    private const float Step = 0.1f;
    private const float Window = 1f;               // Seconds of frames per measurement
    private const float Budget = 1f / 60f;
    private const float SlowFrame = Budget * 1.15f; // Under about 52 FPS: lower the resolution
    private const float FastFrame = Budget * 1.06f; // About 57 FPS or more: room to go back up
    private const int GoodWindowsToRaise = 4;

    // The resolution the world is drawn at, as a share of the screen's
    public static float RenderScale { get; private set; } = 1f;

    private static BrowserGraphics instance;

    private UniversalRenderPipelineAsset lightened;
    private float elapsed;
    private int frames;
    private bool skipWindow;
    private int goodWindows;

    // After lowering the resolution: whether the next measurement should show it helped
    private bool checkDrop;
    private float scaleBeforeDrop, frameBeforeDrop;
    // No lowering until then (the processor is the limit), or no raising until then (the
    // last raise was too much). Each wait doubles every time it's needed again, up to a few minutes.
    private float noDropUntil, noRaiseUntil, lastRaise = -100f;
    private float raiseWait = 10f;
    private float dropWait = 30f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Create()
    {
        if (!Browser.IsBrowser || instance != null) return;
        var go = new GameObject("Browser Graphics");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<BrowserGraphics>();
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        RenderPipelineManager.beginCameraRendering += OnBeginCamera;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        RenderPipelineManager.beginCameraRendering -= OnBeginCamera;
    }

    private void Update()
    {
        // A new graphics quality brings its own settings asset
        if (QualitySettings.renderPipeline is UniversalRenderPipelineAsset asset && asset != lightened)
        {
            Lighten(asset);
            lightened = asset;
            asset.renderScale = RenderScale;
        }
        Adapt();
    }

    #region Lighter settings

    private static void Lighten(UniversalRenderPipelineAsset asset)
    {
        asset.shadowDistance = Mathf.Min(asset.shadowDistance, 30f);
        asset.shadowCascadeCount = 1;
        asset.mainLightShadowmapResolution = Mathf.Min(asset.mainLightShadowmapResolution, 1024);
    }

    // Soft shadows are set on each light: make the scene's hard
    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Light light in root.GetComponentsInChildren<Light>(true))
            {
                if (light.shadows == LightShadows.Soft) light.shadows = LightShadows.Hard;
            }
        }
    }

    // FXAA costs much less than SMAA. Done as each camera draws, so cameras made later count too
    // (the gun's camera, the death camera, the awards stage).
    private static void OnBeginCamera(ScriptableRenderContext context, Camera camera)
    {
        if (camera.TryGetComponent(out UniversalAdditionalCameraData data) && data.antialiasing == AntialiasingMode.SubpixelMorphologicalAntiAliasing)
        {
            data.antialiasing = AntialiasingMode.FastApproximateAntialiasing;
        }
    }

    #endregion

    #region Adaptive resolution

    private void Adapt()
    {
        float dt = Time.unscaledDeltaTime;
        // A hitch (loading, the tab coming back) says nothing about the usual frame time
        if (dt > 0.25f) { elapsed = 0f; frames = 0; return; }
        elapsed += dt;
        frames++;
        if (elapsed < Window) return;

        float frame = elapsed / frames;
        elapsed = 0f;
        frames = 0;
        // The first measurement after a change includes the change itself
        if (skipWindow) { skipWindow = false; return; }

        float now = Time.unscaledTime;

        if (checkDrop)
        {
            checkDrop = false;
            // Not even 5% faster: the graphics aren't the limit, so take the sharper picture back
            if (frame > frameBeforeDrop * 0.95f)
            {
                SetScale(scaleBeforeDrop);
                noDropUntil = now + dropWait;
                dropWait = Mathf.Min(dropWait * 2f, 300f);
                return;
            }
        }

        if (frame > SlowFrame)
        {
            goodWindows = 0;
            if (RenderScale <= MinScale + 0.001f || now < noDropUntil) return;
            // A raise that didn't hold: wait longer before the next one
            if (now - lastRaise < Window * 4f)
            {
                noRaiseUntil = now + raiseWait;
                raiseWait = Mathf.Min(raiseWait * 2f, 120f);
            }
            scaleBeforeDrop = RenderScale;
            frameBeforeDrop = frame;
            checkDrop = true;
            SetScale(RenderScale - Step);
        }
        else if (frame < FastFrame)
        {
            if (RenderScale >= 0.999f || now < noRaiseUntil) return;
            if (++goodWindows < GoodWindowsToRaise) return;
            goodWindows = 0;
            lastRaise = now;
            SetScale(RenderScale + Step);
        }
        else goodWindows = 0;
    }

    private void SetScale(float scale)
    {
        RenderScale = Mathf.Clamp(Mathf.Round(scale * 10f) / 10f, MinScale, 1f);
        if (lightened != null) lightened.renderScale = RenderScale;
        skipWindow = true;
    }

    #endregion
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Dev tool: renders the menu tabs and the in-game HUD, scoreboard, pause, settings and end screens
// to PNG files, with the runtime UITheme styling and sample data applied, so UI changes can be
// reviewed without playing a match. Run it from Tools > Capture UI Screenshots (writes to
// Temp/UICaptures), or create Temp/capture-ui.trigger containing an output folder.
// Scenes are opened as preview copies, so nothing in the open scenes is touched or saved.
static class UICapture
{
    const string Trigger = "Temp/capture-ui.trigger";
    static readonly Vector2Int[] Sizes = { new Vector2Int(1920, 1080), new Vector2Int(1280, 960) };

    [InitializeOnLoadMethod]
    static void Hook() => EditorApplication.update += Poll;

    static double nextPoll;

    static void Poll()
    {
        // Checks for the trigger file about once a second
        if (EditorApplication.timeSinceStartup < nextPoll) return;
        nextPoll = EditorApplication.timeSinceStartup + 1.0;
        if (!File.Exists(Trigger) || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;
        string outDir = File.ReadAllText(Trigger).Trim();
        File.Delete(Trigger);
        Run(outDir);
    }

    [MenuItem("Tools/Capture UI Screenshots")]
    static void MenuCapture()
    {
        string outDir = Path.GetFullPath("Temp/UICaptures");
        Run(outDir);
        EditorUtility.RevealInFinder(outDir);
    }

    static void Run(string outDir)
    {
        Directory.CreateDirectory(outDir);
        var log = new List<string>();
        try { CaptureMenu(outDir, log); } catch (Exception e) { log.Add("menu: " + e); }
        try { CaptureGame(outDir, log); } catch (Exception e) { log.Add("game: " + e); }
        File.WriteAllLines(Path.Combine(outDir, "log.txt"), log);
        File.WriteAllText(Path.Combine(outDir, "done.txt"), DateTime.Now.ToString("O"));
        Debug.Log("UI captures written to " + outDir);
    }

    // Temp/capture-ui.raw: capture without the runtime restyle. Temp/capture-ui.dump: also write the UI hierarchy.
    static bool Styled => !File.Exists("Temp/capture-ui.raw");
    static bool Dump => File.Exists("Temp/capture-ui.dump");

    // ---------------------------------------------------------------- menu

    static void CaptureMenu(string outDir, List<string> log)
    {
        Scene scene = OpenPreview("Assets/Scenes/Menu.unity");
        try
        {
            Launcher launcher = FindIn<Launcher>(scene);
            Camera source = FindIn<Camera>(scene);
            if (launcher == null) { log.Add("No Launcher in menu"); return; }

            Launcher.myProfile = new ProfileData("Capper", 4, 620, 150f, 150f);
            launcher.usernameField.text = Launcher.myProfile.username;
            if (Styled)
            {
                Call(launcher, "CreateModeButton");
                Call(launcher, "ShowWordmark");
                Call(launcher, "ApplyStyle");
            }

            if (Dump)
            {
                var sb = new System.Text.StringBuilder();
                foreach (GameObject root in scene.GetRootGameObjects())
                    if (root.GetComponent<Canvas>() != null) sb.Append(Tree(root.transform));
                File.WriteAllText(Path.Combine(outDir, "tree-menu.txt"), sb.ToString());
            }

            launcher.TabOpenMain();
            RenderScene(scene, source, outDir, "menu-main", log);

            launcher.TabOpenRooms();
            RenderScene(scene, source, outDir, "menu-rooms-empty", log);
            if (Styled)
            {
                Call(launcher, "ShowRoomsEmptyState", false, null);
                Call(launcher, "CreateRoomRow", "Friday night frag fest", "3 / 8", "Battlefield  \u00B7  FFA");
                Call(launcher, "CreateRoomRow", "caps only", "7 / 10", "Nuketown  \u00B7  TDM");
                Call(launcher, "CreateRoomRow", "USER_482", "1 / 8", "Battlefield  \u00B7  FFA");
                RenderScene(scene, source, outDir, "menu-rooms", log);
                if (Dump) File.WriteAllText(Path.Combine(outDir, "tree-rooms.txt"), Tree(launcher.tabRooms.transform));
            }

            launcher.TabOpenCreate();
            RenderScene(scene, source, outDir, "menu-create", log);
        }
        finally
        {
            EditorSceneManager.ClosePreviewScene(scene);
        }
    }

    // ---------------------------------------------------------------- game

    static void CaptureGame(string outDir, List<string> log)
    {
        Scene scene = OpenPreview("Assets/Scenes/Nuketown.unity");
        try
        {
            Canvas canvas = null;
            foreach (GameObject root in scene.GetRootGameObjects())
                if (root.name == "Canvas") canvas = root.GetComponent<Canvas>();
            Camera source = FindIn<Camera>(scene);
            if (canvas == null) { log.Add("No Canvas in Map"); return; }

            Transform hud = canvas.transform.Find("HUD");
            Transform pause = canvas.transform.Find("Pause");
            Transform end = canvas.transform.Find("End Game");

            if (Styled)
            {
                UITheme.StyleHud(hud);
                UITheme.StyleEndGame(end);
                UITheme.StylePause(pause);
            }
            if (Dump) File.WriteAllText(Path.Combine(outDir, "tree-game.txt"), Tree(canvas.transform));

            // Sample values, as a match would show them
            SetText(hud, "Timer/Text", "04:32");
            SetText(hud, "Stats/Kills", "7 KILLS");
            SetText(hud, "Stats/Deaths", "3 DEATHS");
            SetText(hud, "Username/Text", Manager.UsernameWithLevel(Launcher.myProfile));
            Text ammo = hud != null ? hud.Find("Ammo/Text")?.GetComponent<Text>() : null;
            if (ammo != null)
                ammo.text = $"12<size={Mathf.RoundToInt(ammo.fontSize * 0.55f)}><color={UITheme.Hex(UITheme.MutedText)}> / 90</color></size>";
            Transform bar = hud != null ? hud.Find("Health/Bar") : null;
            if (bar != null)
            {
                bar.localScale = new Vector3(0.72f, 1f, 1f);
                if (Styled) bar.GetComponent<Image>().color = UITheme.HealthColor(0.72f);
            }
            FillLeaderboard(hud != null ? hud.Find("Leaderboard") : null);
            FillLeaderboard(end != null ? end.Find("Leaderboard") : null);

            void Only(params Transform[] show)
            {
                foreach (Transform child in canvas.transform) child.gameObject.SetActive(Array.IndexOf(show, child) >= 0);
            }

            if (hud != null)
            {
                Only(hud);
                Transform leaderboard = hud.Find("Leaderboard");
                if (leaderboard != null) leaderboard.gameObject.SetActive(false);
                RenderScene(scene, source, outDir, "game-hud", log);
                if (leaderboard != null)
                {
                    leaderboard.gameObject.SetActive(true);
                    RenderScene(scene, source, outDir, "game-leaderboard", log);
                    leaderboard.gameObject.SetActive(false);
                }
            }
            if (pause != null)
            {
                Only(hud, pause);
                if (pause.childCount > 0) pause.GetChild(0).gameObject.SetActive(true);

                // The settings panel (and its button) is built at runtime; build it here the same way
                Pause p = pause.GetComponent<Pause>();
                SettingsMenu settings = null;
                if (p != null)
                {
                    GameInput.Initialize();
                    settings = SettingsMenu.Create(p);
                    Call(settings, "Start");
                }
                RenderScene(scene, source, outDir, "game-pause", log);
                if (settings != null)
                {
                    Call(settings, "OpenSettings");
                    RenderScene(scene, source, outDir, "game-settings", log);
                }
            }
            if (end != null)
            {
                Only(end);
                RenderScene(scene, source, outDir, "game-endgame", log);
            }
        }
        finally
        {
            EditorSceneManager.ClosePreviewScene(scene);
        }
    }

    static void SetText(Transform root, string path, string text)
    {
        Text t = root != null ? root.Find(path)?.GetComponent<Text>() : null;
        if (t != null) t.text = text;
    }

    // Copies the scoreboard's row template for a few sample players, like Manager.Leaderboard
    static void FillLeaderboard(Transform lb)
    {
        if (lb == null || lb.childCount < 2) return;
        GameObject card = lb.GetChild(1).gameObject;
        string[] names = { "Capper", "sniperwolf", "USER_482", "noscope_nora" };
        int[] kills = { 12, 9, 4, 2 };
        for (int i = 0; i < names.Length; i++)
        {
            GameObject row = UnityEngine.Object.Instantiate(card, lb);
            if (i % 2 == 1 && Styled) row.GetComponent<Image>().color = UITheme.SurfaceAlt;
            row.transform.Find("Level").GetComponent<Text>().text = (4 + i * 3).ToString("00");
            row.transform.Find("Username").GetComponent<Text>().text = names[i];
            row.transform.Find("Score Value").GetComponent<Text>().text = (kills[i] * 100).ToString();
            row.transform.Find("Kills Value").GetComponent<Text>().text = kills[i].ToString();
            row.transform.Find("Deaths Value").GetComponent<Text>().text = (i + 3).ToString();
        }
        card.SetActive(false);
    }

    // ---------------------------------------------------------------- helpers

    static Scene OpenPreview(string path)
    {
        MethodInfo open = typeof(EditorSceneManager).GetMethod("OpenPreviewScene", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static, null, new[] { typeof(string) }, null);
        if (open == null) throw new Exception("EditorSceneManager.OpenPreviewScene not available");
        return (Scene)open.Invoke(null, new object[] { path });
    }

    static T FindIn<T>(Scene scene) where T : Component
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            T found = root.GetComponentInChildren<T>(true);
            if (found != null) return found;
        }
        return null;
    }

    static object Call(object target, string method, params object[] args)
    {
        MethodInfo info = target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (info == null) throw new MissingMethodException(target.GetType().Name, method);
        return info.Invoke(target, args.Length == 0 ? null : args);
    }

    static string Tree(Transform root)
    {
        var sb = new System.Text.StringBuilder();
        void Walk(Transform t, int depth)
        {
            sb.Append(new string(' ', depth * 2)).Append(t.name);
            if (!t.gameObject.activeSelf) sb.Append(" [OFF]");
            if (t.localScale != Vector3.one) sb.Append($" scale=({t.localScale.x:0.###},{t.localScale.y:0.###})");
            if (t is RectTransform r)
                sb.Append($" a=({r.anchorMin.x:0.##},{r.anchorMin.y:0.##})-({r.anchorMax.x:0.##},{r.anchorMax.y:0.##}) piv=({r.pivot.x:0.##},{r.pivot.y:0.##}) pos=({r.anchoredPosition.x:0.#},{r.anchoredPosition.y:0.#}) size=({r.sizeDelta.x:0.#},{r.sizeDelta.y:0.#})");
            foreach (Component c in t.GetComponents<Component>())
            {
                switch (c)
                {
                    case Text x: sb.Append($" TEXT['{x.text.Replace("\n", "\\n")}' {(x.font ? x.font.name : "-")} {x.fontSize}{(x.resizeTextForBestFit ? "fit" : "")} {x.alignment} #{ColorUtility.ToHtmlStringRGBA(x.color)}]"); break;
                    case Image x: sb.Append($" IMG[{(x.sprite ? x.sprite.name : "-")} {x.type} #{ColorUtility.ToHtmlStringRGBA(x.color)}]"); break;
                    case Button x: sb.Append($" BUTTON[n#{ColorUtility.ToHtmlStringRGBA(x.colors.normalColor)}]"); break;
                    case Slider x: sb.Append($" SLIDER[{x.minValue}-{x.maxValue}]"); break;
                    case InputField _: sb.Append(" INPUT"); break;
                    case HorizontalOrVerticalLayoutGroup x: sb.Append($" {x.GetType().Name}[sp={x.spacing}]"); break;
                    case ScrollRect _: sb.Append(" SCROLL"); break;
                    case MonoBehaviour x when !(x is UnityEngine.EventSystems.UIBehaviour): sb.Append($" <{x.GetType().Name}>"); break;
                }
            }
            sb.Append('\n');
            foreach (Transform child in t) Walk(child, depth + 1);
        }
        Walk(root, 0);
        return sb.ToString();
    }

    // ---------------------------------------------------------------- rendering

    // Renders the scene through a copy of its camera, with its overlay canvases switched to
    // the camera for the duration (restored afterwards)
    static void RenderScene(Scene scene, Camera source, string outDir, string name, List<string> log)
    {
        foreach (Vector2Int size in Sizes)
        {
            GameObject camObject = new GameObject("UICapture Camera") { hideFlags = HideFlags.HideAndDontSave };
            Camera cam = camObject.AddComponent<Camera>();
            if (source != null)
            {
                cam.CopyFrom(source);
                camObject.transform.SetPositionAndRotation(source.transform.position, source.transform.rotation);
            }
            if (EditorSceneManager.IsPreviewScene(scene))
            {
                SceneManager.MoveGameObjectToScene(camObject, scene);
                cam.scene = scene;
            }
            cam.nearClipPlane = 0.05f;
            cam.enabled = false;

            var rt = new RenderTexture(size.x, size.y, 24, GraphicsFormat.R8G8B8A8_SRGB) { antiAliasing = 4 };
            cam.targetTexture = rt;
            cam.aspect = (float)size.x / size.y;

            var canvases = new List<(Canvas c, RenderMode mode, Camera cam, float plane)>();
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Canvas c in root.GetComponentsInChildren<Canvas>(true))
                {
                    if (!c.isRootCanvas) continue;
                    canvases.Add((c, c.renderMode, c.worldCamera, c.planeDistance));
                    c.renderMode = RenderMode.ScreenSpaceCamera;
                    c.worldCamera = cam;
                    c.planeDistance = 0.5f;
                }
            }
            try
            {
                Canvas.ForceUpdateCanvases();
                cam.Render();
                Canvas.ForceUpdateCanvases();
                cam.Render();

                RenderTexture.active = rt;
                var tex = new Texture2D(size.x, size.y, TextureFormat.RGBA32, false);
                tex.ReadPixels(new Rect(0, 0, size.x, size.y), 0, 0);
                tex.Apply();
                RenderTexture.active = null;
                File.WriteAllBytes(Path.Combine(outDir, $"{name}-{size.x}x{size.y}.png"), tex.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(tex);
            }
            catch (Exception e) { log.Add(name + ": " + e); }
            finally
            {
                foreach (var entry in canvases)
                {
                    entry.c.renderMode = entry.mode;
                    entry.c.worldCamera = entry.cam;
                    entry.c.planeDistance = entry.plane;
                }
                cam.targetTexture = null;
                rt.Release();
                UnityEngine.Object.DestroyImmediate(rt);
                UnityEngine.Object.DestroyImmediate(camObject);
            }
        }
    }
}

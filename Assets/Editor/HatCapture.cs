using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.SceneManagement;

// Dev tool: renders every hat on a drone head to a contact sheet (Temp/HatCaptures/hats-*.png, one
// sheet per view, hats in Hats order, left to right then top to bottom), so hat changes can be
// reviewed without playing, and every face (faces-front.png). Run it from Tools > Capture Hats, or
// create Temp/capture-hats.trigger
// containing an output folder. Everything is built in a preview scene, so nothing open is touched.
static class HatCapture
{
    const string Trigger = "Temp/capture-hats.trigger";
    const int Cell = 384, Columns = 6;

    [InitializeOnLoadMethod]
    static void Hook() => EditorApplication.update += Poll;

    static double nextPoll;

    static void Poll()
    {
        if (EditorApplication.timeSinceStartup < nextPoll) return;
        nextPoll = EditorApplication.timeSinceStartup + 1.0;
        if (!File.Exists(Trigger) || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;
        string outDir = File.ReadAllText(Trigger).Trim();
        File.Delete(Trigger);
        if (string.IsNullOrEmpty(outDir)) outDir = Path.GetFullPath("Temp/HatCaptures");
        Run(outDir);
    }

    [MenuItem("Tools/Capture Hats")]
    static void MenuCapture()
    {
        string outDir = Path.GetFullPath("Temp/HatCaptures");
        Run(outDir);
        EditorUtility.RevealInFinder(outDir);
    }

    // The views: name, yaw round the head (0: from the front), pitch down onto it (negative: up from
    // below), and whether the drone is shown (the inside view shows the hat alone, as when shot off)
    static readonly (string name, float yaw, float pitch, bool drone)[] Views =
    {
        ("front", 30f, 12f, true),
        ("back", 150f, 18f, true),
        ("left", -50f, 10f, true),
        ("inside", 25f, -55f, false),
    };

    static void Run(string outDir)
    {
        Directory.CreateDirectory(outDir);
        var log = new List<string>();
        Scene scene = EditorSceneManager.NewPreviewScene();
        var made = new List<UnityEngine.Object>();
        try
        {
            GameObject lightObject = new GameObject("Sun");
            SceneManager.MoveGameObjectToScene(lightObject, scene);
            Light sun = lightObject.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.6f;
            sun.shadows = LightShadows.Soft;
            lightObject.transform.rotation = Quaternion.Euler(40f, -30f, 0f);
            GameObject fillObject = new GameObject("Fill");
            SceneManager.MoveGameObjectToScene(fillObject, scene);
            Light fill = fillObject.AddComponent<Light>();
            fill.type = LightType.Directional;
            fill.intensity = 0.5f;
            fill.color = new Color(0.75f, 0.82f, 1f);
            fillObject.transform.rotation = Quaternion.Euler(20f, 150f, 0f);

            GameObject camObject = new GameObject("Hat Camera");
            SceneManager.MoveGameObjectToScene(camObject, scene);
            Camera cam = camObject.AddComponent<Camera>();
            cam.scene = scene;
            cam.enabled = false;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.16f, 0.17f, 0.2f);
            cam.fieldOfView = 30f;
            cam.nearClipPlane = 0.05f;
            var rt = new RenderTexture(Cell, Cell, 24, GraphicsFormat.R8G8B8A8_SRGB) { antiAliasing = 8 };
            made.Add(rt);
            cam.targetTexture = rt;

            int rows = (Hats.Count + Columns - 1) / Columns;
            foreach (var view in Views)
            {
                var sheet = new Texture2D(Cell * Columns, Cell * rows, TextureFormat.RGBA32, false);
                made.Add(sheet);
                Color32[] blank = new Color32[sheet.width * sheet.height];
                for (int i = 0; i < blank.Length; i++) blank[i] = new Color32(20, 20, 24, 255);
                sheet.SetPixels32(blank);

                for (int hat = 0; hat < Hats.Count; hat++)
                {
                    GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                    body.name = "Body";
                    SceneManager.MoveGameObjectToScene(body, scene);
                    GameObject head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    head.name = "Head";
                    head.transform.SetParent(body.transform, false);
                    head.transform.localPosition = new Vector3(0f, 1.15f, 0f);
                    head.transform.localScale = Vector3.one * 0.7f;
                    try
                    {
                        Color color = PlayerCap.ColorFor(hat + 1);
                        DroneBody.Dress(body.transform, head.transform, color);
                        PlayerCap cap = PlayerCap.Attach(head.transform, color, false, hat);
                        if (!view.drone)
                        {
                            foreach (Renderer r in body.GetComponentsInChildren<Renderer>())
                            {
                                if (r.GetComponentInParent<PlayerCap>() == null) r.enabled = false;
                            }
                        }
                        // Hats that move: let them settle into a pose (Update doesn't run in edit mode)
                        Bounds bounds = new Bounds(head.transform.position + Vector3.up * 0.15f, Vector3.one * 0.5f);
                        foreach (Renderer r in head.GetComponentInChildren<PlayerCap>().GetComponentsInChildren<Renderer>()) bounds.Encapsulate(r.bounds);

                        Quaternion look = Quaternion.Euler(view.pitch, view.yaw + 180f, 0f);
                        float size = Mathf.Max(bounds.extents.magnitude * 0.85f, 0.4f);
                        float distance = size / Mathf.Sin(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) * 1.02f;
                        Vector3 centre = bounds.center;
                        cam.transform.position = centre - look * Vector3.forward * distance;
                        cam.transform.rotation = look;
                        cam.Render();

                        RenderTexture previous = RenderTexture.active;
                        RenderTexture.active = rt;
                        int col = hat % Columns, row = rows - 1 - hat / Columns;
                        sheet.ReadPixels(new Rect(0, 0, Cell, Cell), col * Cell, row * Cell);
                        RenderTexture.active = previous;
                    }
                    catch (Exception e) { log.Add(Hats.Name(hat) + ": " + e); }
                    finally { UnityEngine.Object.DestroyImmediate(body); }
                }
                sheet.Apply();
                File.WriteAllBytes(Path.Combine(outDir, "hats-" + view.name + ".png"), sheet.EncodeToPNG());
            }

            // The faces (see Faces), straight on, each on a drone in the cap: faces-front.png
            int faceRows = (Faces.Count + Columns - 1) / Columns;
            var faceSheet = new Texture2D(Cell * Columns, Cell * faceRows, TextureFormat.RGBA32, false);
            made.Add(faceSheet);
            for (int face = 0; face < Faces.Count; face++)
            {
                GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                SceneManager.MoveGameObjectToScene(body, scene);
                GameObject head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                head.transform.SetParent(body.transform, false);
                head.transform.localPosition = new Vector3(0f, 1.15f, 0f);
                head.transform.localScale = Vector3.one * 0.7f;
                try
                {
                    Color color = PlayerCap.ColorFor(face + 1);
                    DroneBody.Dress(body.transform, head.transform, color, false, false, face);
                    PlayerCap.Attach(head.transform, color, false, Hats.Cap);
                    Quaternion look = Quaternion.Euler(4f, 180f + 12f, 0f);
                    cam.transform.position = head.transform.position - look * Vector3.forward * 2.2f;
                    cam.transform.rotation = look;
                    cam.Render();
                    RenderTexture previous = RenderTexture.active;
                    RenderTexture.active = rt;
                    faceSheet.ReadPixels(new Rect(0, 0, Cell, Cell), face % Columns * Cell, (faceRows - 1 - face / Columns) * Cell);
                    RenderTexture.active = previous;
                }
                catch (Exception e) { log.Add(Faces.Name(face) + ": " + e); }
                finally { UnityEngine.Object.DestroyImmediate(body); }
            }
            faceSheet.Apply();
            File.WriteAllBytes(Path.Combine(outDir, "faces-front.png"), faceSheet.EncodeToPNG());
        }
        catch (Exception e) { log.Add(e.ToString()); }
        finally
        {
            EditorSceneManager.ClosePreviewScene(scene);
            foreach (UnityEngine.Object o in made) if (o != null) UnityEngine.Object.DestroyImmediate(o);
        }
        File.WriteAllLines(Path.Combine(outDir, "log.txt"), log);
        File.WriteAllText(Path.Combine(outDir, "done.txt"), DateTime.Now.ToString("O"));
        Debug.Log("Hat captures written to " + outDir);
    }
}

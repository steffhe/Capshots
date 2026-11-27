# Unity setup, builds and releases

## Requirements

- Unity **6000.6.9f1** (`ProjectSettings/ProjectVersion.txt`). Use exactly this version to avoid reserializing assets.
- URP, Input System (the only input backend in code), AI Navigation, Test Framework: see `Packages/manifest.json`.
- Photon Fusion 2 SDK is vendored in `Assets/Photon/` (no package install needed).
- Python 3 for the map/texture generators in `Tools/` (standard library only, except `Tools/WeaponDesign` which uses Pillow).

## Opening and running

1. Open the repository folder in Unity Hub with 6000.6.9f1.
2. Open `Assets/Scenes/Menu.unity` and press Play. The menu must be build index 0 (see [maps](../gameplay/maps.md)).
3. **Practice** runs offline against bots. Quick play / create / join need Photon.

## Photon configuration

- App ID: `Assets/Photon/Fusion/Resources/PhotonAppSettings.asset`.
- Network config: `Assets/Photon/Fusion/Resources/NetworkProjectConfig.fusion` (see [fusion.md](../networking/fusion.md#configuration-verified)).
- For a fork, create a free Photon **Fusion** app and set its App ID there, or in `Assets/Editor/FusionSetup.cs` before running **Capshots → Set Up Fusion**. That menu also (re)applies the Player prefab's Fusion components and the map Managers' `NetworkObject`s; run it only if you mean to.
- Matchmaking version = `Application.version` (release tag), so only identical releases meet.

## Testing multiplayer locally

- Build a player (**File → Build Profiles**) and run it alongside the editor, or run two builds; one hosts, the other joins by room code or the room list.
- Unity's Multiplayer Play Mode was the plan in the old migration notes, but the package is not in `Packages/manifest.json`; whether it's set up is **unverified**.
- Pull request CI produces a WebGL test build (see below) for cross-machine tests.
- `NetworkProjectConfig.fusion` has a `NetworkConditions` block (disabled) for simulating latency/loss.

## Editor menus

| Menu | Script |
| --- | --- |
| Capshots → Set Up Fusion | `Assets/Editor/FusionSetup.cs` |
| Capshots → Bake Office Map / Remove Office Map Bake | `Assets/Editor/OfficeMapBaker.cs` |
| Capshots → Test Site 07 → Build Kit / Build Scene / Build Everything / Register In Build And Menu / Validate / Capture Views | `Assets/Editor/TestSite07/` |
| Tools → Capture UI Screenshots | `Assets/Editor/UICapture.cs` |
| Tools → Capture Hats | `Assets/Editor/HatCapture.cs` |

## CI, builds and releases (`.github/workflows/build.yml`)

GameCI builds Windows (`StandaloneWindows64`) and WebGL.

- **Pull requests** that change `Assets/`, `Packages/`, `ProjectSettings/` or the workflow: EditMode tests plus a WebGL test build (artifact on the run). Other pull requests (e.g. docs only) still run every job so the required checks report, but the setup job's `project` output skips Unity and they pass in seconds. PR WebGL builds use "Shorter Build Time" code optimization (`CIWebGLCodeOptimization.cs`); releases use "Runtime Speed". Draft PRs only run tests until marked ready.
- **Pushes to `main`**: tests only (also warms the Library cache). Builds always start from a clean Library.
- **Tags `v*`**: tests, both builds, a GitHub Release with the Windows build, and the WebGL build published to GitHub Pages. The tag without `v` becomes the game version.
- **Manual**: Actions → Build → Run workflow (use this for a Windows test build of a branch).

Release:

```bash
git tag v0.2
```

```bash
git push origin v0.2
```

Required repository secrets: `UNITY_LICENSE` (contents of `Unity_lic.ulf`), `UNITY_EMAIL`, `UNITY_PASSWORD`. Without them PR builds are skipped with a warning.

GitHub Pages one-time setup:

1. Settings → Pages → Build and deployment → Source: **GitHub Actions**.
2. Settings → Environments → `github-pages` → Deployment branches and tags: add a rule for tags matching `v*`.

## Browser (WebGL) specifics

- `AllowClientServerModesInWebGL` is on so browsers can host; a browser host always goes through Photon's relay.
- `Browser.cs` + `Assets/Plugins/WebGL/Browser.jslib`: pointer lock, leave-page guard, invite links, gamepad (`BrowserGamepad`).
- `BrowserGraphics` lowers render scale (down to 60%) when FPS drops below about 52 and forces lighter shadows/FXAA.
- Crouch is bound to `Z` in the browser (Ctrl+W closes tabs).
- Template: `Assets/WebGLTemplates/`.

# Architecture overview

Capshots is a Unity 6 (URP) first-person shooter networked with Photon Fusion 2 in **Host mode**. One player's game is the host: it simulates every body, decides every hit and owns the match state. Every other game is a client that sends input, predicts its own player and draws what the host reports.

Related: [networking](../networking/fusion.md) · [player](../gameplay/player.md) · [weapons](../gameplay/weapons.md) · [match and modes](../gameplay/match-and-modes.md)

## Project layout

| Path | Contents |
| --- | --- |
| `Assets/Scripts/` | All gameplay, UI and audio code (global namespace, default `Assembly-CSharp`) |
| `Assets/Scripts/Core/` | Pure rules with no scene access, in the `Capshots.Core` assembly so EditMode tests can reference them |
| `Assets/Tests/EditMode/` | NUnit EditMode tests (`Capshots.Tests.EditMode`, references `Capshots.Core` and the Fusion DLLs) |
| `Assets/Editor/` | Editor tools: `FusionSetup` (**Capshots → Set Up Fusion**), `OfficeMapBaker`, `TestSite07/*`, `HatCapture`, `UICapture`, `CIWebGLCodeOptimization`, `MapModelPostprocessor` |
| `Assets/Resources/` | Assets loaded by name at runtime: `Player`, `Dead Player`, the four gun-form prefabs, `Bullethole`, materials, `Hats/`, `Maps/` (generated map data), `UI/` |
| `Assets/ScriptableObjects/Guns/` | The four `Gun` assets (one per gun form) |
| `Assets/Scenes/` | `Menu` plus map scenes (see [maps](../gameplay/maps.md)) |
| `Assets/Prefabs/` | UI, misc and map-kit prefabs |
| `Assets/Photon/` | Fusion 2 SDK, Photon Realtime 5 (vendor code, do not edit) |
| `Assets/Plugins/WebGL/Browser.jslib` | JavaScript side of `Browser.cs` |
| `Tools/` | Python generators: `OfficeMap`, `PrototypeMap`, `SurfaceTextures`, `WeaponDesign` |
| `.github/workflows/build.yml` | CI: EditMode tests, WebGL + Windows builds, releases, GitHub Pages |

## Major systems

| System | Main scripts | Runs where |
| --- | --- | --- |
| Session / connection | `NetworkSession` (owns the single `NetworkRunner`, `INetworkRunnerCallbacks`, input collection), `Launcher*` (main menu), `Pause` | Every machine |
| Input | `GameInput` (Input System actions built in code), `InputCollector` + `PlayerInputData` (in `PlayerInputData.cs`), `Look`, `TouchControls`, `BrowserGamepad` | Local; sent to host each tick |
| Player body | `Player` (movement, health, damage, death), `HoverVisuals`, `DroneBody`, `DroneHands`, `PlayerCap`, `PlayerSounds` | Simulated on host + predicted by owner; visuals local |
| Weapon | `Weapon` (firing, energy, form changes, grenades input), `Gun` assets, `GunModel`/`GunMorph`/`GunSatellites`/`GunParts` (looks), `ShotEffects` | Simulated on host + predicted by owner |
| Match | `Manager` partial class (`Manager.*.cs`): state, timer, stats, spawning, teams, modes, bots, grenades, glass, chat, XP, voting, awards, host migration | Host owns; clients mirror |
| Bots | `BotBrain`, `BotNavigation`, `Manager.Bots`, `Core/BotLogic` | Host only |
| UI | `UITheme`, `UITheme.Hud`, `Launcher.Style`/`Launcher.Home`, `FeedbackUI`, `HudFeedback`, `SettingsMenu`, `CustomizeMenu`, `MenuPanel`, `MenuNavigation`, `LoadingScreen`, `ChatUI` | Local |
| Audio | `PlayerSounds`, `MenuAudio`, `MenuMusic`, `VictoryMusic`, `MatchMusic` (sounds and music synthesized in code) | Local |
| Cameras | `DeathCam`, `MapCamera`, `MenuScenery` | Local |
| Persistence | `Data` (profile JSON / PlayerPrefs on WebGL), `GameSettings` (PlayerPrefs), `Core/ProfileData` | Local |

Only three classes are `NetworkBehaviour`s: `Player`, `Weapon` and `Manager`. Everything else is local presentation or plain C#.

## How systems communicate

- **Input → simulation:** `NetworkSession.OnInput` hands Fusion a `PlayerInputData` from `InputCollector`. `Player.FixedUpdateNetwork` and `Weapon.FixedUpdateNetwork` read it via `Player.ReadInput` (bots get theirs from `BotBrain.InputForTick` on the host instead).
- **Simulation → match:** `Player.TakeDamage` (host) marks death; next tick `FinishDeath` calls `Manager.SpawnRagdoll`, `Manager.DropCap` and `Manager.PlayerKilled`, then despawns the body. `Manager.Spawning` respawns it.
- **Match → everyone:** `[Networked]` state on `Manager` (`State`, `MatchTime`, `Stats` dictionary, `HostPlayer`, `AwardCards`) plus `RPC_*` methods for one-off events (kill feed, chat, grenades, cap pickups, votes).
- **Singletons / registries:** `Manager.Instance`, `Player.Local`, `Player.All`, `NetworkSession.Runner`. UI reads these rather than holding references.
- **Pure rules:** gameplay code calls into `Core/*Rules`, `GunEnergy`, `GunTransform`, `ShotRules`, `BotLogic`, etc., so behaviour is testable without a scene.

## Scene and prefab relationships

- `Menu.unity` (build index 0) holds the `Launcher`, whose `maps` array (Inspector) maps each map name to a **build index**. Reordering or adding enabled scenes in Build Settings shifts those indices.
- Each map scene contains a `Manager` GameObject with a `NetworkObject`, plus `spawnPoints` and HUD objects that `Manager` finds by path (e.g. `GameObject.Find("HUD/Stats/Kills")`).
- `Resources/Player.prefab` carries `NetworkObject`, `NetworkTransform`, `CharacterController`, `HitboxRoot` + `Hitbox`es, `Player`, `Weapon`, `Look`. `Capshots → Set Up Fusion` (`Assets/Editor/FusionSetup.cs`) created this wiring.
- Gun-form prefabs (`Resources/Shotgun`, `Machine Gun`, `Pistol`, `Sniper Rifle`) are referenced from the `Gun` assets; `GunModel` rebuilds their visible body in code.
- Much UI, all procedural visuals (drone, hats, gun details) and all sounds are **built at runtime in code** so scenes and prefabs rarely need editing. Prefer extending this pattern over adding scene objects.

## External dependencies

- Unity **6000.6.9f1**, URP 17.6, Input System 1.20.1, AI Navigation 2.0.16, Cinemachine 6.6, Test Framework 1.8 (`Packages/manifest.json`).
- Photon Fusion 2 (SDK in `Assets/Photon/Fusion`) and Photon Realtime 5. Project config: `Assets/Photon/Fusion/Resources/NetworkProjectConfig.fusion`; App ID: `Assets/Photon/Fusion/Resources/PhotonAppSettings.asset`.
- TextMesh Pro, Starter Assets, SimplePoly Town Pack and other art packs under `Assets/`.
- CI: GameCI `unity-test-runner@v4` and `unity-builder@v5`.

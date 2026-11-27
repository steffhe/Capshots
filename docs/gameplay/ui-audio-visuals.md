# UI, audio and visuals

These systems are local presentation: they never decide gameplay and send nothing over the network (except where noted).

## Key pattern: built at runtime

Most UI, visuals and all sound are **created or restyled in code** so scenes and prefabs rarely change:

- `UITheme` / `UITheme.Hud` restyle the original scene UI (colours, Jura font, rounded shapes). `Launcher.Style`, `Launcher.Home` rearrange the main menu at runtime.
- `SettingsMenu`, `CustomizeMenu` (via `MenuPanel`), `ChatUI`, `FeedbackUI`, `HudFeedback`, `LoadingScreen`, `ControlsHint`, `FpsCounter`, `OptionPicker` build their objects in code.
- Drone, hands, hats, faces, gun body and effects are built from primitives and generated meshes (`DroneBody`, `DroneHands`, `PlayerCap`, `SurfacePrint`, `GunModel`, `GunParts`).
- Sounds and music are synthesized in code (`PlayerSounds`, `MenuAudio`, `MenuMusic`, `VictoryMusic`, `MatchMusic`). `MenuSoundHook` attaches menu blips to buttons.

When adding UI, follow this pattern and `UITheme` helpers instead of adding new scene objects, unless the feature clearly needs authored layout.

## Where things live

| Area | Scripts |
| --- | --- |
| Main menu | `Launcher.cs` (rooms, create/join, settings fields wired in `Menu.unity`), `Launcher.Home.cs`, `Launcher.Style.cs`, `MainMenu`, `MenuScenery`, `MenuBattle` (cosmetic bots), `RotateTowards` |
| In-match menus | `Pause.cs`, `SettingsMenu`, `CustomizeMenu`, `Manager.Scoreboard`, `Manager.Voting`, `AwardStage` |
| HUD and feedback | `UITheme.Hud`, `HudFeedback`, `FeedbackUI` (damage flash, kill feed, killed-by), `Manager.CapArrows`, `Manager.RaceScore`, `Manager.Lobby` (lobby card) |
| Controller / touch / browser | `MenuNavigation` (controller menus), `TouchControls`, `BrowserGamepad`, `Browser` + `Assets/Plugins/WebGL/Browser.jslib`, `BrowserGraphics` |
| Cameras | `Look` (player view), `DeathCam`, `MapCamera` (directed spectator/results camera) |
| Effects | `ShotEffects` (pooled), `BeamFade`, `KillSpark`, `CapshotBurst`, `NeckSparks`, `HoverVisuals` |
| Settings and saves | `GameSettings` (PlayerPrefs), `Data` (profile JSON; PlayerPrefs on WebGL), `GameInput` (rebinds in PlayerPrefs `input.bindings`) |

## Notes

- Colour-blind mode changes team and player colours (`PlayerCap.ColorFor`, team colours); use those helpers rather than hard-coded colours.
- HUD scales with screen height (phones and wide screens).
- Editor capture tools: **Tools → Capture UI Screenshots** (`UICapture`), **Tools → Capture Hats** (`HatCapture`).

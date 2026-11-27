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

## Player camera and look (`Look`, `Player.UpdateCamera`, local player only)

- **Mouse:** no smoothing or acceleration. Degrees per count = sensitivity x `GameInput.MouseScale` (0.1) / 60 (`Look.DegreesPerCount`); the settings show the X value as cm per 360 at 800 DPI (`LookRules.CentimetresPer360`). New profiles start at 60 (0.1 degrees per count, about 11 cm per turn); saved profiles keep theirs. The DPI readout assumes raw mouse counts; browsers' pointer-lock movement may be scaled by the OS (unverified).
- **Gamepad stick:** separate from the mouse. A response curve (`LookRules.StickResponse`, exponent 2, round the stick so diagonals keep their direction) after the Input System's deadzone: half a push turns at a quarter speed. A full push turns `Look.StickYawSpeed` (180) and `StickPitchSpeed` (120) degrees a second times STICK SENSITIVITY (`GameSettings.StickSensitivity`, 0.3-2.5, default 1, shown in degrees a second). Aim assist slowdown and zoom-matched aiming apply as for the mouse.
- **Latency:** the mouse is read once per frame by `Look.SampleLook`, called from `Look.Update` and from `InputCollector.Take` before it sends `Yaw`/`Pitch`, whichever runs first, so a tick's shots use the aim drawn that frame. The view is applied in `LateUpdate`.
- **Sprint** is held, or toggled with the SPRINT setting (`GameSettings.ToggleSprint`); a controller's stick click always toggles. A toggled sprint stops when the player stops moving forwards (`GameInput.SprintWanted`).
- **Pitch** is limited to +-`Look.maxAngle` (85 in the prefab), which `InputRules.Sanitize` also enforces on the host.
- **Aiming:** turning slows by the zoom (`LookRules.ZoomSensitivity`: the ratio of the half-angle tangents of the aim and normal views; the scope's `Gun.scopeFov` or `Player.AimFovMultiplier` x the setting), times the AIM SENSITIVITY multiplier.
- **Field of view:** the setting (60-110, vertical), narrowed while aiming. With CAMERA EFFECTS on, up to +5 degrees at sprinting speed (from the real speed, `Player.HorizontalSpeed`) and +8 while sliding, capped at 120. Eased with `LookRules.Ease`, which matches the old 60 FPS feel at any frame rate.
- **Shake:** firing shake (`Gun.shake`) and the kill/grenade punch only roll the view, which leaves the crosshair on the real aim; their up and sideways kick moves the gun model. Off with CAMERA EFFECTS (`GameSettings.ScreenShake`). Recoil moves the real aim and is always on.
- **Crouching** lowers the view within about 0.15 s, matching shots, which start from the crouched height straight away (`Player.EyePosition`).
- No head bob; the gun drifts and trails slightly (`Player.UpdateWeaponHover`) and rides the hover cushion. The camera sits inside the 0.5 m capsule (near clip 0.2), so it doesn't clip walls; as players pass through each other, it can end up inside another player's body.

## Notes

- Colour-blind mode changes team and player colours (`PlayerCap.ColorFor`, team colours); use those helpers rather than hard-coded colours.
- HUD scales with screen height (phones and wide screens).
- Editor capture tools: **Tools → Capture UI Screenshots** (`UICapture`), **Tools → Capture Hats** (`HatCapture`).

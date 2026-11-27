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
| HUD and feedback | `UITheme.Hud` (layout and tokens), `HudFeedback` (health, weapon cluster, crosshair, kill confirmation), `FeedbackUI` (damage flash, kill feed, death screen), `Manager.ScoreStrip`, `Manager.CapArrows`, `Manager.Lobby` (lobby card), `Core/HudRules` |
| Controller / touch / browser | `MenuNavigation` (controller menus), `TouchControls`, `BrowserGamepad`, `Browser` + `Assets/Plugins/WebGL/Browser.jslib`, `BrowserGraphics` |
| Cameras | `Look` (player view), `DeathCam`, `MapCamera` (directed spectator/results camera) |
| Effects | `ShotEffects` (pooled), `BeamFade`, `KillSpark`, `CapshotBurst`, `NeckSparks`, `HoverVisuals` |
| Settings and saves | `GameSettings` (PlayerPrefs), `Data` (profile JSON; PlayerPrefs on WebGL), `GameInput` (rebinds in PlayerPrefs `input.bindings`) |

## Combat HUD

Laid out by `UITheme.StyleHud` on the map's `HUD` prefab when the map loads (the prefab objects stay where `Player`, `Weapon` and `Manager` look them up by path; only their look and place change), then driven by `HudFeedback`, `FeedbackUI` and `Manager`. Four clusters round a clear centre, canvas units at 1080 high:

| Where | What | Driven by |
| --- | --- | --- |
| Bottom left | Health: the number over a slim bar marked in quarters, with a red trail for damage just taken | `Player.RefreshHealthBar` → `HudFeedback.SetHealth` |
| Bottom right | Weapon: shots left (`HUD/Ammo/Text`), a status line left of it (the form's name, or SWAPPING CELL / TRANSFORMING / NO ENERGY), a segmented energy meter under both, the form strip above (`HUD/Arsenal`) and the plasma grenade above that | `Weapon.RefreshAmmo` → `HudFeedback.SyncWeapon` / `SetGunState`; grenade: `Weapon.RefreshGrenadeIndicator` |
| Top centre | Match clock with a score plate either side and the goal line under it; stays up while dead | `Manager.RefreshTimerUI`, `Manager.ScoreStrip` |
| Top right | Kill feed | `FeedbackUI` |
| Centre | Dot, spread ticks, hit marker, kill ring, cap shot stamp | `Weapon` (dot alpha, hit marker), `HudFeedback` |

- **Colour means one thing each** (tokens in `UITheme.Hud`): white and muted white for information, accent blue for you and the interface, `Capshot` gold only for cap shots, `Danger` red only for being hurt or out of energy, `Warning` amber for running low, `Shield` light blue for spawn protection. Health is white, amber from half and red from a quarter (`HudRules.StateFor`), as steps rather than a blend.
- **Energy meter:** one segment per shot in a full cell; a form with more than 12 (the machine gun) gets a smooth bar (`HudRules.EnergySegments`). The segment being refilled is drawn faint until that shot is ready. Swapping the cell sweeps an accent bar across the meter.
- **Form strip:** one slot per form with its number key; the active one is lit and underlined, a change of form fills the underline in under the new one. In Gun Game the forms the ladder hasn't given you are dimmed without keys. Hidden with touch controls (the SWAP button covers it), as is the grenade.
- **Score strip:** team modes show your team on the left, the other on the right, and "YOU'RE BLUE · FIRST TO 50 KILLS". Free for all and Cap Grab show YOU against the LEADER, or the 2ND while you lead outright (`HudRules.OpponentLabel`). Gun Game shows the form each is on ("2/4") and the kills to the next form. The plates show from the countdown on; the goal line throughout.
- **Death screen** (`FeedbackUI.ShowDeath`): ELIMINATED BY and the killer's name, the weapon (and CAPSHOT), and a respawn countdown. The countdown starts when the kill feed message arrives and runs `Manager.respawnTime`, so it's an estimate of the host's respawn; at zero it reads RESPAWNING until the body appears. A fall reads YOU FELL, your own grenade SELF-ELIMINATED.
- **Ultrawide:** the HUD and kill feed keep to a centred area no wider than twice the height (`HudRules.SafeInset`), so on 21:9 and wider the corners don't drift away from the crosshair. Full-screen effects (damage flash, low-health edge) still cover the whole screen.
- The player's own name and the kills / deaths lines are still in the prefab and still written to, but not drawn (`Text.enabled = false`); the scoreboard and score strip carry that information. The XP popup is copied from the Kills text and turns its copy back on.

## Combat feedback

Local presentation driven only by the host's RPCs, so it shows what the host decided (never a predicted hit that didn't count) and each event once.

| Event | Source (host) | Shown by | When |
| --- | --- | --- | --- |
| Hit / kill confirmation | `Weapon.RPC_HitConfirm` → shooter (once per target a shot or blast hurt; host forward ticks only, per-target damage merged per shot) | `FeedbackUI.ConfirmHit` → `HudFeedback.HitMarker`, `Confirmed`; hit / kill sound | Tick of the hit |
| Spawn shield hit | `Weapon.RPC_ShieldHit` → shooter | `FeedbackUI.ShieldHit` | Tick of the hit |
| Damage taken | `Player.RPC_Damaged` → victim (from the attacker, or a grenade's blast via `TakeDamage`'s `damageSource`) | `FeedbackUI.Damaged`: edge flash, direction arc | Tick of the hit |
| Kill | `Manager.RPC_KillFeed` → everyone (from `FinishDeath`, the tick after the kill) | Kill feed row; for the killer "ELIMINATED name", callouts, XP; for the victim the death screen | Tick after the kill |

- **One per frame:** confirmations that land in the same frame (a shotgun blast or grenade catching several players) are merged in `FeedbackUI.LateUpdate`: one marker for the strongest (`HudRules.TierFor`) and one sound, and one view punch (`Weapon.RPC_HitConfirm`).
- **Hit marker tiers** (`HudFeedback`, four diagonal ticks that snap in round the crosshair): hit (white, sized and held by the hit's share of the target's health), head hit (gold, longer), kill (white, heavier, drifting out, with a ring), cap shot kill (gold, heaviest, with a gold ring and the cap logo and CAPSHOT stamped above the crosshair), spawn shield (small, light blue). Red is never used for your hits. The scene's own `Hitmark` image is kept hidden.
- **Kill sound once:** it plays with the kill's hit confirmation. The kill feed plays it only when that can't have arrived because the shooter's body is gone (a trade, or a grenade thrown before dying) and none has just played (`HudRules.KillSoundFallback`).
- **ELIMINATED line:** under the crosshair, from the kill feed, the victim's name (gold for a cap shot); kills within 1.6 s list up to three names together.
- **Damage:** a red glow at the screen's edges (the middle stays clear) and a red arc round the crosshair towards each source, which keeps pointing there as you turn. Hits within 35 degrees of a showing arc refresh it (`HudRules.SameDirection`); up to four at once. A grenade points at the blast, including your own; one stuck to you has no direction.
- **Kill feed:** names escaped (`ChatUI.Escape`), weapons upper case, gold only for cap shots, your own name in the accent in free for all (team colours in team modes) and an accent stripe on rows involving you. A death nobody else caused reads "[FELL]  NAME". New rows slide in.
- While the HUD is hidden (dead), markers aren't started, so nothing replays on the next spawn; sounds and the kill feed still play.

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

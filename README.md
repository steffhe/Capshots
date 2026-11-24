<p align="center"><img src="Assets/Resources/UI/CapshotsWordmark.png" alt="Capshots" width="560"></p>

# Capshots

Capshots (formerly MultiplayerFPS) is a simple online multiplayer first-person shooter made in Unity, built while following Welton King's YouTube tutorial series. Networking is handled by [Photon Fusion](https://www.photonengine.com/fusion) in Host mode: one player hosts the match and decides hits and scores, with lag compensation so shots count where they landed on the shooter's screen. Photon's cloud matches players and connects them to the host (through its relay when a direct connection isn't possible), so nobody needs to run a server.

## Play

**In your browser:** go to **[steffhe.github.io/Capshots](https://steffhe.github.io/Capshots/)**. It works in desktop browsers (Chrome, Edge or Firefox) and on phones and tablets, which get on-screen touch controls (see below). On a computer, click the game to lock the mouse. **Esc** releases the mouse and opens the pause menu; click again after resuming to lock it. Crouch is on `Z` in the browser, because Ctrl+W would close the tab. During a match the browser asks before closing or reloading the tab.

**On Windows:**

1. Go to the [latest release](https://github.com/steffhe/Capshots/releases/latest).
2. Download the build from the **Assets** section and extract it.
3. Run the `.exe` file and enjoy!

To play together, each player runs the game, picks a username and either creates a room or joins one from the room list. Browser and Windows players on the same release can play in the same rooms.

## Features

- Online multiplayer through Photon Fusion: host rooms, browse and join open rooms, or quick-join a random one
- Room options: game mode, map, bots, max players and kills to win
- Bots: the host can fill a room with up to 7 computer players. They find their way around every map, hunt down enemies, aim with a short reaction time and some inaccuracy, change gun form for the distance and swap energy cells like anyone else. They play by the same rules as people (same movement, gun, damage and spawn protection), show on the scoreboard and kill feed with BOT in their name, and make way when people join: a bot leaves when someone joins a full room, and comes back when they leave. In Team Deathmatch they fill both teams evenly
- Two game modes: Free For All, and Team Deathmatch (blue vs red, automatic team balancing, no friendly fire, first team to the kill limit wins)
- Several maps to choose from, including Office: a real office floor with glass-walled offices and meeting rooms around a courtyard garden. Its glass walls crack when shot and shatter on the second hit, leaving a gap to walk and shoot through
- Warmup until enough players have joined, then a countdown before the match starts
- One transforming energy gun with four forms, shortest range first: an 8-pellet shotgun, machine gun, pistol and sniper rifle. You spawn holding the machine gun, or whichever form you pick in **Settings → Starting gun** (in the main menu or the pause menu); the player model in the main menu holds it too, with its hand-orbs and floating pieces, and fires a pretend shot now and then to show them off. Changing form reshapes the gun in half a second, during which it can't fire. All forms share one energy cell that refills by itself shortly after you stop firing; `R` (or firing on empty) swaps in a fresh cell. The gun is dark, satin gunmetal, and part of it is made of floating pieces of the same metal, each with a glowing strip in the player's colour, which re-form into different parts for each form: the sniper's barrel is a line of floating segments that a shot ripples down; the machine gun has a rotary barrel of slats that spins up as you fire; the shotgun's muzzle is a claw of petals that blooms open with each blast, with pump plates that rack back after it; and the pistol has side panels that recoil like a slide. They're always moving, tighten up while you aim, trail the gun slightly as it moves, dim as the energy runs down and drift loose while the cell is swapped, so you can tell anyone's form at a glance. Iron sights, a scope on the sniper form, and weapon sway; laser beams and muzzle flashes glow in the shooter's colour (the team colour in Team Deathmatch)
- Hovering players: every player is a little drone with a capsule body, a round head with a glowing face screen and blinking eyes, a thruster and two floating hand-orbs that hold the gun, in their own (or their team's) colour; it floats, bobs and leans into its movement over a glowing disc, and the gun drifts instead of bobbing with footsteps
- Weapon balance: damage falls off over distance, per-weapon headshot multipliers, heavier guns and aiming slow you down, and shots spread more while moving or jumping
- Sprinting, jumping, crouching and sliding
- Smarter spawns (away from enemies and out of their sight) and 3 seconds of spawn protection (other players see you semi-transparent), which ends early if you shoot
- Health regeneration after 5 seconds without taking damage
- Every player wears a cap: the team colour in Team Deathmatch, otherwise a colour of their own
- Physics ragdolls, with X-es for eyes and a broken neck that sparks and crackles as it shorts out: the body is thrown along the killing bullet's path, and the head (cap and all) comes off and flies separately, harder on a headshot
- Players drop an ammo box when they die; walk over it to refill your gun's energy
- Scoreboard with kills, deaths and ping
- Text chat
- Settings menu, in the main menu and the pause menu: field of view, aim sensitivity, volume, graphics quality, starting gun and key rebinding
- Gamepad support (Unity Input System)
- Graphics: colour grading, bloom, vignette, soft shadows, anti-aliasing (SMAA) and light fog on every map
- Sound: a distinct sci-fi shot for each gun, a hovering hum and woosh while moving (silent while crouching), a soft landing puff and energy-cell reload sounds, all heard in 3D from where they happen
- Combat feedback: camera recoil, muzzle flash, hit and kill sounds (a grander one for a cap shot kill, a headshot, which also bursts into confetti and stars; an ordinary kill makes the drone short out in a spark and a zap), a red kill marker that pops out big, a little shake of your view on a kill, a damage flash with a direction indicator, a kill feed and a "killed by" screen. When you die you hear your drone power down, and the camera lingers on your body for a moment, following your head (or your cap, on a headshot) as it flies off, before fading to the map view. You respawn 5 seconds after dying
- A directed map view while you wait to respawn and over the match results: the camera scouts each map for high vantage points, drifts slowly through each shot, keeps the players running around in frame and cuts to whichever angle shows the most action
- Levels and XP: earn XP for kills, finishing matches and winning, saved to your profile between sessions

## Controls

Keyboard and mouse controls can be changed in **Settings**, from the main menu or **Pause → Settings**. A gamepad also works.

| Action | Keyboard / mouse | Gamepad |
| --- | --- | --- |
| Move | `W` `A` `S` `D` / arrow keys | Left stick |
| Look | Mouse | Right stick |
| Shoot | Left mouse button | Right trigger |
| Aim | Right mouse button | Left trigger |
| Swap energy cell | `R` | X / Square |
| Change gun form | `1` shotgun, `2` machine gun, `3` pistol, `4` sniper / mouse wheel (wraps round) | Bumpers (wrap round) |
| Sprint | `Shift` | Left stick press |
| Jump | `Space` | A / Cross |
| Crouch | `Left Ctrl` (`Z` in the browser) | B / Circle |
| Slide | `C` (while sprinting) | Right stick press |
| Scoreboard | Hold `Tab` | View / Select |
| Chat | `T` or `Enter` | - |
| Pause | `Esc` | Menu / Start |

### Touch controls (phones and tablets)

Hold the device sideways. On phones that allow it (Android), the game goes fullscreen on your first tap.

- **Move:** put your thumb anywhere on the lower left of the screen and drag; the joystick follows your thumb. Push it far forward to sprint (the knob turns yellow).
- **Look:** drag anywhere else on the screen. Dragging on the **FIRE** button turns the view too, so you can shoot and aim with one thumb.
- **AIM** is a toggle: tap to aim down sights, tap again to stop.
- **CROUCH** crouches when standing, and slides when sprinting.
- **JUMP**, **RELOAD** and **SWAP** (next gun form; after the sniper it goes back to the shotgun) are buttons on the right; **SCORE** (hold) and **II** (pause) are at the top.
- A keyboard, mouse or trackpad connected to a tablet works as on a computer. Using the mouse or trackpad hides the on-screen controls, and touching the screen brings them back. The keyboard works alongside them, including **Esc** to pause and **T** to chat.
- Without a keyboard, chat isn't available.

Look sensitivity for touch follows the aim sensitivity in **Pause → Settings**.

## Building from source

Requirements:

- [Unity](https://unity.com/download) **6000.6.9f1** (Unity 6)

Steps:

1. Clone the repository:
   ```bash
   git clone https://github.com/steffhe/Capshots.git
   ```
2. Open the project folder in Unity Hub with the editor version above.
3. Open `Assets/Scenes/Menu.unity` and press Play, or build via **File → Build Profiles**. The menu scene must be first in the scene list, followed by the map scenes.

Photon Fusion is configured in `Assets/Photon/Fusion/Resources/PhotonAppSettings.asset` (App ID) and `NetworkProjectConfig.fusion`. If you fork the project, create your own free Photon **Fusion** app and put its App ID there (or in `Assets/Editor/FusionSetup.cs` before running **Capshots → Set Up Fusion**).

## Tests

The tests in `Assets/Tests/EditMode` check gun energy, damage falloff, XP and the bots' decisions (room filling, gun choice, aiming and steering). Run them in Unity from **Window → General → Test Runner → EditMode → Run All**. The build workflow also runs them alongside every build, and only publishes a release when they pass.

## Releasing

Builds are made automatically by GitHub Actions with [GameCI](https://game.ci) (`.github/workflows/build.yml`):

- **Release:** push a version tag. The Windows build is attached to a new GitHub Release, and the browser version is published to GitHub Pages:
  ```bash
  git tag v0.2
  ```
  ```bash
  git push origin v0.2
  ```
  The tag (without the `v`) becomes the game's version, which is also used as Photon's matchmaking version, so players on different releases never end up in the same room.
- **Pull requests** that change the project get a browser test build. Download it from the workflow run's **Artifacts**. It's built with WebGL's "Shorter Build Time" code optimization, so it builds faster but runs slower than a release, which uses "Runtime Speed". For a Windows test build, use **Run workflow** on the branch (Actions → Build). Draft pull requests only run the tests; the build starts when you mark the pull request ready for review.
- **Merges to `main`** only run the tests, not the builds: the pull request already built that code, and a release builds it again with its real version. The test run on `main` saves Unity's Library cache for the tests in later pull requests. Builds always start from a clean Library.
- **On demand:** run the **Build** workflow from the **Actions** tab.

The workflow needs three repository secrets (**Settings → Secrets and variables → Actions**): `UNITY_LICENSE` (the contents of your Unity license file, `Unity_lic.ulf`), `UNITY_EMAIL` and `UNITY_PASSWORD`. Without them, pull request builds are skipped with a warning.

Publishing the browser version needs GitHub Pages set up once:

1. **Settings → Pages → Build and deployment → Source:** choose **GitHub Actions**.
2. **Settings → Environments → github-pages → Deployment branches and tags:** add a rule for tags matching `v*`. By default only the `main` branch may publish there, and releases are published from tags.

## Project structure

| Path | Contents |
| --- | --- |
| `Assets/Scripts` | Game code: launcher/lobby, player movement, weapons, input, settings, chat, pause menu |
| `Assets/Scripts/Manager*.cs` | The match manager, split by topic: match flow (`Manager.cs`), teams, scoreboard, spawning, bots, pickups, chat and XP |
| `Assets/Scripts/Bot*.cs` | Bots: `BotBrain` turns what a bot sees into player input on the host, `BotNavigation` builds the map's walkable area when a match with bots starts |
| `Assets/Scripts/Core` | Game logic with no scene dependencies (`Gun`, `ProfileData`, `BotLogic`), in its own assembly so it can be tested |
| `Assets/Tests/EditMode` | Automated tests for the `Core` code |
| `Assets/Scenes` | `Menu` plus the playable maps |
| `Assets/Resources` | Prefabs loaded at runtime: the networked player, weapons, ragdoll and bullet holes |
| `Assets/Prefabs` | UI and misc prefabs |
| `Assets/Photon` | Photon Fusion 2 networking SDK (with Photon Realtime 5) |
| `Tools/OfficeMap` | Generator for the Office map: the floor plan layout and the script that turns it into `Assets/Resources/Maps/Office.txt` |

## Credits

- Tutorial: Welton King's YouTube series on making a multiplayer FPS in Unity
- Networking: Photon Fusion by Exit Games
- Assets: Robot Kyle, SimplePoly Town Pack and Unity Starter Assets

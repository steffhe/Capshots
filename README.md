# MultiplayerFPS

A simple online multiplayer first-person shooter made in Unity, built while following Welton King's YouTube tutorial series. Networking is handled by [Photon PUN](https://www.photonengine.com/pun), so players can host and join rooms over the internet without running their own server.

## Play

**In your browser:** go to **[steffhe.github.io/MultiplayerFPS](https://steffhe.github.io/MultiplayerFPS/)**. It needs a desktop browser (Chrome, Edge or Firefox) and a mouse; phones and tablets aren't supported. Click the game to lock the mouse. **Esc** releases the mouse and opens the pause menu; click again after resuming to lock it. Crouch is on `Z` in the browser, because Ctrl+W would close the tab. During a match the browser asks before closing or reloading the tab.

**On Windows:**

1. Go to the [latest release](https://github.com/steffhe/MultiplayerFPS/releases/latest).
2. Download the build from the **Assets** section and extract it.
3. Run the `.exe` file and enjoy!

To play together, each player runs the game, picks a username and either creates a room or joins one from the room list. Browser and Windows players on the same release can play in the same rooms.

## Features

- Online multiplayer through Photon: create rooms, browse and join open rooms, or quick-join a random one
- Room options: game mode, map, max players and kills to win
- Two game modes: Free For All, and Team Deathmatch (blue vs red, automatic team balancing, no friendly fire, first team to the kill limit wins)
- Several maps to choose from
- Warmup until enough players have joined, then a countdown before the match starts
- Three weapons (pistol, machine gun and sniper rifle) with aiming, reloading and weapon sway
- Weapon balance: damage falls off over distance, per-weapon headshot multipliers, heavier guns and aiming slow you down, and shots spread more while moving or jumping
- Sprinting, jumping, crouching and sliding
- Smarter spawns (away from enemies and out of their sight) and 3 seconds of spawn protection, which ends early if you shoot
- Health regeneration after 5 seconds without taking damage
- Players drop an ammo box when they die; walk over it to refill your reserve ammo
- Scoreboard with kills, deaths and ping
- Text chat
- Settings menu: field of view, aim sensitivity, volume, graphics quality and key rebinding
- Gamepad support (Unity Input System)
- Graphics: colour grading, bloom, vignette, soft shadows, anti-aliasing (SMAA) and light fog on every map
- Combat feedback: camera recoil, muzzle flash, hit and kill sounds, a damage flash with a direction indicator, a kill feed and a "killed by" screen
- Levels and XP: earn XP for kills, finishing matches and winning, saved to your profile between sessions

## Controls

Keyboard and mouse controls can be changed in **Pause → Settings**. A gamepad also works.

| Action | Keyboard / mouse | Gamepad |
| --- | --- | --- |
| Move | `W` `A` `S` `D` / arrow keys | Left stick |
| Look | Mouse | Right stick |
| Shoot | Left mouse button | Right trigger |
| Aim | Right mouse button | Left trigger |
| Reload | `R` | X / Square |
| Switch weapon | `1` `2` `3` / mouse wheel | Bumpers |
| Sprint | `Shift` | Left stick press |
| Jump | `Space` | A / Cross |
| Crouch | `Left Ctrl` (`Z` in the browser) | B / Circle |
| Slide | `C` (while sprinting) | Right stick press |
| Scoreboard | Hold `Tab` | View / Select |
| Chat | `T` or `Enter` | - |
| Pause | `Esc` | Menu / Start |

## Building from source

Requirements:

- [Unity](https://unity.com/download) **6000.6.9f1** (Unity 6)

Steps:

1. Clone the repository:
   ```bash
   git clone https://github.com/steffhe/MultiplayerFPS.git
   ```
2. Open the project folder in Unity Hub with the editor version above.
3. Open `Assets/Scenes/Menu.unity` and press Play, or build via **File → Build Profiles**. The menu scene must be first in the scene list, followed by the map scenes.

Photon is configured in `Assets/Photon/PhotonUnityNetworking/Resources/PhotonServerSettings.asset`. If you fork the project, consider creating your own free Photon app and replacing the App ID there.

## Tests

The tests in `Assets/Tests/EditMode` check gun ammo, damage falloff and XP. Run them in Unity from **Window → General → Test Runner → EditMode → Run All**. The build workflow also runs them alongside every build, and only publishes a release when they pass.

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
- **Pull requests** that change the project get test builds for Windows and the browser. Download them from the workflow run's **Artifacts**. Draft pull requests only run the tests; the builds start when you mark the pull request ready for review.
- **Merges to `main`** only run the tests, not the builds: the pull request already built that code, and a release builds it again with its real version. The test run on `main` saves Unity's Library cache, which later pull requests start from. Pull request builds use the cache and build again from a clean Library if that fails; release builds always start clean.
- **On demand:** run the **Build** workflow from the **Actions** tab.

The workflow needs three repository secrets (**Settings → Secrets and variables → Actions**): `UNITY_LICENSE` (the contents of your Unity license file, `Unity_lic.ulf`), `UNITY_EMAIL` and `UNITY_PASSWORD`. Without them, pull request builds are skipped with a warning.

Publishing the browser version needs GitHub Pages set up once:

1. **Settings → Pages → Build and deployment → Source:** choose **GitHub Actions**.
2. **Settings → Environments → github-pages → Deployment branches and tags:** add a rule for tags matching `v*`. By default only the `main` branch may publish there, and releases are published from tags.

## Project structure

| Path | Contents |
| --- | --- |
| `Assets/Scripts` | Game code: launcher/lobby, player movement, weapons, input, settings, chat, pause menu |
| `Assets/Scripts/Manager*.cs` | The match manager, split by topic: match flow (`Manager.cs`), teams, scoreboard, spawning, pickups, chat and XP |
| `Assets/Scripts/Core` | Game logic with no scene dependencies (`Gun`, `ProfileData`), in its own assembly so it can be tested |
| `Assets/Tests/EditMode` | Automated tests for the `Core` code |
| `Assets/Scenes` | `Menu` plus the playable maps |
| `Assets/Resources` | Networked prefabs spawned through Photon (player, weapons, bullet holes) |
| `Assets/Prefabs` | UI and misc prefabs |
| `Assets/Photon` | Photon PUN networking package |

## Credits

- Tutorial: Welton King's YouTube series on making a multiplayer FPS in Unity
- Networking: Photon PUN by Exit Games
- Assets: Robot Kyle, SimplePoly Town Pack and Unity Starter Assets

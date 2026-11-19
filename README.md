# MultiplayerFPS

A simple online multiplayer first-person shooter made in Unity, built while following Welton King's YouTube tutorial series. Networking is handled by [Photon PUN](https://www.photonengine.com/pun), so players can host and join rooms over the internet without running their own server.

## Play

1. Go to the [latest release](https://github.com/steffhe/MultiplayerFPS/releases/latest).
2. Download the build from the **Assets** section and extract it.
3. Run the `.exe` file and enjoy!

To play together, each player runs the game, picks a username and either creates a room or joins one from the room list.

## Features

- Online multiplayer through Photon: create rooms, browse and join open rooms, or quick-join a random one
- Room options: game mode, map, max players and kills to win
- Two game modes: Free For All, and Team Deathmatch (blue vs red, automatic team balancing, no friendly fire, first team to the kill limit wins)
- Several maps to choose from
- Warmup until enough players have joined, then a countdown before the match starts
- Three weapons (pistol, machine gun and sniper rifle) with aiming, reloading and weapon sway
- Sprinting, jumping, crouching and sliding
- Smarter spawns (away from enemies and out of their sight) and 3 seconds of spawn protection, which ends early if you shoot
- Health regeneration after 5 seconds without taking damage
- Players drop an ammo box when they die; walk over it to refill your reserve ammo
- Scoreboard with kills and deaths
- Combat feedback: camera recoil, muzzle flash, hit and kill sounds, a damage flash with a direction indicator, a kill feed and a "killed by" screen
- Levels and XP: earn XP for kills, finishing matches and winning, saved to your profile between sessions

## Controls

| Action | Key |
| --- | --- |
| Move | `W` `A` `S` `D` |
| Look | Mouse |
| Shoot | Left mouse button |
| Aim | Right mouse button |
| Reload | `R` |
| Switch weapon | `1` `2` `3` / mouse wheel |
| Sprint | `Shift` |
| Jump | `Space` |
| Crouch | `Left Ctrl` |
| Slide | `C` (while sprinting) |
| Scoreboard | Hold `Tab` |
| Pause | `Esc` |

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

## Releasing

Builds are made automatically by GitHub Actions with [GameCI](https://game.ci) (`.github/workflows/build.yml`):

- **Release:** push a version tag, and the Windows build is attached to a new GitHub Release:
  ```bash
  git tag v0.2
  ```
  ```bash
  git push origin v0.2
  ```
  The tag (without the `v`) becomes the game's version, which is also used as Photon's matchmaking version, so players on different releases never end up in the same room.
- **Pull requests** that change the project get a test build. Download it from the workflow run's **Artifacts**.
- **On demand:** run the **Build** workflow from the **Actions** tab.

The workflow needs three repository secrets (**Settings → Secrets and variables → Actions**): `UNITY_LICENSE` (the contents of your Unity license file, `Unity_lic.ulf`), `UNITY_EMAIL` and `UNITY_PASSWORD`. Without them, pull request builds are skipped with a warning.

## Project structure

| Path | Contents |
| --- | --- |
| `Assets/Scripts` | Game code: launcher/lobby, player movement, weapons, match manager, pause menu |
| `Assets/Scenes` | `Menu` plus the playable maps |
| `Assets/Resources` | Networked prefabs spawned through Photon (player, weapons, bullet holes) |
| `Assets/Prefabs` | UI and misc prefabs |
| `Assets/Photon` | Photon PUN networking package |

## Credits

- Tutorial: Welton King's YouTube series on making a multiplayer FPS in Unity
- Networking: Photon PUN by Exit Games
- Assets: Robot Kyle, SimplePoly Town Pack and Unity Starter Assets

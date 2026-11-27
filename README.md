<p align="center"><img src="Assets/Resources/UI/CapshotsWordmark.png" alt="Capshots" width="560"></p>

# Capshots

Capshots (formerly MultiplayerFPS) is a small online multiplayer first-person shooter made in Unity, started from Welton King's YouTube tutorial series. Every player is a little hovering drone wearing a cap, carrying one energy gun that transforms between four forms. Knock caps off with headshots, fight bots or friends, and play in the browser or on Windows.

Networking uses [Photon Fusion](https://www.photonengine.com/fusion) in Host mode: one player hosts and decides hits and scores, with lag compensation so shots count where they landed on the shooter's screen. Photon's cloud handles matchmaking and relaying, so nobody needs to run a server.

## Play

**In your browser:** [steffhe.github.io/Capshots](https://steffhe.github.io/Capshots/) (Chrome, Edge or Firefox on desktop; phones and tablets get touch controls). Click the game to lock the mouse; **Esc** releases it and opens the pause menu. A controller works after you press a button on it with the game open.

**On Windows:** download the build from the [latest release](https://github.com/steffhe/Capshots/releases/latest), extract it and run the `.exe`.

Pick a username, then **Quick play**, create a room, join one from the list, type a friend's 4-letter room code, or open an invite link. **Practice** plays offline against bots. Browser and Windows players on the same release can play together.

## Features

- **Online multiplayer** (Photon Fusion, Host mode): room list, quick play (starts a room with bots if none is open), room codes and invite links, a ready-up lobby for created rooms, and host migration so a match survives the host leaving.
- **Five game modes:** Free For All, Team Deathmatch, Cap Grab (a kill drops the victim's cap; grab it to score), Team Cap Grab, and Gun Game (climb through the gun forms, two kills each).
- **Room options:** map, mode, bots and their skill, max players, and winning by kills (up to 50) or by time (1–20 minutes).
- **Bots:** up to 7, Easy/Normal/Hard, with personalities (balanced, rusher, sniper, grenadier). They play by the same rules as people and make room when people join.
- **One transforming energy gun:** shotgun, machine gun, pistol and sniper rifle forms sharing one self-refilling energy cell, each best at its own range, with headshot multipliers, damage falloff, aim-down-sights and a sniper scope.
- **Sticky plasma grenade:** cook it, throw it; it sticks to enemies. Recharges after 15 seconds.
- **Movement:** sprint, jump, crouch and slide.
- **Fair play touches:** smart spawns away from enemies, 3 s spawn protection, health regeneration, simultaneous kills trade.
- **Maps:** Proving Ground, Downtown, Prototype, Office (breakable glass walls around a courtyard garden) and Test Site 07 (a 1950s desert test town). Vote for the next map after each match.
- **Progression:** XP and levels saved to your profile, kill streaks and multi-kills, match awards and a results screen. 30 hats and 12 drone faces unlock as you level up (**Customize**).
- **Feedback and presentation:** hit markers, cap shot bursts, kill feed, damage direction, death cam and killer spectating, a directed map camera, ragdolls, and sounds and music generated in code.
- **Settings:** field of view, sensitivity, volumes, graphics quality, key rebinding, invert Y, hold or toggle sprint, camera effects (screen shake and speed FOV), FPS counter and a colour-blind palette.
- **Input:** keyboard and mouse, gamepads (with light aim assist), and touch controls on phones and tablets. Text chat.

## Controls

Keyboard and mouse bindings can be changed in **Settings** (main menu or pause menu).

| Action | Keyboard / mouse | Gamepad |
| --- | --- | --- |
| Move | `W` `A` `S` `D` / arrow keys | Left stick |
| Look | Mouse | Right stick |
| Shoot | Left mouse button | Right trigger |
| Aim | Right mouse button | Left trigger |
| Swap energy cell | `R` | X / Square |
| Plasma grenade (hold to cook, release to throw) | `G` | Y / Triangle |
| Change gun form | `1` shotgun, `2` machine gun, `3` pistol, `4` sniper / mouse wheel | Bumpers |
| Sprint | `Shift` (hold, or toggle in settings) | Left stick press (toggles) |
| Jump | `Space` | A / Cross |
| Crouch | `Left Ctrl` (`Z` in the browser) | B / Circle |
| Slide | `C` (while sprinting) | Right stick press |
| Scoreboard | Hold `Tab` | View / Select |
| Chat | `T` or `Enter` | – |
| Pause | `Esc` | Menu / Start |
| Ready up (in a lobby) | `F` | D-pad up |

**Touch (phones and tablets, held sideways):** drag on the lower left to move (push far forward to sprint), drag anywhere else to look. On-screen buttons: FIRE, AIM (toggle), JUMP, CROUCH (slides while sprinting), RELOAD, NADE, SWAP, SCORE and pause. A connected keyboard and mouse also work.

## Getting started (from source)

1. Install [Unity](https://unity.com/download) **6000.6.9f1** (Unity 6).
2. Clone the repository:
   ```bash
   git clone https://github.com/steffhe/Capshots.git
   ```
3. Open the folder in Unity Hub with that editor version.
4. Open `Assets/Scenes/Menu.unity` and press Play. **Practice** works offline.
5. Online play uses the Photon App ID in `Assets/Photon/Fusion/Resources/PhotonAppSettings.asset`. If you fork the project, create your own free Photon **Fusion** app and put its App ID there.
6. To test multiplayer, build a player (**File → Build Profiles**) and run it next to the editor: host in one, join with the room code in the other.

Run the automated tests from **Window → General → Test Runner → EditMode → Run All**. Details: [Unity setup, builds and releases](docs/technical/unity-setup.md).

## Development

- Gameplay code is in `Assets/Scripts/`; pure, testable rules are in `Assets/Scripts/Core/` with tests in `Assets/Tests/EditMode/`.
- The host is authoritative: read the [networking guide](docs/networking/fusion.md) before changing players, weapons or the match.
- Pull requests get a browser test build from GitHub Actions; pushing a `v*` tag publishes a release and the browser version.
- Follow the [coding standards](docs/technical/coding-standards.md) and update the docs with behaviour changes.

## Documentation

- [Documentation index](docs/README.md)
- [Architecture overview](docs/architecture/overview.md)
- [Networking (Photon Fusion)](docs/networking/fusion.md)
- [Gameplay systems](docs/README.md): player, weapons, match and modes, bots, maps, UI and audio
- [Unity setup, CI and releases](docs/technical/unity-setup.md) · [Testing](docs/technical/testing.md)

## Credits

- Tutorial: Welton King's YouTube series on making a multiplayer FPS in Unity
- Networking: Photon Fusion by Exit Games
- Assets: Robot Kyle, SimplePoly Town Pack, Unity Starter Assets and Prototype Map by AngeloMaN87 (no longer used by the Prototype map, which is now generated)
- Font: Jura by Daniel Johnson (SIL Open Font License)

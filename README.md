<p align="center"><img src="Assets/Resources/UI/CapshotsWordmark.png" alt="Capshots" width="560"></p>

# Capshots

Capshots (formerly MultiplayerFPS) is a simple online multiplayer first-person shooter made in Unity, built while following Welton King's YouTube tutorial series. Networking is handled by [Photon Fusion](https://www.photonengine.com/fusion) in Host mode: one player hosts the match and decides hits and scores, with lag compensation so shots count where they landed on the shooter's screen. Photon's cloud matches players and connects them to the host (through its relay when a direct connection isn't possible), so nobody needs to run a server. The game runs at 40 network ticks a second, a rate every gun's fire rate fits exactly. Each player's game predicts their own movement and shooting and re-runs the latest ticks when the host's answer arrives; to keep that cheap on slower computers and browsers, only objects that changed are re-run, a shot's hit checks aren't redone, and a client that falls far behind adds a little input delay instead of re-running ever more ticks each frame.

## Play

**In your browser:** go to **[steffhe.github.io/Capshots](https://steffhe.github.io/Capshots/)**. It works in desktop browsers (Chrome, Edge or Firefox) and on phones and tablets, which get on-screen touch controls (see below). On a computer, click the game to lock the mouse (Chrome and Edge lock it with raw mouse input, which turns the view sooner and without the system's pointer acceleration). **Esc** releases the mouse and opens the pause menu (also while you wait to respawn); click again after resuming to lock it. Crouch is on `Z` in the browser, because Ctrl+W would close the tab. A controller works in the browser too: browsers only let the game see one after you press a button on it with the game open. The game uses the controller you last pressed a button on, so other devices a phone lists as controllers (like a fingerprint sensor) don't get in the way, and it shows which controller it's using at the top of the screen for a few seconds. Controllers clipped to a phone, like a Backbone One, work in Chrome on Android, including ones Chrome doesn't recognise. The menus work with a controller too: the stick or d-pad moves between buttons (a glowing frame shows which), A presses and B goes back. If it still does nothing, the browser's console (F12) says which controller the game found. During a match the browser asks before closing or reloading the tab.

**On Windows:**

1. Go to the [latest release](https://github.com/steffhe/Capshots/releases/latest).
2. Download the build from the **Assets** section and extract it.
3. Run the `.exe` file and enjoy!

To play together, each player runs the game, picks a username and either creates a room or joins one from the room list. Browser and Windows players on the same release can play in the same rooms.

## Features

- Online multiplayer through Photon Fusion: host rooms, browse and join open rooms, or quick-join a random one. Every hosted match has a 4-letter room code (on the scoreboard and under the pause menu's title): friends type it under the room list to join, or click an invite link: **Copy invite link** under the code in the pause menu copies a link that opens the browser version straight into the match. Quick play joins any open match, whatever its settings. If quick play finds no open room, it starts one filled with bots (5, unless you picked a number in the create tab), so there's a match to play straight away; the bots make way as people join
- Host migration: if the host leaves mid-match, another player takes over hosting and the match carries on. The map reloads and everyone respawns, but the clock, everyone's kills, deaths and team, the bots and the broken glass are kept. A match that's already over (results and map voting) still ends when the host leaves
- Room options: game mode, map, bots and their skill, max players, and how the match is won: first to a number of kills (up to 50, with a clock counting up) or the most kills when the time runs out (1 to 20 minutes). The room list shows which
- Bots: the host can fill a room with up to 7 computer players. They find their way around every map, hunt down enemies, aim with a short reaction time and some inaccuracy, change gun form for the distance and swap energy cells like anyone else. They play by the same rules as people (same movement, gun, damage and spawn protection), show on the scoreboard and kill feed with BOT in their name, and make way when people join: a bot leaves when someone joins a full room, and comes back when they leave. In Team Deathmatch they fill both teams evenly. Pick their skill when creating the room (Easy, Normal or Hard: reaction time, aim and how far they see). On Normal and Hard they back off into cover to heal when badly hurt and grab a nearby ammo box when low; with the sniper they hold their ground. They walk through glass walls once they're shot out, like people. Each bot has a personality: balanced; a rusher that closes in with the shotgun and never backs off; a sniper that keeps its distance and scopes in early; or a grenadier that throws plasma grenades far more often
- Practice: an offline match against bots from **Practice** in the main menu, with the create tab's map, mode, bot skill and win condition (5 bots unless you picked a number). It runs on your own device without connecting to anyone, so it works with nobody else online
- Five game modes: Free For All; Team Deathmatch (blue vs red, automatic team balancing, no friendly fire, first team to the kill limit wins); and Cap Grab, where a kill only knocks the victim's cap off: it drops to the ground where they fell (or, on a cap shot, flies off their head and lands a little way on), and floats there in their colour under a column of light; walking over it scores a point (CAPS instead of KILLS on the scoreboard). Grab your own cap back first to deny it. Caps out of view nearby get an arrow at the edge of the screen (white for your own). Caps vanish after 15 seconds, blinking at the end; your own cap's column of light is white. Bots go for caps near them too. Team Cap Grab is the same in blue against red: grabbing a teammate's cap saves it. And Gun Game: everyone climbs the same ladder of gun forms (machine gun, shotgun, pistol, sniper rifle), two kills on each, and the second kill with the sniper rifle wins. Your gun changes form by itself as you climb, and grenade kills don't count
- Several maps to choose from, including Office: a real office floor with glass-walled offices and meeting rooms around a courtyard garden. Its glass walls crack when shot and shatter on the second hit, leaving a gap to walk and shoot through
- Warmup until enough players have joined, then a countdown before the match starts
- A lobby for rooms made in the create tab: the first match waits in the warmup (fly about and shoot, nothing counts) while friends join with the room code or an invite link. A panel in the top left shows the room code and who's in, with READY or NOT READY next to each person (bots are just counted, and never hold it up). Ready up with `F` (D-pad up on a gamepad) or **Ready up** in the pause menu, and press it again to take it back. Once everyone is ready, the countdown starts; someone unreadying, or a new player coming in, stops it. The host can start whenever they like with **Start match now** in the pause menu, whoever is ready. If the host leaves during the lobby, the new host gets the button and everyone stays as ready as they were. Quick play and practice start by themselves as before, and so do the maps after a vote, since everyone's already there
- One transforming energy gun with four forms, shortest range first: an 8-pellet shotgun, machine gun, pistol and sniper rifle. You spawn holding the machine gun, or whichever form you pick in **Customize → Starting gun** (in the main menu or the pause menu); the player model in the main menu holds it too, with its hand-orbs and floating pieces, and fires a pretend shot now and then to show them off. Changing form reshapes the gun in half a second, during which it can't fire. All forms share one energy cell that refills by itself shortly after you stop firing; `R` (or firing on empty) swaps in a fresh cell. The gun is dark, satin gunmetal, and part of it is made of floating pieces of the same metal, each with a glowing strip in the player's colour, which re-form into different parts for each form: the sniper's barrel is a line of floating segments that a shot ripples down; the machine gun has a rotary barrel of slats that spins up as you fire; the shotgun's muzzle is a claw of petals that blooms open with each blast, with pump plates that rack back after it; and the pistol has side panels that recoil like a slide. They're always moving, tighten up while you aim, trail the gun slightly as it moves, dim as the energy runs down and drift loose while the cell is swapped, so you can tell anyone's form at a glance. Iron sights, a scope on the sniper form, and weapon sway; laser beams and muzzle flashes glow in the shooter's colour (the team colour in Team Deathmatch)
- Hovering players: every player is a little drone with a capsule body, a round head with a glowing face screen and blinking eyes, a thruster and two floating hand-orbs that hold the gun, in their own (or their team's) colour; it floats, bobs and leans into its movement over a glowing disc, and the gun drifts instead of bobbing with footsteps
- Weapon balance: damage falls off over distance, per-weapon headshot multipliers, heavier guns and aiming slow you down, and shots spread more while moving or jumping
- Sprinting, jumping, crouching and sliding
- Smarter spawns (away from enemies and out of their sight) and 3 seconds of spawn protection (other players see you semi-transparent), which ends early if you shoot
- Health regeneration after 5 seconds without taking damage
- Damage you can see: a drone flashes white and throws a few sparks when it's hit, and below about a third of its health its lights and eyes flicker and its collar spits sparks, so everyone can tell who's hurt. When you're badly hurt yourself, the edges of your screen pulse red
- Every player wears a cap: the team colour in Team Deathmatch, otherwise a colour of their own. Levelling up unlocks new ones: a gamer headset with a microphone over the drone's ear pods (level 1), backwards cap (2), traffic cone (3), beanie (4), chef's hat (5), bucket hat (6), rabbit-ear antenna with glowing tips (7), graduation cap with a swinging tassel (8), hard hat (9), plunger (10), military beret with a badge (11), propeller beanie (12, the propeller spins faster as you move), birthday cake with a flickering candle that goes out when it's knocked off (13), cowboy hat (14), police siren (15, its lights go round faster as you move), top hat (16), sweeping radar dish (17), sombrero (18), jester hat with bouncing, belled prongs (19), viking helmet (20), pirate tricorne with a skull and crossbones (22), plasma globe with crackling lightning (23), fishbowl (24, a glass astronaut dome with three fish swimming round over the head, and bubbles), crown (25), hovering UFO with spinning lights and a tractor beam (26), wizard hat (27), devil horns (28), an atom with glowing electrons circling its rings (29) and halo (30). Pick yours in **Customize**, in the main menu or the pause menu; a NEW badge on it marks hats unlocked since you last looked, and in the main menu pointing at (or tapping) a locked hat previews it on the player model as a see-through grey ghost. Each is made of its own materials, with their own shine and fine surface detail: cotton twill caps, a knitted beanie, felt hats with satin bands, glossy plastic, hammered and polished metal, clear glass and glowing parts. It's always in your (or your team's) colour, and never changes where you can be hit: only the head counts. Bots wear one of the first few
- Physics ragdolls, with X-es for eyes and a broken neck that sparks and crackles as it shorts out: the body is thrown along the killing bullet's path, and the head (cap and all) comes off and flies separately, harder on a headshot
- A sticky plasma grenade: everyone spawns with one (and can carry only one). It's a ball of plasma glowing in the thrower's colour (the team colour in Team Deathmatch), held in a small dark claw, thrown from the free hand-orb in a floaty arc with a glowing tail. Its 3-second fuse starts when you press the button and it's thrown when you let go, so you can cook it: hold it up in the free hand-orb while it pulses faster and the HUD counts down, but hold it too long and it goes off in your hand. It sticks only to enemies: walls and floors it bounces off, lively, going off wherever it ended up when the fuse runs out. Stuck to someone, it fizzles, pulses faster and faster and swells for 1.5 seconds (less if it was cooked), then always kills them; everyone else within 6.5 m (including the thrower, but not teammates) takes damage by distance unless a wall shields them, and nearby glass shatters. It goes off in a bright bubble and a big spray of glowing sparks. Both sides get a STUCK! warning. Bots throw them too, cooking them for a moment, at enemies in the middle distance
- Players drop an ammo box when they die; walk over it to refill your gun's energy and get a used grenade back
- Scoreboard with kills, deaths and ping, your own row highlighted. It works while you wait to respawn too. In free for all modes with a limit, the line under the timer shows the race: your score against the leader's (or, while you lead, the next player's)
- Text chat
- Settings menu, in the main menu and the pause menu: field of view, aim sensitivity, volume, graphics quality, colours, an FPS counter and key rebinding. **FPS counter** shows the frame rate in the top right corner, with the average and slowest frame time over the last half second, colour-coded green (55 FPS or more), yellow (30 or more) or red. In the browser it also shows RES and a percentage while the resolution is lowered **Colours → Colour blind** swaps red-green for colours people with colour blindness can tell apart: blue against orange teams, and player colours from the Okabe-Ito palette
- Customize menu, below Settings: your cap and starting gun
- Gamepad support (Unity Input System), with light aim assist for gamepad and touch players: turning slows a little while the crosshair is over an enemy you can see (never a pull, and never for a mouse)
- The first time you play, a card lists the controls (as you've bound them, or the gamepad's) for a few seconds
- A living main menu: behind the player model, three bots hover about and fight each other with their gun forms, shorting out in sparks and coming back with another form a few seconds later. Just for show, nothing networked
- Browser performance: the browser version uses lighter graphics whatever quality you pick (hard shadows reaching 30 m, and FXAA instead of SMAA anti-aliasing), and lowers the resolution the world is drawn at (down to 60%, never the menus or HUD) when frames drop under about 52 FPS, raising it again once there's room. If a lower resolution doesn't help, because the processor rather than the graphics is the limit, it goes back up. The Windows version is unchanged
- Graphics: colour grading, bloom, vignette, soft shadows, anti-aliasing (SMAA) and light fog on every map
- Sound: a distinct sci-fi shot for each gun, a hovering hum and woosh while moving (silent while crouching), a soft landing puff and energy-cell reload sounds, all heard in 3D from where they happen
- Combat feedback: camera recoil, muzzle flash, hit and kill sounds (a grander one for a cap shot kill, a headshot, which also bursts into confetti and stars; an ordinary kill makes the drone short out in a spark and a zap), a red kill marker that pops out big, a little shake of your view on a kill, a damage flash with a direction indicator, a kill feed and a "killed by" screen (a headshot shows on both as a gold CAPSHOT with the cap logo). When you die you hear your drone power down, and the camera lingers on your body for a moment, following your head (or your cap, on a headshot) as it flies off, before fading to the map view. You respawn 5 seconds after dying
- A directed map view while you wait to respawn and over the match results: the camera scouts each map for high vantage points, drifts slowly through each shot, keeps the players running around in frame and cuts to whichever angle shows the most action
- Spectating your killer: after the camera lingers on your body, it cuts to over your killer's shoulder, looking where they aim, until you respawn (or they die, and it goes to the map view)
- Kill streaks and multi-kills: DOUBLE, TRIPLE, QUAD and MULTI KILL for kills within 4 seconds of each other, and KILLING SPREE, RAMPAGE, UNSTOPPABLE and LEGENDARY every 5 kills without dying, each called out with a sound and bonus XP. Everyone sees streaks in the kill feed, and ending someone's streak of 5 or more is a SHUTDOWN worth 100 XP. Shooting someone under spawn protection shows a small blue marker, so you know the shot landed
- Match awards: before the results, a screen of up to four awards: each winner's card comes up in turn with their drone (in their colour and cap, holding a fitting gun form) hovering over it, and a player who won several gets one card listing them all: CAPSHOT KING (most cap shots), ON A ROLL (best streak), STICKY FINGERS (most grenade kills), UNTOUCHABLE (fewest deaths with a kill) and PUNCHING BAG (most deaths, its drone sparking and flickering)
- Results screen: the scoreboard, your accuracy, cap shots, best streak and favourite gun form, and the XP you earned
- Map voting: at the end of a match everyone votes for the next map (the same one again, or one of two others) for 15 seconds, and the room carries on there with the same settings, people and bots. Or leave to the menu, there or from the pause menu, which works on the awards and results screens too
- Levels and XP: earn XP for kills (a capshot earns a bonus 50 on top of the 100, shown with the cap logo), finishing matches and winning, saved to your profile between sessions. Levelling up plays a fanfare. New caps unlock as you level up ("NEW CAP UNLOCKED")

## Controls

Keyboard and mouse controls can be changed in **Settings**, from the main menu or **Pause → Settings**. A gamepad also works.

| Action | Keyboard / mouse | Gamepad |
| --- | --- | --- |
| Move | `W` `A` `S` `D` / arrow keys | Left stick |
| Look | Mouse | Right stick |
| Shoot | Left mouse button | Right trigger |
| Aim | Right mouse button | Left trigger |
| Swap energy cell | `R` | X / Square |
| Plasma grenade (hold to cook, release to throw) | `G` | Y / Triangle |
| Change gun form | `1` shotgun, `2` machine gun, `3` pistol, `4` sniper / mouse wheel (wraps round) | Bumpers (wrap round) |
| Sprint | `Shift` | Left stick press |
| Jump | `Space` | A / Cross |
| Crouch | `Left Ctrl` (`Z` in the browser) | B / Circle |
| Slide | `C` (while sprinting) | Right stick press |
| Scoreboard | Hold `Tab` | View / Select |
| Chat | `T` or `Enter` | - |
| Pause | `Esc` | Menu / Start |
| Ready up (in a lobby) | `F` | D-pad up |

### Touch controls (phones and tablets)

Hold the device sideways. On phones that allow it (Android), the game goes fullscreen on your first tap.

- **Move:** put your thumb anywhere on the lower left of the screen and drag; the joystick follows your thumb. Push it far forward to sprint (the knob turns yellow).
- **Look:** drag anywhere else on the screen. Dragging on the **FIRE** button turns the view too, so you can shoot and aim with one thumb.
- **AIM** is a toggle: tap to aim down sights, tap again to stop.
- **CROUCH** crouches when standing, and slides when sprinting.
- **JUMP**, **RELOAD**, **NADE** (plasma grenade; hold to cook; faint when you have none, flashing while it cooks) and **SWAP** (next gun form; after the sniper it goes back to the shotgun) are buttons on the right; **SCORE** (hold) and **II** (pause) are at the top. In a lobby, ready up from the pause menu.
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

The tests in `Assets/Tests/EditMode` check gun energy, damage falloff, XP, the match awards, room codes, the Gun Game ladder and the bots' decisions (room filling, gun choice, personalities, aiming and steering). Run them in Unity from **Window → General → Test Runner → EditMode → Run All**. The build workflow also runs them alongside every build, and only publishes a release when they pass.

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

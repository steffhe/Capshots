# Migrating from PUN 2 to Photon Fusion

This plan moves the game's networking from **Photon PUN 2** (what the game uses today) to **Photon Fusion 2** in **Host mode**.

## Status (November 2026)

The code for stages 0–6 is written on the `fusion` branch, against Fusion 2.1.3. It compiles, but it has **not been play-tested yet**. Where the work differs from the plan below:

- **PUN was removed in stage 0, not stage 6.** Fusion 2 brings Photon Realtime 5 into the same `Assets/Photon/PhotonRealtime` and `PhotonLibs` folders that PUN's Realtime 4 uses. The two can't be in one project, so stages couldn't be merged one at a time with PUN still working. The whole move is one change on `fusion`.
- **The editor work is a script.** Run **Capshots → Set Up Fusion** once in the editor. It sets the App ID and adds `NetworkObject`, `NetworkTransform`, a `CharacterController` and the hitboxes to the Player prefab. It also adds a `NetworkObject` to each map's Manager and removes PUN leftovers. See `Assets/Editor/FusionSetup.cs`.
- **Movement uses Unity's `CharacterController`, not Simple KCC.** It is driven in `FixedUpdateNetwork` and synced by `NetworkTransform`, so there's no add-on to download. The old speeds carry over: walk 10 m/s, sprint 20, crouch 5, slide 25, and a jump of about 13 m/s. Replace it with Simple KCC later if the feel isn't right.
- **Hitboxes:** a box for each body and a sphere for each head, sized from the old colliders, on a new `Hitbox` layer (10). Shots use `Runner.LagCompensation.Raycast`, and the host applies damage.
- **Pickups:** the host gives out ammo boxes by checking who stands on them, so the claim and answer messages are gone.
- **WebGL:** Host mode works in the browser, but Photon recommends Shared mode there, and a browser host always goes through the relay. Test hosting from the WebGL build early.

## Why Fusion, and why Host mode

PUN 2 is Photon's older networking library. It's still maintained, but Photon recommends Fusion for new work. For a shooter, Fusion's main gains are:

- **Lag compensation:** when you shoot, Fusion rewinds the other players' hitboxes to where they were on your screen, so shots that look like hits count. With PUN, shots are checked against slightly out-of-date positions.
- **Client-side prediction and a fixed network tick:** movement and shooting feel more responsive and consistent.
- **Host authority:** the host decides what happens (damage, scores), not each player's own game, which closes the easy cheating route PUN leaves open.
- **`[Networked]` properties:** state such as health, kills and match state is kept in sync automatically, replacing the hand-written scoreboard events in `Manager.cs`.

As far as we know, **lag compensation is only available in Host/Server mode**, not Shared mode. Confirm this in the current Fusion documentation before starting. Shared mode works more like PUN and would give a working game sooner, but the movement and weapon code would then need a second rewrite for Host mode. Since hit registration is the main reason to switch, this plan goes straight to Host mode.

## Groundwork

- **Branching:** do the work on a `fusion` branch, with one pull request per stage into it. Merge `fusion` into `main` only once the whole game works. A half-migrated game can't be played, so `main` stays on PUN until then, and the build workflow keeps producing PUN releases.
- **Photon App ID:** create a Fusion app in the Photon dashboard. Fusion apps are a separate app type from PUN apps.
- **Code vs. editor work:** code changes can be written outside Unity. Importing the SDK, adding components to prefabs and scenes, and wiring them up need the Unity editor.
- **Testing:** use Unity's **Multiplayer Play Mode** to run 2–4 players inside the editor, and the pull request builds from the build workflow to test across machines.

## Stage 0: Setup and a quick test

- Import Fusion 2 into the project, in a version that supports Unity 6.
- Enter the Fusion App ID.
- Create a `NetworkRunner` prefab.
- Make a small test scene where two clients connect and move cubes in Host mode.

**Done when:** two players see each other move in Multiplayer Play Mode.

## Stage 1: Lobby and sessions (`Launcher.cs`, `Pause.cs`)

| PUN today | Fusion |
|---|---|
| `ConnectUsingSettings`, `JoinLobby` | `runner.JoinSessionLobby()` |
| `OnRoomListUpdate` | `OnSessionListUpdated` |
| `CreateRoom` with room properties | `StartGame(GameMode.Host, SessionName, PlayerCount, SessionProperties)`, carrying the same map, kill limit, match length and map name |
| `JoinRandomRoom`, `JoinRoom` | `StartGame(GameMode.Client, ...)`, with or without a session name |
| `LoadLevel` and automatic scene sync | `runner.LoadScene(...)`, run by the host |
| `GameVersion` | `AppVersion` in Photon's settings, still set from the release tag |
| `LeaveRoom` | `runner.Shutdown()` |

**Done when:** players can create, list and join sessions, and everyone ends up on the right map.

## Stage 2: Spawning and despawning (`Manager.cs`, `DeadPlayer.cs`, prefabs)

- In the editor, replace the Photon component on the Player and Dead Player prefabs with Fusion's `NetworkObject`.
- The host spawns each player's character in `OnPlayerJoined`, using `Runner.Spawn(prefab, inputAuthority: player)`.
- `PhotonNetwork.Destroy` → `Runner.Despawn`.
- The respawn delay becomes a `TickTimer` on the host.

**Done when:** players spawn, despawn when they leave, and respawn after dying.

## Stage 3: Input and movement (`Player.cs`, `Look.cs`, `Sway.cs`)

This is the biggest stage.

- **Input:** each client packs its controls into one `NetworkInputData` struct in `OnInput`: movement, look angles, and buttons for jump, sprint, crouch, slide, fire, aim, reload and weapon slot.
- **Movement logic:** moves into `FixedUpdateNetwork()`. The host runs it for real, and each player's own client predicts it locally, so movement feels instant.
- **Physics:** replace the current Rigidbody velocity movement with Fusion's **Simple KCC** add-on (a character controller made for Fusion), because Rigidbody physics doesn't predict well. Port sprinting, crouching and sliding onto it.
- **Synced state:**
  - `photonView.IsMine` checks become `HasInputAuthority`, for local-only things like the camera and HUD.
  - The values `OnPhotonSerializeView` sends (aim angle, health) become `[Networked]` properties.
  - The `SetCrouch` and `SyncProfile` RPCs become `[Networked]` crouch, username and level.

**Done when:** movement feels responsive with simulated lag, and other players move smoothly.

## Stage 4: Weapons and damage (`Weapon.cs`, `Gun.cs`)

- **Firing:** checked in `FixedUpdateNetwork` with `WasPressed` for semi-auto and the held button for full-auto. Burst becomes a tick counter.
- **Weapon state:** ammo, cooldowns and the equipped weapon become `[Networked]`.
- **Hit detection:** `Runner.LagCompensation.Raycast` against `Hitbox` components (one for the body, one for the head) under a `HitboxRoot` on the player. This is where hit registration improves.
- **Damage:** applied directly on the host. The `TakeDamage` RPC and the client-decided damage go away.
- **Effects:** shot lines, sound and the hit marker play from a networked shot counter or a small RPC. They're visuals only and decide nothing.

**Done when:** headshots and body shots register correctly at about 150 ms ping, and switching weapons or reloading behaves the same as today.

## Stage 5: Match flow and scoreboard (`Manager.cs`)

- **Replace the hand-written scoreboard events:** the four `RaiseEvent` messages (join, scoreboard update, stat change, timer) become one `MatchManager` network object owned by the host. It holds:
  - the match state (warmup, countdown, playing, ending),
  - the timer as a `TickTimer`,
  - each player's stats in a networked dictionary keyed by player.
- **What disappears:** the manual syncing code, including most of the logic for re-sending lost joins. Fusion keeps these values in sync itself.
- **XP:** the host sends a small RPC to the player who got the kill, so their own game awards the XP and saves the profile.
- **Host leaving:** in Host mode, the session normally ends when the host leaves. Fusion 2 does offer host migration, but it's complex. To start with, show "Host left" and return everyone to the menu. Add host migration later if it matters.

**Done when:** warmup, countdown, scoring, the kill limit, the timer, the end screen and XP all work as they do today.

## Stage 6: Cleanup and release

- Delete the PUN package and its leftovers. The project settings still reference `Photon/PhotonUnityNetworking`.
- Update the README and the build workflow if needed.
- Do a full play-test with 3–4 players, then merge `fusion` into `main` and tag a new release.

## Size and risk

- **Stage 3** (movement) and **Stage 4** (weapons) are the largest. Plan for several rounds of play-testing on each.
- **Stages 1, 2 and 5** are mostly straightforward code mapping, plus editor wiring.
- **Biggest risk:** porting movement onto the character controller can change how the game feels (speeds, slide, jump height). Expect some tuning.
- **Cost:** Fusion has a free tier for development, then pricing by number of concurrent players. Check photonengine.com for current limits.

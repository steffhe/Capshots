# Networking: Photon Fusion 2 (Host mode)

Read this before changing anything in `Player`, `Weapon`, `Manager*`, `NetworkSession`, `PlayerInputData` or bots.

## Configuration (verified)

`Assets/Photon/Fusion/Resources/NetworkProjectConfig.fusion`:

| Setting | Value | Why |
| --- | --- | --- |
| Tick rate | 40 Hz | Every `Gun.fireRate` is a whole number of ticks (asserted by `NetworkConfigTests`) |
| Lag compensation | on, `HitboxBufferLengthInMs` 400 | Rewinds hitboxes up to 400 ms (test asserts ≥ 400) |
| `ClientsOnlyRollbackUpdatedObjects` | true | Clients only resimulate objects that changed |
| `ClientsCanAddInputDelayToLimitResims` | true, `ClientMaxResimsPerFrame` 8 | A client far behind adds input delay instead of ever more resims |
| `AllowClientServerModesInWebGL` | true | Fusion blocks Host mode in WebGL by default; the browser build needs it |
| `PlayerCount` | 10 | Default session size |
| `HostMigration.EnableAutoUpdate` | true | Needed for host migration |

`NetworkSession.AppSettings()` sets Photon `AppVersion` to `Application.version` (the release tag in CI), so different releases never share a room.

## Session lifecycle (`NetworkSession`)

- One static, `DontDestroyOnLoad` `NetworkSession` owns at most one `NetworkRunner`; `CreateRunner()` shuts down any previous one.
- `JoinLobby()` → `JoinSessionLobby(SessionLobby.ClientServer)` for the room list (`SessionListUpdated` event).
- `Host(...)` → `GameMode.Host` with a new 4-letter room code (`Core/RoomCodes`) as the session name; retries on `GameIdAlreadyExists`.
- `JoinRandomOrHost(...)` (quick play) → join any open session, else host one.
- `JoinByCode` / `Join` → `GameMode.Client` with a session name.
- `Practice(...)` → Fusion single-player mode, no Photon connection.
- The scene to load is passed as a build index (`SceneRef.FromIndex`). Map voting loads the next map with `Runner.LoadScene` (`Manager.Voting`).

## Authority model

- **Host = state authority** for `Player`, `Weapon` and `Manager`. Damage, kills, scores, spawns, bots, grenades, glass and match state are decided only where `HasStateAuthority` is true.
- **Owner = input authority** of their own `Player`/`Weapon`. `HasInputAuthority` (`Player.IsLocal`) gates camera, HUD and local-only feedback.
- **Bots** have no input authority. Their bodies are spawned with `PlayerRef.None` and carry a made-up id in `Player.BotId` (raw values from `Manager.BotRawBase` = 101). `Manager.IsBot(...)` / `IsBotActor(...)` identify them. Never hand those ids to Fusion as real players.

## Input and simulation

1. `InputCollector.Update()` (every rendered frame) latches pressed buttons so taps between ticks aren't lost.
2. `NetworkSession.OnInput` → `InputCollector.Take()` builds one `PlayerInputData` per tick: `Buttons` (`InputButton` enum), `Move`, `Yaw`, `Pitch`, `WeaponSlot`.
3. `Player.ReadInput` returns that input (or `BotBrain.InputForTick` for bots on the host) and runs `Core/InputRules.Sanitize` on it, since a modified client could send NaN or out-of-range values.
4. `Player.FixedUpdateNetwork` / `Weapon.FixedUpdateNetwork` simulate from it. Button edges use a `[Networked] PreviousButtons` and `NetworkButtons.GetPressed`.
5. `Look` applies yaw/pitch locally every frame, so turning never waits for a tick.

Adding a control means: a `GameInput` action → latch in `InputCollector` → field/bit in `PlayerInputData`/`InputButton` → read in `FixedUpdateNetwork`. Bots must then produce it too (`BotBrain`).

## Prediction and resimulation

- The owner's client predicts its own `Player` and `Weapon`; the host's state corrects it and Fusion resimulates.
- Anything that must happen **once** (raycasts for hits, damage, sounds, effects, stat counting) is gated with `Runner.IsForward`. Example: `Weapon` updates cooldown/energy/`ShotCount` on every run of a tick but skips the raycasts on resimulation. The host never resimulates.
- Player movement (`Player.Move`, `Core/MovementRules`) only reads `Velocity`, `Grounded`, the input and the scenery, so a resimulated tick gives the same result. Players don't collide with each other (`Player.LetPlayersPassThrough`): a client sees other bodies slightly in the past, so body blocking would mispredict.
- State that resimulation restores must be `[Networked]` (timers are `TickTimer`). `Player` re-applies the capsule shape when `Runner.IsResimulation` because `Crouched` is rolled back.

## Lag compensation and hits

- Shots use `Runner.LagCompensation.Raycast` with `HitOptions.IncludePhysX | IgnoreInputAuthority | SubtickAccuracy` against the `Hitbox` layer (10) plus scenery. Player body colliders are excluded.
- Bots shoot with `RaycastAll` on the host, skipping their own `HitboxRoot` (no rewind needed, bots live on the host).
- Body boxes and head spheres are `Hitbox`es; headshots are detected by a `Head` tag up the hierarchy. Only the standing or crouching set is active (`Player.SetHitboxes`, host only).
- Pellet spread is deterministic from a seed (`ShotCount`, `Object.Id`) via `ShotRules.PelletDirection`, so every machine draws the same pellets.
- **Trades:** a kill sets `Dead` and `DeathTick`; the body is removed and the kill counted on the *next* tick (`FinishDeath`), so a shot the victim fires in the same tick still lands.

## Networked state (where to look)

| Object | `[Networked]` state |
| --- | --- |
| `Player` | `Health`, `Crouched`, `Sliding`, `SlideTimer`, `SlideDirection`, `Velocity`, `Grounded`, `Pitch`, `ProtectionTimer`, regen timers, `PreviousButtons`, `Dead`, `DeathTick`, `BotId` (transform via `NetworkTransform`) |
| `Weapon` | `Equipped` (with `OnChangedRender`), `Energy`, regen/morph/fire/reload/burst timers, `Aiming`, `ShotCount`, starting-form window, grenade count/recharge/throw/cook timers |
| `Manager` | `State` (`GameState`), `MatchTime`, `Heartbeat`, `Stats` (`NetworkDictionary<PlayerRef, PlayerStats>`, capacity `Manager.MaxPlayers` = 16), `HostPlayer`, `AwardCards` |

`PlayerStats` (in `MatchTypes.cs`) is the synced scoreboard row; `PlayerInfo` is the local copy rebuilt when `Stats` changes.

## RPCs and non-networked objects

- RPCs are named `RPC_*`. Most are `RpcSources.StateAuthority → RpcTargets.All` (events) or `→ InputAuthority` (personal feedback like `RPC_Damaged`, `RPC_HitConfirm`). Client requests use `RpcSources.All → RpcTargets.StateAuthority` (register, hat/face, ready, vote, XP). `RPC_Chat` is `All → All`.
- `Weapon.RPC_Shot` is **unreliable** and cosmetic: other clients re-trace the shot locally from a seed. Never put gameplay decisions in it.
- Ragdolls (`DeadPlayer`), plasma grenades (`PlasmaGrenade`), cap pickups and shot effects are **not** network objects: each client spawns its own copy from an RPC; the host corrects grenades via RPCs.
- Office glass state is packed into networked words (`Manager.Glass`, 2 bits per pane).

## Host migration (`Manager.HostMigration`, `NetworkSession.OnHostMigration`)

Fusion's host-migration snapshot is **not** used, because players get new `PlayerRef`s. Every client keeps a `MigrationCarry` (state, clock, players by old actor id, glass, XP) and the new host rebuilds the match from it as players re-register with their old actor id. The map reloads and everyone respawns. A match already in results/voting ends instead.

## Common pitfalls

- Changing gameplay state outside `FixedUpdateNetwork`, or without `HasStateAuthority`, desyncs or gets overwritten.
- Using `Time.deltaTime` / `Update` for simulation instead of `Runner.DeltaTime` / ticks.
- Spawning effects or counting stats without `Runner.IsForward` (duplicates on resimulation).
- Random numbers in simulation: use deterministic seeds like `ShotRules`. `UnityEngine.Random` is only used for local looks (recoil sideways, sparks, flash).
- Changing a `Gun.fireRate` to a value that isn't a whole number of 40 Hz ticks (test fails).
- Reordering enums or index lists that are sent over the network or saved (`MatchMode`, `InputButton`, `Hats`, `Faces`, room properties). Append only.
- Forgetting bots: anything keyed by `PlayerRef` must handle bot ids and bodies with no input authority.
- Forgetting host migration: new host-only state that should survive must be added to `MigrationCarry`.
- Changing networked layout (fields, RPC signatures) breaks compatibility with other releases; the app version already separates releases.

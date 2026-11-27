# Player: movement, health, damage and spawning

Main script: `Assets/Scripts/Player.cs` (`NetworkBehaviour`, `[RequireComponent(typeof(CharacterController))]`) on `Assets/Resources/Player.prefab`. Networking rules: [fusion.md](../networking/fusion.md).

## Movement (`Player.Move`, called from `FixedUpdateNetwork`)

- Unity `CharacterController` driven per tick with `Runner.DeltaTime`, synced by `NetworkTransform`; the owner predicts it.
- States: walk, sprint (forward + grounded; the Sprint input bit is held, or toggled locally by `GameInput.SprintWanted`), crouch (toggle), slide (sprint + Slide button), jump. Velocity is kept in `[Networked] Velocity`.
- Speeds are legacy values multiplied by `LegacyStep` (0.02) to get m/s (`Player.WalkSpeed`, `Player.SprintSpeed`). Prefab values: `speed` 500 (10 m/s), `sprintModifier` 1.5 (15 m/s), `crouchModifier` 0.5, `slideModifier` 1.8 (a slide starts at 18 m/s), `lengthOfSlide` 0.75 s, `jumpForce` 520 (10.4 m/s, a jump rises about 2.6 m with the project's gravity of -20 and lasts about 1 s).
- `Weapon.MoveSpeedMultiplier()` scales speed by the gun form (`Gun.moveSpeedMultiplier`) and while aiming (`Gun.aimMoveSpeedMultiplier`).
- How far the move input is pushed scales the speed (a half-pushed stick walks at half speed). Keyboard input is always full; touch reaches full speed at 70% of the stick (`TouchControls.MoveStick`).
- When the match is `Ending`, input is zeroed (the body brakes to a stop) but gravity still applies.
- Crouching swaps the capsule (`standingHeight`/`crouchingHeight`) and the active hitbox set. Standing up (crouch toggle, jump, sprint, a slide ending) only happens if the standing capsule's top fits (`Player.TryStand`/`HasHeadroom`, a capsule check against `WorldMask`); otherwise the body stays crouched, and sprinting falls back to crouching speed.
- **Jump:** coyote time (`coyoteTime` 0.1 s after the body last stood on something, `[Networked] LastGroundedTick`) and a jump buffer (`jumpBufferTime` 0.1 s, a press is kept in `[Networked] JumpPressTick`). A jump uses up both, so there's no second jump.
- **Slide** (Slide button while sprinting, not during `slideCooldown`): starts at the larger of the current speed and `slideModifier` x walking speed x the gun's multiplier, slows by `slideFriction` (17 m/s per second) down to crouching speed and turns towards the input at up to `slideSteering` (90 degrees a second) (`MovementRules.SlideStep`). It ends when `lengthOfSlide` is up, on leaving the ground, on jumping (momentum carries on under the air rules), or when slowed to crouching speed (a wall does that). Then `SlideTimer` runs the `slideCooldown` (0.6 s). On average a slide is slower than sprinting; it's a burst and a lower hitbox, not a way to travel.
- Players hover visually only (see *Hover look and sound* below); the simulated body is the capsule.

### Acceleration, braking and air control (`Core/MovementRules`)

Horizontal speed is not set directly from input; each tick it moves towards what the input asks for. Rates are Inspector fields under *Movement feel* (script defaults; not overridden in the prefab), in m/s per second:

| Field | Default | Effect |
| --- | --- | --- |
| `groundAcceleration` | 140 | Standing to walking speed in 3 ticks |
| `groundBraking` | 160 | Walking to a stop with no input in 3 ticks |
| (both together) | 300 | Pushing against the current movement (counter-strafe, sideways drift after turning): stops in 2 ticks, A to D reversal in 4 |
| `slowDownRate` | 40 | Dropping to a lower speed while moving (letting go of sprint, crouching, aiming, a slide ending) |
| `sprintAcceleration` | 20 | Walking to sprinting speed in about 0.25 s |
| `airAcceleration` | 25 | Steering in the air; it never takes the speed above the larger of the wanted speed and the current speed |
| `airDrag` | 8 | In the air, speed above sprinting speed (with the current gun) bleeds off |

- In the air momentum is kept: a sprint jump carries sprinting speed, and no input means no braking.
- A slide follows its own rules while it lasts (see *Slide* above); afterwards the speed slows down at `slowDownRate` on the ground or `airDrag` in the air.
- When the controller hits something sideways, the horizontal velocity is cut to what the body actually moved (`MovementRules.LimitToActual`), so pushing into a wall stores no speed and the gun's spread (`Player.MoveSpeedRatio`) follows the real speed.
- **Ground snapping:** a body that was grounded, isn't jumping and ends a move off the ground is moved down onto ground found within `groundSnapDistance` (0.5 m) by a sphere cast (`Player.SnapToGround`), so running down stairs and ramps stays grounded.
- **Players don't collide with each other.** `Player.LetPlayersPassThrough` turns off collisions between the `LocalPlayer` and `RemotePlayer` layers at runtime (`Physics.IgnoreLayerCollision`). Shots are unaffected (hitboxes). Dead bodies (`DeadPlayer`) are on the `Ragdoll` layer (12), which players, shots, ground checks, grenades, cap drops and spawn sight checks ignore, because each machine simulates its own copy in its own place.

- **Ground check** (`Player.OnGround`): the controller touched ground on its last move, or a 0.15 m ray from `groundDetector` hits any scenery (`Player.WorldMask`: everything but players, guns and hitboxes; the `ground` mask is included but no longer limits it). It runs before the move (can jump, can sprint, ground or air rules) and after it; `[Networked] Grounded` holds the after-move result, so the gun's airborne spread and the HUD follow this tick.
- **Physics sync:** before the first tick of each batch, `Player.SyncPhysicsBeforeFirstTick` calls `Physics.SyncTransforms()` once (for all bodies). `NetworkTransform` puts bodies back where the simulation had them before ticks run, and with `Physics.autoSyncTransforms` off the `CharacterController` would otherwise move from a stale position.

Tests: `MovementRulesTests` (rates, air rules, slide, coyote/buffer window, and that the prefab's jump reaches the Prototype decks but not its crates).

### Hover look and sound (local, every client)

Measured in `LateUpdate` from how the body moved on this machine (after Fusion places it), so it needs no network traffic.

- `HoverVisuals` (added at runtime): the body model floats `lift` (5 cm) up and bobs `bobHeight` (3 cm), less while moving (`movingBob`). It leans into its movement (`leanPerSpeed`, `maxLean` 8°) on a spring (`leanFrequency`, `leanDamping`), so it tips into a start and rocks back a little when braking. A hover cushion (`Suspension`) sinks the body on landing and as it lifts off (about 3.5 cm for a full jump, at most `cushionDown` 7 cm) and springs back; the local player's gun rides it (`Player.UpdateWeaponHover`), the camera doesn't. The glow and its light are placed on the ground below by a ray (`Player.WorldMask`), fading out and widening up to `glowFadeHeight` (4 m), so a player in the air shows where they are over. Hitboxes don't move with any of this, so it's kept small.
- `PlayerSounds`: the hover loop's pitch and loudness follow speed (`Player.WalkSpeed`/`SprintSpeed`); a thrust sound when leaving the ground upwards faster than 5 m/s (a jump); a landing sound whose loudness and pitch follow the fastest fall since leaving the ground. Ground for sounds is any scenery, like the simulation.

## Health, damage and death

- `maxHealth` 1000 (prefab). `[Networked] Health`.
- `TakeDamage(damage, attacker, weaponName, headshot, bulletDirection, hitOffset)` is **host only** and returns whether it killed. Ignored while dead or spawn-protected.
- Regeneration: after `regenDelay` (5 s) without damage, `regenPerSecond` (150) via a fractional `RegenBuffer`.
- Spawn protection: `spawnProtectionTime` 3 s; firing ends it (`EndSpawnProtection`). Others see a ghost material (`Resources/ProtectionGhost`).
- Falling below `Manager.killHeight` (-25 by default) kills, even when protected (`CheckFellOut`).
- Death is two-step: `Dead`/`DeathTick` set on the killing tick; `FinishDeath` on the next tick spawns the ragdoll and dropped cap, calls `Manager.PlayerKilled` and despawns the body. This allows trades.
- Feedback: `RPC_Damaged` (victim's screen, `FeedbackUI`), `Weapon.RPC_HitConfirm` / `RPC_ShieldHit` (shooter). Bots get `BotBrain.OnDamaged` instead.
- Ragdoll: `DeadPlayer` (local physics, not networked). Death camera: `DeathCam`, then `MapCamera` / spectating the killer.

## Spawning (`Manager.Spawning.cs`)

- Host spawns bodies with `Runner.Spawn(playerPrefab, ..., inputAuthority: player)`; bots with `PlayerRef.None` (`Manager.Bots`).
- Delays: `joinSpawnTime` 3 s after joining, `respawnTime` 5 s after dying (`TickTimer`s in `pendingSpawns`).
- Spawn point scoring penalizes points near or visible to enemies (`spawnSightPenalty`, `spawnRandomness`). Optional `SpawnSide` components give team sides a bonus (`ownSideSpawnBonus`). Glass doesn't block sight for this.

## Appearance (local only)

`DroneBody` (body, face screen, `Core/Faces`, `Core/FaceDesigns`), `DroneHands`, `PlayerCap` (hats from `Core/Hats`, unlocked by level), `HatMotion`/`HatDangle`/`HatSpinner`/`HatArcs`, `NeckSparks`, `KillSpark`, `CapshotBurst`. Colours come from `PlayerCap.ColorFor(actor)` or the team colour (`Manager.Teams`). Hats never change hitboxes.

## Modifying safely

- Keep simulation in `FixedUpdateNetwork` and networked properties; keep visuals in `Render`/`Update` and gated by `IsLocal` where personal.
- Tunables live on the prefab; script defaults may differ. Check `Player.prefab` before relying on a default.
- New per-player state that must survive host migration also needs `Manager.HostMigration` changes.

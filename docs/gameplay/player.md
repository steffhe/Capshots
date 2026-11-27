# Player: movement, health, damage and spawning

Main script: `Assets/Scripts/Player.cs` (`NetworkBehaviour`, `[RequireComponent(typeof(CharacterController))]`) on `Assets/Resources/Player.prefab`. Networking rules: [fusion.md](../networking/fusion.md).

## Movement (`Player.Move`, called from `FixedUpdateNetwork`)

- Unity `CharacterController` driven per tick with `Runner.DeltaTime`, synced by `NetworkTransform`; the owner predicts it.
- States: walk, sprint (forward + grounded), crouch (toggle), slide (sprint + Slide button, lasts `lengthOfSlide`), jump. Velocity is kept in `[Networked] Velocity`.
- Speeds are legacy values multiplied by `LegacyStep` (0.02) to get m/s. Prefab values: `speed` 500 (10 m/s), `sprintModifier` 2, `crouchModifier` 0.5, `slideModifier` 2.5, `lengthOfSlide` 0.75 s, `jumpForce` 650 (13 m/s).
- `Weapon.MoveSpeedMultiplier()` scales speed by the gun form (`Gun.moveSpeedMultiplier`) and while aiming (`Gun.aimMoveSpeedMultiplier`).
- When the match is `Ending`, input is zeroed but gravity still applies.
- Crouching swaps the capsule (`standingHeight`/`crouchingHeight`) and the active hitbox set.
- Players hover visually (`HoverVisuals`, added at runtime, local only). Movement sounds: `PlayerSounds`.

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

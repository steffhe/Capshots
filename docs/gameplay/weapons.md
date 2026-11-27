# Weapons and grenades

Every player carries **one transforming energy gun** with four forms (shortest range first): Shotgun, Machine Gun, Pistol, Sniper Rifle. Networking rules: [fusion.md](../networking/fusion.md). Visual design history: [weapon-redesign](../weapon-redesign/README.md).

## Scripts

| Script | Role |
| --- | --- |
| `Weapon.cs` (`NetworkBehaviour` on the Player prefab) | Firing, energy, cell swap, form changes, aiming, grenade cook/throw input; hit detection and damage on the host |
| `Core/Gun.cs` (`ScriptableObject`, menu **Create → Gun**) | Per-form stats; assets in `Assets/ScriptableObjects/Guns/`, referenced by `Weapon.loadout` |
| `Core/ShotRules.cs` | Spread/aim progress, deterministic pellet directions, recoil pattern, cap shot rule, hit weight |
| `Core/GunEnergy.cs` | Energy regen and when to auto-swap the cell |
| `Core/GunTransform.cs` | Form-change rules (`None` / `Instant` starting pick / `Morph`) and whether the gun may fire |
| `Core/GunForms.cs` | Loadout index lookups |
| `GunModel`, `GunParts`, `Core/GunDesign` | The gun's machined body, built in code from shared unit meshes |
| `GunMorph`, `GunSatellites`, `Core/GunSatelliteLayout` | Form-change animation and floating pieces (looks only) |
| `ShotEffects`, `BeamFade` | Pooled beams, scorch marks and sparks |
| `Manager.Grenades.cs`, `PlasmaGrenade.cs`, `Core/PlasmaGrenadeRules.cs` | Plasma grenade flight, sticking and blast (host decides, clients show copies) |

## Gun assets (current values)

| Form | `damage` | `fireRate` (s) | `energyPerShot` | `burst` | `pellets` | Headshot × | Falloff start → end (m), min × | Move × |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| Shotgun | 140 / pellet | 0.6 | 14 | semi | 8 | 1.5 | 6 → 18, 0.25 | 1 |
| Machine Gun | 200 | 0.075 | 4 | auto | 1 | 1.75 | 12 → 30, 0.35 | 0.92 |
| Pistol | 400 | 0.2 | 10 | semi | 1 | 2 | 25 → 45, 0.4 | 1 |
| Sniper Rifle | 800 | 1.0 | 20 | semi | 1 | 2 | 100 → 200, 1 | 0.85 |

Players have 1000 health. `burst`: 0 semi, 1 auto, 2 burst. The sniper rifle has `scopeFov` 20 and `adsTime` 0.25 s (others 0.15). Each `fireRate` must be a whole number of 40 Hz ticks (`NetworkConfigTests`). Role expectations (shotgun one-shots point-blank, sniper one-shots headshots, etc.) are pinned by `GunBalanceTests` / `GunFormsTests`; update them deliberately when rebalancing.

## Energy and forms (`Weapon` fields, script defaults)

- Shared pool `maxEnergy` 100; refills `regenRate` 40/s after `regenDelay` 0.9 s.
- Cell swap (Reload, or firing on empty) takes `cellSwapTime` 1.8 s; `autoSwapPatience` 0.25 s waits for the refill when only slightly short.
- Changing form takes `morphTime` 0.5 s; the gun can't fire or aim meanwhile. The starting form (`defaultStartingGun`, or `GameSettings.StartingGun`) may be taken instantly in the first `StartingFormSeconds` (1 s) after spawning.
- The requested form is sent in every input (`PlayerInputData.WeaponSlot`) so a lost input can't lose a change.
- Gun Game overrides the form from the ladder (`Manager.GunGame`, `Core/GunGameRules`).

## Firing pipeline (`Weapon.FixedUpdateNetwork` → shot)

1. Checks via `GunTransform` (not morphing, swapping, throwing or cooking), energy and `FireCooldown`.
2. Updates energy, cooldown and `ShotCount` on every run of the tick; ends spawn protection.
3. `if (!Runner.IsForward) return;` – raycasts, damage and effects happen once.
4. For each pellet: deterministic direction from `ShotRules.PelletDirection` with current bloom (aim progress, movement and airborne multipliers) → lag-compensated raycast (bots: `BotRaycast`).
5. Host: per-target damage summed over pellets with `Gun.DamageAt(distance, headshot)`; no friendly fire (`Manager.AreEnemies`); cap shot if the head took ≥ half the damage (`ShotRules.IsCapshot`); `Player.TakeDamage`; `RPC_HitConfirm` or `RPC_ShieldHit` to the shooter.
6. Glass: `OfficeGlass.Shoot` (shots pass through the Glass layer but crack/shatter panes).
7. Effects: shooter and host draw their own; others get unreliable `RPC_Shot` and re-trace locally.
8. Shooter only: `KickView` recoil/shake from the `Gun` "Feel" fields.

## Plasma grenade (`Core/PlasmaGrenadeRules`)

Carry `MaxCarried` 1; recharge `RechargeTime` 15 s. Fuse 3 s from button press (cooking); thrown on release. Sticks only to enemies; stuck fuse 1.5 s and a stuck grenade always kills (`StuckDamage`). Blast radius 6.5 m (1000 at centre down to 150 at edge, walls shield, teammates excluded, thrower included). Flight is simulated by the host in `Manager.Grenades`; `PlasmaGrenade` is a local visual corrected by host RPCs.

## Extending safely

- New stats belong on `Gun`; logic that can be pure belongs in `Core/` with a test.
- Adding a fifth form touches `Weapon.MaxGuns` (4), the loadout, input slots (`GameInput.Weapon1..4`, `InputCollector`), `GunDesign`/`GunSatelliteLayout`, `GunGameRules`, `BotLogic` form choice, HUD icons and tests.
- Keep cosmetic RPCs cosmetic; keep damage host-only.

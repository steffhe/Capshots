# Bots

Computer players that fill a room to the host's chosen count. They exist **on the host only**; clients see ordinary players. Networking context: [fusion.md](../networking/fusion.md#authority-model).

## Scripts

| Script | Role |
| --- | --- |
| `Manager.Bots.cs` | How many bots (setting, humans, room size), adding/removing them, spawning their bodies with `PlayerRef.None` and a fake id (`BotRawBase` 101+) in `Player.BotId` |
| `BotBrain.cs` | Per-tick decision making on the host; outputs a `PlayerInputData` via `InputForTick` |
| `BotNavigation.cs` | Builds a NavMesh at runtime from the map's colliders (AI Navigation package) when a match with bots starts; no baked NavMesh in scenes. Until ready, bots head straight for their goal. The agent climbs at most 0.3 m, the player capsule's step, so paths never use a ledge a body can't walk up |
| `Core/BotLogic.cs` | Pure, tested rules: desired bot count, form choice by distance, aim/steer maths, skills and personalities |

## Behaviour

- Bots produce the same input a person sends, so `Player` and `Weapon` treat them identically (movement, gun rules, damage, spawn protection).
- Count: up to 7 (`Launcher.MaxBots`); quick play fills an empty room with 5. A bot leaves when a person joins a full room and returns when they leave. Teams are filled evenly.
- Skill (`BotLogic.Easy/Normal/Hard`): reaction time, aim error and sight range. Normal and Hard retreat to heal when badly hurt.
- Personality by bot slot: Balanced, Rusher, Sniper, Grenadier (`BotLogic.Personality`).
- Form choice by distance: `ShotgunRange` 9 m, `PistolRange` 18 m, `SniperRange` 40 m thresholds in `BotLogic`.
- Bot shots use `Weapon.BotRaycast` (no lag-compensation rewind needed on the host).
- Bots go for nearby caps in Cap Grab modes and throw grenades at mid range.

## Modifying safely

- Put tunable or decision maths in `BotLogic` and cover it in `BotLogicTests`.
- Anything new a player can do must be producible by `BotBrain`, or bots silently won't use it.
- Code keyed by `PlayerRef` must accept bot ids (`Manager.IsBot`) and bodies without input authority (no RPCs to `InputAuthority` for bots; see the `IsBot` checks in `Player.TakeDamage` and `Weapon`).

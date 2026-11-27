# Capshots – instructions for Claude

Multiplayer FPS in **Unity 6000.6.9f1** (URP, Input System) networked with **Photon Fusion 2 in Host mode**. Players are hovering drones with one transforming energy gun (shotgun / machine gun / pistol / sniper); modes include FFA, TDM, Cap Grab, Team Cap Grab, Gun Game; host-run bots. Ships as WebGL and Windows via GitHub Actions (GameCI).

## Architecture rules (verified, preserve them)

- **Host is authoritative.** Gameplay state changes only in `FixedUpdateNetwork` and only where `HasStateAuthority`. Damage, kills, scores, spawns, bots, grenades and glass are host-only.
- **Input drives simulation:** `GameInput` → `InputCollector` → `PlayerInputData` → `Player`/`Weapon.FixedUpdateNetwork`. Bots feed the same struct from `BotBrain` on the host. Sanitize client input via `Core/InputRules`.
- **Prediction:** the owner predicts its `Player`/`Weapon`. Gate one-time work (raycasts, damage, effects, stat counts) with `Runner.IsForward`. Use `[Networked]` + `TickTimer`, never `Time`/`Update` for simulation. Seeded randomness only (`ShotRules`).
- Only `Player`, `Weapon`, `Manager` are `NetworkBehaviour`s. Cosmetics (ragdolls, grenade visuals, shot effects) are local copies driven by RPCs; `RPC_Shot` is unreliable and must stay cosmetic.
- **Append-only:** `MatchMode`, `InputButton`, `Hats`, `Faces` and room property values are saved/sent over the network. Never reorder or renumber.
- 40 Hz tick: every `Gun.fireRate` must be a whole number of ticks (tested).
- Bots use fake `PlayerRef`s (raw ≥ 101) with no input authority; host migration rebuilds state from `Manager.HostMigration`'s `MigrationCarry`. New per-player/host state must handle both.
- `Launcher.maps` (in `Menu.unity`) stores **build indices**; `Resources.Load` uses asset names; `Manager` finds HUD objects by path. Renames/reorders break at runtime.
- Pure rules belong in `Assets/Scripts/Core/` (`Capshots.Core`, no scene access) with EditMode tests.
- UI, visuals and audio are mostly **built in code** (`UITheme`, `DroneBody`, `PlayerCap`, synthesized sounds). Extend that pattern instead of editing scenes/prefabs.

## Code conventions

Global namespace; 4 spaces, Allman braces; `PascalCase` types/members/`[Networked]` props, `camelCase` private fields, `RPC_` prefix; public Inspector fields (never rename serialized fields). Large classes are `partial` per topic (`Manager.*.cs`). Each script starts with a header comment on what it does and where it runs: keep it accurate. Details: `docs/technical/coding-standards.md`.

## Workflow

1. Find the relevant scripts with targeted searches in `Assets/Scripts/` (skip `Assets/Photon/`, `*.unity`, `*.prefab`, `*.meta` unless the task is about them). Read the script header comments first.
2. Read only the doc(s) for the task (index below). Networked code → always `docs/networking/fusion.md`.
3. Make focused changes; no rewrites or renames of public APIs/serialized fields unless required.
4. Consider multiplayer: host vs client, prediction/resimulation, bots, host migration, WebGL.
5. Validate: there is no Unity editor in cloud sessions, so code can't be compiled or tested here. Re-read changes carefully; run `python3 Tools/<Map>/generate.py --preview` when touching generators; EditMode tests run in CI on PRs.
6. Report what changed and what could not be verified.
7. Update the matching doc in the same change when behaviour or architecture changes.

Don't hand-edit scene/prefab/asset YAML or GUIDs unless trivial and certain. Don't touch `Assets/Photon/` (vendor SDK).

## Docs index (read on demand)

| Topic | File |
| --- | --- |
| Networking, authority, prediction, RPCs, pitfalls | `docs/networking/fusion.md` |
| System map, project layout, scene/prefab wiring | `docs/architecture/overview.md` |
| Movement, health, damage, spawning | `docs/gameplay/player.md` |
| Guns, balance values, grenades | `docs/gameplay/weapons.md` |
| Match states, modes, lobby, XP, awards | `docs/gameplay/match-and-modes.md` |
| Bots | `docs/gameplay/bots.md` |
| Maps, build indices, generators | `docs/gameplay/maps.md` |
| Menus, HUD, audio, effects, browser/touch | `docs/gameplay/ui-audio-visuals.md` |
| Unity/Photon setup, CI, releases | `docs/technical/unity-setup.md` |
| Tests | `docs/technical/testing.md` |
| Performance | `docs/technical/performance.md` |
| Decisions / known issues | `docs/development/decisions.md`, `docs/development/known-issues.md` |

## Documentation rules

- Docs describe the implementation as it is; mark anything unverified as unverified. Never document assumptions as facts.
- One home per fact; link instead of duplicating. Remove outdated text when replacing it.
- `README.md` = player-facing intro and getting started. `CLAUDE.md` = short instructions for Claude. `docs/` = technical detail.

# Architectural decisions

**Verified** decisions are visible in code/config (with the reason the code or its history gives). **Recommendations** are guidance, not existing decisions.

## Verified

1. **Photon Fusion 2 in Host mode (migrated from PUN 2).** Reason: lag compensation and host authority (hit registration, no client-decided damage), client prediction on a fixed tick, `[Networked]` state instead of hand-written sync. Shared mode was rejected because lag compensation needs Host/Server mode. PUN and Fusion 2 share Photon Realtime folders, so the migration was done in one change. (The original migration plan, `fusion-migration.md`, is in git history, last changed in commit 76f097b8; it was removed because its status and stages were out of date.)
2. **Unity `CharacterController` instead of Fusion's Simple KCC.** Driven in `FixedUpdateNetwork`, synced by `NetworkTransform`; no add-on needed. Old Rigidbody speeds were preserved via `Player.LegacyStep`.
3. **40 Hz tick** so every gun's fire interval is a whole number of ticks (enforced by `NetworkConfigTests`).
4. **400 ms hitbox history** so high-ping shots still count.
5. **Kill resolved one tick late** (`Dead` then `FinishDeath`) so simultaneous shots trade.
6. **Cosmetic shots via one unreliable seed RPC**; hits decided only on the host.
7. **Bots run on the host as fake `PlayerRef`s** (raw ≥ 101) that feed `PlayerInputData` through the same simulation as people.
8. **Custom host migration** from each client's last-seen state instead of Fusion's snapshot, because `PlayerRef`s change on the new host.
9. **Host mode allowed in WebGL** (`AllowClientServerModesInWebGL`) so browser and Windows players share rooms; browser hosts go through the relay.
10. **Pure rules in `Capshots.Core`** so they can be EditMode-tested without scenes or a running match.
11. **Runtime-built UI, visuals and synthesized audio** to avoid scene/prefab churn and art dependencies.
12. **Generated maps** (Office, Prototype from Python; Test Site 07 from editor code) with the generator as the source of truth.
13. **Runtime NavMesh** for bots instead of baked NavMeshes per scene.
14. **App version = release tag** so incompatible network code never matches.
15. **Append-only indices** for anything sent or saved (`MatchMode`, hats, faces, input buttons).

## Recommendations

- Record new significant decisions here (one numbered line: what and why).
- If Multiplayer Play Mode is adopted for local testing, add it to `Packages/manifest.json` and document it in [unity-setup](../technical/unity-setup.md).

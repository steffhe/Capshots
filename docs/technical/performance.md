# Performance

Targets that shape the code: a 40 Hz network tick with client resimulation, up to 16 bodies (people + bots), and a WebGL build on phones and low-end browsers.

## Existing measures (verified)

- **Resimulation cost:** `ClientsOnlyRollbackUpdatedObjects`; shot raycasts/damage/effects skipped when `!Runner.IsForward`; clients add input delay rather than exceeding `ClientMaxResimsPerFrame` 8 (`NetworkProjectConfig.fusion`).
- **One message per shot:** `Weapon.RPC_Shot` (unreliable) sends a seed; clients re-trace pellets locally instead of receiving 8 pellet results.
- **Pooling:** `ShotEffects` reuses beams, scorch marks and sparks.
- **Shared meshes/materials:** `GunParts` builds unit meshes once for every gun; `MaterialPropertyBlock` for per-player tints and ghosting.
- **No per-shot allocations:** static reusable lists in `Weapon` (`pelletHits`, `botHits`).
- **Runtime NavMesh** is built in the background over several frames (`BotNavigation`), host only.
- **Browser:** `BrowserGraphics` adaptive render scale (down to 0.6), hard shadows ≤ 30 m, shadow map ≤ 1024, FXAA. `FpsCounter` shows frame time when enabled.
- **Office map:** optional baked floor (**Capshots → Bake Office Map**) enables baked lighting and occlusion culling.
- URP Forward+ rendering (`Assets/Settings/ForwardRenderer.asset`, `m_RenderingMode: 2`).

## Guidelines for hot paths

Hot paths: `FixedUpdateNetwork` (runs several times per frame during resimulation), `Render`/`Update`/`LateUpdate` on per-player components, `BotBrain.InputForTick`.

- Avoid allocations (LINQ, closures, `new List`, string building) in those paths; reuse static buffers.
- Cache components in `Spawned`/`Awake`; avoid `GetComponent`, `Find*` and `Resources.Load` per frame.
- Don't add new per-shot or per-tick network objects; prefer networked counters/seeds + local effects.
- Check WebGL impact for anything rendering-heavy (lights, post-processing, transparent overdraw).

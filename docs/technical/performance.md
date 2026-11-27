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

## UI (verified by reading the code; not profiled)

- **Canvas rebuilds:** everything in a map scene's `Canvas` (HUD, scoreboard, menus) is one batch, so any change re-batches it all. The parts that change often have nested canvases of their own (`UITheme.IsolateCanvas`): the crosshair, the weapon readout (`HUD/Ammo`), health, the form strip, the clock and score strip, the cap arrows' layer and the whole of `FeedbackUI` (flashes, damage arcs, hit text, kill feed). Nested canvases are drawn in the same order; they aren't raycast by the root canvas's `GraphicRaycaster`, so only parts nobody clicks are isolated.
- **Write only on change:** per-frame HUD code compares before it moves, resizes, recolours or re-texts anything (`HudFeedback` helpers `SetScaleX` / `SetColor`, `Player.RefreshHealthBar`, the weapon label, the grenade label, spawn protection). Text is rebuilt only when the value shown changes.
- **No per-event objects in a match:** hit markers, kill ring, cap shot stamp, damage arcs, cap arrows, kill feed rows (`FeedbackUI.feedPool`), scoreboard rows (`Manager.boardRows`) and lobby rows are made once and reused; the room list's rows in the menu too (`Launcher.roomRows`).
- **Idle menus cost nothing per frame:** button rails (`MenuButtonFx`) have no update of their own; one `MenuButtonFxDriver` animates only the buttons that are changing. `MenuReveal` runs a short coroutine when shown.
- **Settings reads:** values read every frame (field of view, sensitivities, invert Y, camera effects, colour blind colours) are cached by `GameSettings` instead of calling `PlayerPrefs` each time; setting one writes both.
- **Networked data to UI:** match state reaches the UI through `Manager.Render`'s `ChangeDetector` (Stats, State, MatchTime), not polling; the local player's own values (`Health`, `Energy`, timers) are read once a frame by `Player` / `Weapon` and handed to `HudFeedback`. A `Stats` change (pings included, at most about once a second per player) rebuilds the small `playerInfo` list (at most 16 entries); judged too small to be worth reusing, not measured.
- **Text:** legacy uGUI `Text` with `Shadow` / `Outline` components (each multiplies the vertex count); best fit is off everywhere (`UITheme.StyleText`). TextMeshPro is only used for the name tags, which are re-texted only when the name changes. Moving the UI to TextMeshPro is not planned.

## Guidelines for hot paths

Hot paths: `FixedUpdateNetwork` (runs several times per frame during resimulation), `Render`/`Update`/`LateUpdate` on per-player components, `BotBrain.InputForTick`.

- Avoid allocations (LINQ, closures, `new List`, string building) in those paths; reuse static buffers.
- Cache components in `Spawned`/`Awake`; avoid `GetComponent`, `Find*` and `Resources.Load` per frame.
- Don't add new per-shot or per-tick network objects; prefer networked counters/seeds + local effects.
- Check WebGL impact for anything rendering-heavy (lights, post-processing, transparent overdraw).

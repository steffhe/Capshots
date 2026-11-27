# Coding standards

Conventions observed in `Assets/Scripts/`. Match the surrounding code; don't reformat untouched lines.

## C# style

- 4-space indent, Allman braces; short guard clauses on one line (`if (x == null) return;`).
- Gameplay code is in the **global namespace** (only `Assets/Editor/TestSite07` uses a namespace, `TestSite07`). Don't introduce namespaces piecemeal.
- Types, methods, properties, constants: `PascalCase`. Private fields: `camelCase`, no underscore prefix.
- Inspector fields are `public` (no `[SerializeField]` in gameplay code). Some legacy public fields are lowercase (`killcount`, `matchlength`, `mainmenu`); **don't rename serialized fields** — it silently drops their Inspector values.
- `[Networked]` properties: `PascalCase` (`Health`, `Energy`, `State`). RPCs: `RPC_` prefix.
- Large classes are split into `partial` files by topic (`Manager.*.cs`, `Launcher.*.cs`, `UITheme.*.cs`). Add a new partial file for a new topic rather than growing `Manager.cs`.
- `#region` blocks group `Player` and `Weapon`; keep new code in the matching region.

## Comments

- Every script starts with a header comment saying what it is, where it runs (host, owner, everyone) and how it connects to other scripts. Keep it accurate when behaviour changes.
- Comments explain *why* and the rule being enforced, in plain sentences (British spelling: colour, behaviour). Say "Host only." on host-only methods.

## Unity practices

- Pure logic with no scene access goes in `Assets/Scripts/Core/` (`Capshots.Core` assembly) with EditMode tests. Gameplay scripts call into it.
- Tunable values are Inspector fields on prefabs/scenes or fields on `Gun` ScriptableObjects. Check the prefab/asset value before trusting a script default.
- `Resources.Load` uses asset **names** (`"Player"`, `"Dead Player"`, `"Maps/Office"`, `"Hats/Hat…"`, `"UI/…"`), and `Manager` finds HUD objects by hierarchy path. Renaming or moving these breaks things at runtime, not compile time.
- Prefer building UI/visuals in code with `UITheme` helpers over editing scenes (see [ui-audio-visuals](../gameplay/ui-audio-visuals.md)).
- Don't hand-edit `.unity`, `.prefab`, `.asset` or `.meta` YAML unless the change is trivial and certain; never change GUIDs.
- Physics layers are in `Layers` (`MatchTypes.cs`) and must match Project Settings → Tags and Layers.

## Networking rules

See [fusion.md](../networking/fusion.md). In short: simulate in `FixedUpdateNetwork`, mutate authoritative state only with `HasStateAuthority`, gate one-shot effects with `Runner.IsForward`, keep RPCs that carry looks cosmetic, append-only for networked/saved enums and index lists.

## Error handling

- Recoverable network failures are logged with `Debug.LogWarning` and the game falls back (e.g. return to menu); see `NetworkSession`.
- Optional components and singletons are null-checked (`Manager.Instance`, `Player.Local`, `Object != null && Object.IsValid`) because objects spawn/despawn across scene loads and host migration.
- Input from clients is untrusted: sanitize through `Core/InputRules`.

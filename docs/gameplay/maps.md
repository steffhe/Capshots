# Maps

## Scenes and build indices

`ProjectSettings/EditorBuildSettings.asset` (enabled scenes only get a build index):

| Build index | Scene | Menu name |
| --- | --- | --- |
| 0 | `Assets/Scenes/Menu.unity` | (main menu, must stay first: `NetworkSession.MenuScene = 0`) |
| 1 | `Assets/Scenes/Map 2.unity` | Proving Ground |
| 2 | `Assets/Scenes/Downtown.unity` | Downtown |
| 3 | `Assets/Scenes/Prototype.unity` | Prototype |
| 4 | `Assets/Scenes/Office.unity` | Office |
| 5 | `Assets/Scenes/Test Site 07.unity` | Test Site 07 |

`Map.unity` and `Map 1.unity` are listed but **disabled**. The `Launcher.maps` array in `Menu.unity` (name, scene index, enabled) is what the create tab and room list use; it stores **build indices**, so enabling, adding or reordering scenes requires updating it.

Every map scene needs a `Manager` with a `NetworkObject`, spawn points, a map camera and the HUD hierarchy `Manager` looks up by path.

## Generated maps

| Map | Source of truth | How to regenerate | Docs |
| --- | --- | --- | --- |
| Office | `Tools/OfficeMap/layout.py` → `Assets/Resources/Maps/Office.txt`, built at load by `OfficeMap.cs` (data parsing in `Core/OfficeMapData`) | `python3 Tools/OfficeMap/generate.py --preview`; optional **Capshots → Bake Office Map** | [Tools/OfficeMap](../../Tools/OfficeMap/README.md) |
| Prototype | `Tools/PrototypeMap/generate.py` → `Assets/Resources/Maps/Prototype.txt`, also built by `OfficeMap.cs` | `python3 Tools/PrototypeMap/generate.py --preview` | [Tools/PrototypeMap](../../Tools/PrototypeMap/README.md) |
| Test Site 07 | Editor code in `Assets/Editor/TestSite07/` (kit, layout, props, validator) | **Capshots → Test Site 07 → Build Everything / Validate** | [test-site-07](../test-site-07/README.md) |

Generated files are checked in; edit the generator, not the output. `OfficeMapTests` and `PrototypeMapTests` check the generated data.

## Map features

- Office glass (`OfficeGlass`, `Manager.Glass`, layer 11 `Glass`): cracks on the first hit, shatters on the second; blocks movement, not shots or spawn sight checks.
- `BoundaryField`: renders boundary colliders as force fields (per-map setup described in its header comment).
- `SpawnSide`: optional per-spawn-point team side.
- `ObjectiveMarker`: capture point markers on Test Site 07; **no objective mode uses them yet**.
- Bots need no baked NavMesh (`BotNavigation` builds it at runtime).

---
paths:
  - "**/*.unity"
  - "**/*.prefab"
  - "**/*.asset"
  - "**/*.meta"
  - "**/*.mat"
  - "ProjectSettings/**"
  - "Packages/**"
---

# Unity serialized files

- These are Unity-serialized YAML. Prefer changing behaviour in code (most UI and visuals are built at runtime). Edit by hand only for small, certain changes; never change GUIDs or `fileID`s.
- Scene files are large: search them with `grep` for the specific field instead of reading them whole.
- Build Settings order defines build indices used by `Launcher.maps` in `Menu.unity` (see `docs/gameplay/maps.md`).
- Generated data (`Assets/Resources/Maps/*.txt`) is produced by `Tools/*/generate.py`; edit the generator.
- Don't upgrade packages or the Unity version unless asked.

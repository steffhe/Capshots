# Known issues and limitations

Only verified items. Remove an entry when it's fixed.

## Verified

- **Unreachable Office rooms:** the Office generator reports rooms 44a and 58 can't be entered (their doors open onto a 24 cm gap). See [Tools/OfficeMap](../../Tools/OfficeMap/README.md).
- **Capture points without a mode:** `ObjectiveMarker` places A/B/C points on Test Site 07, but no game mode uses them.
- **Disabled build scenes:** `Assets/Scenes/Map.unity` and `Map 1.unity` are in Build Settings but disabled. Whether they are still needed is unverified.
- **No automated network or PlayMode tests:** networking, prediction and host migration are only checked by play-testing.
- **Browser hosting:** a browser host always uses Photon's relay (no direct connection), so it may add latency for distant players.

## Unverified / worth checking

- `Assets/AngeloMaN87/Prototype Map` is credited as no longer used by the Prototype map; whether any scene still references it hasn't been checked.
- `Assets/Readme.asset` and `Assets/TutorialInfo/` look like Unity template leftovers.
- `Logs/ApiUpdaterCheck.txt` is tracked although `/Logs/` is in `.gitignore`.

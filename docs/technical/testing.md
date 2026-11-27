# Testing and validation

## Automated tests

- Location: `Assets/Tests/EditMode/` (assembly `Capshots.Tests.EditMode`, NUnit). Editor-only; references `Capshots.Core` and the Fusion DLLs.
- Run in Unity: **Window → General → Test Runner → EditMode → Run All**.
- CI runs them on every PR, push to `main` and release tag (`game-ci/unity-test-runner`, `testMode: editmode`); a release is only published if they pass.
- There are no PlayMode tests.

Tests only reach code in `Assets/Scripts/Core/` (plus config files they parse). To make something testable, move its rules into `Core/` without scene access.

| Area | Test files |
| --- | --- |
| Guns | `GunTests`, `GunBalanceTests`, `GunFormsTests`, `GunEnergyTests`, `GunTransformTests`, `GunDesignTests`, `GunSatelliteLayoutTests`, `ShotRulesTests` |
| Network config | `NetworkConfigTests` (40 Hz tick, fire rates fit ticks, ≥ 400 ms hitbox buffer), `HitboxLayoutTests`, `InputRulesTests` |
| Match | `GunGameRulesTests`, `KillStreaksTests`, `LobbyRulesTests`, `MatchAwardsTests`, `ModeOrderTests`, `RoomCodesTests`, `CapFlightTests`, `PlasmaGrenadeRulesTests` |
| Bots / input | `BotLogicTests`, `AimAssistTests` |
| Profile / cosmetics | `ProfileDataTests`, `HatsTests`, `FacesTests`, `FaceDesignsTests`, `SurfacePrintTests` |
| Maps | `OfficeMapTests`, `PrototypeMapTests`, `BoundaryLayoutTests` |

## Other checks

- Map generators validate their output: `python3 Tools/OfficeMap/generate.py --preview`, `python3 Tools/PrototypeMap/generate.py --preview` (fail on blocked/off-floor spawns).
- Test Site 07: **Capshots → Test Site 07 → Validate** (reach, sightlines, routes).

## What needs manual play-testing

Anything networked (prediction feel, hit registration under latency, host migration, bots in a full room), visuals, audio and UI layout. Test with at least two instances (see [unity-setup](unity-setup.md#testing-multiplayer-locally)); for browser-specific changes use the PR WebGL build.

## Without a Unity editor (e.g. cloud sessions)

Code cannot be compiled or tested locally. Validate by careful reading, keep changes small, run the Python generators if relevant, and rely on CI's EditMode run on the PR. Report clearly what was not verified.

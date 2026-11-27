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
| UI rules | `HudRulesTests` (safe width, energy segments, health states, score strip, hit marker tiers, damage arc merging, kill sound fallback), `PopupQueueTests`, `TextRulesTests` (name cleaning and escaping) |

## Other checks

- Map generators validate their output: `python3 Tools/OfficeMap/generate.py --preview`, `python3 Tools/PrototypeMap/generate.py --preview` (fail on blocked/off-floor spawns).
- Test Site 07: **Capshots → Test Site 07 → Validate** (reach, sightlines, routes).

## What needs manual play-testing

Anything networked (prediction feel, hit registration under latency, host migration, bots in a full room), visuals, audio and UI layout. Test with at least two instances (see [unity-setup](unity-setup.md#testing-multiplayer-locally)); for browser-specific changes use the PR WebGL build.

## Without a Unity editor (e.g. cloud sessions)

Code cannot be compiled or tested locally. Validate by careful reading, keep changes small, run the Python generators if relevant, and rely on CI's EditMode run on the PR. Report clearly what was not verified.

Partial checks that do work without Unity (unverified as a general approach, used for the UI rules): `mcs --parse -langversion:experimental <file>` (Mono, `apt-get install mono-mcs`) catches syntax errors, but not C# 8 syntax (switch expressions, local functions), which it reports as errors in files that compile fine in Unity. Core rules that use only plain .NET (`TextRules`, `PopupQueue`) can be compiled and run together with their test files against a minimal stand-in for NUnit's `Test` / `TestCase` / `Assert`.

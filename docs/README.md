# Capshots documentation

Technical documentation for developers. For what the game is and how to play it, see the [root README](../README.md).

Read only what your task needs:

| Task | Read |
| --- | --- |
| Anything networked (players, weapons, match, bots) | [networking/fusion.md](networking/fusion.md) |
| Where code lives / how systems connect | [architecture/overview.md](architecture/overview.md) |
| Movement, health, damage, spawning, player looks | [gameplay/player.md](gameplay/player.md) |
| Guns, balance, firing, grenades | [gameplay/weapons.md](gameplay/weapons.md) |
| Match flow, game modes, lobby, XP, voting | [gameplay/match-and-modes.md](gameplay/match-and-modes.md) |
| Bots | [gameplay/bots.md](gameplay/bots.md) |
| Maps, scenes, map generators | [gameplay/maps.md](gameplay/maps.md) |
| Menus, HUD, audio, effects, browser/touch | [gameplay/ui-audio-visuals.md](gameplay/ui-audio-visuals.md) |
| Unity version, Photon setup, CI, releases | [technical/unity-setup.md](technical/unity-setup.md) |
| Code conventions | [technical/coding-standards.md](technical/coding-standards.md) |
| Tests and validation | [technical/testing.md](technical/testing.md) |
| Performance | [technical/performance.md](technical/performance.md) |
| Why things are built this way | [development/decisions.md](development/decisions.md) |
| Known problems | [development/known-issues.md](development/known-issues.md) |

Design notes with images:

- [test-site-07/](test-site-07/README.md) – Test Site 07 map design
- [weapon-redesign/](weapon-redesign/README.md) – modular transforming gun design

Tool guides next to their scripts: [Tools/OfficeMap](../Tools/OfficeMap/README.md), [Tools/PrototypeMap](../Tools/PrototypeMap/README.md), [Tools/SurfaceTextures](../Tools/SurfaceTextures/README.md).

There is no written roadmap.

## Maintaining these docs

- Describe the implementation as it is; mark anything unverified as such.
- Each fact lives in one file; link instead of copying.
- Update the relevant file in the same change that alters behaviour, and delete what it replaces.
- Script header comments are the most detailed per-file reference; these docs give the map and the rules.

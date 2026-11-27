---
paths:
  - "Assets/Scripts/Player.cs"
  - "Assets/Scripts/Weapon.cs"
  - "Assets/Scripts/Manager*.cs"
  - "Assets/Scripts/NetworkSession.cs"
  - "Assets/Scripts/PlayerInputData.cs"
  - "Assets/Scripts/MatchTypes.cs"
  - "Assets/Scripts/Bot*.cs"
---

# Networked gameplay code

Read `docs/networking/fusion.md` before changing this file if you haven't this session. Before finishing, check:

- State changes happen in `FixedUpdateNetwork`, on `[Networked]` properties, under `HasStateAuthority` where authoritative.
- One-time work (raycasts, damage, effects, sounds, stat counting) is gated by `Runner.IsForward`.
- Works for bots (fake `PlayerRef`, no input authority) and survives host migration (`MigrationCarry`) if it is host-kept state.
- New controls go through `GameInput` → `InputCollector` → `PlayerInputData` and `BotBrain`.
- No renumbering of `MatchMode`, `InputButton` or other sent/saved values; RPC signatures and networked fields change only when needed.
- Update the matching doc in `docs/gameplay/` or `docs/networking/`.

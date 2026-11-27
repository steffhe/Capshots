---
paths:
  - "Assets/Scripts/Core/**"
  - "Assets/Tests/**"
---

# Core rules and EditMode tests

- `Assets/Scripts/Core/` is the `Capshots.Core` assembly: pure rules with no scene access, no `MonoBehaviour`s, and no references to `Assembly-CSharp` (gameplay scripts call Core, never the reverse). UnityEngine value types and `ScriptableObject` (`Gun`) are fine.
- Every rule change gets or updates a test in `Assets/Tests/EditMode/` (NUnit, `Capshots.Tests.EditMode`). Tests that pin balance or config (`GunBalanceTests`, `NetworkConfigTests`) are changed only on purpose.
- Tests can't run in cloud sessions (no Unity); CI runs them on PRs. Say so when reporting.

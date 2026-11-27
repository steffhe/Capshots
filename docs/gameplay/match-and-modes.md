# Match flow and game modes

The match is one `NetworkBehaviour`, `Manager`, split into partial files by topic. There is one `Manager` (with a `NetworkObject`) in every map scene; the host owns it. Networking rules: [fusion.md](../networking/fusion.md).

## Manager files

| File | Responsibility |
| --- | --- |
| `Manager.cs` | Room settings (kill limit, match length), `GameState` machine, timer, `Stats` dictionary, registration, `PlayerKilled`, end of match |
| `Manager.Lobby.cs` + `Core/LobbyRules` | Ready-up lobby for rooms made in the create tab; host can force-start |
| `Manager.Spawning.cs` | Spawn timers, spawn point choice, ragdolls |
| `Manager.Teams.cs` | Teams, balancing, team score and colours |
| `Manager.CapGrab.cs`, `Manager.CapArrows.cs`, `CapPickup.cs`, `Core/CapFlight` | Cap Grab and Team Cap Grab |
| `Manager.GunGame.cs` + `Core/GunGameRules` | Gun Game ladder |
| `Manager.ScoreStrip.cs` | Score plates either side of the match clock and the goal line under it (all modes; see [ui-audio-visuals.md](ui-audio-visuals.md#combat-hud)) |
| `Manager.Bots.cs` | Filling the room with bots (see [bots.md](bots.md)) |
| `Manager.Grenades.cs` | Plasma grenades (see [weapons.md](weapons.md)) |
| `Manager.Glass.cs` | Office glass pane state |
| `Manager.Chat.cs` | Kill feed, chat, ping |
| `Manager.Scoreboard.cs` | Scoreboard UI |
| `Manager.Xp.cs` + `Core/ProfileData`, `Core/KillStreaks` | XP, levels, streak callouts |
| `Manager.Awards.cs` + `Core/MatchAwards`, `AwardStage.cs` | End-of-match awards |
| `Manager.Voting.cs` | Next-map vote (3 options, 15 s) and `Runner.LoadScene` |
| `Manager.Presentation.cs` | Intro, ending freeze, VICTORY/DEFEAT, music handoff |
| `Manager.HostMigration.cs` | Rebuilding the match on a new host |

## Game state (`GameState`, host drives it once a second)

`Waiting` (warmup until `minPlayersToStart` 2, or lobby ready-up) → `Starting` (`startCountdown` 5 s; stats reset at the end) → `Playing` → `Ending` (players frozen, awards, results, vote). The kill feed, scoring and XP only count while playing.

Win conditions come from room properties set in `Launcher`: kills (up to 50, default 20, clock counts up) or time (1–20 minutes, default 5, most kills wins). `Manager.killcount` / `matchlength` hold them; 0 means no limit of that kind.

## Game modes (`MatchMode` in `MatchTypes.cs`)

| Value | Mode | Notes |
| --- | --- | --- |
| 0 | Free For All | Kills score |
| 1 | Team Deathmatch | Blue vs red, auto-balanced, no friendly fire, team kill limit |
| 2 | Cap Grab | A kill drops the victim's cap; walking over it scores. Caps vanish after 15 s (`CapLifetime`) |
| 3 | Gun Game | Ladder of forms, `KillsPerStep` 2 per form; grenade kills don't count |
| 4 | Team Cap Grab | Cap Grab in teams; picking up a teammate's cap denies it |

Values are sent in room properties and read by older builds: **never renumber**. Display order in the create tab comes from `Core/ModeOrder`. Adding a mode means a new enum value at the end, `ModeOrder`, `LoadingScreen.ModeName`, scoring/win rules in the relevant `Manager` partials, bot behaviour, and tests.

## Room properties

Set in `Launcher` and read by `Manager` on spawn (e.g. `map`, `mapname`, mode, bots, bot skill, win condition). Quick play joins any open room regardless of properties. Practice uses the same properties offline.

### Shooting range

The home menu opens a single-player firing lane with the player's equipped starting form and normal weapon controls. The first drill uses a stationary crown, then a moving target with a different hat. A body hit gives a hint during the first drill. A capshot knocks the hat loose and advances the lane counter. Completing both targets saves the drill on the profile. Return visits open directly in free practice.

Pause to keep shooting or start an Easy Cap Grab match, first to ten collected caps. Free practice also offers stationary or moving targets at marks 8, 13 and 18 metres from the firing line. Range capshots use the normal weapon hitboxes and pellet rules. They do not award match XP or advance a multiplayer score.


## XP and progression

`Manager.Xp` awards XP on the player's own game (amounts are `xp*` fields on `Manager`: kill, cap shot bonus, cap grab, deny, match complete, win); `ProfileData` stores level/XP and is saved by `Data`. Hats (`Core/Hats`, 30) and faces (`Core/Faces`, 12) unlock by level and are identified by list index, which is saved and sent over the network, so new entries go at the end.

- Kill streaks (`Core/KillStreaks`): multi-kills within 4 s (DOUBLE … MULTI KILL); streaks every 5 kills (KILLING SPREE, RAMPAGE, UNSTOPPABLE, LEGENDARY); ending a streak of 5+ is a SHUTDOWN (100 XP).
- Awards (`Core/MatchAwards`): CAPSHOT KING, ON A ROLL, STICKY FINGERS, UNTOUCHABLE, PUNCHING BAG; the host counts the inputs in `Manager.Awards`.
- Cap shot: a kill where the head took at least half the shot's damage (`ShotRules.IsCapshot`). The host counts each player's cap shot kills while the match is played (`PlayerStats.Capshots`, reset with kills and deaths when it starts, carried through host migration) for the scoreboard; they count in every mode, whether or not the mode scores the kill.
- End banner in free for all: a tie for the top score reads TIED FOR 1ST (for those sharing it) or TIE AT THE TOP, rather than naming one winner. XP is unchanged: everyone on the top score gets the win bonus, as before.

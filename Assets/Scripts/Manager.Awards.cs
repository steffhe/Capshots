using System.Collections.Generic;
using Fusion;
using UnityEngine;
using UnityEngine.UI;

// End-of-match awards (MatchAwards): CAPSHOT KING, ON A ROLL, STICKY FINGERS, UNTOUCHABLE and
// PUNCHING BAG. The host counts cap shots, grenade kills and best streaks while the match is played
// (kills and deaths are on the scoreboard already), picks the awards when it ends and sends them to
// everyone. They get a screen of their own before the results (AwardStage: each winner's drone over
// their card, with all their awards listed on it).
public partial class Manager
{
    // Host only: what each player did this match, beyond kills and deaths
    private struct AwardCount
    {
        public int capshots, grenadeKills, bestStreak;
    }
    private readonly Dictionary<PlayerRef, AwardCount> awardCounts = new Dictionary<PlayerRef, AwardCount>();

    // Everyone: the awards the host sent, for the awards screen (AwardStage), and how many players won
    // something (set by the host with the end of the match, so it reaches everyone with it)
    private List<MatchAwards.Award> awards;
    [Networked] private byte AwardCards { get; set; }

    // Host only: a kill while the match is being played
    private void CountForAwards(PlayerRef killer, PlayerRef victim, string weaponName, bool headshot, int streak)
    {
        if (!killer.IsRealPlayer || killer == victim) return;
        awardCounts.TryGetValue(killer, out AwardCount count);
        if (headshot) count.capshots++;
        if (weaponName == PlasmaWeaponName) count.grenadeKills++;
        count.bestStreak = Mathf.Max(count.bestStreak, streak);
        awardCounts[killer] = count;
    }

    // Host only: the match ended. Picks the awards and sends them to everyone.
    private void SendAwards()
    {
        List<MatchAwards.Tally> tallies = new List<MatchAwards.Tally>();
        foreach (KeyValuePair<PlayerRef, PlayerStats> entry in Stats)
        {
            awardCounts.TryGetValue(entry.Key, out AwardCount count);
            tallies.Add(new MatchAwards.Tally
            {
                actor = entry.Key.PlayerId,
                name = entry.Value.Username.ToString(),
                kills = entry.Value.Kills,
                deaths = entry.Value.Deaths,
                capshots = count.capshots,
                grenadeKills = count.grenadeKills,
                bestStreak = count.bestStreak,
            });
        }
        // The same order on every machine, so ties go the same way
        tallies.Sort((a, b) => a.actor.CompareTo(b.actor));
        List<MatchAwards.Award> chosen = MatchAwards.Choose(tallies);
        // How many cards the awards screen has, so every machine gives it the same time, even one
        // the awards themselves reach late (it arrives with the end of the match: Manager.Presentation)
        AwardCards = (byte)MatchAwards.GroupByPlayer(chosen).Count;
        if (chosen.Count > 0) RPC_Awards(MatchAwards.Pack(chosen));
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_Awards(string packed)
    {
        awards = MatchAwards.Unpack(packed);
    }

    // For the awards screen: each player who won something, once, with all their awards, in the order
    // their first award comes
    private List<AwardStage.Entry> AwardEntries()
    {
        List<AwardStage.Entry> entries = new List<AwardStage.Entry>();
        int me = LocalActor;
        foreach (List<MatchAwards.Award> group in MatchAwards.GroupByPlayer(awards))
        {
            MatchAwards.Award first = group[0];
            AwardStage.Entry entry = new AwardStage.Entry
            {
                name = first.name,
                color = PlayerCap.ColorFor(first.actor),
                hat = GetPlayerHat(first.actor),
                face = GetPlayerFace(first.actor),
                teamMode = IsTeamMode,
                mine = first.actor == me,
            };
            foreach (MatchAwards.Award award in group)
            {
                entry.titles.Add(award.title);
                entry.details.Add(award.detail);
            }
            entries.Add(entry);
        }
        return entries;
    }
}

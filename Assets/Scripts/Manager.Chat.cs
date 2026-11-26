using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Fusion;

// Kill feed, text chat and ping.
public partial class Manager
{
    // Sent by the host when a player dies, so everyone can show it in the kill feed.
    // killer is -1 when nobody gets the kill.
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_KillFeed(int killer, int victim, string weapon, NetworkBool headshot, byte multiKill, byte streak, byte endedStreak)
    {
        int me = LocalActor;
        // The killer's drone looks happy for a moment
        if (killer >= 0 && killer != victim)
        {
            foreach (Player player in Player.All)
            {
                if (player != null && player.ActorId == killer) player.Celebrate();
            }
        }
        if (IsTeamMode)
        {
            FeedbackUI.AddKillFeed(GetPlayerName(killer), GetPlayerName(victim), weapon, headshot, killer == me || victim == me,
                TeamHex(GetTeam(killer)), TeamHex(GetTeam(victim)));
        }
        else
        {
            FeedbackUI.AddKillFeed(GetPlayerName(killer), GetPlayerName(victim), weapon, headshot, killer == me || victim == me);
        }

        // Streaks: a milestone, or a long one ended, goes in everyone's kill feed
        string streakName = KillStreaks.StreakName(streak);
        if (streakName != null && killer >= 0 && killer != me)
        {
            FeedbackUI.AddKillFeedNote($"{GetPlayerName(killer)} IS ON A <color={FeedbackUI.CapshotColor}>{streakName}</color>  ({streak})");
        }
        bool shutdown = endedStreak >= KillStreaks.ShutdownStreak && killer >= 0 && killer != victim;
        if (shutdown && killer != me && victim != me)
        {
            FeedbackUI.AddKillFeedNote($"{GetPlayerName(killer)} ENDED {GetPlayerName(victim)}'S STREAK OF {endedStreak}");
        }

        bool counts = state == GameState.Playing && !matchEnded;
        if (killer == me && victim != me)
        {
            FeedbackUI.Kill(headshot);
            // Kills during warmup and the countdown don't count
            if (counts)
            {
                Summary.Kill(weapon, headshot);
                // The biggest thing that happened is called out in the middle of the screen
                string multiName = KillStreaks.MultiKillName(multiKill);
                string callout = streakName ?? multiName ?? (shutdown ? "SHUTDOWN" : null);
                if (callout != null)
                {
                    string detail = streakName != null ? $"{streak} KILLS WITHOUT DYING" : shutdown && multiName == null ? $"ENDED {GetPlayerName(victim)}'S STREAK OF {endedStreak}" : null;
                    FeedbackUI.Callout(callout, detail);
                }

                int bonus = KillStreaks.BonusXp(multiKill, streak) + (shutdown ? KillStreaks.ShutdownXp : 0);
                string reason = callout ?? (headshot ? "CAPSHOT" : "KILL");
                // A cap shot kill earns a bonus, shown with the cap logo
                if (headshot) AwardXp(xpPerKill + xpCapshotBonus + bonus, callout != null ? "CAPSHOT + " + callout : "CAPSHOT", true);
                else AwardXp(xpPerKill + bonus, reason);
            }
        }

        if (victim == me)
        {
            if (counts) Summary.Died();
            FeedbackUI.Died();
            FeedbackUI.ShowDeath(GetPlayerName(killer), weapon, headshot, respawnTime);
            // Watch whoever it was until respawning (not after a fall or your own grenade)
            DeathCam.SetKiller(killer >= 0 && killer != me ? killer : -1, GetPlayerName(killer));
            if (endedStreak >= KillStreaks.ShutdownStreak) FeedbackUI.AddKillFeedNote($"YOUR STREAK OF {endedStreak} IS OVER");
        }
    }

    public void SendChat_S(string message)
    {
        message = message.Trim();
        if (message.Length == 0) return;
        if (message.Length > ChatUI.MaxMessageLength) message = message.Substring(0, ChatUI.MaxMessageLength);

        RPC_Chat(message);
    }

    // Sent to the host, which passes it on to everyone
    [Rpc(RpcSources.All, RpcTargets.All)]
    private void RPC_Chat(string message, RpcInfo info = default)
    {
        if (string.IsNullOrEmpty(message)) return;
        int actor = info.Source.IsRealPlayer ? info.Source.PlayerId : LocalActor;
        if (message.Length > ChatUI.MaxMessageLength) message = message.Substring(0, ChatUI.MaxMessageLength);

        int team = GetTeam(actor);
        string color = team >= 0 ? TeamHex(team) : (actor == LocalActor ? "#ffd84a" : "#dddddd");
        ChatUI.AddMessage(GetPlayerName(actor), color, message);
    }

    // Host only, every second: measure everyone's round trip time for the scoreboard
    private void UpdatePings()
    {
        foreach (PlayerRef player in StatsKeys())
        {
            // Bots run on the host: no connection to measure
            if (IsBot(player)) continue;
            int ping = player == Runner.LocalPlayer ? 0 : Mathf.RoundToInt((float)Runner.GetPlayerRtt(player) * 1000f);
            PlayerStats stats = Stats[player];
            // Only send real changes, so the scoreboard isn't resent every second
            if (Mathf.Abs(stats.Ping - ping) < 5) continue;
            stats.Ping = (short)Mathf.Clamp(ping, 0, short.MaxValue);
            Stats.Set(player, stats);
        }
    }

    private string PingLabel(int actor)
    {
        int index = FindPlayerIndex(actor);
        if (index < 0) return "";
        // Bots' names already say BOT, and they have no connection to measure
        if (IsBotActor(actor)) return "";

        int ping = playerInfo[index].ping;
        // Our own round trip to the host is known here first hand
        if (actor == LocalActor && Runner != null && !HasStateAuthority) ping = Mathf.RoundToInt((float)Runner.GetPlayerRtt(Runner.LocalPlayer) * 1000f);
        if (ping < 0) return "";

        string color = ping < 80 ? "#7ddc6f" : ping < 150 ? "#e8c84a" : "#e8604a";
        return $"  <size=14><color={color}>{ping} ms</color></size>";
    }
}
